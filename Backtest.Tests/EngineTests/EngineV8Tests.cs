using Backtest.Net.Engines;

namespace Backtest.Tests.EngineTests;

/// <summary>
/// Testing backtesting Engine V8
/// </summary>
public class EngineV8Tests : EngineTests
{
    /// <summary>
    /// Initializing Engine V7
    /// </summary>
    public EngineV8Tests()
    {
        WarmupCandlesCount = 2;
        Trade = new TestTrade();
        Strategy = new TestStrategy();

        EngineV2 = new EngineV8(WarmupCandlesCount)
        {
            OnTick = OnTickMethodV2
        };
    }
}