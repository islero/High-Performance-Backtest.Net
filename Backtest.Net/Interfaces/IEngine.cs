namespace Backtest.Net.Interfaces
{
    /// <summary>
    /// Run the backtesting, prepare symbol data parts before feeding them into strategy
    /// </summary>
    public interface IEngine
    {
        // --- Delegates
        public Action? OnCancellationFinishedDelegate { get; set; }
        public Func<IEnumerable<ISymbolData>, Task> OnTick { get; set; }

        /// <summary>
        /// Main Method that starts the engine
        /// </summary>
        /// <param name="symbolDataParts"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task RunAsync(IEnumerable<IEnumerable<ISymbolData>> symbolDataParts, CancellationToken? cancellationToken = default);

        /// <summary>
        /// Gets Current Progress From 0.0 to 100.0
        /// </summary>
        /// <returns></returns>
        public decimal GetProgress();
    }
}
