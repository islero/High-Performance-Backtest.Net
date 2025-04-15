using System.Runtime.CompilerServices;
using Backtest.Net.Candlesticks;
using Backtest.Net.SymbolsData;

namespace Backtest.Net.Utils;

/// <summary>
/// Provides utility methods for processing and manipulating candlestick data.
/// </summary>
public static class CandleHelper
{
    /// <summary>
    /// Filters a list of candlesticks to include only those that have an open time greater than or equal to a specified open time, starting from a reference candlestick.
    /// </summary>
    /// <param name="candles">The list of candlesticks to filter.</param>
    /// <param name="referenceCandle">The candlestick to include first in the result regardless of its open time.</param>
    /// <param name="openTime">The minimum open time for candlesticks to be included in the result.</param>
    /// <returns>A list of candlesticks starting with the reference candlestick, followed by those with an open time greater than or equal to the specified open time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<CandlestickV2> TakeCandlesFromOpenTime(
        List<CandlestickV2> candles,
        CandlestickV2 referenceCandle,
        DateTime openTime)
    {
        var n = candles.Count;
        // Binary search for the first index (starting at index 0) where OpenTime >= openTime.
        var lo = 0;
        var hi = n - 1;
        var firstValidIndex = n; // default value if none is found

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

        // Calculate the number of valid candidate candles.
        var validCount = firstValidIndex < n ? n - firstValidIndex : 0;

        // If no valid candles or only one valid candle exist,
        // then return a list with the reference candle.
        if (validCount <= 1)
        {
            return [referenceCandle];
        }

        // When there are at least 2 valid candles, replace the last candidate.
        // Get all but the final candidate.
        var result = candles.GetRange(firstValidIndex, validCount - 1);
        // Append the reference candle at the end.
        result.Add(referenceCandle);
        return result;
    }

    /// <summary>
    /// Reverses the order of candlestick lists for all timeframes associated with the specified symbol.
    /// </summary>
    /// <param name="symbol">The symbol data containing timeframes with candlestick lists to be reversed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReverseTimeframesCandles(SymbolDataV2 symbol)
    {
        var timeframes = symbol.Timeframes;
        var count = timeframes.Count;
        for (var i = 0; i < count; i++)
        {
            ReverseInPlace(timeframes[i].Candlesticks);
        }
    }

    /// <summary>
    /// Reverses the order of elements in the given list of candlesticks in place.
    /// </summary>
    /// <param name="candlesticks">The list of candlesticks to be reversed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReverseInPlace(List<CandlestickV2> candlesticks)
    {
        var n = candlesticks.Count;
        // Only perform reversal if there is more than one element.
        for (int j = 0, k = n - 1; j < k; j++, k--)
        {
            // Manually swap the elements.
            (candlesticks[j], candlesticks[k]) = (candlesticks[k], candlesticks[j]);
        }
    }
}