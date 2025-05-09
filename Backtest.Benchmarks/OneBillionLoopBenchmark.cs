using BenchmarkDotNet.Attributes;

namespace Backtest.Benchmarks;

public class OneBillionLoopBenchmark
{
    [Benchmark]
    public void Combined_Loop_Run()
    {
        for (var i = 0; i < 1000; i++)
        {
            for (var j = 0; j < 1_000_000; j++)
            {
                // Do nothing-only loop
            }
        }
    }
    
    [Benchmark]
    public void One_Loop_Run()
    {
        for (var i = 0; i < 1_000_000_000; i++)
        {
            // Do nothing-only loop
        }
    }
}