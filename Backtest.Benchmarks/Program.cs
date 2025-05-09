using Backtest.Benchmarks;
using Backtest.Benchmarks.EngineBenchmarks;
using BenchmarkDotNet.Running;

//var eb = new EngineBenchmark();
//await eb.Setup();
//await eb.EngineV1_Run();
BenchmarkRunner.Run<EngineBenchmark>();
//BenchmarkRunner.Run<OneBillionLoopBenchmark>();