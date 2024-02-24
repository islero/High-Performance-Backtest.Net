using Backtest.Net.Engines;

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

            Engine = new EngineV3(WarmupCandlesCount)
            {
                OnTick = async symbolData =>
                {
                    var signals = await Strategy.Execute(symbolData);
                    if (signals.Any())
                    {
                        foreach (var signal in signals)
                        {
                            _ = await Trade.ExecuteSignal(signal);
                        }
                    }
                }
            };
        }
    }
}
