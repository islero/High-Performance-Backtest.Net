using Backtest.Net.SymbolsData;

namespace Backtest.Net.Interfaces;

/// <summary>
/// Run the backtesting, prepare symbol data parts before feeding them into strategy
/// 
/// SymbolDataParts parameter is a list of SymbolDataV2 lists.
/// Each inner list is a "part" of data for the same symbol, 
/// and each part is processed sequentially.
/// 
/// Within each part, each SymbolDataV2 is processed in parallel.
/// 
/// </summary>
public interface IEngineV2
{
    // --- Delegates
    public Action? OnCancellationFinishedDelegate { get; set; }
    public Func<SymbolDataV2[], Task> OnTick { get; set; }

    /// <summary>
    /// Main Method that starts the engine
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task RunAsync(List<List<SymbolDataV2>> symbolDataParts, CancellationToken? cancellationToken = null);

    /// <summary>
    /// Gets Current Progress From 0.0 to 100.0
    /// </summary>
    /// <returns></returns>
    public decimal GetProgress();
}