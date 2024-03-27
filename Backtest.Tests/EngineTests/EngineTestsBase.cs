using Backtest.Net.Interfaces;
using Models.Net.ConditionParameters;
using Models.Net.Interfaces;

namespace Backtest.Tests.EngineTests
{
    /// <summary>
    /// Base class for all engine tests
    /// </summary>
    public class EngineTestsBase : TestBase;

    /// <summary>
    /// Class for trade testing
    /// </summary>
    public class TestTrade : ITrade
    {
        // --- Delegates
        public Action<ISignal>? TradeSignalDelegate { get; set; }

        /// <summary>
        /// Execute Signal Test
        /// </summary>
        /// <param name="signal"></param>
        /// <returns></returns>
        public Task<ITradeResult?> ExecuteSignal(ISignal signal)
        {
            TradeSignalDelegate?.Invoke(signal);
            return Task.FromResult<ITradeResult?>(null);
        }
    }

    /// <summary>
    /// Class for strategy testing
    /// </summary>
    public class TestStrategy : IStrategy
    {
        public Action<IEnumerable<ISymbolData>>? ExecuteStrategyDelegate { get; set; }

        public Task<IEnumerable<ISignal>> Execute(IEnumerable<ISymbolData> symbols,
            CloseConditionParameter close,
            ModifyConditionParameter modify,
            OpenConditionParameter open)
        {
            ExecuteStrategyDelegate?.Invoke(symbols);
            
            return Task.FromResult(Enumerable.Empty<ISignal>());
        }
    }
}
