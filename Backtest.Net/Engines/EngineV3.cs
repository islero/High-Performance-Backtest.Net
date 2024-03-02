using System.Collections.Concurrent;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V3
/// Prepares parts before feeding them into strategy
/// </summary>
public class EngineV3(int warmupCandlesCount) : EngineV2(warmupCandlesCount)
{
    /// <summary>
    /// Increment Symbol Data indexes
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override Task IncrementIndexes(List<ISymbolData> symbolData)
    {
        Parallel.ForEach(symbolData, symbol =>
        {
            var lowestTimeframeIndexTime = DateTime.MinValue;
            foreach (var timeframe in symbol.Timeframes)
            {
                // Handling the lowest timeframe
                if (timeframe == symbol.Timeframes.First())
                {
                    if (timeframe.Index >= timeframe.StartIndex && timeframe.Index < timeframe.EndIndex)
                    {
                        timeframe.Index++;
                        lowestTimeframeIndexTime = timeframe.Candlesticks.ElementAt(timeframe.Index).OpenTime;
                        
                        // --- Managing bot progress
                        Index++;
                    }

                    continue;
                }

                // Handling higher timeframes
                var closeTime = timeframe.Candlesticks.ElementAt(timeframe.Index).CloseTime;
                if (closeTime < lowestTimeframeIndexTime && timeframe.Index >= timeframe.StartIndex &&
                    timeframe.Index < timeframe.EndIndex)
                {
                    timeframe.Index++;
                }
            }
        });

        return Task.CompletedTask;
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
            var timeframes = new ConcurrentBag<ITimeframe>();

            Parallel.ForEach(symbol.Timeframes, timeframe =>
            {
                var warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex
                    ? timeframe.Index - WarmupCandlesCount
                    : timeframe.StartIndex;
                var clonedCandlesticks = timeframe.Candlesticks
                    .Take(warmedUpIndex..(timeframe.Index + 1))
                    .Select(candle => candle.Clone()).ToList();

                clonedCandlesticks.Reverse();

                // --- No need to add nothing more except interval and candles themself
                timeframes.Add(new TimeframeV1
                {
                    Timeframe = timeframe.Timeframe,
                    Candlesticks = clonedCandlesticks
                });
            });

            // Create a new symbol data with cloned candlesticks
            ISymbolData cloned = new SymbolDataV1()
            {
                Symbol = symbol.Symbol,
                Timeframes = timeframes.OrderBy(x => x.Timeframe).ToList()
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
        Parallel.ForEach(symbolData, symbol =>
        {
            // --- Enumerating symbol timeframes to list
            var timeframesList = symbol.Timeframes.ToList();

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
        });

        return Task.CompletedTask;
    }

}