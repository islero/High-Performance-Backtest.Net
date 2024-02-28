using Backtest.Net.Engines;

namespace Backtest.Tests.EngineTests
{
    /// <summary>
    /// Testing backtesting Engine V4
    /// </summary>
    public class EngineV4Tests : EngineTests
    {
        /// <summary>
        /// Initializing Engine V4
        /// </summary>
        public EngineV4Tests()
        {
            WarmupCandlesCount = 2;
            Trade = new TestTrade();
            Strategy = new TestStrategy();

            Engine = new EngineV4(WarmupCandlesCount)
            {
                OnTick = OnTickMethod
            };
        }
    }
}
