using Backtest.Net.Candlesticks;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Models.Net.Enums;
using Models.Net.Interfaces;

namespace Backtest.Net.SymbolDataSplitters;

/// <summary>
/// Symbol data splitter V2
/// The main goal is split Symbol Data on smaller parts with recalculating indexes of these parts in the most efficient
/// way and use this smaller parts to speed up the backtesting process
/// </summary>
public class SymbolDataSplitterV2(
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
    /// Main Method that actually splits the symbol data
    /// </summary>
    /// <param name="symbolsData"></param>
    /// <returns></returns>
    public Task<List<List<SymbolDataV2>>> SplitAsyncV2(List<SymbolDataV2> symbolsData)
    {
        // --- Enumerating Symbols Data
        var symbolsDataList = symbolsData.ToList();
            
        // --- Quick Symbol Data validation
        if (!QuickSymbolDataValidationV2(symbolsDataList))
            throw new ArgumentException("symbolsData argument contains invalid or not properly sorted data");

        // --- Symbol or timeframe duplicates validation
        if (IsThereSymbolTimeframeDuplicatesV2(symbolsDataList))
            throw new Exception("symbolsData contain duplicated symbols or timeframes");
            
        // --- Creating Result Split Symbols Data
        List<List<SymbolDataV2>> splitSymbolsData = [];
            
        // --- Checking if splitting is enabled
        if(DaysPerSplit <= 0)
        {
            foreach (var symbol in symbolsData)
            {
                foreach (var timeframe in symbol.Timeframes)
                {
                    // --- Setting indexes without adjusting
                    timeframe.Index =
                        GetCandlesticksIndexByOpenTimeV2(timeframe.Candlesticks, BacktestingStartDateTime);
                    timeframe.StartIndex = GetWarmupCandlestickIndex(timeframe.Index);

                    // --- Calculating EndIndex
                    timeframe.EndIndex = timeframe.Candlesticks.Count - 1;
                }
            }

            splitSymbolsData.Add(symbolsData);
            return Task.FromResult(splitSymbolsData);
        }

        // --- Getting the correct warmup timeframe
        WarmupTimeframe = GetWarmupTimeframeV2(symbolsDataList);

        var ongoingBacktestingTime = BacktestingStartDateTime;
        while (!AreAllSymbolDataReachedHistoryEndV2(symbolsDataList))
        {
            var symbolsDataPart = new List<SymbolDataV2>();
            foreach (var symbol in symbolsDataList)
            {
                // --- Checking if there is any symbol with no more history
                if (symbol.Timeframes.Any(x => x.NoMoreHistory))
                {
                    // --- Adding days per split to ongoing backtesting time
                    ongoingBacktestingTime = AddDaysToOngoingBacktestingTime(ongoingBacktestingTime, symbol == symbolsDataList.Last());

                    continue;
                }

                // --- Creating new symbol data
                var symbolDataPart = new SymbolDataV2
                {
                    Symbol = symbol.Symbol
                };

                foreach (var timeframe in symbol.Timeframes)
                {
                    // --- Setting indexes without adjusting
                    timeframe.Index =
                        GetCandlesticksIndexByOpenTimeV2(timeframe.Candlesticks, ongoingBacktestingTime);
                    timeframe.StartIndex = GetWarmupCandlestickIndex(timeframe.Index);

                    // --- Calculating EndIndex
                    timeframe.EndIndex = GetCandlesticksIndexByCloseTimeV2(timeframe.Candlesticks,
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
                            timeframe.EndIndex = GetCandlesticksIndexByCloseTimeV2(timeframe.Candlesticks,
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
                        // --- Note that candles readjusting is corrupting source candles perform readjusting
                        timeframe.Candlesticks = timeframe.Candlesticks.Skip(timeframe.StartIndex).ToList();

                        // --- Perform reindexing
                        timeframe.Index -= timeframe.StartIndex;
                        timeframe.EndIndex -= timeframe.StartIndex;
                        timeframe.StartIndex = 0;
                    }

                    // --- Filling the candlesticks with data
                    var candlesticks = timeframe.Candlesticks.Take(timeframe.EndIndex + 1)
                        .Select(candle => candle.Clone()).ToList();

                    // --- Creating timeframe
                    var timeframePart = new TimeframeV2
                    {
                        Timeframe = timeframe.Timeframe,
                        StartIndex = timeframe.StartIndex,
                        Index = timeframe.Index,
                        EndIndex = timeframe.EndIndex,
                        Candlesticks = candlesticks,
                    };

                    // --- Appending a timeframe to the timeframe list
                    symbolDataPart.Timeframes.Add(timeframePart);
                }

                // --- Adding a new item into symbolsDataPart
                if (symbolDataPart.Timeframes.Any(x => x.Index > -1))
                    symbolsDataPart.Add(symbolDataPart);

                // --- Adding days per split to ongoing backtesting time
                ongoingBacktestingTime = AddDaysToOngoingBacktestingTime(ongoingBacktestingTime, symbol == symbolsDataList.Last());
            }

            // --- Append symbolsDataPart if it contains any record
            if (symbolsDataPart.Count != 0)
                splitSymbolsData.Add(symbolsDataPart);
        }

        return Task.FromResult(splitSymbolsData);
    }
    
    /// <summary>
    /// Quick not a very accurate way to perform a validation, maybe it will not be necessary in future, and I'll remove it
    /// </summary>
    /// <param name="symbolsData"></param>
    /// <returns></returns>
    protected static bool QuickSymbolDataValidationV2(List<SymbolDataV2> symbolsData)
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

    /// <summary>
    /// Checks for symbol or timeframe duplicates
    /// </summary>
    /// <param name="symbolsData"></param>
    /// <returns></returns>
    private static bool IsThereSymbolTimeframeDuplicatesV2(List<SymbolDataV2> symbolsData)
    {
        var symbolDataList = symbolsData.ToList();
        var symbolDuplicatesExist = symbolDataList.GroupBy(x => x.Symbol).Any(symbol => symbol.Count() > 1);
        var timeframeDuplicatesExist = false;
        foreach (var symbol in symbolDataList)
        {
            // Validating
            if (timeframeDuplicatesExist) continue;

            timeframeDuplicatesExist = symbol.Timeframes.GroupBy(timeframe => timeframe.Timeframe)
                .Any(interval => interval.Count() > 1);
            break;
        }

        return symbolDuplicatesExist || timeframeDuplicatesExist;
    }
    
    /// <summary>
    /// Returns the candlestick index of the targetDateTime by candle OpenTime, or -1 if the index wasn't found
    /// </summary>
    /// <param name="candlesticks"></param>
    /// <param name="targetDateTime"></param>
    /// <returns></returns>
    private static int GetCandlesticksIndexByOpenTimeV2(List<CandlestickV2> candlesticks, DateTime targetDateTime)
    {
        var candlesticksList = candlesticks.ToList();
        var index = candlesticksList.FindIndex(candle => candle.OpenTime >= targetDateTime);

        if (index < 0) index = 0;

        return index;
    }
    
    /// <summary>
    /// Gets WarmupTimeframe, if WarmupTimeframe is null then calculates the WarmupTimeframe automatically based on constant that 
    /// defines how much of the history can be maximum allocated for a warmup period
    /// </summary>
    /// <param name="symbolsData"></param>
    /// <returns></returns>
    protected CandlestickInterval GetWarmupTimeframeV2(List<SymbolDataV2> symbolsData)
    {
        // --- Return WarmupTimeframe value if it is not null
        if (WarmupTimeframe.HasValue)
            return WarmupTimeframe.Value;

        // --- Setting the lowest symbolsData timeframe
        var symbolDataList = symbolsData.ToList();
        var potentialWarmupTimeframe = symbolDataList.Min(x => x.Timeframes.Min(y => y.Timeframe));

        foreach (var symbol in symbolDataList)
        {
            foreach (var timeframe in symbol.Timeframes)
            {
                // --- No need to perform calculations for same or lower timeframes that has been already set
                if (timeframe.Timeframe <= potentialWarmupTimeframe)
                    continue;

                // --- Count Validation
                if (timeframe.Candlesticks.Count <= WarmupCandlesCount) continue;
                    
                var warmupCandlesCountDate = timeframe.Candlesticks.ElementAt(WarmupCandlesCount).OpenTime;

                // --- Checking if warming up by using this timeframe will not exceed backtesting start date time
                if (warmupCandlesCountDate < BacktestingStartDateTime && timeframe.Timeframe > potentialWarmupTimeframe)
                {
                    potentialWarmupTimeframe = timeframe.Timeframe;
                }
            }
        }

        // --- It basically returns the highest timeframe that after warming up not exceed backtesting starting date
        return potentialWarmupTimeframe;
    }
    
    /// <summary>
    /// Checks if all the symbols reached the history end
    /// </summary>
    /// <param name="symbolsData"></param>
    /// <returns></returns>
    private static bool AreAllSymbolDataReachedHistoryEndV2(List<SymbolDataV2> symbolsData)
    {
        return symbolsData.All(x => x.Timeframes.Any(tf => tf.NoMoreHistory));
    }
    
    /// <summary>
    /// Returns the candlestick index of the targetDateTime by candle CloseTime, or -1 if the index wasn't found
    /// </summary>
    /// <param name="candlesticks"></param>
    /// <param name="targetDateTime"></param>
    /// <returns></returns>
    private static int GetCandlesticksIndexByCloseTimeV2(List<CandlestickV2> candlesticks, DateTime targetDateTime)
    {
        var candlesticksList = candlesticks.ToList();
        return candlesticksList.FindIndex(candle => candle.CloseTime >= targetDateTime);
    }

    public override Task<List<List<ISymbolData>>> SplitAsync(List<ISymbolData> symbolsData)
    {
        throw new NotImplementedException();
    }
}