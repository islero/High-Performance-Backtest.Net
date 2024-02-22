using System.Collections.Concurrent;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines
{
    /// <summary>
    /// Engine V3
    /// Prepares parts before feeding them into strategy
    /// </summary>
    public class EngineV3(int warmupCandlesCount, ITrade trade, IStrategy strategy) : IEngine
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
                    // --- Main cycle
                    while (part.All(x => x.Timeframes.First().Index < x.Timeframes.First().EndIndex))
                    {
                        // --- Checking for cancellation
                        if (cancellationToken is { IsCancellationRequested: true })
                            throw new OperationCanceledException();

                        // --- Preparing feeding data
                        var feedingData = await CloneFeedingSymbolData(part);

                        // --- Apply Open Price to OHLC for all first candles
                        await HandleOhlc(feedingData);

                        // --- Strategy part
                        var signals = await Strategy.Execute(feedingData);
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
                        await IncrementIndexes(part);
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
            await Parallel.ForEachAsync(symbolData, new ParallelOptions(), async (symbol, _) =>
            {
                var lowestTimeframeIndexTime = DateTime.MinValue;

                // Enumerating timeframes as array
                var timeframesArray = symbol.Timeframes.ToArray();
                
                //await Parallel.ForEachAsync(symbol.Timeframes, new ParallelOptions(), (timeframe, _) =>
                for(var i = 0; i < timeframesArray.Length; i++)
                {
                    // Creating array of candlesticks
                    var candlesticksArray = timeframesArray[i].Candlesticks.ToArray();
                    
                    // Handling the lowest timeframe
                    if (timeframesArray[i] == symbol.Timeframes.First())
                    {
                        if (timeframesArray[i].Index >= timeframesArray[i].StartIndex && timeframesArray[i].Index < timeframesArray[i].EndIndex)
                        {
                            timeframesArray[i].Index++;
                            lowestTimeframeIndexTime = candlesticksArray[timeframesArray[i].Index].OpenTime;
                        }

                        break;
                    }

                    // Handling higher timeframes
                    var closeTime = candlesticksArray[timeframesArray[i].Index].CloseTime;
                    if (lowestTimeframeIndexTime < closeTime && timeframesArray[i].Index >= timeframesArray[i].StartIndex &&
                        timeframesArray[i].Index < timeframesArray[i].EndIndex)
                    {
                        timeframesArray[i].Index++;
                    }

                    break;
                }
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
            // Clearing data before clone it
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
                        .Take(warmedUpIndex..(timeframe.Index + 1))
                        .Select(candle => candle.Clone())
                        .ToArray();

                    // --- Sorting in descending direction
                    Array.Sort(clonedCandlesticks, (x, y) => y.OpenTime.CompareTo(x.OpenTime));

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
                // --- Enumerating symbol timeframes to array
                var timeframesArray = symbol.Timeframes.ToArray();
                
                // --- Getting First Timeframe from the list
                var firstTimeframe = timeframesArray[0];

                // --- Enumerating First Timeframe Candlesticks to Array
                var firstTimeframeCandlesticksArray = firstTimeframe.Candlesticks.ToArray();

                // --- Using null propagation and getting first candle
                var firstTimeframeCandle = firstTimeframeCandlesticksArray[0];
                
                //foreach (var timeframe in timeframesArray)
                for (var i = 0; i < timeframesArray.Length; i++)
                {
                    var candleSticksArray = timeframesArray[i].Candlesticks.ToArray();
                    var firstCandle = candleSticksArray[0];
                    
                    // Applying OHLC value as lowest timeframe open price and close time as open time
                    firstCandle.Open = firstTimeframeCandle.Open;
                    firstCandle.High = firstTimeframeCandle.Open;
                    firstCandle.Low = firstTimeframeCandle.Open;
                    firstCandle.Close = firstTimeframeCandle.Open;
                    firstCandle.CloseTime = firstTimeframeCandle.OpenTime;

                    // Assign the modified list back to the enumerable
                    timeframesArray[i].Candlesticks = candleSticksArray;
                }

                return default;
            });
        }
    }
}
