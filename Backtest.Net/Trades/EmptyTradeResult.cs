using Backtest.Net.Interfaces;

namespace Backtest.Net.Trades;

/// <summary>
/// Empty Trade Result
/// </summary>
public class EmptyTradeResult : ITradeResult
{
    public bool Success { get; set; }
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
}