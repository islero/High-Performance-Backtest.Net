namespace Backtest.Net.Interfaces;

/// <summary>
/// Interface for condition parameters
/// </summary>
public interface IConditionParameter
{
    /// <summary>
    /// Condition parameter affected Symbol
    /// </summary>
    public string Symbol { get; set; }

    /// <summary>
    /// Magic Number separates access to transactions
    /// </summary>
    public int? MagicNumber { get; set; }

    /// <summary>
    /// Clear the condition parameter data
    /// </summary>
    public void Clear();

    /// <summary>
    /// Clone Method For All Child Classes
    /// </summary>
    /// <returns></returns>
    public IConditionParameter Clone();

    /// <summary>
    /// Checks if condition parameter is valid
    /// </summary>
    /// <returns></returns>
    public bool IsValid();
}
