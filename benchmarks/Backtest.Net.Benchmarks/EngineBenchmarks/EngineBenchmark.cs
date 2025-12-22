using Backtest.Net.Benchmarks.SymbolDataSplitterBenchmarks;
using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
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
            [CandlestickInterval.M5, CandlestickInterval.D1],
            StartingDate.AddHours(-WarmupCandlesCount), 5000);

        var symbolDataSplitterV2 = new SymbolDataSplitterV2(DaysPerSplit, WarmupCandlesCount, StartingDate);
        SplitDataV2 = await symbolDataSplitterV2.SplitAsyncV2(GeneratedSymbolsDataV2);
    }

    [Benchmark]
    public async Task EngineV8Run()
    {
        var engine = new EngineV8(WarmupCandlesCount, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplitDataV2!);
    }

    [Benchmark]
    public async Task EngineV9Run()
    {
        var engine = new EngineV9(WarmupCandlesCount, true, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplitDataV2!);
    }

    [Benchmark]
    public async Task EngineV10Run()
    {
        var engine = new EngineV10(WarmupCandlesCount, true, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplitDataV2!);
    }
}
