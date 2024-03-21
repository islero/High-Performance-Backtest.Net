using Backtest.Net.Interfaces;
using Models.Net.Interfaces;

namespace Backtest.Net.SymbolsData;

/// <summary>
/// Simple Symbol Data Class
/// </summary>
public class SymbolDataV1 : ISymbolData
{
    public string Symbol { get; set; }
    public List<ITimeframe> Timeframes { get; set; }

    // --- Constructors
    public SymbolDataV1()
    {
        Symbol = string.Empty;
        Timeframes = new List<ITimeframe>();
    }
}