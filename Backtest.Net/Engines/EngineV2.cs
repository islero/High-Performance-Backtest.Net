using System.Collections.Concurrent;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines
{
    /// <summary>
    /// Engine V2
    /// Prepares parts before feeding them into strategy
    /// </summary>
    public class EngineV2(int warmupCandlesCount, ITrade trade, IStrategy strategy) : IEngine
    {
        // --- Delegates
        public Action? OnCancellationFinishedDelegate { get; set; }

        // --- Properties
        private ITrade Trade { get; } = trade;
        private IStrategy Strategy { get; } = strategy;
        private int WarmupCandlesCount { get; } = warmupCandlesCount; // The amount of warmup candles count

        // --- Methods
        /// <summary>
        /// Starts the engine and feeds the strategy with data
        /// </summary>
        /// <param name="symbolDataParts"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task RunAsync(IEnumerable<IEnumerable<ISymbolData>> symbolDataParts, CancellationToken? cancellationToken = default)
        {
            try
            {
                // --- Run every symbolDataPart
                foreach (var part in symbolDataParts)
                {
                    // --- Enumerating Part
                    var partList = part;
                    
                    // --- Main cycle
                    while (partList.All(x => x.Timeframes.First().Index < x.Timeframes.First().EndIndex))
                    {
                        // --- Checking for cancellation
                        if (cancellationToken is { IsCancellationRequested: true })
                            throw new OperationCanceledException();

                        // --- Preparing feeding data
                        var feedingData = await CloneFeedingSymbolData(partList);

                        // --- Enumerating Feeding Data
                        var feedingDataList = feedingData;
                        
                        // --- Apply Open Price to OHLC for all first candles
                        await HandleOhlc(feedingDataList);

                        // --- Strategy part
                        var signals = await Strategy.Execute(feedingDataList);
                        var signalsList = signals;
                        if (signalsList.Any())
                        {
                            foreach (var signal in signalsList)
                            {
                                _ = await Trade.ExecuteSignal(signal);
                            }
                        }

                        // --- Clearing unnecessary data
                        _clonedSymbolsData.Clear();
                        
                        // --- Incrementing indexes
                        await IncrementIndexes(partList);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // --- Cancellation been requested and executed
                OnCancellationFinishedDelegate?.Invoke();
            }
        }

        /// <summary>
        /// Increment Symbol Data indexes
        /// </summary>
        /// <param name="symbolData"></param>
        /// <returns></returns>
        private static async Task IncrementIndexes(IEnumerable<ISymbolData> symbolData)
        {
            //foreach (var symbol in symbolData)
            await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
            {
                var lowestTimeframeIndexTime = DateTime.MinValue;
                foreach (var timeframe in symbol.Timeframes)
                {
                    // Handling the lowest timeframe
                    if (timeframe == symbol.Timeframes.First())
                    {
                        if (timeframe.Index >= timeframe.StartIndex && timeframe.Index < timeframe.EndIndex)
                        {
                            timeframe.Index++;
                            lowestTimeframeIndexTime = timeframe.Candlesticks.ElementAt(timeframe.Index).OpenTime;
                        }

                        continue;
                    }

                    // Handling higher timeframes
                    var closeTime = timeframe.Candlesticks.ElementAt(timeframe.Index).CloseTime;
                    if (lowestTimeframeIndexTime < closeTime && timeframe.Index >= timeframe.StartIndex &&
                        timeframe.Index < timeframe.EndIndex)
                    {
                        timeframe.Index++;
                    }
                }

                return ValueTask.CompletedTask;
            });
        }

        /// <summary>
        /// Field for allocated ISymbolData to clone it
        /// </summary>
        private readonly ConcurrentQueue<ISymbolData> _clonedSymbolsData = new();
        
        /// <summary>
        /// Cloning necessary symbol data range
        /// </summary>
        /// <param name="symbolData"></param>
        /// <returns></returns>
        private async Task<IEnumerable<ISymbolData>> CloneFeedingSymbolData(IEnumerable<ISymbolData> symbolData)
        {
            _clonedSymbolsData.Clear();
            
            await Parallel.ForEachAsync(symbolData, new ParallelOptions(), async (symbol, _) =>
            {
                var timeframes = new ConcurrentQueue<TimeframeV1>();

                await Parallel.ForEachAsync(symbol.Timeframes, new ParallelOptions(), (timeframe, _) =>
                {
                    var warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex
                        ? timeframe.Index - WarmupCandlesCount
                        : timeframe.StartIndex;
                    var clonedCandlesticks = timeframe.Candlesticks
                        .Take(warmedUpIndex..(timeframe.Index + 1)).OrderByDescending(x => x.OpenTime)
                        .Select(candle => candle.Clone());

                    // --- No need to add nothing more except interval and candles themself
                    timeframes.Enqueue(new TimeframeV1()
                    {
                        Timeframe = timeframe.Timeframe,
                        Candlesticks = clonedCandlesticks
                    });
                    
                    return ValueTask.CompletedTask;
                });
                
                // Create a new symbol data with cloned candlesticks
                ISymbolData cloned = new SymbolDataV1()
                {
                    Symbol = symbol.Symbol,
                    Timeframes = timeframes,
                };

                _clonedSymbolsData.Enqueue(cloned);
            });

            return _clonedSymbolsData;
        }

        /// <summary>
        /// Apply Open Price to OHLC for all first candles
        /// </summary>
        /// <param name="symbolData"></param>
        /// <returns></returns>
        private static async Task HandleOhlc(IEnumerable<ISymbolData> symbolData)
        {
            await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
            {
                // --- Enumerating symbol timeframes to list
                var timeframesList = symbol.Timeframes;
                
                // --- Getting First Timeframe from the list
                var firstTimeframe = timeframesList.First();

                // --- Using null propagation and getting first candle
                var firstTimeframeCandle = firstTimeframe.Candlesticks.First();
                
                foreach (var timeframe in timeframesList)
                {
                    var candleSticks = timeframe.Candlesticks.ToList();
                    var firstCandle = candleSticks.First();
                    
                    // Applying OHLC value as lowest timeframe open price and close time as open time
                    firstCandle.Open = firstTimeframeCandle.Open;
                    firstCandle.High = firstTimeframeCandle.Open;
                    firstCandle.Low = firstTimeframeCandle.Open;
                    firstCandle.Close = firstTimeframeCandle.Open;
                    firstCandle.CloseTime = firstTimeframeCandle.OpenTime;

                    // Assign the modified list back to the enumerable
                    timeframe.Candlesticks = candleSticks;
                }

                return default;
            });
        }
    }
}
