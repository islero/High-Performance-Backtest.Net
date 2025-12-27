using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Backtest.Net.Candlesticks;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

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
    private readonly bool _useFullCandleForCurrent = useFullCandleForCurrent;

    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    public override async Task RunAsync(List<List<SymbolDataV2>> symbolDataParts,
        CancellationToken? cancellationToken = null)
    {
        try
        {
            ApplySumOfEndIndexes(symbolDataParts);

            CancellationToken ct = cancellationToken ?? CancellationToken.None;

            foreach (List<SymbolDataV2> part in symbolDataParts)
            {
                // Pre-extract first timeframes for faster loop condition check
                int partCount = part.Count;
                var firstTimeframes = new List<TimeframeV2>(partCount);
                for (int i = 0; i < partCount; i++) firstTimeframes.Add(part[i].Timeframes[0]);

                // Main cycle
                while (AllTimeframesHaveMoreData(firstTimeframes))
                {
                    ct.ThrowIfCancellationRequested();

                    SymbolDataV2[] feedingData = await CloneFeedingSymbolData(part).ConfigureAwait(false);

                    if (!_useFullCandleForCurrent)
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
    private static bool AllTimeframesHaveMoreData(List<TimeframeV2> timeframes)
    {
        Span<TimeframeV2> span = CollectionsMarshal.AsSpan(timeframes);
        foreach (TimeframeV2 t in span)
        {
            if (t.Index >= t.EndIndex)
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
            List<TimeframeV2> timeframes = symbol.Timeframes;
            TimeframeV2 firstTimeframe = timeframes[0];
            List<CandlestickV2> firstTfCandles = firstTimeframe.Candlesticks;
            int lastIndex = firstTfCandles.Count - 1;

            // Extract reference values directly without cloning
            CandlestickV2 refCandle = firstTfCandles[lastIndex];
            decimal refOpen = refCandle.Open;
            DateTime refOpenTime = refCandle.OpenTime;
            DateTime refCloseTime = refCandle.CloseTime; // Original close time before modification

            // Process each timeframe
            int tfCount = timeframes.Count;
            for (int tfIdx = 0; tfIdx < tfCount; tfIdx++)
            {
                TimeframeV2 timeframe = timeframes[tfIdx];
                List<CandlestickV2> candles = timeframe.Candlesticks;
                int candleIndex = candles.Count - 1;
                CandlestickV2 candle = candles[candleIndex];

                // Skip if close times match but open times differ
                if (candle.CloseTime == refCloseTime && candle.OpenTime != refOpenTime)
                    continue;

                // Create a new candle with optimized values
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
                    (decimal high, decimal low) = ComputeHighLowFromOpenTime(
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
        int n = candles.Count;

        // Binary search for the first index where OpenTime >= openTime
        int lo = 0;
        int hi = n - 1;
        int firstValidIndex = n;

        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (candles[mid].OpenTime >= openTime)
            {
                firstValidIndex = mid;
                hi = mid - 1;
            }
            else
                lo = mid + 1;
        }

        // No valid candles found
        if (firstValidIndex >= n)
            return (refHigh, refLow);

        // Single-pass to compute min/max, excluding the last candle (will use ref values)
        decimal high = refHigh;
        decimal low = refLow;
        int endIndex = n - 1; // Exclude the last candle, we use ref values for it

        // Use span for faster iteration
        Span<CandlestickV2> span = CollectionsMarshal.AsSpan(candles);
        for (int i = firstValidIndex; i < endIndex; i++)
        {
            CandlestickV2 c = span[i];
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
            List<TimeframeV2> timeframes = symbol.Timeframes;
            int tfCount = timeframes.Count;

            // Find the first timeframe with Index + 1 < EndIndex
            int baseTfIndex = -1;
            for (int i = 0; i < tfCount; i++)
            {
                TimeframeV2 tf = timeframes[i];
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

            TimeframeV2 baseTf = timeframes[baseTfIndex];

            if (baseTf.Index < baseTf.StartIndex || baseTf.Index >= baseTf.EndIndex)
                return;

            baseTf.Index++;
            if (Index != MaxIndex)
                Index = baseTf.Index;

            DateTime referenceTime = baseTf.Candlesticks[baseTf.Index].OpenTime;

            // Update higher timeframes
            for (int i = baseTfIndex + 1; i < tfCount; i++)
            {
                TimeframeV2 tf = timeframes[i];
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
            List<TimeframeV2> timeframes = symbol.Timeframes;
            int tfCount = timeframes.Count;

            for (int i = 0; i < tfCount; i++)
            {
                List<CandlestickV2> candles = timeframes[i].Candlesticks;
                Span<CandlestickV2> span = CollectionsMarshal.AsSpan(candles);
                span.Reverse();
            }
        });
    }
}
