namespace Backtest.Net.Interfaces;

/// <summary>
/// Sends ISignal to trade class
/// </summary>
public interface ITrade
{
    public Task<ITradeResult?> ExecuteSignal(ISignal signal);
}