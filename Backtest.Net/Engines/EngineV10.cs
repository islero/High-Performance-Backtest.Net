using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Backtest.Net.Candlesticks;
using Backtest.Net.SymbolsData;

namespace Backtest.Net.Engines;

/// <summary>
/// EngineV10: Optimized backtesting engine with reduced allocations and improved performance.
/// Key optimizations over V9:
/// - Eliminated unnecessary Clone() operations by extracting primitive values directly
/// - Single-pass High/Low computation without intermediate list allocation
/// - Reduced LINQ usage in hot paths
/// - Uses Span-based iteration where beneficial
/// </summary>
public sealed class EngineV10(int warmupCandlesCount, bool sortCandlesInDescOrder, bool useFullCandleForCurrent)
    : EngineV8(warmupCandlesCount, useFullCandleForCurrent)
{
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    public override async Task RunAsync(List<List<SymbolDataV2>> symbolDataParts,
        CancellationToken? cancellationToken = null)
    {
        try
        {
            ApplySumOfEndIndexes(symbolDataParts);

            var ct = cancellationToken ?? CancellationToken.None;

            foreach (var part in symbolDataParts)
            {
                // Pre-extract first timeframes for faster loop condition check
                var partCount = part.Count;
                var firstTimeframes = new List<Backtest.Net.Timeframes.TimeframeV2>(partCount);
                for (var i = 0; i < partCount; i++)
                {
                    firstTimeframes.Add(part[i].Timeframes[0]);
                }

                // Main cycle
                while (AllTimeframesHaveMoreData(firstTimeframes))
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException();

                    var feedingData = await CloneFeedingSymbolData(part).ConfigureAwait(false);

                    if (!useFullCandleForCurrent)
                        HandleCurrentCandleOhlcOptimized(feedingData);

                    if (sortCandlesInDescOrder)
                        ReverseCandlesOptimized(feedingData);

                    await OnTick(feedingData).ConfigureAwait(false);

                    IncrementIndexesOptimized(part);
                }
            }
        }
        catch (OperationCanceledException)
        {
            OnCancellationFinishedDelegate?.Invoke();
        }
    }

    /// <summary>
    /// Optimized check for whether all timeframes have more data to process.
    /// Avoids LINQ allocation overhead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AllTimeframesHaveMoreData(List<Backtest.Net.Timeframes.TimeframeV2> timeframes)
    {
        var span = CollectionsMarshal.AsSpan(timeframes);
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i].Index >= span[i].EndIndex)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Optimized OHLC handling that eliminates unnecessary Clone() operations
    /// and uses single-pass High/Low computation.
    /// </summary>
    private static void HandleCurrentCandleOhlcOptimized(SymbolDataV2[] symbolData)
    {
        Parallel.ForEach(symbolData, symbol =>
        {
            var timeframes = symbol.Timeframes;
            var firstTimeframe = timeframes[0];
            var firstTfCandles = firstTimeframe.Candlesticks;
            var lastIndex = firstTfCandles.Count - 1;

            // Extract reference values directly without cloning
            var refCandle = firstTfCandles[lastIndex];
            var refOpen = refCandle.Open;
            var refOpenTime = refCandle.OpenTime;
            var refCloseTime = refCandle.CloseTime; // Original close time before modification

            // Process each timeframe
            var tfCount = timeframes.Count;
            for (var tfIdx = 0; tfIdx < tfCount; tfIdx++)
            {
                var timeframe = timeframes[tfIdx];
                var candles = timeframe.Candlesticks;
                var candleIndex = candles.Count - 1;
                var candle = candles[candleIndex];

                // Skip if close times match but open times differ
                if (candle.CloseTime == refCloseTime && candle.OpenTime != refOpenTime)
                    continue;

                // Create new candle with optimized values
                var newCandle = new CandlestickV2
                {
                    OpenTime = candle.OpenTime,
                    Open = candle.Open,
                    Volume = candle.Volume,
                    Close = refOpen,
                    CloseTime = refOpenTime,
                    High = refOpen,
                    Low = refOpen
                };

                // For higher timeframes where reference falls within candle range,
                // compute actual High/Low from candles in range
                if (refOpenTime > candle.OpenTime && refCloseTime < candle.CloseTime)
                {
                    var (high, low) = ComputeHighLowFromOpenTime(
                        firstTfCandles, refOpen, refOpen, candle.OpenTime);
                    newCandle.High = high;
                    newCandle.Low = low;
                }

                candles[candleIndex] = newCandle;
            }
        });
    }

    /// <summary>
    /// Computes High and Low values from candles starting at a given open time.
    /// Single-pass computation without intermediate list allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (decimal High, decimal Low) ComputeHighLowFromOpenTime(
        List<CandlestickV2> candles,
        decimal refHigh,
        decimal refLow,
        DateTime openTime)
    {
        var n = candles.Count;

        // Binary search for first index where OpenTime >= openTime
        var lo = 0;
        var hi = n - 1;
        var firstValidIndex = n;

        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (candles[mid].OpenTime >= openTime)
            {
                firstValidIndex = mid;
                hi = mid - 1;
            }
            else
            {
                lo = mid + 1;
            }
        }

        // No valid candles found
        if (firstValidIndex >= n)
            return (refHigh, refLow);

        // Single-pass to compute min/max, excluding the last candle (will use ref values)
        var high = refHigh;
        var low = refLow;
        var endIndex = n - 1; // Exclude last candle, we use ref values for it

        // Use span for faster iteration
        var span = CollectionsMarshal.AsSpan(candles);
        for (var i = firstValidIndex; i < endIndex; i++)
        {
            var c = span[i];
            if (c.High > high) high = c.High;
            if (c.Low < low) low = c.Low;
        }

        return (high, low);
    }

    /// <summary>
    /// Optimized index incrementing without Task wrapper overhead.
    /// </summary>
    private void IncrementIndexesOptimized(List<SymbolDataV2> symbolData)
    {
        Parallel.ForEach(symbolData, symbol =>
        {
            var timeframes = symbol.Timeframes;
            var tfCount = timeframes.Count;

            // Find the first timeframe with Index + 1 < EndIndex
            var baseTfIndex = -1;
            for (var i = 0; i < tfCount; i++)
            {
                var tf = timeframes[i];
                if (tf.Index + 1 < tf.EndIndex)
                {
                    baseTfIndex = i;
                    break;
                }
                tf.Index = tf.EndIndex;
                Index = MaxIndex;
            }

            if (baseTfIndex == -1)
                return;

            var baseTf = timeframes[baseTfIndex];

            if (baseTf.Index < baseTf.StartIndex || baseTf.Index >= baseTf.EndIndex)
                return;

            baseTf.Index++;
            if (Index != MaxIndex)
                Index = baseTf.Index;

            var referenceTime = baseTf.Candlesticks[baseTf.Index].OpenTime;

            // Update higher timeframes
            for (var i = baseTfIndex + 1; i < tfCount; i++)
            {
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
    }

    /// <summary>
    /// Optimized candle reversal using span-based in-place swap.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReverseCandlesOptimized(SymbolDataV2[] symbolData)
    {
        Parallel.ForEach(symbolData, symbol =>
        {
            var timeframes = symbol.Timeframes;
            var tfCount = timeframes.Count;

            for (var i = 0; i < tfCount; i++)
            {
                var candles = timeframes[i].Candlesticks;
                var span = CollectionsMarshal.AsSpan(candles);
                span.Reverse();
            }
        });
    }
}
