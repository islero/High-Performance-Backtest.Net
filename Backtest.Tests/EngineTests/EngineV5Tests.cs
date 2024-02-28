using Backtest.Net.Engines;

namespace Backtest.Tests.EngineTests
{
    /// <summary>
    /// Testing backtesting Engine V5
    /// </summary>
    public class EngineV5Tests : EngineTests
    {
        /// <summary>
        /// Initializing Engine V5
        /// </summary>
        public EngineV5Tests()
        {
            WarmupCandlesCount = 2;
            Trade = new TestTrade();
            Strategy = new TestStrategy();

            Engine = new EngineV5(WarmupCandlesCount)
            {
                OnTick = OnTickMethod
            };
        }
    }
}
