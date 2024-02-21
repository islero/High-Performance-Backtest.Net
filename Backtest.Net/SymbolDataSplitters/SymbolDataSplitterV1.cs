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
    public class SymbolDataSplitterV1(
        int daysPerSplit,
        int warmupCandlesCount,
        DateTime backtestingStartDateTime,
        bool correctEndIndex = false,
        CandlestickInterval? warmupTimeframe = null)
        : SymbolDataSplitterBase(daysPerSplit, warmupCandlesCount, backtestingStartDateTime, warmupTimeframe)
    {
        // --- Properties
        private bool CorrectEndIndex { get; } = correctEndIndex; // Automatically corrects EndIndex if there is no more history for any other timeframe

        // --- Methods
        /// <summary>
        /// Main Method that actually splits the symbols data
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        public override Task<IEnumerable<IEnumerable<ISymbolData>>> SplitAsync(IEnumerable<ISymbolData> symbolsData)
        {
            // --- Enumerating Symbols Data
            var symbolsDataList = symbolsData.ToList();
            
            // --- Quick Symbol Data validation
            if (!QuickSymbolDataValidation(symbolsDataList))
                throw new ArgumentException("symbolsData argument contains invalid or not properly sorted data");

            // --- Symbol or timeframe duplicates validation
            if (IsThereSymbolTimeframeDuplicates(symbolsDataList))
                throw new Exception("symbolsData contain duplicated symbols or timeframes");
            
            // --- Creating Result Split Symbols Data
            IEnumerable<IEnumerable<ISymbolData>> splitSymbolsData = new List<IEnumerable<ISymbolData>>();
            
            // --- Checking if splitting is enabled
            if(DaysPerSplit <= 0)
            {
                foreach (var symbol in symbolsData)
                {
                    foreach (var timeframe in symbol.Timeframes)
                    {
                        // --- Setting indexes without adjusting
                        timeframe.Index =
                            GetCandlesticksIndexByOpenTime(timeframe.Candlesticks, BacktestingStartDateTime);
                        timeframe.StartIndex = GetWarmupCandlestickIndex(timeframe.Index);

                        // --- Calculating EndIndex
                        timeframe.EndIndex = timeframe.Candlesticks.Count() - 1;
                    }
                }
                return Task.FromResult(splitSymbolsData.Append(symbolsData));
            }

            // --- Getting correct warmup timeframe
            WarmupTimeframe = GetWarmupTimeframe(symbolsDataList);

            var ongoingBacktestingTime = BacktestingStartDateTime;
            while (!AreAllSymbolDataReachedHistoryEnd(symbolsDataList))
            {
                var symbolsDataPart = new List<ISymbolData>();
                foreach (var symbol in symbolsDataList)
                {
                    // --- Checking if there any symbol with no more history
                    if (symbol.Timeframes.Any(x => x.NoMoreHistory))
                    {
                        // --- Adding days per split to ongoing backtesting time
                        ongoingBacktestingTime = AddDaysToOngoingBacktestingTime(ongoingBacktestingTime, symbol == symbolsDataList.Last());

                        continue;
                    }

                    // --- Creating new symbol data
                    ISymbolData symbolDataPart = new SymbolDataV1
                    {
                        Symbol = symbol.Symbol
                    };

                    foreach (var timeframe in symbol.Timeframes)
                    {
                        // --- Setting indexes without adjusting
                        timeframe.Index =
                            GetCandlesticksIndexByOpenTime(timeframe.Candlesticks, ongoingBacktestingTime);
                        timeframe.StartIndex = GetWarmupCandlestickIndex(timeframe.Index);

                        // --- Calculating EndIndex
                        timeframe.EndIndex = GetCandlesticksIndexByCloseTime(timeframe.Candlesticks,
                            ongoingBacktestingTime.AddDays(DaysPerSplit).AddSeconds(-1));

                        // --- Correcting EndIndex
                        if (CorrectEndIndex && !timeframe.NoMoreHistory)
                        {
                            // --- Searching for a Timeframe that has no more history
                            var targetTimeframe = symbol.Timeframes.FirstOrDefault(x => x.NoMoreHistory);
                            if (targetTimeframe != null)
                            {
                                // --- Searching for target candle
                                var targetCandle = targetTimeframe.Candlesticks.ElementAt(targetTimeframe.EndIndex);
                                timeframe.EndIndex = GetCandlesticksIndexByCloseTime(timeframe.Candlesticks,
                                    targetCandle.CloseTime);
                            }
                        }

                        // --- Validating EndIndex
                        if (timeframe.EndIndex == -1)
                        {
                            timeframe.EndIndex = timeframe.Candlesticks.Count() - 1;
                            timeframe.NoMoreHistory = true;
                        }

                        // --- Check if symbol history already began
                        if (timeframe is { Index: 0, StartIndex: 0, EndIndex: 0 })
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

                        // --- Filling the candlesticks with data
                        var candlesticks = timeframe.Candlesticks.Take(timeframe.EndIndex + 1)
                            .Select(candle => candle.Clone());

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
                    ongoingBacktestingTime = AddDaysToOngoingBacktestingTime(ongoingBacktestingTime, symbol == symbolsDataList.Last());
                }

                // --- Append symbolsDataPart if it contain any record
                if (symbolsDataPart.Count != 0)
                    splitSymbolsData = splitSymbolsData.Append(symbolsDataPart);
            }

            return Task.FromResult(splitSymbolsData);
        }
    }
}
