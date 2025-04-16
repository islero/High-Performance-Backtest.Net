using Backtest.Net.SymbolsData;
using Backtest.Net.Utils;

namespace Backtest.Net.Engines;

/// <summary>
/// The EngineV8 is responsible for running backtesting simulations
/// Aiming to use .NET 9 performance gains as much as possible
/// </summary>
public sealed class EngineV9(int warmupCandlesCount, bool sortCandlesInDescOrder, bool useFullCandleForCurrent) : EngineV8(warmupCandlesCount, useFullCandleForCurrent)
{
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task RunAsync(List<List<SymbolDataV2>> symbolDataParts,
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
                    if (!useFullCandleForCurrent)
                        await HandleCurrentCandleOhlc(feedingData).ConfigureAwait(false);

                    // Sorting candles in descending order
                    if (sortCandlesInDescOrder)
                        await ReverseCandles(feedingData).ConfigureAwait(false);

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
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    private static Task HandleCurrentCandleOhlc(SymbolDataV2[] symbolData)
    {
        Parallel.ForEach(symbolData, symbol =>
        {
            // The lowest timeframe from the list of symbol.Timeframes
            var firstTimeframe = symbol.Timeframes[0];
            
            // Clone the last candle of the first timeframe
            var referenceCandle = firstTimeframe.Candlesticks[^1].Clone();
            
            // Reserving the reference candle close time
            var referenceCandleCloseTime = referenceCandle.CloseTime;
            
            // Setting the reference candle OHLC to open price and time to open time
            // This is necessary to avoid using the data from the future
            referenceCandle.High = referenceCandle.Open;
            referenceCandle.Low = referenceCandle.Open;
            referenceCandle.Close = referenceCandle.Open;
            referenceCandle.CloseTime = referenceCandle.OpenTime;

            foreach (var timeframe in symbol.Timeframes)
            {
                // Getting the current candle
                var candle = timeframe.Candlesticks[^1];

                // Don't update the candle at the close time
                if (candle.CloseTime == referenceCandleCloseTime
                    &&
                    candle.OpenTime != referenceCandle.OpenTime)
                {
                    continue;
                }
                
                // Cloning the latest candle (the current candle)
                var candleClone = timeframe.Candlesticks[^1].Clone();
                
                // Resetting a current if the open time is the same as for the reference candle
                candleClone.Close = referenceCandle.Close;
                candleClone.CloseTime = referenceCandle.CloseTime;
                candleClone.High = referenceCandle.High;
                candleClone.Low = referenceCandle.Low;
                
                // Make sure the candle is in between the higher timeframe open time and close time
                if(referenceCandle.OpenTime > candle.OpenTime && referenceCandleCloseTime < candle.CloseTime)
                {
                    candleClone.Close = referenceCandle.Close;
                    candleClone.CloseTime = referenceCandle.CloseTime;

                    var candlesFromOpenTime = CandleHelper.TakeCandlesFromOpenTime(firstTimeframe.Candlesticks,
                        referenceCandle, candle.OpenTime);
                    
                    candleClone.High = candlesFromOpenTime.Max(x => x.High);
                    candleClone.Low = candlesFromOpenTime.Min(x => x.Low);
                }

                timeframe.Candlesticks[^1] = candleClone;
            }
        });

        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Updates the indexes of timeframes in the provided symbol data to align with the latest available timestamps.
    /// </summary>
    /// <param name="symbolData">A list of symbol data containing timeframes and their corresponding candlestick information.</param>
    /// <returns>A completed task once the indexes have been incremented.</returns>
    private Task IncrementIndexes(List<SymbolDataV2> symbolData)
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
    /// Reverses the order of candlestick data for each timeframe within the provided symbol data.
    /// </summary>
    /// <param name="symbolData">An array of symbol data containing candlestick timeframes to reverse.</param>
    /// <returns>A completed task once the reversal operation is done.</returns>
    private static Task ReverseCandles(SymbolDataV2[] symbolData)
    {
        Parallel.ForEach(symbolData, CandleHelper.ReverseTimeframesCandles);
        return Task.CompletedTask;
    }
}