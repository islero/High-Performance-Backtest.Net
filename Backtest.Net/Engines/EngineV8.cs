using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines;

/// <summary>
/// Represents the EngineV8 class, which is responsible for running backtesting simulations.
/// Aiming to use .NET 9 performance gains as much as possible
/// </summary>
public class EngineV8(int warmupCandlesCount, bool useFullCandleForCurrent) : IEngineV2
{
    // --- Delegates
    /// <summary>
    /// Main Action of the Engine, it passes data to OnTick function
    /// </summary>
    public required Func<SymbolDataV2[], Task> OnTick { get; set; }
    
    /// <summary>
    /// Notifies about backtesting cancellation
    /// </summary>
    public Action? OnCancellationFinishedDelegate { get; set; }
    
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task RunAsync(List<List<SymbolDataV2>> symbolDataParts,
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
                var lastTimeframes = part.Select(x => x.Timeframes.Last()).ToList();

                // Main cycle
                while (firstTimeframes.All(tf => tf.Index < tf.EndIndex) && lastTimeframes.All(tf => tf.Index < tf.EndIndex))
                {
                    // Check for cancellation
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    // Prepare feeding data (synchronous parallel code now returns CompletedTask)
                    var feedingData = await CloneFeedingSymbolData(part).ConfigureAwait(false);

                    // Apply open price to OHLC for all first candles (also synchronous parallel)
                    if (!useFullCandleForCurrent)
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
    protected Task<SymbolDataV2[]> CloneFeedingSymbolData(List<SymbolDataV2> symbolData)
    {
        var count = symbolData.Count;
        var result = new SymbolDataV2[count];

        // Execute in parallel to boost performance for large symbolData lists.
        Parallel.For(0, count, i =>
        {
            var originalSymbol = symbolData[i];
            var originalTimeframes = originalSymbol.Timeframes;
            var timeframeCount = originalTimeframes.Count;

            // Pre-allocate a timeframe list matching the symbol's timeframe count.
            var clonedTimeframes = new List<TimeframeV2>(timeframeCount);

            for (var j = 0; j < timeframeCount; j++)
            {
                var originalTimeframe = originalTimeframes[j];

                // Calculate the 'warmed-up' start index.
                var warmedUpIndex = 
                    originalTimeframe.Index - warmupCandlesCount > originalTimeframe.StartIndex
                        ? originalTimeframe.Index - warmupCandlesCount
                        : originalTimeframe.StartIndex;

                // Determine how many candlesticks to copy.
                var length = originalTimeframe.Index + 1 - warmedUpIndex;

                // Clone the necessary candlesticks.
                var clonedCandlesticks = originalTimeframe.Candlesticks.GetRange(warmedUpIndex, length);

                // Create the new timeframe.
                clonedTimeframes.Add(new TimeframeV2
                {
                    Timeframe = originalTimeframe.Timeframe,
                    Candlesticks = clonedCandlesticks
                });
            }

            // Construct the cloned symbol data.
            result[i] = new SymbolDataV2
            {
                Symbol = originalSymbol.Symbol,
                Timeframes = clonedTimeframes
            };
        });

        // Return the result array as a List without extra concurrency overhead.
        return Task.FromResult(result);
    }

    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    private static Task HandleOhlc(SymbolDataV2[] symbolData)
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
    protected Task IncrementIndexesOld(List<SymbolDataV2> symbolData)
    {
        // Use Parallel.ForEach to process symbolData concurrently.
        Parallel.ForEach(symbolData, symbol =>
        {
            var timeframes = symbol.Timeframes;
            var firstTimeframe = timeframes[0];
            var firstTimeframeIndex = firstTimeframe.Index;

            // Initialize this to an invalid date so we can compare with valid times later.
            var lowestTimeframeIndexTime = DateTime.MinValue;

            // 1) Handle the lowest timeframe (index = 0)
            // Make sure we're within the valid range [StartIndex..EndIndex).
            if (firstTimeframeIndex >= firstTimeframe.StartIndex && firstTimeframeIndex < firstTimeframe.EndIndex)
            {
                firstTimeframeIndex++;
                // Safely update the timeframe's Index
                firstTimeframe.Index = firstTimeframeIndex;

                // Extract OpenTime of the candle at the newly incremented index
                lowestTimeframeIndexTime = firstTimeframe.Candlesticks[firstTimeframeIndex].OpenTime;

                // Optionally track this globally/externally
                Index = firstTimeframeIndex;
            }

            // 2) Handle the higher timeframes (index = 1..Count-1)
            var timeframesCount = timeframes.Count;
            for (var i = 1; i < timeframesCount; i++)
            {
                var timeframe = timeframes[i];
                var idx = timeframe.Index;

                // Still within the valid range
                if (idx >= timeframe.StartIndex && idx < timeframe.EndIndex)
                {
                    // Compare the current candlestick's CloseTime to the "lowest" timeframe's OpenTime
                    var closeTime = timeframe.Candlesticks[idx].CloseTime;
                    // firstTimeframeIndex > firstTimeframe.Index means there are no candles left 
                    if (closeTime < lowestTimeframeIndexTime/* || (i == 1 && firstTimeframeIndex > firstTimeframe.Index)*/)
                    {
                        // Only increment if it is strictly below the reference time
                        idx++;
                        timeframe.Index = idx;
                    }
                }
            }
        });

        return Task.CompletedTask;
    }
    
    protected Task IncrementIndexes(List<SymbolDataV2> symbolData)
    {
        Parallel.ForEach(symbolData, symbol =>
        {
            var timeframes = symbol.Timeframes;
            var tfCount = timeframes.Count;

            // Find the first timeframe with an Index < EndIndex.
            // This becomes our base timeframe.
            var baseTfIndex = -1;
            for (var i = 0; i < tfCount; i++)
            {
                if (timeframes[i].Index + 1 < timeframes[i].EndIndex)
                {
                    baseTfIndex = i;
                    break;
                }

                timeframes[i].Index = timeframes[i].EndIndex;
                Index = MaxIndex;
            }

            // No valid timeframe found – nothing to update.
            if (baseTfIndex == -1)
                return;

            var baseTf = timeframes[baseTfIndex];

            // Ensure the base timeframe is within the valid range.
            if (baseTf.Index < baseTf.StartIndex || baseTf.Index >= baseTf.EndIndex)
                return;

            // Increment the base timeframe's index and use its new candle's OpenTime as the reference.
            baseTf.Index++;
            // Optionally track this globally/externally
            if (Index != MaxIndex)
                Index = baseTf.Index;
            
            var referenceTime = baseTf.Candlesticks[baseTf.Index].OpenTime;

            // For all other timeframes, increment their index if the current candle's CloseTime is strictly less than
            // the reference time.
            for (var i = 0; i < tfCount; i++)
            {
                if (i <= baseTfIndex)
                    continue;

                var tf = timeframes[i];
                if (tf.Index >= tf.StartIndex && tf.Index < tf.EndIndex)
                {
                    if (tf.Candlesticks[tf.Index].CloseTime < referenceTime)
                    {
                        tf.Index++;
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
    protected void ApplySumOfEndIndexes(List<List<SymbolDataV2>> symbolDataParts)
    {
        // --- Getting Symbols Data that have highest EndIndexes
        var maxSymbol = symbolDataParts.Select(x => x.MaxBy(
            y => y.Timeframes.First().EndIndex));

        // --- Selecting EndIndexes and forming an array from them
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