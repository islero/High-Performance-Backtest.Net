using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Models.Net.Interfaces;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V7
/// Aiming to utilize .NET 9 performance gain as much as possible
/// </summary>
/// <param name="warmupCandlesCount"></param>
public sealed class EngineV7(int warmupCandlesCount, bool useFullCandleForCurrent = false) : EngineV6(warmupCandlesCount, useFullCandleForCurrent)
{
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task RunAsync(List<List<ISymbolData>> symbolDataParts,
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
    protected override Task<List<ISymbolData>> CloneFeedingSymbolData(List<ISymbolData> symbolData)
    {
        var count = symbolData.Count;
        // Pre-allocate the result array so we can fill it in parallel without synchronization.
        var result = new ISymbolData[count];

        // Run a parallel loop using Parallel.For maximum performance.
        Parallel.For(0, count, i =>
        {
            var symbol = symbolData[i];

            // Pre-allocate a timeframe list with capacity equal to the symbol's timeframe count
            var timeframes = new List<ITimeframe>(symbol.Timeframes.Count);
            timeframes.AddRange(from timeframe in symbol.Timeframes
                let warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex
                    ? timeframe.Index - WarmupCandlesCount
                    : timeframe.StartIndex
                let length = timeframe.Index + 1 - warmedUpIndex
                let clonedCandlesticks = timeframe.Candlesticks.GetRange(warmedUpIndex, length)
                select new TimeframeV1 { Timeframe = timeframe.Timeframe, Candlesticks = clonedCandlesticks });

            // Create a new symbol data instance.
            result[i] = new SymbolDataV1
            {
                Symbol = symbol.Symbol,
                Timeframes = timeframes
            };
        });

        // Convert the array to a List without extra concurrency overhead.
        return Task.FromResult(result.ToList());
    }

    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override Task HandleOhlc(List<ISymbolData> symbolData)
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
    protected override Task IncrementIndexes(List<ISymbolData> symbolData)
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
}