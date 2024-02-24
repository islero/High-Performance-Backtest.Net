using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;

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

            Engine = new EngineV2(WarmupCandlesCount, Trade, Strategy);
        }
    }
}
