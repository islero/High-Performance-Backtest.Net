using Models.Net.Interfaces;

namespace Backtest.Net.SymbolsData;

/// <summary>
/// Simple Symbol Data Class
/// </summary>
public class SymbolDataV1 : ISymbolData
{
    public string Symbol { get; set; } = string.Empty;
    public List<ITimeframe> Timeframes { get; set; } = [];
}