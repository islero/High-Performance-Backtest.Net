using Backtest.Net.Interfaces;

namespace Backtest.Net.Strategies;

/// <summary>
/// The Empty Strategy Created for Unit Tests and Benchmarking Purposes
/// </summary>
public class EmptyStrategy : IStrategy
{
    public Task<IEnumerable<ISignal>> Execute(IEnumerable<ISymbolData> symbols)
    {
        // Do nothing here
        return Task.FromResult(Enumerable.Empty<ISignal>());
    }
}