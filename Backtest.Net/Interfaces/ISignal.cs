namespace Backtest.Net.Interfaces;

/// <summary>
/// Interface that returns IStrategy to execute strategy signal
/// </summary>
public interface ISignal
{
    public string Symbol { get; set; }
}