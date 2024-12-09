using Backtest.Net.Engines;

namespace Backtest.Tests.EngineTests;

/// <summary>
/// Testing backtesting Engine V7
/// </summary>
public class EngineV7Tests : EngineTests
{
    /// <summary>
    /// Initializing Engine V7
    /// </summary>
    public EngineV7Tests()
    {
        WarmupCandlesCount = 2;
        Trade = new TestTrade();
        Strategy = new TestStrategy();

        Engine = new EngineV7(WarmupCandlesCount)
        {
            OnTick = OnTickMethod
        };
    }
}