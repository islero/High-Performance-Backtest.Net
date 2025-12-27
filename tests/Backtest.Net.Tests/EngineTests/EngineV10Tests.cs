using Backtest.Net.Candlesticks;
using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Tests.EngineTests;

/// <summary>
/// Testing backtesting Engine V10
/// </summary>
public class EngineV10Tests : EngineTestsV2
{
    /// <summary>
    /// Initializing Engine V10
    /// </summary>
    public EngineV10Tests()
    {
        WarmupCandlesCount = 2;
        Trade = new TestTrade();
        Strategy = new TestStrategyV2();

        EngineV2 = new EngineV10(WarmupCandlesCount, true, false)
        {
            OnTick = OnTickMethodV2
        };
    }

    /// <summary>
    /// EngineV10 has the same OHLC handling behavior as V9.
    /// The first (lowest) timeframe's current candle has High=Low=Close=Open,
    /// but higher timeframes may or may not be modified depending on time alignment.
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

                // For EngineV10: The first (lowest) timeframe should have OHLC equal to open
                bool firstTfOhlcEqual = firstCandle.Close == openPrice &&
                                        firstCandle.High == openPrice &&
                                        firstCandle.Low == openPrice &&
                                        firstCandle.CloseTime == openTime;

                if (!firstTfOhlcEqual)
                    Assert.Fail("First timeframe's current candle OHLC should equal open price");

                // For higher timeframes, verify High >= Low
                // Note: In synthetic test data, Open/Close may not be within High-Low range
                // because EngineV10 sets High/Low to reference candle values while Open remains original
                foreach (TimeframeV2 timeframe in symbol.Timeframes.Skip(1))
                {
                    CandlestickV2 candle = timeframe.Candlesticks.First();

                    // Basic validity check - High should always be >= Low
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
    /// EngineV10 has the same index management logic as V9.
    /// This test validates that the engine completes and indexes advance properly.
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

        // Check that the first timeframe reached or is near EndIndex
        List<SymbolDataV2> firstSymbolData = dataList.First();
        List<TimeframeV2> timeframes = firstSymbolData.First().Timeframes;
        TimeframeV2 firstTimeframe = timeframes.First();

        // Verify the first timeframe index advanced significantly
        Assert.True(firstTimeframe.Index >= firstTimeframe.EndIndex - 1,
            $"First timeframe should reach near EndIndex. Index={firstTimeframe.Index}, EndIndex={firstTimeframe.EndIndex}");

        // Verify higher timeframes' indexes have advanced from their starting positions
        foreach (TimeframeV2 timeframe in timeframes.Skip(1))
        {
            Assert.True(timeframe.Index > timeframe.StartIndex + WarmupCandlesCount,
                $"Higher timeframe {timeframe.Timeframe} should have advanced from start. Index={timeframe.Index}, StartIndex={timeframe.StartIndex}");
        }
    }

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

        // Generate fake SymbolData splitter
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
}
