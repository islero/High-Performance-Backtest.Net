using Backtest.Net.Candlesticks;
using Backtest.Net.Enums;

namespace Backtest.Net.Timeframes;

/// <summary>
/// Simple implementation of ITimeframe interface
/// </summary>
public sealed class TimeframeV2
{
    public CandlestickInterval Timeframe { get; init; }
    public int StartIndex { get; set; }
    public int Index { get; set; }
    public int EndIndex { get; set; }
    public bool NoMoreHistory { get; set; }
    public List<CandlestickV2> Candlesticks { get; set; } = [];
}
