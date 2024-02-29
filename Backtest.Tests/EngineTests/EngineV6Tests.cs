using Backtest.Net.Engines;

namespace Backtest.Tests.EngineTests
{
    /// <summary>
    /// Testing backtesting Engine V5
    /// </summary>
    public class EngineV6Tests : EngineTests
    {
        /// <summary>
        /// Initializing Engine V6
        /// </summary>
        public EngineV6Tests()
        {
            WarmupCandlesCount = 2;
            Trade = new TestTrade();
            Strategy = new TestStrategy();

            Engine = new EngineV6(WarmupCandlesCount)
            {
                OnTick = OnTickMethod
            };
        }
    }
}
