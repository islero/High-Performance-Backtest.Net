using Backtest.Benchmarks.EngineBenchmarks;
using Backtest.Benchmarks.SymbolDataSplitterBenchmarks;
using BenchmarkDotNet.Running;

var eb = new EngineBenchmark();
await eb.Setup();
// await eb.EngineV1_Run();
BenchmarkRunner.Run<EngineBenchmark>();