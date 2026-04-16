using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Backtest.Net.Candlesticks;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Engines;

/// <summary>
/// Backtesting engine optimized for a zero-allocation steady-state execution path.
/// </summary>
/// <remarks>
/// <para>
/// EngineV11 avoids the per-tick object graph cloning used by previous engines. It prepares a reusable
/// feed buffer for every data part, then repopulates that buffer before each <see cref="EngineV8.OnTick"/>
/// callback. This keeps the hot path allocation-free after the initial buffer setup.
/// </para>
/// <para>
/// The data passed to <see cref="EngineV8.OnTick"/> is a borrowed snapshot. Strategies may read it during
/// the callback, but must not store references to the supplied array, symbols, timeframes, candle lists,
/// or current-candle objects after the callback completes because EngineV11 reuses them on the next tick.
/// </para>
/// </remarks>
public sealed class EngineV11(
    int warmupCandlesCount,
    bool sortCandlesInDescOrder,
    bool useFullCandleForCurrent)
    : EngineV8(warmupCandlesCount, useFullCandleForCurrent)
{
    private readonly int _warmupCandlesCount = warmupCandlesCount;
    private readonly bool _sortCandlesInDescOrder = sortCandlesInDescOrder;
    private readonly bool _useFullCandleForCurrent = useFullCandleForCurrent;

    /// <summary>
    /// Runs the backtest and feeds the strategy with reusable per-tick symbol data snapshots.
    /// </summary>
    /// <param name="symbolDataParts">Prepared symbol data parts produced by the splitter.</param>
    /// <param name="cancellationToken">Optional cancellation token used to stop the backtest loop.</param>
    public override async Task RunAsync(
        List<List<SymbolDataV2>> symbolDataParts,
        CancellationToken? cancellationToken = null)
    {
        try
        {
            ApplySumOfEndIndexes(symbolDataParts);

            CancellationToken ct = cancellationToken.GetValueOrDefault();

            foreach (List<SymbolDataV2> part in symbolDataParts)
            {
                PartFeedBuffer feedBuffer = PartFeedBuffer.Create(part, _warmupCandlesCount);

                while (AllLowestTimeframesHaveData(part))
                {
                    ct.ThrowIfCancellationRequested();

                    SymbolDataV2[] feedingData = feedBuffer.Fill(
                        part,
                        _warmupCandlesCount,
                        _sortCandlesInDescOrder,
                        _useFullCandleForCurrent);

                    await OnTick(feedingData).ConfigureAwait(false);

                    IncrementIndexes(part);
                }
            }
        }
        catch (OperationCanceledException)
        {
            OnCancellationFinishedDelegate?.Invoke();
        }
    }

    /// <summary>
    /// Checks whether every lowest timeframe in the part still has a candle to process.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AllLowestTimeframesHaveData(List<SymbolDataV2> symbolData)
    {
        int count = symbolData.Count;
        if (count == 0)
            return false;

        for (int i = 0; i < count; i++)
        {
            TimeframeV2 lowestTimeframe = symbolData[i].Timeframes[0];
            if (lowestTimeframe.Index >= lowestTimeframe.EndIndex)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Advances source timeframe indexes using the V9/V10 alignment rules without TPL overhead.
    /// </summary>
    private void IncrementIndexes(List<SymbolDataV2> symbolData)
    {
        int symbolCount = symbolData.Count;
        for (int symbolIndex = 0; symbolIndex < symbolCount; symbolIndex++)
        {
            List<TimeframeV2> timeframes = symbolData[symbolIndex].Timeframes;
            int timeframeCount = timeframes.Count;

            int baseTimeframeIndex = -1;
            for (int i = 0; i < timeframeCount; i++)
            {
                TimeframeV2 timeframe = timeframes[i];
                if (timeframe.Index + 1 < timeframe.EndIndex)
                {
                    baseTimeframeIndex = i;
                    break;
                }

                timeframe.Index = timeframe.EndIndex;
                Index = MaxIndex;
            }

            if (baseTimeframeIndex == -1)
                continue;

            TimeframeV2 baseTimeframe = timeframes[baseTimeframeIndex];
            if (baseTimeframe.Index < baseTimeframe.StartIndex || baseTimeframe.Index >= baseTimeframe.EndIndex)
                continue;

            baseTimeframe.Index++;
            if (Index != MaxIndex)
                Index = baseTimeframe.Index;

            DateTime referenceTime = baseTimeframe.Candlesticks[baseTimeframe.Index].OpenTime;

            for (int i = baseTimeframeIndex + 1; i < timeframeCount; i++)
            {
                TimeframeV2 timeframe = timeframes[i];
                if (timeframe.Index >= timeframe.StartIndex &&
                    timeframe.Index < timeframe.EndIndex &&
                    timeframe.Candlesticks[timeframe.Index].CloseTime < referenceTime)
                {
                    timeframe.Index++;
                }
            }
        }
    }

    /// <summary>
    /// Returns the first candle index included in the warmup window for the current source timeframe index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetWarmedUpIndex(TimeframeV2 timeframe, int warmupCandlesCount)
    {
        int warmedUpIndex = timeframe.Index - warmupCandlesCount;
        return warmedUpIndex > timeframe.StartIndex ? warmedUpIndex : timeframe.StartIndex;
    }

    /// <summary>
    /// Reusable data prepared for one split part.
    /// </summary>
    private sealed class PartFeedBuffer
    {
        private readonly SymbolFeedBuffer[] _symbolBuffers;

        private PartFeedBuffer(SymbolDataV2[] symbols, SymbolFeedBuffer[] symbolBuffers)
        {
            Symbols = symbols;
            _symbolBuffers = symbolBuffers;
        }

        /// <summary>
        /// Reused symbols passed to the strategy.
        /// </summary>
        internal SymbolDataV2[] Symbols { get; }

        /// <summary>
        /// Creates the reusable feed graph once for a split part.
        /// </summary>
        internal static PartFeedBuffer Create(List<SymbolDataV2> source, int warmupCandlesCount)
        {
            int symbolCount = source.Count;
            var symbols = new SymbolDataV2[symbolCount];
            var symbolBuffers = new SymbolFeedBuffer[symbolCount];

            for (int symbolIndex = 0; symbolIndex < symbolCount; symbolIndex++)
            {
                SymbolDataV2 sourceSymbol = source[symbolIndex];
                List<TimeframeV2> sourceTimeframes = sourceSymbol.Timeframes;
                int timeframeCount = sourceTimeframes.Count;

                var targetTimeframes = new List<TimeframeV2>(timeframeCount);
                var timeframeBuffers = new TimeframeFeedBuffer[timeframeCount];

                for (int timeframeIndex = 0; timeframeIndex < timeframeCount; timeframeIndex++)
                {
                    TimeframeV2 sourceTimeframe = sourceTimeframes[timeframeIndex];
                    int capacity = GetMaxWarmupWindowLength(sourceTimeframe, warmupCandlesCount);

                    var targetTimeframe = new TimeframeV2
                    {
                        Timeframe = sourceTimeframe.Timeframe,
                        Candlesticks = new List<CandlestickV2>(capacity)
                    };

                    targetTimeframes.Add(targetTimeframe);
                    timeframeBuffers[timeframeIndex] = new TimeframeFeedBuffer(
                        targetTimeframe,
                        new CandlestickV2());
                }

                var targetSymbol = new SymbolDataV2
                {
                    Symbol = sourceSymbol.Symbol,
                    Timeframes = targetTimeframes
                };

                symbols[symbolIndex] = targetSymbol;
                symbolBuffers[symbolIndex] = new SymbolFeedBuffer(timeframeBuffers);
            }

            return new PartFeedBuffer(symbols, symbolBuffers);
        }

        /// <summary>
        /// Repopulates the reusable feed graph with the source candles visible on the current tick.
        /// </summary>
        internal SymbolDataV2[] Fill(
            List<SymbolDataV2> source,
            int warmupCandlesCount,
            bool sortCandlesInDescOrder,
            bool useFullCandleForCurrent)
        {
            int symbolCount = _symbolBuffers.Length;
            for (int symbolIndex = 0; symbolIndex < symbolCount; symbolIndex++)
            {
                SymbolFeedBuffer symbolBuffer = _symbolBuffers[symbolIndex];
                List<TimeframeV2> sourceTimeframes = source[symbolIndex].Timeframes;

                if (!useFullCandleForCurrent)
                    symbolBuffer.PrepareCurrentCandleViews(sourceTimeframes, warmupCandlesCount);

                symbolBuffer.Fill(sourceTimeframes, warmupCandlesCount, sortCandlesInDescOrder, useFullCandleForCurrent);
            }

            return Symbols;
        }

        /// <summary>
        /// Calculates the maximum list capacity required for this timeframe during the run.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMaxWarmupWindowLength(TimeframeV2 timeframe, int warmupCandlesCount)
        {
            int availableCandles = timeframe.EndIndex >= timeframe.StartIndex
                ? timeframe.EndIndex - timeframe.StartIndex + 1
                : 0;

            if (availableCandles == 0)
                return 0;

            int warmupWindowLength = Math.Max(0, warmupCandlesCount) + 1;
            return Math.Min(availableCandles, warmupWindowLength);
        }
    }

    /// <summary>
    /// Reusable feed state for a single symbol.
    /// </summary>
    private sealed class SymbolFeedBuffer(TimeframeFeedBuffer[] timeframeBuffers)
    {
        private readonly TimeframeFeedBuffer[] _timeframeBuffers = timeframeBuffers;

        /// <summary>
        /// Prepares per-timeframe current-candle views without mutating the source history.
        /// </summary>
        internal void PrepareCurrentCandleViews(List<TimeframeV2> sourceTimeframes, int warmupCandlesCount)
        {
            TimeframeV2 lowestTimeframe = sourceTimeframes[0];
            List<CandlestickV2> lowestCandles = lowestTimeframe.Candlesticks;
            CandlestickV2 referenceCandle = lowestCandles[lowestTimeframe.Index];

            decimal referenceOpen = referenceCandle.Open;
            DateTime referenceOpenTime = referenceCandle.OpenTime;
            DateTime referenceCloseTime = referenceCandle.CloseTime;
            int lowestWarmupIndex = GetWarmedUpIndex(lowestTimeframe, warmupCandlesCount);

            int timeframeCount = _timeframeBuffers.Length;
            for (int timeframeIndex = 0; timeframeIndex < timeframeCount; timeframeIndex++)
            {
                TimeframeV2 sourceTimeframe = sourceTimeframes[timeframeIndex];
                CandlestickV2 sourceCandle = sourceTimeframe.Candlesticks[sourceTimeframe.Index];
                TimeframeFeedBuffer buffer = _timeframeBuffers[timeframeIndex];

                if (sourceCandle.CloseTime == referenceCloseTime && sourceCandle.OpenTime != referenceOpenTime)
                {
                    buffer.UseCurrentCandleView = false;
                    continue;
                }

                CandlestickV2 currentCandleView = buffer.CurrentCandleView;
                currentCandleView.OpenTime = sourceCandle.OpenTime;
                currentCandleView.Open = sourceCandle.Open;
                currentCandleView.Volume = sourceCandle.Volume;
                currentCandleView.Close = referenceOpen;
                currentCandleView.CloseTime = referenceOpenTime;
                currentCandleView.High = referenceOpen;
                currentCandleView.Low = referenceOpen;

                if (referenceOpenTime > sourceCandle.OpenTime && referenceCloseTime < sourceCandle.CloseTime)
                {
                    ApplyConsolidatedHighLow(
                        lowestCandles,
                        lowestWarmupIndex,
                        lowestTimeframe.Index,
                        sourceCandle.OpenTime,
                        currentCandleView);
                }

                buffer.UseCurrentCandleView = true;
            }
        }

        /// <summary>
        /// Fills the target timeframe candle lists from the current source indexes.
        /// </summary>
        internal void Fill(
            List<TimeframeV2> sourceTimeframes,
            int warmupCandlesCount,
            bool sortCandlesInDescOrder,
            bool useFullCandleForCurrent)
        {
            int timeframeCount = _timeframeBuffers.Length;
            for (int timeframeIndex = 0; timeframeIndex < timeframeCount; timeframeIndex++)
            {
                TimeframeV2 sourceTimeframe = sourceTimeframes[timeframeIndex];
                TimeframeFeedBuffer buffer = _timeframeBuffers[timeframeIndex];
                List<CandlestickV2> sourceCandles = sourceTimeframe.Candlesticks;
                List<CandlestickV2> targetCandles = buffer.TargetTimeframe.Candlesticks;

                targetCandles.Clear();

                int warmedUpIndex = GetWarmedUpIndex(sourceTimeframe, warmupCandlesCount);
                int currentIndex = sourceTimeframe.Index;

                if (sortCandlesInDescOrder)
                {
                    for (int sourceIndex = currentIndex; sourceIndex >= warmedUpIndex; sourceIndex--)
                    {
                        targetCandles.Add(SelectFeedCandle(
                            sourceCandles[sourceIndex],
                            sourceIndex,
                            currentIndex,
                            buffer,
                            useFullCandleForCurrent));
                    }
                }
                else
                {
                    for (int sourceIndex = warmedUpIndex; sourceIndex <= currentIndex; sourceIndex++)
                    {
                        targetCandles.Add(SelectFeedCandle(
                            sourceCandles[sourceIndex],
                            sourceIndex,
                            currentIndex,
                            buffer,
                            useFullCandleForCurrent));
                    }
                }
            }
        }

        /// <summary>
        /// Selects the source candle or the reusable current-candle view for the visible window.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CandlestickV2 SelectFeedCandle(
            CandlestickV2 sourceCandle,
            int sourceIndex,
            int currentIndex,
            TimeframeFeedBuffer buffer,
            bool useFullCandleForCurrent)
        {
            if (!useFullCandleForCurrent && sourceIndex == currentIndex && buffer.UseCurrentCandleView)
                return buffer.CurrentCandleView;

            return sourceCandle;
        }

        /// <summary>
        /// Applies high/low values from the visible lowest-timeframe range to a higher-timeframe current candle.
        /// </summary>
        private static void ApplyConsolidatedHighLow(
            List<CandlestickV2> lowestCandles,
            int lowestWarmupIndex,
            int referenceIndex,
            DateTime openTime,
            CandlestickV2 targetCandle)
        {
            int firstValidIndex = FindFirstOpenTimeAtOrAfter(
                lowestCandles,
                lowestWarmupIndex,
                referenceIndex,
                openTime);

            if (firstValidIndex >= referenceIndex)
                return;

            decimal high = targetCandle.High;
            decimal low = targetCandle.Low;
            Span<CandlestickV2> lowestCandlesSpan = CollectionsMarshal.AsSpan(lowestCandles);

            for (int i = firstValidIndex; i < referenceIndex; i++)
            {
                CandlestickV2 candle = lowestCandlesSpan[i];
                if (candle.High > high)
                    high = candle.High;

                if (candle.Low < low)
                    low = candle.Low;
            }

            targetCandle.High = high;
            targetCandle.Low = low;
        }

        /// <summary>
        /// Finds the first candle with OpenTime greater than or equal to the requested time.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindFirstOpenTimeAtOrAfter(
            List<CandlestickV2> candles,
            int startIndex,
            int endIndexInclusive,
            DateTime openTime)
        {
            int low = startIndex;
            int high = endIndexInclusive;
            int result = endIndexInclusive + 1;

            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                if (candles[middle].OpenTime >= openTime)
                {
                    result = middle;
                    high = middle - 1;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Reusable feed state for a single timeframe.
    /// </summary>
    private sealed class TimeframeFeedBuffer(TimeframeV2 targetTimeframe, CandlestickV2 currentCandleView)
    {
        internal TimeframeV2 TargetTimeframe { get; } = targetTimeframe;
        internal CandlestickV2 CurrentCandleView { get; } = currentCandleView;
        internal bool UseCurrentCandleView { get; set; }
    }
}
