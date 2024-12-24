using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V8
/// Aiming to utilize .NET 9 performance gain as much as possible
/// </summary>
/// <param name="warmupCandlesCount"></param>
public sealed class EngineV8(int warmupCandlesCount, bool useFullCandleForCurrent = false) : IEngineV2
{
    // --- Delegates
    /// <summary>
    /// Main Action of the Engine, it passes data to OnTick function
    /// </summary>
    public required Func<List<SymbolDataV2>, Task> OnTick { get; set; }
    
    /// <summary>
    /// Notifies about backtesting cancellation
    /// </summary>
    public Action? OnCancellationFinishedDelegate { get; set; }
    
    // --- Properties
    private int WarmupCandlesCount { get; } = warmupCandlesCount; // The number of warmup candles count

    /// <summary>
    /// Determines whether the backtester uses the full (completed) candle for the current candle logic.
    /// When set to true, the backtester will treat the current candle as fully formed,
    /// including all its OHLC (Open, High, Low, Close) data, rather than partial real-time data.
    /// </summary>
    /// <remarks>
    /// Enabling this option can impact backtesting behavior by ensuring that decisions
    /// are made based on completed candle data, which is particularly useful for historical backtesting.
    /// If set to false, the current candle data will be treated as incomplete.
    /// </remarks>
    private bool UseFullCandleForCurrent { get; } = useFullCandleForCurrent;
    
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task RunAsync(List<List<SymbolDataV2>> symbolDataParts,
        CancellationToken? cancellationToken = null)
    {
        try
        {
            // Apply the sum of all parts' EndIndexes to MaxIndex
            ApplySumOfEndIndexes(symbolDataParts);

            var ct = cancellationToken ?? CancellationToken.None;

            // Iterate through each symbolDataPart
            foreach (var part in symbolDataParts)
            {
                // Pre-extract the first timeframes for faster checks
                var firstTimeframes = part.Select(x => x.Timeframes.First()).ToList();

                // Main cycle
                while (firstTimeframes.All(tf => tf.Index < tf.EndIndex))
                {
                    // Check for cancellation
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    // Prepare feeding data (synchronous parallel code now returns CompletedTask)
                    var feedingData = await CloneFeedingSymbolData(part).ConfigureAwait(false);

                    // Apply open price to OHLC for all first candles (also synchronous parallel)
                    if (!UseFullCandleForCurrent)
                        await HandleOhlc(feedingData).ConfigureAwait(false);

                    // Trigger OnTick action (assuming OnTick is truly async and must be awaited)
                    await OnTick(feedingData).ConfigureAwait(false);

                    // Increment indexes (also synchronous parallel)
                    await IncrementIndexes(part).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation has been requested and handled
            OnCancellationFinishedDelegate?.Invoke();
        }
    }

    /// <summary>
    /// Cloning necessary symbol data range
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    private Task<List<SymbolDataV2>> CloneFeedingSymbolData(List<SymbolDataV2> symbolData)
    {
        var count = symbolData.Count;
        var result = new SymbolDataV2[count];

        Parallel.For(0, count, i =>
        {
            var originalSymbol = symbolData[i];
            var originalTimeframes = originalSymbol.Timeframes;
            var timeframeCount = originalTimeframes.Count;

            // Pre-allocate the timeframe list with capacity equal to the symbol's timeframe count.
            var clonedTimeframes = new List<TimeframeV2>(timeframeCount);

            for (var j = 0; j < timeframeCount; j++)
            {
                var originalTimeframe = originalTimeframes[j];

                // Calculate the start index for warmed-up candlesticks.
                var warmedUpIndex = originalTimeframe.Index - WarmupCandlesCount > originalTimeframe.StartIndex
                    ? originalTimeframe.Index - WarmupCandlesCount
                    : originalTimeframe.StartIndex;

                // Calculate how many candlesticks to copy.
                var length = (originalTimeframe.Index + 1) - warmedUpIndex;

                // Create a copy of the necessary candlesticks via GetRange.
                var clonedCandlesticks = originalTimeframe.Candlesticks.GetRange(warmedUpIndex, length);

                // Build the cloned timeframe object.
                var newTimeframe = new TimeframeV2
                {
                    Timeframe = originalTimeframe.Timeframe,
                    Candlesticks = clonedCandlesticks
                };

                clonedTimeframes.Add(newTimeframe);
            }

            // Create a new symbol data instance.
            result[i] = new SymbolDataV2
            {
                Symbol = originalSymbol.Symbol,
                Timeframes = clonedTimeframes
            };
        });

        // Convert the result array to a List without extra concurrency overhead.
        return Task.FromResult(result.ToList());
    }

    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    private static Task HandleOhlc(List<SymbolDataV2> symbolData)
    {
        Parallel.ForEach(symbolData, symbol =>
        {
            // Clone the last candle of the first timeframe
            var firstTimeframe = symbol.Timeframes[0];
            var referenceCandle = firstTimeframe.Candlesticks[^1].Clone();
            referenceCandle.High = referenceCandle.Open;
            referenceCandle.Low = referenceCandle.Open;
            referenceCandle.Close = referenceCandle.Open;
            referenceCandle.CloseTime = referenceCandle.OpenTime;

            foreach (var timeframe in symbol.Timeframes)
            {
                // If we must reverse:
                timeframe.Candlesticks.Reverse();

                // Now the last candle is at index 0 due to the reversal
                var candle = timeframe.Candlesticks[0].Clone();

                candle.Close = referenceCandle.Close;
                candle.CloseTime = referenceCandle.CloseTime;
                candle.High = referenceCandle.High;
                candle.Low = referenceCandle.Low;

                timeframe.Candlesticks[0] = candle;
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Increments Symbol Data indexes
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    private Task IncrementIndexes(List<SymbolDataV2> symbolData)
    {
        // Use Parallel.ForEach to avoid async overhead
        Parallel.ForEach(symbolData, symbol =>
        {
            var timeframes = symbol.Timeframes;
            var firstTimeframe = timeframes[0];
            var lowestTimeframeIndexTime = DateTime.MinValue;

            // Handle the lowest timeframe (firstTimeframe)
            if (firstTimeframe.Index >= firstTimeframe.StartIndex && firstTimeframe.Index < firstTimeframe.EndIndex)
            {
                firstTimeframe.Index++;
                lowestTimeframeIndexTime = firstTimeframe.Candlesticks[firstTimeframe.Index].OpenTime;
                Index = firstTimeframe.Index; // Managing bot progress
            }

            // Handle higher timeframes
            for (var i = 1; i < timeframes.Count; i++)
            {
                var timeframe = timeframes[i];
                // Check we can safely access the current candle at timeframe.Index
                if (timeframe.Index >= timeframe.StartIndex && timeframe.Index < timeframe.EndIndex)
                {
                    // Extract closeTime once
                    var closeTime = timeframe.Candlesticks[timeframe.Index].CloseTime;
                    if (closeTime < lowestTimeframeIndexTime)
                    {
                        timeframe.Index++;
                    }
                }
            }
        });

        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Index iterator to manage backtesting progress
    /// </summary>
    private decimal Index { get; set; }
    
    
    /// <summary>
    /// Max Index property to calculate the total index increments during the backtesting
    /// </summary>
    private decimal MaxIndex { get; set; }
    
    /// <summary>
    /// Applies Sum of all EndIndexes to MaxIndex
    /// </summary>
    /// <param name="symbolDataParts"></param>
    private void ApplySumOfEndIndexes(List<List<SymbolDataV2>> symbolDataParts)
    {
        // --- Getting Symbols Data that have highest EndIndexes
        var maxSymbol = symbolDataParts.Select(x => x.MaxBy(
            y => y.Timeframes.First().EndIndex));

        // --- Selecting EndIndexes and forming array from them
        var endIndexesArray = maxSymbol.Select(x => x!.Timeframes.First().EndIndex).ToArray();
        
        // --- Calculating Sum of the all-parts max indexes
        MaxIndex = endIndexesArray.Sum();
    }

    /// <summary>
    /// Gets Current Progress From 0.0 to 100.0
    /// </summary>
    /// <returns></returns>
    public decimal GetProgress()
    {
        // --- Validating dividing by 0
        if (MaxIndex == 0) return 0;
        
        // --- Returning current accurate progress
        return Index / MaxIndex * 100;
    }
}