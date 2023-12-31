using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;

namespace Backtest.Net.SymbolDataSplitters
{
    /// <summary>
    /// Symbol data splitter V1
    /// The main goal is split Symbol Data on smaller parts with recalculating indexes of these parts in most efficient
    /// way and use this smaller parts in order to speed up the backtesting process
    /// </summary>
    public class SymbolDataSplitterV1 : ISymbolDataSplitter
    {
        // --- Properties
        protected int DaysPerSplit { get; } // How many days in one split range should exist
        protected int WarmupCandlesCount { get; } // The amount of warmup candles count
        protected DateTime BacktestingStartDateTime { get; } // Backtesting Start DateTime
        protected CandlestickInterval? WarmupTimeframe { get; set; } // The timeframe must be warmed up and all lower timeframes accordingly, if null - will be set automatically

        // --- Constructors
        public SymbolDataSplitterV1(int daysPerSplit, int warmupCandlesCount, DateTime backtestingStartDateTime, CandlestickInterval? warmupTimeframe = null)
        {
            DaysPerSplit = daysPerSplit;
            WarmupCandlesCount = warmupCandlesCount;
            BacktestingStartDateTime = backtestingStartDateTime;
            WarmupTimeframe = warmupTimeframe;
        }

        // --- Methods
        /// <summary>
        /// Main Method that actually splits the symbols data
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IEnumerable<ISymbolData>>> SplitAsync(IEnumerable<ISymbolData> symbolsData)
        {
            // --- Quick Symbol Data validation
            if (!QuickSymbolDataValidation(symbolsData))
                throw new ArgumentException("symbolsData argument contains invalid or not properly sorted data");

            // --- Getting correct warmup timeframe
            WarmupTimeframe = GetWarmupTimeframe(symbolsData);

            IEnumerable<IEnumerable<ISymbolData>> splitSymbolsData = new List<IEnumerable<ISymbolData>>();

            DateTime targetDateTimePart = BacktestingStartDateTime;
            while (!AreAllSymbolDataReachedHistoryEnd(symbolsData))
            {
                var symbolsDataPart = new List<ISymbolData>();
                foreach (var symbol in symbolsData)
                {
                    // --- Checking if there any symbol with no more history
                    if (symbol.Timeframes.Any(x => x.NoMoreHistory))
                    {
                        continue;
                    }

                    // --- Creating new symbol data
                    ISymbolData symbolDataPart = new SymbolDataV1()
                    {
                        Symbol = symbol.Symbol
                    };

                    foreach (var timeframe in symbol.Timeframes)
                    {
                        // --- Setting indexes without adjusting
                        timeframe.Index = GetCandlesticksIndexByDateTime(timeframe.Candlesticks, targetDateTimePart);
                        timeframe.StartIndex = GetWarmupCandlestickIndex(timeframe.Index);
                        
                        timeframe.EndIndex = GetCandlesticksIndexByDateTime(timeframe.Candlesticks, targetDateTimePart.AddDays(DaysPerSplit));
                        if (timeframe.EndIndex == -1)
                        {
                            timeframe.EndIndex = timeframe.Candlesticks.Count() - 1;
                            timeframe.NoMoreHistory = true;
                        }

                        // --- Deleting source candles and readjusting indexes
                        if (timeframe.StartIndex > 0)
                        {
                            // --- Perform readjusting
                            timeframe.Candlesticks = timeframe.Candlesticks.Skip(timeframe.StartIndex);

                            // --- Perform reindexing
                            timeframe.Index -= timeframe.StartIndex;
                            timeframe.EndIndex -= timeframe.StartIndex;
                            timeframe.StartIndex = 0;
                        }

                        // --- Creating candlesticks
                        IEnumerable<ICandlestick> candlesticks = new List<ICandlestick>();

                        // --- Filling the candlesticks with data
                        candlesticks = timeframe.Candlesticks.Take(timeframe.EndIndex).Select(candle => candle.Clone());

                        // --- Creating timeframe
                        ITimeframe timeframePart = new TimeframeV1()
                        {
                            Timeframe = timeframe.Timeframe,
                            StartIndex = timeframe.StartIndex,
                            Index = timeframe.Index,
                            EndIndex = timeframe.EndIndex,
                            Candlesticks = candlesticks,
                        };

                        // --- Appending a timeframe to the timeframes list
                        symbolDataPart.Timeframes = symbolDataPart.Timeframes.Append(timeframePart);
                    }

                    // --- Adding a new item into symbolsDataPart
                    symbolsDataPart.Add(symbolDataPart);

                    // --- Adding days per split after whole part was formed
                    if(symbol == symbolsData.Last())
                    {
                        targetDateTimePart = targetDateTimePart.AddDays(DaysPerSplit);
                    }
                }

                // --- Append symbolsDataPart
                splitSymbolsData = splitSymbolsData.Append(symbolsDataPart);
            }

            return splitSymbolsData;
        }

