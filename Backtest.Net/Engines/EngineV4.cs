using System.Collections.Concurrent;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Models.Net.Interfaces;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V4
/// The whole point of this version is optimizing all we have in V3
/// Getting rid of all ToList, ToArray conversions, in practise it increases real backtesting time despite
/// benchmark shows better results
/// </summary>
/// <param name="warmupCandlesCount"></param>
public class EngineV4(int warmupCandlesCount) : EngineV3(warmupCandlesCount)
{
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task RunAsync(List<List<ISymbolData>> symbolDataParts, CancellationToken? cancellationToken = default)
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

                    // --- Apply Open Price to OHLC for all first candles
                    await HandleOhlc(feedingData);

                    // --- Sending OnTick Action
                    await OnTick(feedingData);
                        
                    // --- Clearing unnecessary data right after the strategy is executed
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
    /// Cloning necessary symbol data range
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override Task<List<ISymbolData>> CloneFeedingSymbolData(List<ISymbolData> symbolData)
    {
        // Clearing temporary data
        ClonedSymbolsData.Clear();

        Parallel.ForEach(symbolData, symbol =>
        {
            var timeframes = new ConcurrentQueue<ITimeframe>();

            //Parallel.ForEach(symbol.Timeframes, timeframe =>
            foreach (var timeframe in symbol.Timeframes)
            {
                var warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex
                    ? timeframe.Index - WarmupCandlesCount
                    : timeframe.StartIndex;
                var clonedCandlesticks = timeframe.Candlesticks
                    .Take(warmedUpIndex..(timeframe.Index + 1))
                    .Select(candle => candle.Clone()).ToList();

                clonedCandlesticks.Reverse();

                // --- No need to add nothing more except interval and candles themselves
                timeframes.Enqueue(new TimeframeV1
                {
                    Timeframe = timeframe.Timeframe,
                    Candlesticks = clonedCandlesticks
                });
            }

            // Create new symbol data with cloned candlesticks
            ISymbolData cloned = new SymbolDataV1
            {
                Symbol = symbol.Symbol,
                Timeframes = timeframes.ToList()
            };

            ClonedSymbolsData.Enqueue(cloned);
        });

        return Task.FromResult(ClonedSymbolsData.ToList());
    }
    
    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override Task HandleOhlc(List<ISymbolData> symbolData)
    {
        //await Parallel.ForEachAsync(symbolData, (symbol, _) =>
        Parallel.ForEach(symbolData, symbol =>
        {
            // --- Getting First Timeframe from the list
            var firstTimeframe = symbol.Timeframes.First();

            // --- Using null propagation and getting first candle
            var firstTimeframeCandle = firstTimeframe.Candlesticks.First();

            foreach (var timeframe in symbol.Timeframes)
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
        });
        
        return Task.CompletedTask;
    }
}