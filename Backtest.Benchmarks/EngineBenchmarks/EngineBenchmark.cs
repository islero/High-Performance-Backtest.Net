using Backtest.Benchmarks.SymbolDataSplitterBenchmarks;
using Backtest.Net.Engines;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
using BenchmarkDotNet.Attributes;
using Models.Net.Enums;
using Models.Net.Interfaces;

namespace Backtest.Benchmarks.EngineBenchmarks;

/// <summary>
/// Benchmarking engine performance
/// </summary>
[MemoryDiagnoser]
public class EngineBenchmark
{
    // --- Properties
    private List<ISymbolData>? GeneratedSymbolsData { get; set; }
    private List<SymbolDataV2>? GeneratedSymbolsDataV2 { get; set; }
    private List<List<ISymbolData>> SplittedData;
    private List<List<SymbolDataV2>> SplittedDataV2;
    private DateTime StartingDate { get; set; }
    private int DaysPerSplit { get; set; }
    private int WarmupCandlesCount { get; set; }

    // --- Setup
    [GlobalSetup]
    public async Task Setup()
    {
        StartingDate = new DateTime(2023, 1, 1, 3, 6, 50);
        DaysPerSplit = 1;
        WarmupCandlesCount = 2;

        GeneratedSymbolsData = SymbolDataSplitterBenchmark.GenerateFakeSymbolsData(["BTCUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.D1],
            StartingDate.AddHours(-WarmupCandlesCount), 5000);
        
        var symbolDataSplitter = new SymbolDataSplitterV1(DaysPerSplit, WarmupCandlesCount, StartingDate, true);
        SplittedData = await symbolDataSplitter.SplitAsync(GeneratedSymbolsData!);
        
        GeneratedSymbolsDataV2 = SymbolDataSplitterBenchmark.GenerateFakeSymbolsDataV2(["BTCUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.D1],
            StartingDate.AddHours(-WarmupCandlesCount), 5000);
        
        var symbolDataSplitterV2 = new SymbolDataSplitterV2(DaysPerSplit, WarmupCandlesCount, StartingDate, false);
        SplittedDataV2 = await symbolDataSplitterV2.SplitAsyncV2(GeneratedSymbolsDataV2);
    }
    
    //[Benchmark(Baseline = true)]
    public async Task EngineV1_Run()
    {
        var engine = new EngineV1(WarmupCandlesCount)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedData);
    }
    
    //[Benchmark]
    public async Task EngineV2_Run()
    {
        var engine = new EngineV2(WarmupCandlesCount)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedData);
    }
    
    //[Benchmark]
    public async Task EngineV3_Run()
    {
        var engine = new EngineV3(WarmupCandlesCount)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedData);
    }
    
    //[Benchmark]
    //[Benchmark(Baseline = true)]
    public async Task EngineV4_Run()
    {
        var engine = new EngineV4(WarmupCandlesCount)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedData);
    }
    
    //[Benchmark]
    public async Task EngineV5_Run()
    {
        var engine = new EngineV5(WarmupCandlesCount)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedData);
    }
    
    //[Benchmark]
    public async Task EngineV6_Run()
    {
        var engine = new EngineV6(WarmupCandlesCount)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedData);
    }
    
    [Benchmark(Baseline = true)]
    public async Task EngineV7_Run()
    {
        var engine = new EngineV7(WarmupCandlesCount)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedData);
    }
    
    [Benchmark]
    public async Task EngineV8_Run()
    {
        var engine = new EngineV8(WarmupCandlesCount, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedDataV2);
    }
    
    [Benchmark]
    public async Task EngineV9_Run()
    {
        var engine = new EngineV9(WarmupCandlesCount, true, false)
        {
            OnTick = _ => Task.CompletedTask
        };
        await engine.RunAsync(SplittedDataV2);
    }
}