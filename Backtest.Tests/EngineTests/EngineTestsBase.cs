using Backtest.Net.SymbolsData;
using Models.Net.ApiResults;
using Models.Net.Interfaces;

namespace Backtest.Tests.EngineTests;

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
/// Class for strategy testing
/// </summary>
public class TestStrategy : IStrategy
{
    public Action<List<ISymbolData>>? ExecuteStrategyDelegate { get; set; }
    public Action<List<SymbolDataV2>>? ExecuteStrategyDelegateV2 { get; set; }

    public Task<List<IConditionParameter>> Execute(List<ISymbolData> symbolDataList)
    {
        ExecuteStrategyDelegate?.Invoke(symbolDataList);
            
        return Task.FromResult(Enumerable.Empty<IConditionParameter>().ToList());
    }
    
    public Task<List<IConditionParameter>> ExecuteV2(List<SymbolDataV2> symbolDataList)
    {
        ExecuteStrategyDelegateV2?.Invoke(symbolDataList);
            
        return Task.FromResult(Enumerable.Empty<IConditionParameter>().ToList());
    }
}

/// <summary>
/// Class for strategy testing
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