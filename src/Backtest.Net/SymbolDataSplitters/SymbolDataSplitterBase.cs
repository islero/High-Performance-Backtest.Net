using Backtest.Net.Enums;

namespace Backtest.Net.SymbolDataSplitters;

/// <summary>
/// Represents the base class for handling and managing the splitting of symbol data
/// into smaller datasets for backtesting purposes.
/// </summary>
public abstract class SymbolDataSplitterBase(
    int daysPerSplit,
    int warmupCandlesCount,
    DateTime backtestingStartDateTime,
    CandlestickInterval? warmupTimeframe = null)
{
    // --- Properties
    protected CandlestickInterval? WarmupTimeframe { get; set; } = warmupTimeframe; // The timeframe must be warmed up,
                                                                                    // and all lower timeframes accordingly,
                                                                                    // if null - will be set automatically
    protected int DaysPerSplit { get; } = daysPerSplit; // How many days in one split range should exist?
    protected int WarmupCandlesCount { get; } = warmupCandlesCount; // The number of warmup candles count
    protected DateTime BacktestingStartDateTime { get; } = backtestingStartDateTime; // Backtesting Start DateTime

    /// <summary>
    /// Returns warmup candlestick index if historical data is larger or smaller than WarmupCandlesCount
    /// </summary>
    /// <param name="backtestingStartDateIndex"></param>
    /// <returns></returns>
    protected int GetWarmupCandlestickIndex(int backtestingStartDateIndex)
    {
        // --- The historical data is bigger than the WarmupCandlesCount case
        if (backtestingStartDateIndex > WarmupCandlesCount)
        {
            return backtestingStartDateIndex - WarmupCandlesCount;
        }

        // --- Returning the first index of the candlesticks history
        return 0;
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
