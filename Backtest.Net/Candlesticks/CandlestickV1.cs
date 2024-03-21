using Backtest.Net.Interfaces;

namespace Backtest.Net.Candlesticks;

/// <summary>
/// Simple implementation of ICandlestick interface
/// </summary>
public class CandlestickV1 : ICandlestick
{
    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public DateTime CloseTime { get; set; }

    /// <summary>
    /// Cloning themself method
    /// </summary>
    /// <returns></returns>
    public virtual ICandlestick Clone()
    {
        CandlestickV1 candlestickV1Clone = new CandlestickV1();

        candlestickV1Clone.OpenTime = OpenTime;
        candlestickV1Clone.Open = Open;
        candlestickV1Clone.High = High;
        candlestickV1Clone.Low = Low;
        candlestickV1Clone.Close = Close;
        candlestickV1Clone.CloseTime = CloseTime;

        return candlestickV1Clone;
    }
}