using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;

namespace Backtest.Tests.EngineTests
{
    /// <summary>
    /// Testing backtesting Engine V3
    /// </summary>
    public class EngineV3Tests : EngineTests
    {
        /// <summary>
        /// Initializing Engine V3
        /// </summary>
        public EngineV3Tests()
        {
            WarmupCandlesCount = 2;
            Trade = new TestTrade();
            Strategy = new TestStrategy();

            Engine = new EngineV3(WarmupCandlesCount, Trade, Strategy);
        }
    }
}
