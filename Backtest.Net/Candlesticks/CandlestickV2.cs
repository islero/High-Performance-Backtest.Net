using Models.Net.Interfaces;

namespace Backtest.Net.Candlesticks;

/// <summary>
/// Simple implementation of ICandlestick interface
/// </summary>
public sealed class CandlestickV2
{
    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public DateTime CloseTime { get; set; }

    /// <summary>
    /// Cloning themselves method
    /// </summary>
    /// <returns></returns>
    public CandlestickV2 Clone()
    {
        var candlestickV1Clone = new CandlestickV2
        {
            OpenTime = OpenTime,
            Open = Open,
            High = High,
            Low = Low,
            Close = Close,
            CloseTime = CloseTime
        };

        return candlestickV1Clone;
    }
}