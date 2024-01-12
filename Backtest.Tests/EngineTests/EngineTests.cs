using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;

namespace Backtest.Tests.EngineTests
{
    /// <summary>
    /// Testing backtesting Engine
    /// </summary>
    public class EngineTests : EngineTestsBase
    {
        [Fact]
        public async Task TestRunningEngineWithoutExceptions()
        {

            int warmupCandlesCount = 2;
            var trade = new TestTrade();

            var strategy = new TestStrategy();
            strategy.ExecuteStrategyDelegate = (symbols) =>
            {
                var copy = symbols;
            };

            var engine = new EngineV1(warmupCandlesCount, trade, strategy);

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, warmupCandlesCount);

            await engine.RunAsync(data);

            Assert.True(true);
        }

        [Fact]
        public async Task TestCancellationToken()
        {
            var tokenSource = new CancellationTokenSource();

            int warmupCandlesCount = 2;
            var trade = new TestTrade();

            var strategy = new TestStrategy();
            strategy.ExecuteStrategyDelegate = (symbols) =>
            {
                tokenSource.Cancel();
            };

            var engine = new EngineV1(warmupCandlesCount, trade, strategy);
            engine.OnCancellationFinishedDelegate = () =>
            {
                Assert.True(true);
            };

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, warmupCandlesCount);

            await engine.RunAsync(data, tokenSource.Token);
        }

        [Fact]
        public async Task TestCandlesOrder()
        {
            var tokenSource = new CancellationTokenSource();

            int warmupCandlesCount = 2;
            var trade = new TestTrade();

            var strategy = new TestStrategy();
            strategy.ExecuteStrategyDelegate = (symbols) =>
            {
                // --- Checking candles order
                if (symbols.Any())
                {
                    var firstSymbol = symbols.First();
                    if (firstSymbol.Timeframes.Any())
                    {
                        var firstTimeframe = firstSymbol.Timeframes.First();

                        if (firstTimeframe.Candlesticks.Count() >= 2)
                        {
                            var firstCandles = firstTimeframe.Candlesticks.Take(2);

                            Assert.True(firstCandles.First().OpenTime > firstCandles.Last().OpenTime);
                        }
                    }

                }

                tokenSource.Cancel();
            };

            var engine = new EngineV1(warmupCandlesCount, trade, strategy);

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, warmupCandlesCount);

            await engine.RunAsync(data, tokenSource.Token);
        }

        [Fact]
        public async Task TestWarmupCandlesResultCount()
        {
            var tokenSource = new CancellationTokenSource();

            int warmupCandlesCount = 3;
            var trade = new TestTrade();

            bool allWarmupCandlesResultsAreCorrect = true;

            var strategy = new TestStrategy();
            strategy.ExecuteStrategyDelegate = (symbols) =>
            {
                // --- Checking candles order
                if (symbols.Any())
                {
                    foreach (var symbol in symbols)
                    {
                        foreach (var timeframe in symbol.Timeframes)
                        {
                            int candlesCount = timeframe.Candlesticks.Count();

                            allWarmupCandlesResultsAreCorrect = allWarmupCandlesResultsAreCorrect && candlesCount == warmupCandlesCount + 1;
                        }
                    }
                }

                tokenSource.Cancel();
            };

            var engine = new EngineV1(warmupCandlesCount, trade, strategy);

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, warmupCandlesCount);

            await engine.RunAsync(data, tokenSource.Token);

            Assert.True(allWarmupCandlesResultsAreCorrect);
        }

        /// <summary>
        /// Generates Dummy Data Splitter Result
        /// </summary>
        /// <returns></returns>
        private IEnumerable<IEnumerable<ISymbolData>> GenerateCandles(DateTime startingDate, int totalCandlesCount, int daysPerSplit, int warmupCandlesCount)
        {
            ISymbolDataSplitter symbolDataSplitter = new SymbolDataSplitterV1(daysPerSplit, warmupCandlesCount, startingDate, true);

            var generatedSymbolsData = GenerateFakeSymbolsData(new List<string> { "BTCUSDT", "ETHUSDT", "SOLUSDT" },
                new List<CandlestickInterval> { CandlestickInterval.M5, CandlestickInterval.M15, CandlestickInterval.M30, CandlestickInterval.H1 },
                startingDate.AddHours(-warmupCandlesCount), totalCandlesCount);

            return symbolDataSplitter.SplitAsync(generatedSymbolsData).Result;
        }
    }
}