        /// <summary>
        /// Returns the candlestick index of the targetDateTime, or -1 if the index wasn't found
        /// </summary>
        /// <param name="timeframe"></param>
        /// <returns></returns>
        protected int GetCandlesticksIndexByDateTime(IEnumerable<ICandlestick> candlesticks, DateTime targetDateTime)
        {
            var candlesticksList = candlesticks.ToList();
            return candlesticksList.FindIndex(candle => candle.OpenTime >= targetDateTime);
        }

        /// <summary>
        /// Returns warmup candlestick index, if historical data is larger or smaller than WarmupCandlesCount
        /// </summary>
        /// <param name="backtestingStartDateIndex"></param>
        /// <returns></returns>
        protected int GetWarmupCandlestickIndex(int backtestingStartDateIndex)
        {
            // --- The historical data is bigger than WarmupCandlesCount case
            if (backtestingStartDateIndex > WarmupCandlesCount)
            {
                return backtestingStartDateIndex - WarmupCandlesCount;
            }

            // --- Returning the first index of the candlesticks history
            return 0;
        }

        /// <summary>
        /// Gets WarmupTimeframe, if WarmupTimeframe is null then calculates the WarmupTimeframe automatically based on constant that 
        /// defines how much of the history can be maximum allocated for warmup period
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        protected CandlestickInterval GetWarmupTimeframe(IEnumerable<ISymbolData> symbolsData)
        {
            // --- Return WarmupTimeframe value if it's not null
            if (WarmupTimeframe.HasValue)
                return WarmupTimeframe.Value;

            // --- Setting the lowest symbolsData timeframe
            CandlestickInterval potentialWarmupTimeframe = symbolsData.Min(x => x.Timeframes.Min(y => y.Timeframe));

            foreach (ISymbolData symbol in symbolsData)
            {
                foreach (var timeframe in symbol.Timeframes)
                {
                    // --- No need to perform calculations for same or lower timeframes that has been already set
                    if (timeframe.Timeframe <= potentialWarmupTimeframe)
                        continue;

                    if (timeframe.Candlesticks.Count() > WarmupCandlesCount)
                    {
                        DateTime warmupCandlesCountDate = timeframe.Candlesticks.ElementAt(WarmupCandlesCount).OpenTime;

                        // --- Checking if warming up by using this timeframe will not exceed backtesting start date time
                        if (warmupCandlesCountDate < BacktestingStartDateTime && timeframe.Timeframe > potentialWarmupTimeframe)
                        {
                            potentialWarmupTimeframe = timeframe.Timeframe;
                        }
                    }
                }
            }

            // --- It basically returns the highest timeframe that after warming up not exceed backtesting starting date
            return potentialWarmupTimeframe;
        }

        /// <summary>
        /// Checks if all the symbols reached history end
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        private bool AreAllSymbolDataReachedHistoryEnd(IEnumerable<ISymbolData> symbolsData)
        {
            return symbolsData.All(x => x.Timeframes.Any(tf => tf.NoMoreHistory));
        }

        /// <summary>
        /// Quick not a very accurate way to perform a validation, maybe it will not be necessary in future, and I'll remove it
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        private bool QuickSymbolDataValidation(IEnumerable<ISymbolData> symbolsData)
        {
            foreach (var symbol in symbolsData)
            {
                var priorTimeframe = symbol.Timeframes.First();
                foreach (var timeframe in symbol.Timeframes.Skip(1))
                {
                    // --- Validating that timeframes are sorted by ascending
                    if (timeframe.Timeframe < priorTimeframe.Timeframe)
                    {
                        // --- Timeframes aren't sorted
                        return false;
                    }

                    // --- Validating that candles are sorted by ascending
                    if (timeframe.Candlesticks.Skip(1).First().OpenTime < timeframe.Candlesticks.First().OpenTime)
                    {
                        // --- Candles aren't sorted (very rough check, but it's quicker)
                        return false;
                    }

                    priorTimeframe = timeframe;
                }
            }

            // --- Validation is passed
            return true;
        }
    }
}
