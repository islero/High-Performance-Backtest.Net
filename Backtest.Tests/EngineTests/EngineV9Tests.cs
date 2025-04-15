using Backtest.Net.Engines;

namespace Backtest.Tests.EngineTests;

/// <summary>
/// Testing backtesting Engine V9
/// </summary>
public class EngineV9Tests : EngineTests
{
    /// <summary>
    /// Initializing Engine V7
    /// </summary>
    public EngineV9Tests()
    {
        WarmupCandlesCount = 2;
        Trade = new TestTrade();
        Strategy = new TestStrategy();

        EngineV2 = new EngineV9(WarmupCandlesCount, true, false)
        {
            OnTick = OnTickMethodV2
        };
    }
}