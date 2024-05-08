using Backtest.Net.Enums;
using Models.Net.Enums;
using Models.Net.Interfaces;
using ISymbolDataSplitter = Backtest.Net.Interfaces.ISymbolDataSplitter;

namespace Backtest.Net.SymbolDataSplitters;

public abstract class SymbolDataSplitterBase(
    int daysPerSplit,
    int warmupCandlesCount,
    DateTime backtestingStartDateTime,
    CandlestickInterval? warmupTimeframe = null)
    : ISymbolDataSplitter
{
    // --- Properties
    protected CandlestickInterval? WarmupTimeframe { get; set; } = warmupTimeframe; // The timeframe must be warmed up and all lower timeframes accordingly, if null - will be set automatically
    protected int DaysPerSplit { get; } = daysPerSplit; // How many days in one split range should exist
    private int WarmupCandlesCount { get; } = warmupCandlesCount; // The amount of warmup candles count
    protected DateTime BacktestingStartDateTime { get; } = backtestingStartDateTime; // Backtesting Start DateTime
        
    // --- Constructors

    // --- Methods
    public abstract Task<List<List<ISymbolData>>> SplitAsync(List<ISymbolData> symbolsData);

    /// <summary>
    /// Returns the candlestick index of the targetDateTime by candle OpenTime, or -1 if the index wasn't found
    /// </summary>
    /// <param name="candlesticks"></param>
    /// <param name="targetDateTime"></param>
    /// <returns></returns>
    protected static int GetCandlesticksIndexByOpenTime(IEnumerable<ICandlestick> candlesticks, DateTime targetDateTime)
    {
        var candlesticksList = candlesticks.ToList();
        var index = candlesticksList.FindIndex(candle => candle.OpenTime >= targetDateTime);

        if (index < 0) index = 0;

        return index;
    }

    /// <summary>
    /// Returns the candlestick index of the targetDateTime by candle CloseTime, or -1 if the index wasn't found
    /// </summary>
    /// <param name="candlesticks"></param>
    /// <param name="targetDateTime"></param>
    /// <returns></returns>
    protected int GetCandlesticksIndexByCloseTime(IEnumerable<ICandlestick> candlesticks, DateTime targetDateTime)
    {
        var candlesticksList = candlesticks.ToList();
        return candlesticksList.FindIndex(candle => candle.CloseTime >= targetDateTime);
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
                if (timeframe.Candlesticks.Count() <= WarmupCandlesCount) continue;
                    
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
    /// Checks if all the symbols reached history end
    /// </summary>
    /// <param name="symbolsData"></param>
    /// <returns></returns>
    protected bool AreAllSymbolDataReachedHistoryEnd(IEnumerable<ISymbolData> symbolsData)
    {
        return symbolsData.All(x => x.Timeframes.Any(tf => tf.NoMoreHistory));
    }

    /// <summary>
    /// Quick not a very accurate way to perform a validation, maybe it will not be necessary in future, and I'll remove it
    /// </summary>
    /// <param name="symbolsData"></param>
    /// <returns></returns>
    protected bool QuickSymbolDataValidation(IEnumerable<ISymbolData> symbolsData)
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
    protected bool IsThereSymbolTimeframeDuplicates(IEnumerable<ISymbolData> symbolsData)
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
    /// Adding days per split to ongoing backtesting time
    /// </summary>
    /// <param name="ongoingBacktestingTime"></param>
    /// <param name="isLastSymbol"></param>
    /// <returns></returns>
    protected DateTime AddDaysToOngoingBacktestingTime(DateTime ongoingBacktestingTime, bool isLastSymbol)
    {
        if (isLastSymbol)
            return ongoingBacktestingTime.AddDays(DaysPerSplit);

        return ongoingBacktestingTime;
    }
}