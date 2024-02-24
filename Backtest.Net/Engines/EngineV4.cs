using System.Collections.Concurrent;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V4
/// The whole point of this version is optimizing all we have in V3
/// Getting rid of all ToList, ToArray conversions, in practise it increases real backtesting time despite
/// benchmark shows better results
/// </summary>
/// <param name="warmupCandlesCount"></param>
/// <param name="trade"></param>
/// <param name="strategy"></param>
public class EngineV4(int warmupCandlesCount, ITrade trade, IStrategy strategy) : EngineV3(warmupCandlesCount, trade, strategy)
{
    /// <summary>
    /// Cloning necessary symbol data range
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override Task<IEnumerable<ISymbolData>> CloneFeedingSymbolData(IEnumerable<ISymbolData> symbolData)
    {
        // Clearing temporary data
        ClonedSymbolsData.Clear();

        Parallel.ForEach(symbolData, symbol =>
        {
            var timeframes = new ConcurrentQueue<TimeframeV1>();

            Parallel.ForEach(symbol.Timeframes, timeframe =>
            {
                var warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex
                    ? timeframe.Index - WarmupCandlesCount
                    : timeframe.StartIndex;
                var clonedCandlesticks = timeframe.Candlesticks
                    .Take(warmedUpIndex..(timeframe.Index + 1))
                    .Select(candle => candle.Clone());

                // --- No need to add nothing more except interval and candles themself
                timeframes.Enqueue(new TimeframeV1
                {
                    Timeframe = timeframe.Timeframe,
                    Candlesticks = clonedCandlesticks.Reverse()
                });
            });

            // Create a new symbol data with cloned candlesticks
            ISymbolData cloned = new SymbolDataV1
            {
                Symbol = symbol.Symbol,
                Timeframes = timeframes.OrderBy(x => x.Timeframe),
            };

            ClonedSymbolsData.Enqueue(cloned);
        });

        return Task.FromResult(ClonedSymbolsData.AsEnumerable());
    }
    
    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected override Task HandleOhlc(IEnumerable<ISymbolData> symbolData)
    {
        Parallel.ForEach(symbolData, symbol =>
        {
            // --- Getting First Timeframe from the list
            var firstTimeframe = symbol.Timeframes.First();

            // --- Using null propagation and getting first candle
            var firstTimeframeCandle = firstTimeframe.Candlesticks.First();

            foreach (var timeframe in symbol.Timeframes)
            {
                // Create a new IEnumerable with the modified first candle
                timeframe.Candlesticks = timeframe.Candlesticks.Select((candle, index) =>
                {
                    if (index == 0)
                    {
                        // Applying OHLC value as lowest timeframe open price and close time as open time
                        candle.Open = firstTimeframeCandle.Open;
                        candle.High = firstTimeframeCandle.Open;
                        candle.Low = firstTimeframeCandle.Open;
                        candle.Close = firstTimeframeCandle.Open;
                        candle.CloseTime = firstTimeframeCandle.OpenTime;
                    }

                    return candle;
                });
            }
        });

        return Task.CompletedTask;
    }
}