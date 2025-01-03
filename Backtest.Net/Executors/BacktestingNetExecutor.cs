using System.Reflection;
using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
using Models.Net.Enums;

namespace Backtest.Net.Executors;

/// <summary>
/// High-Performance Backtesting Executor
/// </summary>
public sealed class BacktestingNetExecutor
{
    // --- Properties
    public static bool IsRunning { get; private set; } // Checks whether backtesting is currently running
    private DateTime StartDateTime { get; } // Backtesting Start DateTime
    private int WarmupCandlesCount { get; } // The number of warmup candles count
    private bool CorrectEndIndex { get; } // Makes sure the end index is the same for all symbols and timeframes
    private CandlestickInterval? WarmupTimeframe { get; } // The timeframe must be warmed up and all lower timeframes
                                                          // accordingly, if null – is set automatically
    private int DaysPerSplit { get; } // Decides how many days in one split range should exist?
    // Concrete types were used on purpose based on this article to improve performance
    // CA1859: Use concrete types when possible for improved performance
    private SymbolDataSplitterV2 Splitter { get; set;  } // Splits entire history on smaller pieces
    private EngineV8 Engine { get; set;  } // The backtesting engine itself, performs backtesting,
                                            // passes prepared history into strategy

    // --- Delegates
    public Action<BacktestingEventStatus, string?>? OnBacktestingEvent; // Notifies subscribed objects about backtesting events

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="startDateTime"></param>
    /// <param name="useFullCandleForCurrent"></param>
    /// <param name="daysPerSplit"></param>
    /// <param name="warmupCandlesCount"></param>
    /// <param name="onTick"></param>
    /// <param name="correctEndIndex"></param>
    /// <param name="warmupTimeframe"></param>
    public BacktestingNetExecutor(DateTime startDateTime,
        int warmupCandlesCount, 
        Func<SymbolDataV2[], Task> onTick, 
        bool useFullCandleForCurrent = false,
        int daysPerSplit = 0,
        bool correctEndIndex = false,
        CandlestickInterval? warmupTimeframe = null)
    {
        StartDateTime = startDateTime;
        WarmupCandlesCount = warmupCandlesCount;
        CorrectEndIndex = correctEndIndex;
        WarmupTimeframe = warmupTimeframe;
        DaysPerSplit = daysPerSplit;
        
        // --- Create and Select DataSplitter version
        Splitter = new SymbolDataSplitterV2(DaysPerSplit, WarmupCandlesCount, StartDateTime, CorrectEndIndex,
            WarmupTimeframe);

        // --- Create and Select Engine version
        Engine = new EngineV8(WarmupCandlesCount, useFullCandleForCurrent)
        {
            OnTick = onTick
        };
    }
    
    // --- Methods
    /// <summary>
    /// Performing the actual backtesting process
    /// </summary>
    /// <param name="symbolsData"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ArgumentException"></exception>
    public async Task PerformAsync(List<SymbolDataV2> symbolsData, CancellationToken cancellationToken = default)
    {
        // --- Validating Symbols Data
        if(symbolsData == null || symbolsData.Count == 0)
            throw new ArgumentException("Symbols Data argument is null or has no elements");

        IsRunning = true;
        // --- Triggering On Started Backtesting Status
        NotifyBacktestingEvent(BacktestingEventStatus.Started,
            Assembly.GetExecutingAssembly().GetName().Version?.ToString());

        // --- Split Symbols Data
        NotifyBacktestingEvent(BacktestingEventStatus.SplitStarted, Splitter.GetType().Name);
        var symbolDataParts = await Splitter.SplitAsyncV2(symbolsData);
        NotifyBacktestingEvent(BacktestingEventStatus.SplitFinished, Splitter.GetType().Name);

        // --- Removing invalid indexes
        foreach (var part in symbolDataParts)
        {
            part.RemoveAll(symbolData => 
                symbolData.Timeframes.Any(timeframe => timeframe.EndIndex < 0));
        }
        
        // --- Run Engine
        NotifyBacktestingEvent(BacktestingEventStatus.EngineStarted, Engine.GetType().Name);
        await Engine.RunAsync(symbolDataParts, cancellationToken);
        NotifyBacktestingEvent(BacktestingEventStatus.EngineFinished, Engine.GetType().Name);
        
        IsRunning = false;
        
        // --- Triggering On Finished Backtesting Status
        NotifyBacktestingEvent(BacktestingEventStatus.Finished,
            Assembly.GetExecutingAssembly().GetName().Version?.ToString());
    }

    /// <summary>
    /// Returning Current backtesting progress
    /// </summary>
    /// <returns></returns>
    public decimal BacktestingProgress()
    {
        return Engine.GetProgress();
    }
    
    /// <summary>
    /// Notifies Subscribed Objects about backtesting status
    /// </summary>
    /// <param name="eventStatus"></param>
    /// <param name="additionalDetails"></param>
    /// <returns></returns>
    private void NotifyBacktestingEvent(BacktestingEventStatus eventStatus, string? additionalDetails = default)
    {
        OnBacktestingEvent?.Invoke(eventStatus, additionalDetails);
    }
}