using Backtest.Net.Engines;

namespace Backtest.Tests.EngineTests
{
    /// <summary>
    /// Testing backtesting Engine V2
    /// </summary>
    public class EngineV2Tests : EngineTests
    {
        /// <summary>
        /// Initializing Engine V2
        /// </summary>
        public EngineV2Tests()
        {
            WarmupCandlesCount = 2;
            Trade = new TestTrade();
            Strategy = new TestStrategy();

            Engine = new EngineV2(WarmupCandlesCount)
            {
                OnTick = OnTickMethod
            };
                
        }
    }
}
