using Backtest.Net.Interfaces;

namespace Backtest.Net.Trades;

/// <summary>
/// Empty Trade For Test And Benchmarking Purposes
/// </summary>
public class EmptyTrade : ITrade
{
    public Task<ITradeResult?> ExecuteSignal(ISignal signal)
    {
        return Task.FromResult<ITradeResult?>(new EmptyTradeResult());
    }
}