namespace Backtest.Net.Interfaces
{
    /// <summary>
    /// Interface for strategies that can be executed by BacktestingExecutor
    /// </summary>
    public interface IStrategy
    {
        public Task<List<ISignal>> Execute(IEnumerable<ISymbolData> symbols);
    }
}
