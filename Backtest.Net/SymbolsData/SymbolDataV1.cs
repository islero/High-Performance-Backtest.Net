using Backtest.Net.Interfaces;

namespace Backtest.Net.SymbolsData
{
    /// <summary>
    /// Simple Symbol Data Class
    /// </summary>
    public class SymbolDataV1 : ISymbolData
    {
        public string Symbol { get; set; }
        public IEnumerable<ITimeframe> Timeframes { get; set; }

        // --- Constructors
        public SymbolDataV1()
        {
            Symbol = string.Empty;
            Timeframes = new List<ITimeframe>();
        }
    }
}
