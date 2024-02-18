using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines
{
    /// <summary>
    /// Engine V1
    /// Prepares parts before feeding them into strategy
    /// </summary>
    public class EngineV1(int warmupCandlesCount, ITrade trade, IStrategy strategy) : IEngine
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
                    var partList = part.ToList();
                    
                    // --- Main cycle
                    while (partList.All(x => x.Timeframes.First().Index < x.Timeframes.First().EndIndex))
                    {
                        // --- Checking for cancellation
                        if (cancellationToken is { IsCancellationRequested: true })
                            throw new OperationCanceledException();

                        // --- Preparing feeding data
                        var feedingData = CloneFeedingSymbolData(partList).ToList();

                        // --- Apply Open Price to OHLC for all first candles
                        HandleOhlc(feedingData);

                        // --- Strategy part
                        var signals = await Strategy.Execute(feedingData);
                        var signalsList = signals.ToList();
                        if (signalsList.Count != 0)
                        {
                            foreach (var signal in signalsList)
                            {
                                _ = await Trade.ExecuteSignal(signal);
                            }
                        }

                        // --- Incrementing indexes
                        IncrementIndexes(partList);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // --- Cancellation been requested and executed
                if (OnCancellationFinishedDelegate != null)
                    OnCancellationFinishedDelegate();
            }
        }

        /// <summary>
        /// Increment Symbol Data indexes
        /// </summary>
        /// <param name="symbolData"></param>
        /// <returns></returns>
        private void IncrementIndexes(IEnumerable<ISymbolData> symbolData)
        {
            foreach (var symbol in symbolData)
            {
                DateTime lowestTimeframeIndexTime = DateTime.MinValue;
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
                    DateTime closeTime = timeframe.Candlesticks.ElementAt(timeframe.Index).CloseTime;
                    if (lowestTimeframeIndexTime < closeTime && timeframe.Index >= timeframe.StartIndex && timeframe.Index < timeframe.EndIndex)
                    {
                        timeframe.Index++;
                    }
                }
            }
        }

        /// <summary>
        /// Cloning necessary symbol data range
        /// </summary>
        /// <param name="symbolData"></param>
        /// <returns></returns>
        private IEnumerable<ISymbolData> CloneFeedingSymbolData(IEnumerable<ISymbolData> symbolData)
        {
            IEnumerable<ISymbolData> clonedSymbolsData = new List<ISymbolData>();
            foreach (var symbol in symbolData)
            {
                IEnumerable<ITimeframe> timeframes = new List<TimeframeV1>();

                foreach (var timeframe in symbol.Timeframes)
                {
                    var warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex
                        ? timeframe.Index - WarmupCandlesCount
                        : timeframe.StartIndex;
                    var clonedCandlesticks = timeframe.Candlesticks
                        .Take(warmedUpIndex..(timeframe.Index + 1)).OrderByDescending(x => x.OpenTime)
                        .Select(candle => candle.Clone());

                    // --- No need to add nothing more except interval and candles themself
                    timeframes = timeframes.Append(new TimeframeV1()
                    {
                        Timeframe = timeframe.Timeframe,
                        Candlesticks = clonedCandlesticks
                    });
                }

                // Create a new symbol data with cloned candlesticks
                ISymbolData cloned = new SymbolDataV1()
                {
                    Symbol = symbol.Symbol,
                    Timeframes = timeframes,
                };

                clonedSymbolsData = clonedSymbolsData.Append(cloned);
            }

            return clonedSymbolsData;
        }

        /// <summary>
        /// Apply Open Price to OHLC for all first candles
        /// </summary>
        /// <param name="symbolData"></param>
        /// <returns></returns>
        private static void HandleOhlc(IEnumerable<ISymbolData> symbolData)
        {
            foreach (var symbol in symbolData)
            {
                var firstTimeframe = symbol.Timeframes.FirstOrDefault();

                // --- Using null propagation and getting first candle
                var firstTimeframeCandle = firstTimeframe?.Candlesticks.FirstOrDefault();
                if (firstTimeframeCandle == null) continue;
                
                foreach (var timeframe in symbol.Timeframes)
                {
                    var candleSticks = timeframe.Candlesticks.ToList();
                    var firstCandle = candleSticks.FirstOrDefault();
                    if (firstCandle != null)
                    {
                        firstCandle.Open = firstTimeframeCandle.Open;
                        firstCandle.High = firstTimeframeCandle.Open;
                        firstCandle.Low = firstTimeframeCandle.Open;
                        firstCandle.Close = firstTimeframeCandle.Open;
                        firstCandle.CloseTime = firstTimeframeCandle.OpenTime;
                    }

                    // Assign the modified list back to the enumerable
                    timeframe.Candlesticks = candleSticks;
                }
            }
        }
    }
}
