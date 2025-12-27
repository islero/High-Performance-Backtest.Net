using Backtest.Net.Candlesticks;
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

            CancellationToken ct = cancellationToken ?? CancellationToken.None;

            // Iterate through each symbolDataPart
            foreach (List<SymbolDataV2> part in symbolDataParts)
            {
                // Pre-extract the first timeframes for faster checks
                var firstTimeframes = part.Select(x => x.Timeframes.First()).ToList();

                // Main cycle
                while (firstTimeframes.All(tf => tf.Index < tf.EndIndex))
                {
                    ct.ThrowIfCancellationRequested();

                    // Prepare feeding data (synchronous parallel code now returns CompletedTask)
                    SymbolDataV2[] feedingData = await CloneFeedingSymbolData(part).ConfigureAwait(false);

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
        int count = symbolData.Count;
        var result = new SymbolDataV2[count];

        // Execute in parallel to boost performance for large symbolData lists.
        Parallel.For(0, count, i =>
        {
            SymbolDataV2 originalSymbol = symbolData[i];
            List<TimeframeV2> originalTimeframes = originalSymbol.Timeframes;
            int timeframeCount = originalTimeframes.Count;

            // Pre-allocate a timeframe list matching the symbol's timeframe count.
            var clonedTimeframes = new List<TimeframeV2>(timeframeCount);

            for (int j = 0; j < timeframeCount; j++)
            {
                TimeframeV2 originalTimeframe = originalTimeframes[j];

                // Calculate the 'warmed-up' start index.
                int warmedUpIndex =
                    originalTimeframe.Index - warmupCandlesCount > originalTimeframe.StartIndex
                        ? originalTimeframe.Index - warmupCandlesCount
                        : originalTimeframe.StartIndex;

                // Determine how many candlesticks to copy.
                int length = originalTimeframe.Index + 1 - warmedUpIndex;

                // Clone the necessary candlesticks.
                List<CandlestickV2> clonedCandlesticks = originalTimeframe.Candlesticks.GetRange(warmedUpIndex, length);

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
            TimeframeV2 firstTimeframe = symbol.Timeframes[0];
            CandlestickV2 referenceCandle = firstTimeframe.Candlesticks[^1].Clone();
            referenceCandle.High = referenceCandle.Open;
            referenceCandle.Low = referenceCandle.Open;
            referenceCandle.Close = referenceCandle.Open;
            referenceCandle.CloseTime = referenceCandle.OpenTime;

            foreach (TimeframeV2 timeframe in symbol.Timeframes)
            {
                // If we must reverse:
                timeframe.Candlesticks.Reverse();

                // Now the last candle is at index 0 due to the reversal
                CandlestickV2 candle = timeframe.Candlesticks[0].Clone();

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
        // Use Parallel.ForEach to process symbolData concurrently.
        Parallel.ForEach(symbolData, symbol =>
        {
            List<TimeframeV2> timeframes = symbol.Timeframes;
            TimeframeV2 firstTimeframe = timeframes[0];
            int firstTimeframeIndex = firstTimeframe.Index;

            // Initialize this to an invalid date so we can compare with valid times later.
            DateTime lowestTimeframeIndexTime = DateTime.MinValue;

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
            int timeframesCount = timeframes.Count;
            for (int i = 1; i < timeframesCount; i++)
            {
                TimeframeV2 timeframe = timeframes[i];
                int idx = timeframe.Index;

                // Still within the valid range
                if (idx >= timeframe.StartIndex && idx < timeframe.EndIndex)
                {
                    // Compare the current candlestick's CloseTime to the "lowest" timeframe's OpenTime
                    DateTime closeTime = timeframe.Candlesticks[idx].CloseTime;
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

    /// <summary>
    /// Index iterator to manage backtesting progress
    /// </summary>
    protected decimal Index { get; set; }

    /// <summary>
    /// Max Index property to calculate the total index increments during the backtesting
    /// </summary>
    protected decimal MaxIndex { get; private set; }

    /// <summary>
    /// Applies Sum of all EndIndexes to MaxIndex
    /// </summary>
    /// <param name="symbolDataParts"></param>
    protected void ApplySumOfEndIndexes(List<List<SymbolDataV2>> symbolDataParts)
    {
        // --- Getting Symbols Data that have highest EndIndexes
        IEnumerable<SymbolDataV2?> maxSymbol = symbolDataParts.Select(x => x.MaxBy(
            y => y.Timeframes.First().EndIndex));

        // --- Selecting EndIndexes and forming an array from them
        int[] endIndexesArray = maxSymbol.Select(x => x!.Timeframes.First().EndIndex).ToArray();

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
