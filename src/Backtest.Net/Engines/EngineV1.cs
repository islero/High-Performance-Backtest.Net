using System.Collections.Concurrent;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Models.Net.Interfaces;
using SimdLinq;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V1
/// Prepares parts before feeding them into strategy
/// </summary>
public class EngineV1(int warmupCandlesCount, bool useFullCandleForCurrent = false) : IEngine
{
    // --- Delegates
    /// <summary>
    /// Main Action of the Engine, it passes data to OnTick function
    /// </summary>
    public required Func<IEnumerable<ISymbolData>, Task> OnTick { get; set; }
    
    /// <summary>
    /// Notifies about backtesting cancellation
    /// </summary>
    public Action? OnCancellationFinishedDelegate { get; set; }
    
    // --- Properties
    protected int WarmupCandlesCount { get; } = warmupCandlesCount; // The number of warmup candles count

    /// <summary>
    /// Determines whether the backtester uses the full (completed) candle for the current candle logic.
    /// When set to true, the backtester will treat the current candle as fully formed,
    /// including all its OHLC (Open, High, Low, Close) data, rather than partial real-time data.
    /// </summary>
    /// <remarks>
    /// Enabling this option can impact backtesting behavior by ensuring that decisions
    /// are made based on completed candle data, which is particularly useful for historical backtesting.
    /// If set to false, the current candle data will be treated as incomplete.
    /// </remarks>
    protected bool UseFullCandleForCurrent { get; } = useFullCandleForCurrent;

    // --- Methods
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task RunAsync(List<List<ISymbolData>> symbolDataParts, CancellationToken? cancellationToken = default)
    {
        try
        {
            // --- Applying Sum of the all parts End Indexes to MaxIndex
            ApplySumOfEndIndexes(symbolDataParts);
            
            // --- Run every symbolDataPart
            foreach (var part in symbolDataParts)
            {
                // --- Enumerating Part
                var partList = part.ToList();
                    
                // --- Main cycle
                while (partList.All(x => x.Timeframes.First().Index < x.Timeframes.First().EndIndex))
                {
                    // --- Checking for cancellation
                    if (cancellationToken is { IsCancellationRequested: true })
                        throw new OperationCanceledException();

                    // --- Preparing feeding data
                    var feedingData = await CloneFeedingSymbolData(partList);

                    // --- Enumerating Feeding Data
                    var feedingDataList = feedingData.ToList();
                        
                    // --- Apply Open Price to OHLC for all first candles
                    if (!UseFullCandleForCurrent)
                        await HandleOhlc(feedingDataList);

                    // --- Sending OnTick Action
                    await OnTick(feedingDataList);

                    // --- Incrementing indexes
                    await IncrementIndexes(partList);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // --- Cancellation been requested and executed
            OnCancellationFinishedDelegate?.Invoke();
        }
    }
    
    /// <summary>
    /// Gets Current Progress From 0.0 to 100.0
    /// </summary>
    /// <returns></returns>
    public decimal GetProgress()
    {
        // --- Validating dividing by 0
        if (MaxIndex == 0) return 0;
        
        // --- Returning current accurate progress
        return Index / MaxIndex * 100;
    }

    /// <summary>
    /// Increment Symbol Data indexes
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected virtual async Task IncrementIndexes(List<ISymbolData> symbolData)
    {
        //foreach (var symbol in symbolData)
        await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
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
                        Index = timeframe.Index;
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

            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// Field for allocated ISymbolData to clone it
    /// </summary>
    protected readonly ConcurrentQueue<ISymbolData> ClonedSymbolsData = new();
        
    /// <summary>
    /// Cloning necessary symbol data range
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected virtual async Task<List<ISymbolData>> CloneFeedingSymbolData(List<ISymbolData> symbolData)
    {
        ClonedSymbolsData.Clear();

        await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
        {
            var timeframes = new List<ITimeframe>();

            //await Parallel.ForEachAsync(symbol.Timeframes, new ParallelOptions(), (timeframe, _) =>
            foreach (var timeframe in symbol.Timeframes)
            {
                var warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex
                    ? timeframe.Index - WarmupCandlesCount
                    : timeframe.StartIndex;
                var clonedCandlesticks = timeframe.Candlesticks
                    .Take(warmedUpIndex..(timeframe.Index + 1)).OrderByDescending(x => x.OpenTime)
                    .Select(candle => candle.Clone());

                // --- No need to add nothing more except interval and candles themselves
                timeframes.Add(new TimeframeV1()
                {
                    Timeframe = timeframe.Timeframe,
                    Candlesticks = clonedCandlesticks.ToList()
                });
            }

            // Create new symbol data with cloned candlesticks
            ISymbolData cloned = new SymbolDataV1()
            {
                Symbol = symbol.Symbol,
                Timeframes = timeframes,
            };

            ClonedSymbolsData.Enqueue(cloned);
            return ValueTask.CompletedTask;
        });

        return ClonedSymbolsData.ToList();
    }

    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected virtual async Task HandleOhlc(List<ISymbolData> symbolData)
    {
        await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
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

            return default;
        });
    }

    

    /// <summary>
    /// Index iterator to manage backtesting progress
    /// </summary>
    protected decimal Index { get; set; }
    
    /// <summary>
    /// Applies Sum of all EndIndexes to MaxIndex
    /// </summary>
    /// <param name="symbolDataParts"></param>
    protected void ApplySumOfEndIndexes(List<List<ISymbolData>> symbolDataParts)
    {
        /*MaxIndex = 0;
        Index = 0;
        // Calculating the sum of all symbol data parts and their symbol data lists and timeframes
        foreach (var symbolDataList in symbolDataParts)
        {
            foreach (var symbolData in symbolDataList)
            {
                foreach (var timeframe in symbolData.Timeframes)
                {
                    MaxIndex += timeframe.EndIndex - 1;
                    Index += timeframe.StartIndex;
                }
            }
        }
        return;*/
        
        // --- Getting Symbols Data that have highest EndIndexes
        var maxSymbol = symbolDataParts.Select(x => x.MaxBy(
            y => y.Timeframes.First().EndIndex));

        // --- Selecting EndIndexes and forming array from them
        var endIndexesArray = maxSymbol.Select(x => x!.Timeframes.First().EndIndex).ToArray();
        
        // --- Calculating Sum of the all-parts max indexes
        MaxIndex = endIndexesArray.Sum();
        
        // --- Taking into account warmup candles for each part
        //MaxIndex -= (endIndexesArray.Length - 1) * (WarmupCandlesCount - 1);

        // --- All indexes must start from warmup candles count value
        //Index = WarmupCandlesCount;
    }

    /// <summary>
    /// Max Index property to calculate the total index increments during the backtesting
    /// </summary>
    private decimal MaxIndex { get; set; }
}