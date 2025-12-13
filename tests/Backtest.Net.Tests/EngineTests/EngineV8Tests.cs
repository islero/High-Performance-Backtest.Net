using Backtest.Net.Engines;

namespace Backtest.Tests.EngineTests;

/// <summary>
/// Testing backtesting Engine V8
/// </summary>
public class EngineV8Tests : EngineTestsV2
{
    /// <summary>
    /// Initializing Engine V8
    /// </summary>
    public EngineV8Tests()
    {
        WarmupCandlesCount = 2;
        Trade = new TestTrade();
        Strategy = new TestStrategyV2();

        EngineV2 = new EngineV8(WarmupCandlesCount, false)
        {
            OnTick = OnTickMethodV2
        };
    }
}