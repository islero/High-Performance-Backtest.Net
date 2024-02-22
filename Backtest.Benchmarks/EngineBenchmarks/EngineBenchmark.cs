using Backtest.Benchmarks.SymbolDataSplitterBenchmarks;
using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.Strategies;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
using Backtest.Net.Trades;
using BenchmarkDotNet.Attributes;
using Newtonsoft.Json;

namespace Backtest.Benchmarks.EngineBenchmarks;

/// <summary>
/// Benchmarking engine performance
/// </summary>
[MemoryDiagnoser]
public class EngineBenchmark
{
    // --- Properties
    private IEnumerable<ISymbolData>? GeneratedSymbolsData { get; set; }
    private IEnumerable<IEnumerable<ISymbolData>> SplittedData;
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

        GeneratedSymbolsData = SymbolDataSplitterBenchmark.GenerateFakeSymbolsData(["BTCUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.D1],
            StartingDate.AddHours(-WarmupCandlesCount), 5000);
        
        ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(DaysPerSplit, WarmupCandlesCount, StartingDate, true);

        SplittedData = await symbolDataSplitter.SplitAsync(GeneratedSymbolsData!);
    }
    
    [Benchmark(Baseline = true)]
    public async Task EngineV1_Run()
    {
        var engine = new EngineV1(WarmupCandlesCount, new EmptyTrade(), new EmptyStrategy());
        await engine.RunAsync(SplittedData);
    }
    
    [Benchmark]
    public async Task EngineV2_Run()
    {
        var engine = new EngineV2(WarmupCandlesCount, new EmptyTrade(), new EmptyStrategy());
        await engine.RunAsync(SplittedData);
    }
    
    [Benchmark]
    public async Task EngineV3_Run()
    {
        var engine = new EngineV3(WarmupCandlesCount, new EmptyTrade(), new EmptyStrategy());
        await engine.RunAsync(SplittedData);
    }
}