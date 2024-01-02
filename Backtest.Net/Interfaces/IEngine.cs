namespace Backtest.Net.Interfaces
{
    /// <summary>
    /// Run the backtesting, prepare symbol data parts before feeding them into strategy
    /// </summary>
    public interface IEngine
    {
        public Task<IEnumerable<ISymbolData>> RunAsync(IEnumerable<IEnumerable<ISymbolData>> symbolDataParts);
    }
}
