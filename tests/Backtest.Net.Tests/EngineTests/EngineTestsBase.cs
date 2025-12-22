using Backtest.Net.Interfaces;
using Backtest.Net.Results;
using Backtest.Net.SymbolsData;

namespace Backtest.Net.Tests.EngineTests;

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
    public Action<List<IConditionParameter>>? TradeSignalDelegate { get; set; }

    /// <summary>
    /// Execute Signal Test
    /// </summary>
    /// <param name="conditionParameters"></param>
    /// <returns></returns>
    public Task<BaseResult?> Execute(List<IConditionParameter> conditionParameters)
    {
        TradeSignalDelegate?.Invoke(conditionParameters);
        return Task.FromResult<BaseResult?>(null);
    }
}

/// <summary>
/// Represents a test strategy implementation designed for use in backtesting scenarios.
/// </summary>
public class TestStrategyV2
{
    public Action<List<SymbolDataV2>>? ExecuteStrategyDelegateV2 { get; set; }

    public Task<List<IConditionParameter>> ExecuteV2(List<SymbolDataV2> symbolDataList)
    {
        ExecuteStrategyDelegateV2?.Invoke(symbolDataList);

        return Task.FromResult(Enumerable.Empty<IConditionParameter>().ToList());
    }
}
