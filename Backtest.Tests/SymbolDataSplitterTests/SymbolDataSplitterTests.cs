using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Tests;
using Backtest.Tests.SymbolDataSplitterTests;

namespace High_Performance_Backtest.Tests
{
    /// <summary>
    /// Testing Symbol Data Splitter
    /// </summary>
    public class SymbolDataSplitterTests : TestBase
    {
        // --- Properties
        private IEnumerable<IEnumerable<ISymbolData>> SplitResult { get; set; }
        private DateTime StartingDate { get; set; }
        private int DaysPerSplit { get; set; }

        // --- Constructors
        public SymbolDataSplitterTests()
        {
            StartingDate = new DateTime(2023, 1, 1, 3, 6, 50);
            DaysPerSplit = 1;

            int warmupCandlesCount = 2;
            ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(DaysPerSplit, warmupCandlesCount, StartingDate, true);

            var generatedSymbolsData = GenerateFakeSymbolsData(new List<string> { "BTCUSDT", "ETHUSDT", "SOLUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15, CandlestickInterval.M30, CandlestickInterval.H1 },
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
            foreach (var result in SplitResult)
            {
                if (result == SplitResult.Last())
                    continue;

                foreach (var symbol in result)
                {
                    foreach (var timeframe in symbol.Timeframes)
                    {
                        var firstCandle = timeframe.Candlesticks.ElementAt(timeframe.Index);
                        Assert.Equal(ongoingDate, firstCandle.OpenTime);

                        DateTime nextPeriodOngoingDate = ongoingDate.AddDays(DaysPerSplit);

                        var lastCandle = timeframe.Candlesticks.ElementAt(timeframe.EndIndex);
                        Assert.Equal(nextPeriodOngoingDate.AddSeconds(-1), lastCandle.CloseTime);
                    }
                }

                ongoingDate = ongoingDate.AddDays(DaysPerSplit);
            }
        }
    }
}
