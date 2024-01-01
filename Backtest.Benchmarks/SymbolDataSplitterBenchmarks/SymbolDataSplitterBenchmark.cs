using Backtest.Net.Candlesticks;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backtest.Benchmarks.SymbolDataSplitterBenchmarks
{
    /// <summary>
    /// The benchmark class was created to improve executing speed of SymbolDataSplitter
    /// </summary>
    [MemoryDiagnoser]
    public class SymbolDataSplitterBenchmark
    {
        // --- Properties
        private List<ISymbolData> GeneratedSymbolsData { get; set; }
        private DateTime StartingDate { get; set; }
        private int DaysPerSplit { get; set; }
        private int WarmupCandlesCount { get; set; }

        // --- Constructors
        public SymbolDataSplitterBenchmark()
        {
            StartingDate = new DateTime(2023, 1, 1, 3, 6, 50);
            DaysPerSplit = 1;
            WarmupCandlesCount = 2;

            GeneratedSymbolsData = GenerateFakeSymbolsData(new List<string> { "BTCUSDT", "ETHUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15 },
                StartingDate.AddHours(-WarmupCandlesCount), 1000);
        }

        // --- Benchmarks
        [Benchmark(Baseline = true)]
        public async Task SymbolDataSplitterV1()
        {
            ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(DaysPerSplit, WarmupCandlesCount, StartingDate, true);

            var result = await symbolDataSplitter.SplitAsync(GeneratedSymbolsData);
        }

        [Benchmark]
        public async Task SymbolDataSplitterV1_WithoutCorrectingEndIndexes()
        {
            ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(DaysPerSplit, WarmupCandlesCount, StartingDate, false);

            var result = await symbolDataSplitter.SplitAsync(GeneratedSymbolsData);
        }

        // --- Methods
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
