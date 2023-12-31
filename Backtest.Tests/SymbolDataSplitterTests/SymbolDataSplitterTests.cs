using Backtest.Net.Candlesticks;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;

namespace High_Performance_Backtest.Tests
{
    /// <summary>
    /// Testing Symbol Data Splitter
    /// </summary>
    public class SymbolDataSplitterTests
    {
        [Fact]
        public void TestCorrectResultPartsCount()
        {
            DateTime startingDate = new DateTime(2023, 1, 1);
            int warmupCandlesCount = 2;
            ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(1, warmupCandlesCount, startingDate);

            var generatedSymbolsData = GenerateFakeSymbolsData(new List<string> { "BTCUSDT", "ETHUSDT" }, 
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15 },
                startingDate.AddHours(-warmupCandlesCount), 672);
            var result = symbolDataSplitter.SplitAsync(generatedSymbolsData).Result;

            // --- Checking parts count
            Assert.Equal(3, result.Count());

            // --- Checking parts index open time
            foreach (var partSymbols in result)
            {
                foreach (var part in partSymbols)
                {
                    var firstTimeframe = part.Timeframes.First();
                    var firstCandle = firstTimeframe.Candlesticks.ElementAt(firstTimeframe.Index);

                    foreach (var timeframe in part.Timeframes)
                    {
                        var timeframeCandle = timeframe.Candlesticks.ElementAt(timeframe.Index);

                        Assert.Equal(firstCandle.OpenTime, timeframeCandle.OpenTime);
                    }
                }
            }
        }

        /// <summary>
        /// Generates Fake Symbols Data for testing purposes
        /// </summary>
        /// <param name="symbols"></param>
        /// <param name="timeframes"></param>
        /// <param name="startDate"></param>
        /// <param name="candlesCount"></param>
        /// <returns></returns>
        private List<ISymbolData> GenerateFakeSymbolsData(List<string> symbols, List<CandlestickInterval> intervals, DateTime startDate, int candlesCount)
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
