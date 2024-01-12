using Backtest.Net.Candlesticks;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using System.Text;

namespace Backtest.Tests
{
    /// <summary>
    /// Base class for all tests
    /// </summary>
    public abstract class TestBase
    {
        /// <summary>
        /// Generates Fake Symbols Data for testing purposes
        /// </summary>
        /// <param name="symbols"></param>
        /// <param name="timeframes"></param>
        /// <param name="startDate"></param>
        /// <param name="candlesCount"></param>
        /// <returns></returns>
        protected List<ISymbolData> GenerateFakeSymbolsData(List<string> symbols, List<CandlestickInterval> intervals, DateTime startDate, int candlesCount)
        {
            List<ISymbolData> result = new List<ISymbolData>();

            // --- Create symbols
            foreach (var symbol in symbols)
            {
                // --- Generating candles
                List<ITimeframe> filledTimeframes = new List<ITimeframe>();
                foreach (var interval in intervals)
                {
                    ITimeframe currentTimeframe = new TimeframeV1();
                    currentTimeframe.Timeframe = interval;
                    for (int i = 0; i < candlesCount; i++)
                    {
                        double basePrice = Random.Shared.NextDouble() * Random.Shared.Next(1000, 10000);
                        double baseMovement = (basePrice * 0.8) * Random.Shared.NextSingle();

                        ICandlestick candlestick = new CandlestickV1()
                        {
                            OpenTime = startDate.AddSeconds(i * (int)currentTimeframe.Timeframe),
                            Open = (decimal)basePrice,
                            High = (decimal)basePrice + (decimal)baseMovement,
                            Low = (decimal)basePrice - (decimal)baseMovement,
                            Close = Random.Shared.NextSingle() > 0.5 ? (decimal)basePrice + ((decimal)baseMovement * (decimal)0.7) : (decimal)basePrice - ((decimal)baseMovement * (decimal)0.7),
                            CloseTime = startDate.AddSeconds(i * (int)currentTimeframe.Timeframe).AddSeconds((int)currentTimeframe.Timeframe).AddSeconds(-1)

                        };
                        currentTimeframe.Candlesticks = currentTimeframe.Candlesticks.Append(candlestick);
                    }
                    filledTimeframes.Add(currentTimeframe);
                }

                result.Add(new SymbolDataV1()
                {
                    Symbol = symbol,
                    Timeframes = filledTimeframes,
                });
            }

            return result;
        }
    }
}
