using System.Collections.Concurrent;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines;

/// <summary>
/// Engine V1
/// Prepares parts before feeding them into strategy
/// </summary>
public class EngineV1(int warmupCandlesCount) : IEngine
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
    protected int WarmupCandlesCount { get; } = warmupCandlesCount; // The amount of warmup candles count

    // --- Methods
    /// <summary>
    /// Starts the engine and feeds the strategy with data
    /// </summary>
    /// <param name="symbolDataParts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task RunAsync(IEnumerable<IEnumerable<ISymbolData>> symbolDataParts, CancellationToken? cancellationToken = default)
    {
        try
        {
            // --- Saving Parts Count for accurate progress calculation
            PartsCount = symbolDataParts.Count();
            
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
        if (_maxIndex == 0) return 0;
        
        // --- Returning current accurate progress
        return _index / _maxIndex * 100;
    }

    /// <summary>
    /// Increment Symbol Data indexes
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected virtual async Task IncrementIndexes(IEnumerable<ISymbolData> symbolData)
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
                        ManageProgress(timeframe.Index, timeframe.EndIndex);
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
    protected virtual async Task<IEnumerable<ISymbolData>> CloneFeedingSymbolData(IEnumerable<ISymbolData> symbolData)
    {
        ClonedSymbolsData.Clear();

        await Parallel.ForEachAsync(symbolData, new ParallelOptions(), (symbol, _) =>
        {
            var timeframes = new ConcurrentQueue<TimeframeV1>();

            //await Parallel.ForEachAsync(symbol.Timeframes, new ParallelOptions(), (timeframe, _) =>
            foreach (var timeframe in symbol.Timeframes)
            {
                var warmedUpIndex = timeframe.Index - WarmupCandlesCount > timeframe.StartIndex
                    ? timeframe.Index - WarmupCandlesCount
                    : timeframe.StartIndex;
                var clonedCandlesticks = timeframe.Candlesticks
                    .Take(warmedUpIndex..(timeframe.Index + 1)).OrderByDescending(x => x.OpenTime)
                    .Select(candle => candle.Clone());

                // --- No need to add nothing more except interval and candles themself
                timeframes.Enqueue(new TimeframeV1()
                {
                    Timeframe = timeframe.Timeframe,
                    Candlesticks = clonedCandlesticks
                });
            }

            // Create a new symbol data with cloned candlesticks
            ISymbolData cloned = new SymbolDataV1()
            {
                Symbol = symbol.Symbol,
                Timeframes = timeframes,
            };

            ClonedSymbolsData.Enqueue(cloned);
            return ValueTask.CompletedTask;
        });

        return ClonedSymbolsData;
    }

    /// <summary>
    /// Apply Open Price to OHLC for all first candles
    /// </summary>
    /// <param name="symbolData"></param>
    /// <returns></returns>
    protected virtual async Task HandleOhlc(IEnumerable<ISymbolData> symbolData)
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
    /// Parts Count
    /// </summary>
    protected decimal PartsCount { get; set; }

    /// <summary>
    /// Index and Max Index and parts count fields are using for tracking progress
    /// </summary>
    private decimal _index, _maxIndex;
    
    /// <summary>
    /// Rewrites index and max index if new max index is bigger than previous max index
    /// </summary>
    /// <param name="index"></param>
    /// <param name="maxIndex"></param>
    protected void ManageProgress(int index, int maxIndex)
    {
        // --- Check if there are more than 1 part
        if(PartsCount > 1)
        {
            _index++;
            
            // --- Validation of max index
            if(maxIndex * PartsCount < _maxIndex) return;
            
            _maxIndex = maxIndex * PartsCount;
            return;
        }
        
        // --- Rewrite Index Value
        _index = index;
        
        // --- Validation of max index
        if(maxIndex < _maxIndex) return;

        _maxIndex = maxIndex;
    }
}