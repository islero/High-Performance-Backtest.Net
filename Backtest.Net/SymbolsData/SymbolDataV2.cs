using Backtest.Net.Timeframes;

namespace Backtest.Net.SymbolsData;

/// <summary>
/// Simple Symbol Data Class
/// </summary>
public class SymbolDataV2
{
    public string Symbol { get; set; } = string.Empty;
    public List<TimeframeV2> Timeframes { get; set; } = [];
}