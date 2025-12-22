namespace Backtest.Net.Results;

/// <summary>
/// Base record for API result records
/// </summary>
public abstract record BaseResult
{
    /// <summary>
    /// Indicates whether the result is successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Contains the error details
    /// </summary>
    public ErrorResult? Error { get; set; }
}
