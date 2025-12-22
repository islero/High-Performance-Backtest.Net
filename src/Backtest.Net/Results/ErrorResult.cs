namespace Backtest.Net.Results;

/// <summary>
/// Error result record contains the information about the error returned from the server
/// to easier understanding and handling the error after it was occurred
/// </summary>
public abstract record ErrorResult
{
    /// <summary>
    /// The message that describes what happened
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// The error code returned from the server
    /// </summary>
    public int? Code { get; set; }

    /// <summary>
    /// String Representation
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        string codeText = string.Empty;
        if (Code != null)
            codeText = $"[{Code}] ";

        return $"{codeText}{Message}";
    }
}
