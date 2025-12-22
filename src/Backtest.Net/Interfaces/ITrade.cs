using Backtest.Net.Results;

namespace Backtest.Net.Interfaces;

/// <summary>
/// Sends ISignal to trade class
/// </summary>
public interface ITrade
{
    public Task<BaseResult?> Execute(List<IConditionParameter> conditionParameters);
}
