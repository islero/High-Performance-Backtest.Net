using Backtest.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backtest.Tests.EngineTests
{
    /// <summary>
    /// Base class for all engine tests
    /// </summary>
    public class EngineTestsBase : TestBase
    {

    }

    /// <summary>
    /// Class for trade testing
    /// </summary>
    public class TestTrade : ITrade
    {
        public Action<ISignal> TradeSignalDelegate { get; set; }

        public Task<ITradeResult> ExecuteSignal(ISignal signal)
        {
            if (TradeSignalDelegate != null)
                TradeSignalDelegate(signal);
            return null;
        }
    }

    /// <summary>
    /// Class for strategy testing
    /// </summary>
    public class TestStrategy : IStrategy
    {
        public Action<IEnumerable<ISymbolData>> ExecuteStrategyDelegate { get; set; }

        public async Task<IEnumerable<ISignal>> Execute(IEnumerable<ISymbolData> symbols)
        {
            if (ExecuteStrategyDelegate != null)
                ExecuteStrategyDelegate(symbols);
            return Enumerable.Empty<ISignal>();
        }
    }
}
