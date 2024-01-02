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
        // --- Properties
        protected ITrade Trade { get; set; }
        protected IStrategy Strategy { get; set; }
        private DateTime StartDateTime { get; } // Backtesting Start DateTime
        private DateTime EndDateTime { get; } // Backtesting End DateTime
        private int WarmupCandlesCount { get; } // The amount of warmup candles count

        // --- Constructors
        public EngineV1(DateTime startDateTime, DateTime endDateTime, int warmupCandlesCount, ITrade trade, IStrategy strategy)
        {
            StartDateTime = startDateTime;
            EndDateTime = endDateTime;
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
        public async Task RunAsync(IEnumerable<IEnumerable<ISymbolData>> symbolDataParts)
        {
            // --- Run every symbolDataPart
            foreach (var part in symbolDataParts)
            {
                // --- Main cycle
                while (part.All(x => x.Timeframes.First().Index == x.Timeframes.First().EndIndex))
                {
                    // --- Preparing feeding data
                    var feedingData = CloneFeedingSymbolData(part);

                    // --- Strategy part
                    var signals = await Strategy.Execute(feedingData);
                    if (signals != null)
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
                    DateTime openTime = timeframe.Candlesticks.ElementAt(timeframe.Index).OpenTime;
                    if (openTime >= lowestTimeframeIndexTime && timeframe.Index >= timeframe.StartIndex && timeframe.Index < timeframe.EndIndex)
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
                    IEnumerable<ICandlestick> clonedCandlesticks = timeframe.Candlesticks.Take(warmedUpIndex..timeframe.Index).Select(candle => candle.Clone());

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
