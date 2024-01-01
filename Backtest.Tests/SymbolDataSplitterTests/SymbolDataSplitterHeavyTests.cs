using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System.Security.Authentication;

namespace Backtest.Tests.SymbolDataSplitterTests
{
    /// <summary>
    /// Tests that require of generating separate specific candles data instead of using 1 data for all tests
    /// </summary>
    public class SymbolDataSplitterHeavyTests : SymbolDataSplitterBase
    {
        /// <summary>
        /// Testing multiple SymbolData instances where one of them has historical candles that begins after backtesting starting datetime
        /// </summary>
        [Fact]
        public void TestSymbolDataWithStartDateOutOfRange()
        {
            DateTime startingDate = new DateTime(2023, 1, 1);
            int warmupCandlesCount = 2;
            ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(1, warmupCandlesCount, startingDate);

            var correctRangeSymbolsData = GenerateFakeSymbolsData(new List<string> { "BTCUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15 },
                startingDate.AddHours(-warmupCandlesCount), 6720);

            var outOfRangeSymbolsData = GenerateFakeSymbolsData(new List<string> { "ETHUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15 },
                new DateTime(2023, 1, 19), 672);

            correctRangeSymbolsData.AddRange(outOfRangeSymbolsData);

            var splitResult = symbolDataSplitter.SplitAsync(correctRangeSymbolsData).Result;

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
            DateTime startingDate = new DateTime(2023, 1, 1);
            int warmupCandlesCount = 2;

            var symbolsDataIncorrectTimeframesOrder = GenerateFakeSymbolsData(new List<string> { "ETHUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M30, CandlestickInterval.M15, CandlestickInterval.M5 },
                startingDate, 672);

            ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(1, warmupCandlesCount, startingDate);

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () => await symbolDataSplitter.SplitAsync(symbolsDataIncorrectTimeframesOrder));

            Assert.Equal("symbolsData argument contains invalid or not properly sorted data", exception.Message);
        }
    }
}
