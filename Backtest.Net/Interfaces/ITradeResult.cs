namespace Backtest.Net.Interfaces
{
    /// <summary>
    /// Trade Result
    /// </summary>
    public interface ITradeResult
    {
        public bool Success { get; set; }
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}
