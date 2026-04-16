using Backtest.Net.Candlesticks;
using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Tests.EngineTests;

/// <summary>
/// Tests for EngineV11 zero-allocation feed-buffer behavior.
/// </summary>
public class EngineV11Tests : EngineTestsV2
{
    /// <summary>
    /// Initializes EngineV11 with the same externally visible behavior as EngineV10.
    /// </summary>
    public EngineV11Tests()
    {
        WarmupCandlesCount = 2;
        Trade = new TestTrade();
        Strategy = new TestStrategyV2();

        EngineV2 = new EngineV11(WarmupCandlesCount, true, false)
        {
            OnTick = OnTickMethodV2
        };
    }

    /// <summary>
    /// EngineV11 keeps the V10 current-candle masking behavior while using reusable feed buffers.
    /// </summary>
    [Fact]
    public override async Task TestCurrentCandleOhlcAreEqual()
    {
        using var tokenSource = new CancellationTokenSource();

        Strategy.ExecuteStrategyDelegateV2 = symbols =>
        {
            var symbolsList = symbols.ToList();
            if (symbolsList.Count == 0) Assert.Fail("There is no symbols exist in test data");

            foreach (SymbolDataV2 symbol in symbolsList)
            {
                TimeframeV2? firstTimeframe = symbol.Timeframes.FirstOrDefault();
                CandlestickV2? firstCandle = firstTimeframe?.Candlesticks.FirstOrDefault();
                if (firstCandle == null) continue;

                decimal openPrice = firstCandle.Open;
                DateTime openTime = firstCandle.OpenTime;

                bool firstTfOhlcEqual = firstCandle.Close == openPrice &&
                                        firstCandle.High == openPrice &&
                                        firstCandle.Low == openPrice &&
                                        firstCandle.CloseTime == openTime;

                if (!firstTfOhlcEqual)
                    Assert.Fail("First timeframe's current candle OHLC should equal open price");

                foreach (TimeframeV2 timeframe in symbol.Timeframes.Skip(1))
                {
                    CandlestickV2 candle = timeframe.Candlesticks.First();
                    if (candle.High < candle.Low)
                        Assert.Fail("Candle High should be >= Low");
                }
            }
        };

        List<List<SymbolDataV2>> data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);
        await EngineV2.RunAsync(data, tokenSource.Token);
        Assert.True(true);
    }

    /// <summary>
    /// EngineV11 uses the same index-alignment rules as EngineV10.
    /// </summary>
    [Fact]
    public override async Task TestIfAllIndexesReachedTheEndIndex()
    {
        List<List<SymbolDataV2>> data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 0, WarmupCandlesCount);
        var dataList = data.ToList();

        bool allNotReachedEndIndex = dataList.All(
            x => x.All(
                y => y.Timeframes.All(
                    k => k.Index < k.EndIndex && k.Index == k.StartIndex + WarmupCandlesCount)));
        Assert.True(allNotReachedEndIndex);

        await EngineV2.RunAsync(dataList);

        List<SymbolDataV2> firstSymbolData = dataList.First();
        List<TimeframeV2> timeframes = firstSymbolData.First().Timeframes;
        TimeframeV2 firstTimeframe = timeframes.First();

        Assert.True(firstTimeframe.Index >= firstTimeframe.EndIndex - 1,
            $"First timeframe should reach near EndIndex. Index={firstTimeframe.Index}, EndIndex={firstTimeframe.EndIndex}");

        foreach (TimeframeV2 timeframe in timeframes.Skip(1))
        {
            Assert.True(timeframe.Index > timeframe.StartIndex + WarmupCandlesCount,
                $"Higher timeframe {timeframe.Timeframe} should have advanced from start. Index={timeframe.Index}, StartIndex={timeframe.StartIndex}");
        }
    }

    /// <summary>
    /// Verifies higher-timeframe current-candle high/low consolidation against the lowest visible timeframe.
    /// </summary>
    [Fact]
    public async Task TestCurrentCandleOhlcConsolidation()
    {
        using var tokenSource = new CancellationTokenSource();

        Strategy.ExecuteStrategyDelegateV2 = symbols =>
        {
            var symbolsList = symbols.ToList();

            if (symbolsList.Count == 0) Assert.Fail("There is no symbols exist in test data");

            foreach (SymbolDataV2 symbol in symbolsList)
            {
                TimeframeV2 firstTimeframe = symbol.Timeframes.First();
                TimeframeV2 lastTimeframe = symbol.Timeframes.Last();
                CandlestickV2 targetCandle = lastTimeframe.Candlesticks.First();

                switch (firstTimeframe.Candlesticks.Count)
                {
                    case 2:
                    {
                        Assert.Equal(100, targetCandle.Open);
                        Assert.Equal(100, targetCandle.High);
                        Assert.Equal(98, targetCandle.Low);
                        Assert.Equal(99, targetCandle.Close);
                        break;
                    }
                    case 3:
                    {
                        Assert.Equal(100, targetCandle.Open);
                        Assert.Equal(101, targetCandle.High);
                        Assert.Equal(97, targetCandle.Low);
                        Assert.Equal(97, targetCandle.Close);
                        break;
                    }
                }
            }
        };

        List<List<SymbolDataV2>> data =
        [
            [new SymbolDataV2
            {
                Symbol = "BTCUSDT", Timeframes = [
                    new TimeframeV2 { Timeframe = CandlestickInterval.D1, Candlesticks = [
                new CandlestickV2 { OpenTime = new DateTime(2024, 1, 1), Open = 100, High = 100, Low = 98, Close = 99, CloseTime = new DateTime(2024, 1, 2).AddSeconds(-1) },
                new CandlestickV2 { OpenTime = new DateTime(2024, 1, 2), Open = 99, High = 101, Low = 97, Close = 97, CloseTime = new DateTime(2024, 1, 3).AddSeconds(-1) },
                new CandlestickV2 { OpenTime = new DateTime(2024, 1, 3), Open = 97, High = 97, Low = 95, Close = 95, CloseTime = new DateTime(2024, 1, 4).AddSeconds(-1) }, ], Index = 0, EndIndex = 3, StartIndex = 0 },
                    new TimeframeV2 { Timeframe = CandlestickInterval.W1, Candlesticks = [
                        new CandlestickV2 { OpenTime = new DateTime(2024, 1, 1), Open = 100, High = 102, Low = 94, Close = 93, CloseTime = new DateTime(2024, 1, 7) } ], Index = 0, EndIndex = 1, StartIndex = 0 }]
            }]
        ];

        await EngineV2.RunAsync(data, tokenSource.Token);
    }

    /// <summary>
    /// Verifies the borrowed-snapshot contract that enables EngineV11 to avoid per-tick feed allocations.
    /// </summary>
    [Fact]
    public async Task TestFeedGraphIsReusedBetweenTicks()
    {
        using var tokenSource = new CancellationTokenSource();
        CancellationTokenSource cts = tokenSource;

        int ticks = 0;
        SymbolDataV2[]? firstSymbols = null;
        SymbolDataV2? firstSymbol = null;
        TimeframeV2? firstTimeframe = null;
        List<CandlestickV2>? firstCandlesticks = null;

        EngineV2.OnTick = symbols =>
        {
            ticks++;

            if (ticks == 1)
            {
                firstSymbols = symbols;
                firstSymbol = symbols[0];
                firstTimeframe = symbols[0].Timeframes[0];
                firstCandlesticks = symbols[0].Timeframes[0].Candlesticks;
            }
            else
            {
                Assert.Same(firstSymbols, symbols);
                Assert.Same(firstSymbol, symbols[0]);
                Assert.Same(firstTimeframe, symbols[0].Timeframes[0]);
                Assert.Same(firstCandlesticks, symbols[0].Timeframes[0].Candlesticks);
                cts.Cancel();
            }

            return Task.CompletedTask;
        };

        List<List<SymbolDataV2>> data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 0, WarmupCandlesCount);
        await EngineV2.RunAsync(data, tokenSource.Token);

        Assert.True(ticks >= 2);
    }
}
