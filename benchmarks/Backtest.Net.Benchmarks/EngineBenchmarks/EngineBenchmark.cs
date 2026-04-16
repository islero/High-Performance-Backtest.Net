using Backtest.Net.Benchmarks.SymbolDataSplitterBenchmarks;
using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using BenchmarkDotNet.Attributes;

namespace Backtest.Net.Benchmarks.EngineBenchmarks;

/// <summary>
/// Benchmarking engine performance
/// </summary>
[MemoryDiagnoser]
public class EngineBenchmark
{
    // --- Properties
    private List<SymbolDataV2>? GeneratedSymbolsDataV2 { get; set; }
    private List<List<SymbolDataV2>>? SplitDataV2 { get; set; }
    private int[][][]? InitialIndexes { get; set; }
    private DateTime StartingDate { get; set; }
    private int DaysPerSplit { get; set; }
    private int WarmupCandlesCount { get; set; }

    // --- Setup
    [GlobalSetup]
    public async Task Setup()
    {
        StartingDate = new DateTime(2023, 1, 1, 3, 6, 50);
        DaysPerSplit = 0;
        WarmupCandlesCount = 2;

        GeneratedSymbolsDataV2 = SymbolDataSplitterBenchmark.GenerateFakeSymbolsDataV2(["BTCUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.M15, CandlestickInterval.M30, CandlestickInterval.H4],
            StartingDate.AddHours(-WarmupCandlesCount), 1_000_000);

        var symbolDataSplitterV2 = new SymbolDataSplitterV2(DaysPerSplit, WarmupCandlesCount, StartingDate);
        SplitDataV2 = await symbolDataSplitterV2.SplitAsyncV2(GeneratedSymbolsDataV2);
        InitialIndexes = CaptureIndexes(SplitDataV2);
    }

    public void ResetInputIndexes()
    {
        ResetIndexes(SplitDataV2!, InitialIndexes!);
    }

    [Benchmark]
    public async Task EngineV8Run()
    {
        ResetInputIndexes();

        var engine = new EngineV8(WarmupCandlesCount, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplitDataV2!);
    }

    [Benchmark]
    public async Task EngineV9Run()
    {
        ResetInputIndexes();

        var engine = new EngineV9(WarmupCandlesCount, true, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplitDataV2!);
    }

    [Benchmark]
    public async Task EngineV10Run()
    {
        ResetInputIndexes();

        var engine = new EngineV10(WarmupCandlesCount, true, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplitDataV2!);
    }

    [Benchmark]
    public async Task EngineV11Run()
    {
        ResetInputIndexes();

        var engine = new EngineV11(WarmupCandlesCount, true, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplitDataV2!);
    }

    private static int[][][] CaptureIndexes(List<List<SymbolDataV2>> splitData)
    {
        var result = new int[splitData.Count][][];

        for (int partIndex = 0; partIndex < splitData.Count; partIndex++)
        {
            List<SymbolDataV2> part = splitData[partIndex];
            result[partIndex] = new int[part.Count][];

            for (int symbolIndex = 0; symbolIndex < part.Count; symbolIndex++)
            {
                List<TimeframeV2> timeframes = part[symbolIndex].Timeframes;
                result[partIndex][symbolIndex] = new int[timeframes.Count];

                for (int timeframeIndex = 0; timeframeIndex < timeframes.Count; timeframeIndex++)
                    result[partIndex][symbolIndex][timeframeIndex] = timeframes[timeframeIndex].Index;
            }
        }

        return result;
    }

    private static void ResetIndexes(List<List<SymbolDataV2>> splitData, int[][][] initialIndexes)
    {
        for (int partIndex = 0; partIndex < splitData.Count; partIndex++)
        {
            List<SymbolDataV2> part = splitData[partIndex];

            for (int symbolIndex = 0; symbolIndex < part.Count; symbolIndex++)
            {
                List<TimeframeV2> timeframes = part[symbolIndex].Timeframes;

                for (int timeframeIndex = 0; timeframeIndex < timeframes.Count; timeframeIndex++)
                    timeframes[timeframeIndex].Index = initialIndexes[partIndex][symbolIndex][timeframeIndex];
            }
        }
    }
}
