using Backtest.Net.Interfaces;
using Models.Net.Interfaces;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V2
/// Prepares parts before feeding them into strategy
/// </summary>
public class EngineV2(int warmupCandlesCount, bool useFullCandleForCurrent = false) : EngineV1(warmupCandlesCount, useFullCandleForCurrent)
{
    // --- Methods
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task RunAsync(List<List<ISymbolData>> symbolDataParts, CancellationToken? cancellationToken = null)
    {
        try
        {
            // --- Applying Sum of the all parts End Indexes to MaxIndex
            ApplySumOfEndIndexes(symbolDataParts);
            
            // --- Run every symbolDataPart
            foreach (var part in symbolDataParts)
            {
                // --- Main cycle
                while (part.All(x => x.Timeframes.First().Index < x.Timeframes.First().EndIndex))
                {
                    // --- Checking for cancellation
                    if (cancellationToken is { IsCancellationRequested: true })
                        throw new OperationCanceledException();

                    // --- Preparing feeding data
                    var feedingData = await CloneFeedingSymbolData(part);

                    // --- Enumerating Feeding Data
                    var feedingDataList = feedingData;
                        
                    // --- Apply Open Price to OHLC for all first candles
                    if (!UseFullCandleForCurrent)
                        await HandleOhlc(feedingDataList);

                    // --- Sending OnTick Action
                    await OnTick(feedingDataList);
                        
                    // --- Clearing unnecessary data right after strategy is executed
                    ClonedSymbolsData.Clear();

                    // --- Incrementing indexes
                    await IncrementIndexes(part);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Freeing up resources
            ClonedSymbolsData.Clear();
                
            // --- Cancellation been requested and executed
            OnCancellationFinishedDelegate?.Invoke();
        }
    }
        
    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override async Task HandleOhlc(List<ISymbolData> symbolData)
    {
        await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
        {
            // --- Enumerating symbol timeframes to list
            var timeframesList = symbol.Timeframes;
                
            // --- Getting First Timeframe from the list
            var firstTimeframe = timeframesList.First();

            // --- Using null propagation and getting first candle
            var firstTimeframeCandle = firstTimeframe.Candlesticks.First();
                
            foreach (var timeframe in timeframesList)
            {
                var candleSticks = timeframe.Candlesticks.ToList();
                var firstCandle = candleSticks.First();
                    
                // Applying OHLC value as lowest timeframe open price and close time as open time
                firstCandle.Open = firstTimeframeCandle.Open;
                firstCandle.High = firstTimeframeCandle.Open;
                firstCandle.Low = firstTimeframeCandle.Open;
                firstCandle.Close = firstTimeframeCandle.Open;
                firstCandle.CloseTime = firstTimeframeCandle.OpenTime;

                // Assign the modified list back to the enumerable
                timeframe.Candlesticks = candleSticks;
            }

            return default;
        });
    }
}