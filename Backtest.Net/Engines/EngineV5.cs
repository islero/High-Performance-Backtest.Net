using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Models.Net.Interfaces;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V5
/// The Engine V5 is going to use fully async/await functionality without returning Task.FromResult
/// Which may be more efficient if real world use rather than on benchmarks, since Engine V2 maybe still
/// the best performer despite it still not fully optimized as V4, for example
/// </summary>
/// <param name="warmupCandlesCount"></param>
public class EngineV5(int warmupCandlesCount) : EngineV4(warmupCandlesCount)
{
    /// <summary>
    /// Cloning necessary symbol data range
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override async Task<List<ISymbolData>> CloneFeedingSymbolData(List<ISymbolData> symbolData)
    {
        // Clearing temporary data
        ClonedSymbolsData.Clear();

        // Parallel Loop, performs the better, the more symbols are in symbolData enumerable
        await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
        {
            var timeframes = new List<ITimeframe>();

            // Making timeframes as regular foreach loop, because after parallel loop it will be necessary to sort 
            // timeframes in ascending order, which may take more resources than benefits from using parallel execution
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
                timeframes.Add(new TimeframeV1
                {
                    Timeframe = timeframe.Timeframe,
                    Candlesticks = clonedCandlesticks
                });
            }

            // Create new symbol data with cloned candlesticks
            ISymbolData cloned = new SymbolDataV1
            {
                Symbol = symbol.Symbol,
                Timeframes = timeframes
            };

            ClonedSymbolsData.Enqueue(cloned);
            
            return default;
        });

        return ClonedSymbolsData.ToList();
    }
    
    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override async Task HandleOhlc(List<ISymbolData> symbolData)
    {
        await Parallel.ForEachAsync(symbolData, (symbol, _) =>
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

            return default;
        });
    }
    
    /// <summary>
    /// Increment Symbol Data indexes
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override async Task IncrementIndexes(List<ISymbolData> symbolData)
    {
        await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
        {
            var lowestTimeframeIndexTime = DateTime.MinValue;
            foreach (var timeframe in symbol.Timeframes)
            {
                // Handling the lowest timeframe
                if (timeframe == symbol.Timeframes[0])
                {
                    if (timeframe.Index >= timeframe.StartIndex && timeframe.Index < timeframe.EndIndex)
                    {
                        timeframe.Index++;
                        lowestTimeframeIndexTime = timeframe.Candlesticks[timeframe.Index].OpenTime;
                        
                        // --- Managing bot progress
                        Index = timeframe.Index;
                    }

                    continue;
                }

                // Handling higher timeframes
                var closeTime = timeframe.Candlesticks[timeframe.Index].CloseTime;
                if (closeTime < lowestTimeframeIndexTime && timeframe.Index >= timeframe.StartIndex &&
                    timeframe.Index < timeframe.EndIndex)
                {
                    timeframe.Index++;
                }
            }

            return default;
        });
    }
}