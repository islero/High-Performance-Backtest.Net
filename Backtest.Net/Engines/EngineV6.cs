using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Models.Net.Interfaces;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V6
/// The Engine V6 trying to avoid cloning whole range of candles, but the latest one instead
/// </summary>
/// <param name="warmupCandlesCount"></param>
public class EngineV6(int warmupCandlesCount, bool useFullCandleForCurrent = false) : EngineV5(warmupCandlesCount, useFullCandleForCurrent)
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
                var clonedCandlesticks = timeframe.Candlesticks[warmedUpIndex..(timeframe.Index + 1)];
                    //.Take(warmedUpIndex..(timeframe.Index + 1)).ToList();

                // --- No need to add nothing more except interval and candles themselves
                timeframes.Add(new TimeframeV1
                {
                    Timeframe = timeframe.Timeframe,
                    Candlesticks = clonedCandlesticks
                });
            }

            // Create a new symbol date with cloned candlesticks
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
            var firstTimeframe = symbol.Timeframes[0];

            // --- Getting the last candle and cloning it
            var firstTimeframeCandle = firstTimeframe.Candlesticks[^1].Clone();
            firstTimeframeCandle.Open = firstTimeframeCandle.Open;
            firstTimeframeCandle.High = firstTimeframeCandle.Open;
            firstTimeframeCandle.Low = firstTimeframeCandle.Open;
            firstTimeframeCandle.Close = firstTimeframeCandle.Open;
            firstTimeframeCandle.CloseTime = firstTimeframeCandle.OpenTime;

            foreach (var timeframe in symbol.Timeframes)
            {
                // Reversing list to make first candle the most recent one
                timeframe.Candlesticks.Reverse();
                
                // Replacing last element with cloned candle
                timeframe.Candlesticks[0] = timeframe.Candlesticks[0].Clone();
                timeframe.Candlesticks[0].Close = firstTimeframeCandle.Close;
                timeframe.Candlesticks[0].CloseTime = firstTimeframeCandle.CloseTime;
                timeframe.Candlesticks[0].High = firstTimeframeCandle.High;
                timeframe.Candlesticks[0].Low = firstTimeframeCandle.Low;
            }

            return default;
        });
    }
}