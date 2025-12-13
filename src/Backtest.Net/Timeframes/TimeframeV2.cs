using Backtest.Net.Candlesticks;
using Models.Net.Enums;

namespace Backtest.Net.Timeframes;

/// <summary>
/// Simple implementation of ITimeframe interface
/// </summary>
public sealed class TimeframeV2
{
    public CandlestickInterval Timeframe { get; set; }
    public int StartIndex { get; set; }
    public int Index { get; set; }
    public int EndIndex { get; set; }
    public bool NoMoreHistory { get; set; } = false;
    public List<CandlestickV2> Candlesticks { get; set; } = [];
}