namespace Backtest.Net.Enums;

/// <summary>
/// This enum contains a completed list of Timeframes that can be used for BacktestingExecutor
/// </summary>
public enum CandlestickInterval
{
    /// <summary>
    /// 1 Tick
    /// </summary>
    Tick = 0,

    /// <summary>
    /// 1 Second
    /// </summary>
    Second = 1,

    /// <summary>
    /// 1 Minute
    /// </summary>
    M1 = 60,

    /// <summary>
    /// 3 Minutes
    /// </summary>
    M3 = 60 * 3,

    /// <summary>
    /// 5 Minutes
    /// </summary>
    M5 = 60 * 5,

    /// <summary>
    /// 10 Minutes
    /// </summary>
    M10 = 60 * 10,

    /// <summary>
    /// 15 Minutes
    /// </summary>
    M15 = 60 * 15,

    /// <summary>
    /// 30 Minutes
    /// </summary>
    M30 = 60 * 30,

    /// <summary>
    /// 1 Hour
    /// </summary>
    H1 = 60 * 60,

    /// <summary>
    /// 2 Hours
    /// </summary>
    H2 = 60 * 60 * 2,

    /// <summary>
    /// 4 Hours
    /// </summary>
    H4 = 60 * 60 * 4,

    /// <summary>
    /// 6 Hours
    /// </summary>
    H6 = 60 * 60 * 6,

    /// <summary>
    /// 8 Hours
    /// </summary>
    H8 = 60 * 60 * 8,

    /// <summary>
    /// 12 Hours
    /// </summary>
    H12 = 60 * 60 * 12,

    /// <summary>
    /// 1 Day
    /// </summary>
    D1 = 60 * 60 * 24,

    /// <summary>
    /// 3 Days
    /// </summary>
    D3 = 60 * 60 * 24 * 3,

    /// <summary>
    /// 1 Week
    /// </summary>
    W1 = 60 * 60 * 24 * 7,

    /// <summary>
    /// 1 Month
    /// </summary>
    MN1 = 60 * 60 * 24 * 30,

    /// <summary>
    /// Custom interval
    /// </summary>
    Custom = 9999999
}
