using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.SymbolDataSplitters
{
    /// <summary>
    /// Symbol data splitter V1
    /// The main goal is split Symbol Data on smaller parts with recalculating indexes of these parts in most efficient
    /// way and use this smaller parts in order to speed up the backtesting process
    /// </summary>
    public class SymbolDataSplitterV1 : SymbolDataSplitterBase
    {
        // --- Properties
        protected bool CorrectEndIndex { get; } // Automatically corrects EndIndex if there is no more history for any other timeframe

        // --- Constructors
        public SymbolDataSplitterV1(int daysPerSplit, int warmupCandlesCount, DateTime backtestingStartDateTime, bool correctEndIndex = false, CandlestickInterval? warmupTimeframe = null) 
            : base(daysPerSplit, warmupCandlesCount, backtestingStartDateTime, warmupTimeframe)
        {
            CorrectEndIndex = correctEndIndex;
        }

        // --- Methods
        /// <summary>
        /// Main Method that actually splits the symbols data
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<IEnumerable<ISymbolData>>> SplitAsync(IEnumerable<ISymbolData> symbolsData)
        {
            // --- Quick Symbol Data validation
            if (!QuickSymbolDataValidation(symbolsData))
                throw new ArgumentException("symbolsData argument contains invalid or not properly sorted data");

            // --- Symbol or timeframe duplicates validation
            if (IsThereSymbolTimeframeDuplicates(symbolsData))
                throw new Exception("symbolsData contain duplicated symbols or timeframes");

            // --- Getting correct warmup timeframe
            WarmupTimeframe = GetWarmupTimeframe(symbolsData);

            IEnumerable<IEnumerable<ISymbolData>> splitSymbolsData = new List<IEnumerable<ISymbolData>>();

            DateTime ongoingBacktestingTime = BacktestingStartDateTime;
            while (!AreAllSymbolDataReachedHistoryEnd(symbolsData))
            {
                var symbolsDataPart = new List<ISymbolData>();
                foreach (var symbol in symbolsData)
                {
                    // --- Checking if there any symbol with no more history
                    if (symbol.Timeframes.Any(x => x.NoMoreHistory))
                    {
                        // --- Adding days per split to ongoing backtesting time
                        ongoingBacktestingTime = AddDaysToOngoingBacktestingTime(ongoingBacktestingTime, symbol == symbolsData.Last());

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
                        timeframe.Index = GetCandlesticksIndexByOpenTime(timeframe.Candlesticks, ongoingBacktestingTime);
                        timeframe.StartIndex = GetWarmupCandlestickIndex(timeframe.Index);

                        // --- Calculating EndIndex
                        timeframe.EndIndex = GetCandlesticksIndexByCloseTime(timeframe.Candlesticks, ongoingBacktestingTime.AddDays(DaysPerSplit).AddSeconds(-1));

                        // --- Correcting EndIndex
                        if (CorrectEndIndex && !timeframe.NoMoreHistory)
                        {
                            // --- Searching for a Timeframe that has no more history
                            var targetTimeframe = symbol.Timeframes.FirstOrDefault(x => x.NoMoreHistory);
                            if (targetTimeframe != null)
                            {
                                // --- Searching for target candle
                                var targetCandle = targetTimeframe.Candlesticks.ElementAt(targetTimeframe.EndIndex);
                                if (targetCandle != null)
                                {
                                    timeframe.EndIndex = GetCandlesticksIndexByCloseTime(timeframe.Candlesticks, targetCandle.CloseTime);
                                }
                            }
                        }

                        // --- Validating EndIndex
                        if (timeframe.EndIndex == -1)
                        {
                            timeframe.EndIndex = timeframe.Candlesticks.Count() - 1;
                            timeframe.NoMoreHistory = true;
                        }

                        // --- Check if symbol history already began
                        if (timeframe.Index == 0 && timeframe.StartIndex == 0 && timeframe.EndIndex == 0)
                            continue;

                        // --- Deleting source candles and readjusting indexes
                        if (timeframe.StartIndex > 0)
                        {
                            // --- Please, note that candles readjusting is corrupting source candles
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
                        candlesticks = timeframe.Candlesticks.Take(timeframe.EndIndex + 1).Select(candle => candle.Clone());

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
                    if (symbolDataPart.Timeframes.Any())
                        symbolsDataPart.Add(symbolDataPart);

                    // --- Adding days per split to ongoing backtesting time
                    ongoingBacktestingTime = AddDaysToOngoingBacktestingTime(ongoingBacktestingTime, symbol == symbolsData.Last());
                }

                // --- Append symbolsDataPart if it contain any record
                if (symbolsDataPart.Any())
                    splitSymbolsData = splitSymbolsData.Append(symbolsDataPart);
            }

            return splitSymbolsData;
        }
    }
}
