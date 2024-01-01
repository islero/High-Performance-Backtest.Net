using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;

namespace Backtest.Tests.SymbolDataSplitterTests
{
    /// <summary>
    /// Tests that require of generating separate specific candles data instead of using 1 data for all tests
    /// </summary>
    public class SymbolDataSplitterHeavyTests : SymbolDataSplitterBase
    {
        // --- Properties
        private ISymbolDataSplitter SymbolDataSplitter { get; set; }
        private DateTime StartingDate { get; set; }
        private int WarmupCandlesCount { get; set; }

        // --- Constructors
        public SymbolDataSplitterHeavyTests()
        {
            StartingDate = new DateTime(2023, 1, 1);
            WarmupCandlesCount = 2;

            SymbolDataSplitter = new SymbolDataSplitterV1(1, WarmupCandlesCount, StartingDate);
        }

        /// <summary>
        /// Testing multiple SymbolData instances where one of them has historical candles that begins after backtesting starting datetime
        /// </summary>
        [Fact]
        public void TestSymbolDataWithStartDateOutOfRange()
        {
            var correctRangeSymbolsData = GenerateFakeSymbolsData(new List<string> { "BTCUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15 },
                StartingDate.AddHours(-WarmupCandlesCount), 6720);

            var outOfRangeSymbolsData = GenerateFakeSymbolsData(new List<string> { "ETHUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15 },
                new DateTime(2023, 1, 19), 672);

            correctRangeSymbolsData.AddRange(outOfRangeSymbolsData);

            var splitResult = SymbolDataSplitter.SplitAsync(correctRangeSymbolsData).Result;

            // --- Check if there are no zero records
            Assert.True(!splitResult.Any(x => !x.Any()));

            for (int i = 0; i <= 17; i++)
                Assert.Single(splitResult.ElementAt(i));

            for (int i = 18; i <= 20; i++)
                Assert.Equal(2, splitResult.ElementAt(i).Count());

            for (int i = 21; i <= 23; i++)
                Assert.Single(splitResult.ElementAt(i));
        }

        /// <summary>
        /// Test passing timeframes with incorrect timeframes order
        /// </summary>
        [Fact]
        public async Task TestTimeframesIncorrectOrder()
        {
            var symbolsDataIncorrectTimeframesOrder = GenerateFakeSymbolsData(new List<string> { "ETHUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M30, CandlestickInterval.M15, CandlestickInterval.M5 },
                StartingDate, 672);

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () => await SymbolDataSplitter.SplitAsync(symbolsDataIncorrectTimeframesOrder));

            Assert.Equal("symbolsData argument contains invalid or not properly sorted data", exception.Message);
        }

        /// <summary>
        /// Test if symbols data contain duplicated symbols
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TestDuplicatedSymbols()
        {
            var duplicatedSymbolsData = GenerateFakeSymbolsData(new List<string> { "ETHUSDT", "ETHUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15 },
                new DateTime(2023, 1, 19), 672);

            var exception = await Assert.ThrowsAsync<Exception>(async () => await SymbolDataSplitter.SplitAsync(duplicatedSymbolsData));

            Assert.Equal("symbolsData contain duplicated symbols or timeframes", exception.Message);
        }

        /// <summary>
        /// Test if symbols data contain duplicated timeframes
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TestDuplicatedTimeframes()
        {
            var duplicatedSymbolsData = GenerateFakeSymbolsData(new List<string> { "ETHUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M5, CandlestickInterval.M15 },
                new DateTime(2023, 1, 19), 672);

            var exception = await Assert.ThrowsAsync<Exception>(async () => await SymbolDataSplitter.SplitAsync(duplicatedSymbolsData));

            Assert.Equal("symbolsData contain duplicated symbols or timeframes", exception.Message);
        }
    }
}
