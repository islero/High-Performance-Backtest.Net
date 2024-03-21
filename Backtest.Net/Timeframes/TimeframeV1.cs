using Backtest.Net.Enums;
using Backtest.Net.Interfaces;

namespace Backtest.Net.Timeframes;

/// <summary>
/// Simple implementation of ITimeframe interface
/// </summary>
public class TimeframeV1 : ITimeframe
{
    public CandlestickInterval Timeframe { get; set; }
    public int StartIndex { get; set; }
    public int Index { get; set; }
    public int EndIndex { get; set; }
    public bool NoMoreHistory { get; set; } = false;
    public List<ICandlestick> Candlesticks { get; set; } = [];
}