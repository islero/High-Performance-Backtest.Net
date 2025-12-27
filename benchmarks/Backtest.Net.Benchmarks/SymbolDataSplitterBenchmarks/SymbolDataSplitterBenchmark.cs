using Backtest.Net.Candlesticks;
using Backtest.Net.Enums;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using BenchmarkDotNet.Attributes;

namespace Backtest.Net.Benchmarks.SymbolDataSplitterBenchmarks;

/// <summary>
/// The benchmark class was created to improve the executing speed of SymbolDataSplitter
/// </summary>
[MemoryDiagnoser]
public class SymbolDataSplitterBenchmark
{
    // --- Properties
    private DateTime StartingDate { get; set; }
    private int DaysPerSplit { get; set; }
    private int WarmupCandlesCount { get; set; }

    // --- Setup
    [GlobalSetup]
    public void Setup()
    {
        StartingDate = new DateTime(2023, 1, 1, 3, 6, 50);
        DaysPerSplit = 1;
        WarmupCandlesCount = 2;
    }

    /// <summary>
    /// Generates Fake Symbols Data for testing purposes
    /// </summary>
    /// <param name="symbols"></param>
    /// <param name="intervals"></param>
    /// <param name="startDate"></param>
    /// <param name="candlesCount"></param>
    /// <returns></returns>
    public static List<SymbolDataV2> GenerateFakeSymbolsDataV2(List<string> symbols, List<CandlestickInterval> intervals, DateTime startDate, int candlesCount)
    {
        var result = new List<SymbolDataV2>();

        // --- Create symbols
        foreach (var symbol in symbols)
        {
            // --- Generating candles
            var filledTimeframes = new List<TimeframeV2>();
            foreach (var interval in intervals)
            {
                var currentTimeframe = new TimeframeV2
                {
                    Timeframe = interval
                };

                for (int i = 0; i < candlesCount; i++)
                {
                    double basePrice = Random.Shared.NextDouble() * Random.Shared.Next(1000, 10000);
                    double baseMovement = (basePrice * 0.8) * Random.Shared.NextSingle();

                    var candlestick = new CandlestickV2
                    {
                        OpenTime = startDate.AddSeconds((double)i * (int)currentTimeframe.Timeframe),
                        Open = (decimal)basePrice,
                        High = (decimal)basePrice + (decimal)baseMovement,
                        Low = (decimal)basePrice - (decimal)baseMovement,
                        Close = Random.Shared.NextSingle() > 0.5 ? (decimal)basePrice + (decimal)baseMovement * (decimal)0.7 : (decimal)basePrice - (decimal)baseMovement * (decimal)0.7,
                        CloseTime = startDate.AddSeconds((double)i * (int)currentTimeframe.Timeframe).AddSeconds((int)currentTimeframe.Timeframe).AddSeconds(-1)
                    };
                    currentTimeframe.Candlesticks.Add(candlestick);
                }
                filledTimeframes.Add(currentTimeframe);
            }

            result.Add(new SymbolDataV2
            {
                Symbol = symbol,
                Timeframes = filledTimeframes,
            });
        }

        return result;
    }
}
