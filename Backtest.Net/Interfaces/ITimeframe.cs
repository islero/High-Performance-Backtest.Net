using Backtest.Net.Enums;

namespace Backtest.Net.Interfaces
{
    /// <summary>
    /// Simple Timeframe Interface
    /// </summary>
    public interface ITimeframe
    {
        public CandlestickInterval Timeframe { get; set; }
        public int StartIndex { get; set; }
        public int Index { get; set; }
        public int EndIndex { get; set; }
        public bool NoMoreHistory { get; set; }
        public List<ICandlestick> Candlesticks { get; set; }
    }
}
