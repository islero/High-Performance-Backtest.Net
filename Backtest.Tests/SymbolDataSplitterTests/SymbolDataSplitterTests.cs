using Backtest.Net.Candlesticks;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using System.Linq;
using Backtest.Tests.SymbolDataSplitterTests;

namespace High_Performance_Backtest.Tests
{
    /// <summary>
    /// Testing Symbol Data Splitter
    /// </summary>
    public class SymbolDataSplitterTests : SymbolDataSplitterBase
    {
        // --- Properties
        public IEnumerable<IEnumerable<ISymbolData>> SplitResult { get; set; }
        private DateTime StartingDate { get; set; }

        // --- Constructors
        public SymbolDataSplitterTests()
        {
            StartingDate = new DateTime(2023, 1, 1, 3, 6, 50);
            int warmupCandlesCount = 2;
            int daysPerSplit = 1;
            ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(daysPerSplit, warmupCandlesCount, StartingDate, true);

            var generatedSymbolsData = GenerateFakeSymbolsData(new List<string> { "BTCUSDT", "ETHUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15 },
                StartingDate.AddHours(-warmupCandlesCount), 672);

            SplitResult = symbolDataSplitter.SplitAsync(generatedSymbolsData).Result;
        }

        /// <summary>
        /// Testing if result contain correct parts count
        /// </summary>
        [Fact]
        public void TestCorrectResultPartsCount()
        {
            // --- Checking parts count
            Assert.Equal(3, SplitResult.Count());
        }

        /// <summary>
        /// Testing if result data contain correct Index Open Time
        /// </summary>
        [Fact]
        public void TestIndexOpenTimeAreEqual()
        {
            // --- Checking parts index open time
            foreach (var partSymbols in SplitResult)
            {
                foreach (var part in partSymbols)
                {
                    var firstTimeframe = part.Timeframes.First();
                    var firstCandle = firstTimeframe.Candlesticks.ElementAt(firstTimeframe.Index);

                    foreach (var timeframe in part.Timeframes)
                    {
                        var timeframeCandle = timeframe.Candlesticks.ElementAt(timeframe.Index);

                        Assert.Equal(firstCandle.OpenTime, timeframeCandle.OpenTime);
                    }
                }
            }
        }

        /// <summary>
        /// Testing if latest parts have correct EndIndex Open Time
        /// </summary>
        [Fact]
        public void TestEndIndexesCloseTimeAreEqual()
        {
            // --- Checking parts index open time
            foreach (var partSymbols in SplitResult)
            {
                foreach (var part in partSymbols)
                {
                    var firstTimeframe = part.Timeframes.First();
                    var firstCandle = firstTimeframe.Candlesticks.ElementAt(firstTimeframe.EndIndex);

                    foreach (var timeframe in part.Timeframes)
                    {
                        var timeframeCandle = timeframe.Candlesticks.ElementAt(timeframe.EndIndex);

                        Assert.Equal(firstCandle.CloseTime, timeframeCandle.CloseTime);
                    }
                }
            }
        }

        /// <summary>
        /// Testing that parts have no 0 records
        /// </summary>
        [Fact]
        public void TestPartsHaveNoZeroRecords()
        {
            bool zeroRecords = SplitResult.Any(x => !x.Any());
            Assert.True(!zeroRecords);
        }

        /// <summary>
        /// Check that Symbol data is sequential
        /// </summary>
        [Fact]
        public void TestPartialSymbolDataSequentially()
        {
            DateTime ongoingDate = StartingDate;
            int daysPerSplit = 1;
            foreach (var result in SplitResult)
            {
                if (result == SplitResult.Last())
                    continue;

                foreach (var symbol in result)
                {
                    var firstTimeframe = symbol.Timeframes.First();
                    var firstCandle = firstTimeframe.Candlesticks.ElementAt(firstTimeframe.Index);
                    Assert.Equal(ongoingDate, firstCandle.OpenTime);

                    DateTime nextPeriodOngoingDate = ongoingDate.AddDays(daysPerSplit);

                    var lastCandle = firstTimeframe.Candlesticks.ElementAt(firstTimeframe.EndIndex);
                    Assert.Equal(nextPeriodOngoingDate.AddSeconds(-1), lastCandle.CloseTime);
                }

                ongoingDate = ongoingDate.AddDays(daysPerSplit);
            }
        }
    }
}
