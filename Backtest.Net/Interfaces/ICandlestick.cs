namespace Backtest.Net.Interfaces;

/// <summary>
/// Simple candlestick interface
/// </summary>
public interface ICandlestick
{
    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public DateTime CloseTime { get; set; }
    public ICandlestick Clone();
}