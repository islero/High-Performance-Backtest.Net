using Backtest.Net.Candlesticks;
using Backtest.Net.Enums;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;

namespace Backtest.Net.Tests.SymbolDataSplitterTests;

/// <summary>
/// Tests that require of generating separate specific candles data instead of using 1 data for all tests
/// </summary>
public class SymbolDataSplitterHeavyTests : TestBase
{
    // --- Properties
    private SymbolDataSplitterV2 SymbolDataSplitter { get; set; }
    private DateTime StartingDate { get; set; }
    private int WarmupCandlesCount { get; set; }

    // --- Constructors
    public SymbolDataSplitterHeavyTests()
    {
        StartingDate = new DateTime(2023, 1, 1);
        WarmupCandlesCount = 2;

        SymbolDataSplitter = new SymbolDataSplitterV2(1, WarmupCandlesCount, StartingDate);
    }

    /// <summary>
    /// Testing multiple SymbolData instances where one of them has historical candles that begins after backtesting starting datetime
    /// </summary>
    [Fact]
    public void TestSymbolDataWithStartDateOutOfRange()
    {
        List<SymbolDataV2> correctRangeSymbolsData = GenerateFakeSymbolsDataV2(["BTCUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.M15],
            StartingDate.AddHours(-WarmupCandlesCount), 6720);

        List<SymbolDataV2> outOfRangeSymbolsData = GenerateFakeSymbolsDataV2(["ETHUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.M15],
            new DateTime(2023, 1, 19), 672);

        correctRangeSymbolsData.AddRange(outOfRangeSymbolsData);

        List<List<SymbolDataV2>> splitResult = SymbolDataSplitter.SplitAsyncV2(correctRangeSymbolsData).Result;

        // --- Check if there are no zero records
        Assert.DoesNotContain(splitResult, x => x.Count == 0);

        for (int i = 0; i <= 17; i++)
            Assert.Single(splitResult.ElementAt(i));

        for (int i = 18; i <= 20; i++)
            Assert.Equal(2, splitResult.ElementAt(i).Count);

        for (int i = 21; i <= 23; i++)
            Assert.Single(splitResult.ElementAt(i));
    }

    /// <summary>
    /// Test passing timeframes with incorrect timeframes order
    /// </summary>
    [Fact]
    public async Task TestTimeframesIncorrectOrder()
    {
        List<SymbolDataV2> symbolsDataIncorrectTimeframesOrder = GenerateFakeSymbolsDataV2(["ETHUSDT"],
            [CandlestickInterval.M30, CandlestickInterval.M15, CandlestickInterval.M5],
            StartingDate, 672);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await SymbolDataSplitter.SplitAsyncV2(symbolsDataIncorrectTimeframesOrder));

        Assert.Equal("symbolsData argument contains invalid or not properly sorted data", exception.Message);
    }

    /// <summary>
    /// Test passing Candlesticks with incorrect order
    /// </summary>
    [Fact]
    public async Task TestCandlestickIncorrectOrder()
    {
        List<SymbolDataV2> symbolsData = GenerateFakeSymbolsDataV2(["ETHUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.M15, CandlestickInterval.M30],
            StartingDate, 672);

        foreach (SymbolDataV2 symbol in symbolsData)
        {
            foreach (TimeframeV2 timeframe in symbol.Timeframes)
            {
                CandlestickV2 firstCandle = timeframe.Candlesticks.First();
                CandlestickV2 secondCandle = timeframe.Candlesticks.Skip(1).First();
                firstCandle.OpenTime = secondCandle.OpenTime.AddSeconds(1);
                firstCandle.CloseTime = secondCandle.CloseTime.AddSeconds(1);
            }
        }

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await SymbolDataSplitter.SplitAsyncV2(symbolsData));

        Assert.Equal("symbolsData argument contains invalid or not properly sorted data", exception.Message);
    }

    /// <summary>
    /// Test if symbols data contain duplicated symbols
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task TestDuplicatedSymbols()
    {
        List<SymbolDataV2> duplicatedSymbolsData = GenerateFakeSymbolsDataV2(["ETHUSDT", "ETHUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.M15],
            new DateTime(2023, 1, 19), 672);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await SymbolDataSplitter.SplitAsyncV2(duplicatedSymbolsData));

        Assert.Equal("symbolsData contain duplicated symbols or timeframes", exception.Message);
    }

    /// <summary>
    /// Test if symbols data contain duplicated timeframes
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task TestDuplicatedTimeframes()
    {
        List<SymbolDataV2> duplicatedSymbolsData = GenerateFakeSymbolsDataV2(["ETHUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.M5, CandlestickInterval.M15],
            new DateTime(2023, 1, 19), 672);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await SymbolDataSplitter.SplitAsyncV2(duplicatedSymbolsData));

        Assert.Equal("symbolsData contain duplicated symbols or timeframes", exception.Message);
    }
}
