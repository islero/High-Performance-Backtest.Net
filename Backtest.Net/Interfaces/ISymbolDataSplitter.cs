namespace Backtest.Net.Interfaces
{
    /// <summary>
    /// Internal interface that splits Symbol Data on parts in order to speed up the backtesting process
    /// </summary>
    public interface ISymbolDataSplitter
    {
        public Task<IEnumerable<IEnumerable<ISymbolData>>> SplitAsync(IEnumerable<ISymbolData> symbolsData);
    }
}
