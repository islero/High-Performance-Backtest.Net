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
        /// <summary>
        /// Engine for tests
        /// </summary>
        protected IEngine Engine { get; init; }
        protected ITrade Trade { get; init; }
        protected TestStrategy Strategy { get; init; }
        protected int WarmupCandlesCount { get; init; }

        /// <summary>
        /// Constructor to initialize Engine Mandatory Properties
        /// </summary>
        public EngineTests()
        {
            WarmupCandlesCount = 2;
            Trade = new TestTrade();
            Strategy = new TestStrategy();

            Engine = new EngineV1(WarmupCandlesCount, Trade, Strategy);
        }

        [Fact]
        public async Task TestRunningEngineWithoutExceptions()
        {
            Strategy.ExecuteStrategyDelegate = symbols =>
            {
                _ = symbols;
            };

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

            await Engine.RunAsync(data);

            Assert.True(true);
        }

        [Fact]
        public async Task TestCancellationToken()
        {
            var tokenSource = new CancellationTokenSource();

            Strategy.ExecuteStrategyDelegate = _ =>
            {
                tokenSource.Cancel();
            };

            Engine.OnCancellationFinishedDelegate = () =>
            {
                Assert.True(true);
            };

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

            await Engine.RunAsync(data, tokenSource.Token);
        }

        [Fact]
        public async Task TestCandlesOrder()
        {
            var tokenSource = new CancellationTokenSource();

            Strategy.ExecuteStrategyDelegate = symbols =>
            {
                // --- Checking candles order
                var symbolsList = symbols.ToList();
                
                if (symbolsList.Count != 0)
                {
                    var firstSymbol = symbolsList.First();
                    if (firstSymbol.Timeframes.Any())
                    {
                        var firstTimeframe = firstSymbol.Timeframes.First();

                        if (firstTimeframe.Candlesticks.Count() >= 2)
                        {
                            var firstCandles = firstTimeframe.Candlesticks.Take(2).ToList();

                            Assert.True(firstCandles.First().OpenTime > firstCandles.Last().OpenTime);
                        }
                    }

                }

                tokenSource.Cancel();
            };

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

            await Engine.RunAsync(data, tokenSource.Token);
        }

        [Fact]
        public async Task TestWarmupCandlesResultCount()
        {
            var tokenSource = new CancellationTokenSource();

            var allWarmupCandlesResultsAreCorrect = true;

            Strategy.ExecuteStrategyDelegate = symbols =>
            {
                // --- Checking candles order
                var symbolsList = symbols.ToList();
                if (symbolsList.Count != 0)
                {
                    foreach (var symbol in symbolsList)
                    {
                        foreach (var timeframe in symbol.Timeframes)
                        {
                            var candlesCount = timeframe.Candlesticks.Count();

                            allWarmupCandlesResultsAreCorrect = allWarmupCandlesResultsAreCorrect && candlesCount == WarmupCandlesCount + 1;
                        }
                    }
                }

                tokenSource.Cancel();
            };

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

            await Engine.RunAsync(data, tokenSource.Token);

            Assert.True(allWarmupCandlesResultsAreCorrect);
        }

        [Fact]
        public async Task TestFirstCandleEqualToStartBacktestingDate()
        {
            var tokenSource = new CancellationTokenSource();

            var backtestingStartingDate = new DateTime(2023, 1, 1);
            var allStartingDatesAreCorrect = true;

            Strategy.ExecuteStrategyDelegate = symbols =>
            {
                // --- Checking candles order
                var symbolsList = symbols.ToList();
                if (symbolsList.Count != 0)
                {
                    foreach (var symbol in symbolsList)
                    {
                        foreach (var timeframe in symbol.Timeframes)
                        {
                            var firstCandle = timeframe.Candlesticks.FirstOrDefault();
                            if (firstCandle == null)
                                Assert.Fail("Engine Returned candle equal to null");

                            allStartingDatesAreCorrect = allStartingDatesAreCorrect && firstCandle.OpenTime == backtestingStartingDate;
                        }
                    }
                }

                tokenSource.Cancel();
            };

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(backtestingStartingDate, 500, 1, WarmupCandlesCount);

            await Engine.RunAsync(data, tokenSource.Token);

            Assert.True(allStartingDatesAreCorrect);
        }

        [Fact]
        public async Task TestIfAllIndexesReachedTheEndIndex()
        {
            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

            // --- Checking that before the EngineRun all the data are not reached the EndIndex
            var dataList = data.ToList();
            
            var allNotReachedEndIndex = dataList.All(
                x => x.All(
                    y => y.Timeframes.All(
                        k => k.Index < k.EndIndex && k.Index == k.StartIndex + WarmupCandlesCount)));
            Assert.True(allNotReachedEndIndex);

            await Engine.RunAsync(dataList);

            // --- Checking that after teh EngineRun all the data are reached EndIndex
            var allReachedEndIndex = dataList.All(
                 x => x.All(
                     y => y.Timeframes.All(
                         k => k.Index == k.EndIndex)));
            Assert.True(allReachedEndIndex);
        }

        [Fact]
        public async Task TestCurrentCandleOhlcAreEqual()
        {
            var tokenSource = new CancellationTokenSource();

            var allCurrentCandleOhlcAreEqual = true;

            Strategy.ExecuteStrategyDelegate = symbols =>
            {
                // --- Checking candles order
                var symbolsList = symbols.ToList();
                
                if (symbolsList.Count == 0) return;
                
                foreach (var symbol in symbolsList)
                {
                    var firstTimeframe = symbol.Timeframes.FirstOrDefault();
                    var firstCandle = firstTimeframe?.Candlesticks.FirstOrDefault();
                    if (firstCandle == null) continue;
                    
                    var openPrice = firstCandle.Open;
                    var openTime = firstCandle.OpenTime;

                    var areOhlcEqual = symbol.Timeframes.All(
                        y => y.Candlesticks.First().Close == openPrice &&
                             y.Candlesticks.First().High == openPrice &&
                             y.Candlesticks.First().Low == openPrice &&
                             y.Candlesticks.First().Open == openPrice &&
                             y.Candlesticks.First().CloseTime == openTime);

                    allCurrentCandleOhlcAreEqual = allCurrentCandleOhlcAreEqual && areOhlcEqual;

                    if (!areOhlcEqual)
                        Assert.Fail($"First candle OHLC aren't equal");
                }

                //tokenSource.Cancel();
            };

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

            await Engine.RunAsync(data, tokenSource.Token);

            Assert.True(allCurrentCandleOhlcAreEqual);
        }
        
        /// <summary>
        /// Checking if strategy gets all TFs sorted in Ascending order
        /// </summary>
        [Fact]
        public async Task TimeframesAreSorted()
        {
            // --- Strategy logic simulation
            Strategy.ExecuteStrategyDelegate = symbols =>
            {
                // --- Checking candles order
                var symbolsList = symbols.ToList();
                
                if (symbolsList.Count == 0) return;
                
                foreach (var symbol in symbolsList)
                {
                    var isSorted = symbol.Timeframes.SequenceEqual(symbol.Timeframes.OrderBy(t => t.Timeframe));
                    if (!isSorted)
                    {
                        Assert.Fail($"Timeframes aren't sorted in ascending order");
                    }
                }
            };

            // --- Generate Dummy SymbolData splitter
            var data = GenerateCandles(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

            await Engine.RunAsync(data);

            Assert.True(true);
        }
        
        /// <summary>
        /// Generates Dummy Data Splitter Result
        /// </summary>
        /// <returns></returns>
        private IEnumerable<IEnumerable<ISymbolData>> GenerateCandles(DateTime startingDate, int totalCandlesCount, int daysPerSplit, int warmupCandlesCount)
        {
            var symbolDataSplitter = new SymbolDataSplitterV1(daysPerSplit, warmupCandlesCount, startingDate, true);

            var generatedSymbolsData = GenerateFakeSymbolsData(["BTCUSDT", "ETHUSDT", "SOLUSDT"],
                [CandlestickInterval.M5, CandlestickInterval.M15, CandlestickInterval.M30, CandlestickInterval.H1],
                startingDate.AddHours(-warmupCandlesCount), totalCandlesCount);

            return symbolDataSplitter.SplitAsync(generatedSymbolsData).Result;
        }
    }
}
