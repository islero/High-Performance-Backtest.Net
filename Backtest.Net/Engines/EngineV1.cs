using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines
{
    /// <summary>
    /// Engine V1
    /// Prepares parts before feeding them into strategy
    /// </summary>
    public class EngineV1 : IEngine
    {
        // --- Delegates
        public Action? OnCancellationFinishedDelegate { get; set; }

        // --- Properties
        protected ITrade Trade { get; set; }
        protected IStrategy Strategy { get; set; }
        private int WarmupCandlesCount { get; } // The amount of warmup candles count

        // --- Constructors
        public EngineV1(int warmupCandlesCount, ITrade trade, IStrategy strategy)
        {
            WarmupCandlesCount = warmupCandlesCount;
            Trade = trade;
            Strategy = strategy;
        }

        // --- Methods
        /// <summary>
        /// Starts the engine and feeds the strategy with data
        /// </summary>
        /// <param name="symbolDataParts"></param>
        /// <returns></returns>
        public async Task RunAsync(IEnumerable<IEnumerable<ISymbolData>> symbolDataParts, CancellationToken? token = null)
        {
            try
            {
                // --- Run every symbolDataPart
                foreach (var part in symbolDataParts)
                {
                    // --- Main cycle
                    while (part.All(x => x.Timeframes.First().Index < x.Timeframes.First().EndIndex))
                    {
                        // --- Checking for cancelation
                        if (token != null && token.Value.IsCancellationRequested)
                            throw new OperationCanceledException();

                        // --- Preparing feeding data
                        var feedingData = CloneFeedingSymbolData(part);

                        // --- Strategy part
                        var signals = await Strategy.Execute(feedingData);
                        if (signals != null && signals.Any())
                        {
                            foreach (var signal in signals)
                            {
                                var result = await Trade.ExecuteSignal(signal);
                            }
                        }

                        // --- Incrementing indexes
                        IncrementIndexes(part);
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
        protected void IncrementIndexes(IEnumerable<ISymbolData> symbolData)
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

        protected IEnumerable<ISymbolData> CloneFeedingSymbolData(IEnumerable<ISymbolData> symbolData)
        {
            IEnumerable<ISymbolData> clonedSymbolsData = new List<ISymbolData>();
            foreach (var symbol in symbolData)
            {
                IEnumerable<ITimeframe> timeframes = new List<TimeframeV1>();

                foreach (var timeframe in symbol.Timeframes)
                {
                    int warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex ? timeframe.Index - WarmupCandlesCount : timeframe.StartIndex;
                    IEnumerable<ICandlestick> clonedCandlesticks = timeframe.Candlesticks.Take(warmedUpIndex..(timeframe.Index + 1)).OrderByDescending(x => x.OpenTime).Select(candle => candle.Clone());

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
    }
}
