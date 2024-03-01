namespace Backtest.Net.Interfaces
{
    /// <summary>
    /// Public interface that splits Symbol Data on parts in order to speed up the backtesting process
    /// </summary>
    public interface ISymbolDataSplitter
    {
        public Task<List<List<ISymbolData>>> SplitAsync(List<ISymbolData> symbolsData);
    }
}
