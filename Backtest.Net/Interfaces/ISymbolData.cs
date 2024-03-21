namespace Backtest.Net.Interfaces;

/// <summary>
/// Interface contains all data necessary for backtesting
/// </summary>
public interface ISymbolData
{
    public string Symbol { get; set; }
    public List<ITimeframe> Timeframes { get; set; }
}