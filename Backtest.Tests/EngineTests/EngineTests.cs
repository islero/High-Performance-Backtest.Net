using Backtest.Net.Engines;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;
using Models.Net.Enums;
using Models.Net.Interfaces;

namespace Backtest.Tests.EngineTests;

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

        Engine = new EngineV1(WarmupCandlesCount)
        {
            OnTick = OnTickMethod
        };
    }

    /// <summary>
    /// On Tick Method Implementation
    /// </summary>
    /// <param name="symbolData"></param>
    protected async Task OnTickMethod(IEnumerable<ISymbolData> symbolData)
    {
        var signals = await Strategy.Execute(symbolData.ToList());
        if (signals.Count != 0)
        {
            _ = await Trade.Execute(signals);
        }
    }
        
    [Fact]
    public async Task TestRunningEngineWithoutExceptions()
    {
        Strategy.ExecuteStrategyDelegate = symbols =>
        {
            _ = symbols;
        };

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

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

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

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

            if (symbolsList.Count == 0) Assert.Fail("There is no symbols exist in test data");

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

            tokenSource.Cancel();
        };

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

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

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

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
            if (symbolsList.Count == 0) Assert.Fail("There is no symbols exist in test data");

            foreach (var symbol in symbolsList)
            {
                foreach (var timeframe in symbol.Timeframes)
                {
                    var firstCandle = timeframe.Candlesticks.FirstOrDefault();
                    if (firstCandle == null)
                        Assert.Fail("Engine Returned candle equal to null");

                    allStartingDatesAreCorrect = allStartingDatesAreCorrect &&
                                                 firstCandle.OpenTime == backtestingStartingDate;
                }
            }
                
            tokenSource.Cancel();
        };

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(backtestingStartingDate, 500, 1, WarmupCandlesCount);

        await Engine.RunAsync(data, tokenSource.Token);

        Assert.True(allStartingDatesAreCorrect);
    }

    [Fact]
    public async Task TestIfAllIndexesReachedTheEndIndex()
    {
        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

        // --- Checking that before the EngineRun all the data are not reached the EndIndex
        var dataList = data.ToList();
            
        var allNotReachedEndIndex = dataList.All(
            x => x.All(
                y => y.Timeframes.All(
                    k => k.Index < k.EndIndex && k.Index == k.StartIndex + WarmupCandlesCount)));
        Assert.True(allNotReachedEndIndex);

        await Engine.RunAsync(dataList);

        // --- Checking that after the EngineRun all the data are reached EndIndex
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
                
            if (symbolsList.Count == 0) Assert.Fail("There is no symbols exist in test data");
                
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
                    Assert.Fail("First candle OHLC aren't equal");
            }

            //tokenSource.Cancel();
        };

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

        await Engine.RunAsync(data, tokenSource.Token);

        Assert.True(allCurrentCandleOhlcAreEqual);
    }
        
    /// <summary>
    /// Checking if strategy gets all TF sorted in Ascending order
    /// </summary>
    [Fact]
    public async Task TimeframesAreSorted()
    {
        // --- Strategy logic simulation
        Strategy.ExecuteStrategyDelegate = symbols =>
        {
            // --- Checking candles order
            var symbolsList = symbols.ToList();
                
            if (symbolsList.Count == 0) Assert.Fail("There is no symbols exist in test data");
                
            foreach (var symbol in symbolsList)
            {
                var isSorted = symbol.Timeframes.SequenceEqual(symbol.Timeframes.OrderBy(t => t.Timeframe));
                if (!isSorted)
                {
                    Assert.Fail("Timeframes aren't sorted in ascending order");
                }
            }
        };

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

        await Engine.RunAsync(data);

        Assert.True(true);
    }
        
    /// <summary>
    /// Testing that each next TF open and close contains prior timeframe open close inside its range,
    /// For example, if 5m tf openTime = 1/1/23 12:00:00 and closeTime 1/1/23 12:05:00
    /// 1d tf can't have openTime 1/2/23 12:00:00 and closeTime 1/2/23 23:59:59
    /// </summary>
    [Fact]
    public async Task TestPriorTfOpenCloseInsideNewTfOpenClose()
    {
        // --- Strategy logic simulation
        Strategy.ExecuteStrategyDelegate = symbols =>
        {
            // --- Checking candles order
            var symbolsList = symbols.ToList();
                
            if (symbolsList.Count == 0) Assert.Fail("There is no symbols exist in test data");
                
            foreach (var symbol in symbolsList)
            {
                // Validation Timeframes count
                if(symbol.Timeframes.Count() < 2) Assert.Fail("There must be at least 2 timeframes to continue test");

                var priorTf = symbol.Timeframes.First();
                foreach (var timeframe in symbol.Timeframes.Skip(1))
                {
                    var lowerTfCandle = priorTf.Candlesticks.ElementAt(priorTf.Index);
                    var higherTfCandle = timeframe.Candlesticks.ElementAt(timeframe.Index);
                        
                    // Checking fail conditions
                    if (higherTfCandle.OpenTime > lowerTfCandle.OpenTime
                        ||
                        higherTfCandle.CloseTime < lowerTfCandle.CloseTime)
                        Assert.Fail($"Lower TF {priorTf.Timeframe} is out of range in Higher TF {timeframe.Timeframe}");
                }
                    
            }
        };

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, 1, WarmupCandlesCount);

        await Engine.RunAsync(data);

        Assert.True(true);
    }
    
    /// <summary>
    /// Testing backtesting progress is 100 at the end of the backtesting for single symbol
    /// </summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(30, 2)]
    [InlineData(0, 10)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(3, 10)]
    [InlineData(30, 10)]
    public async Task TestBacktestingProgress_Single_Symbol(int daysPerSplit, int warmupCandlesCount)
    {
        Strategy.ExecuteStrategyDelegate = symbols =>
        {
            _ = symbols;
        };

        // --- Generate fake SymbolData splitter
        var data = GenerateSingleSymbolDataList(new DateTime(2023, 1, 1), 1000, daysPerSplit, warmupCandlesCount);

        await Engine.RunAsync(data);
        Assert.Equal(100, Engine.GetProgress());
    }
    
    /// <summary>
    /// Testing backtesting progress is 100 at the end of the backtesting for multiple symbols
    /// </summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(30, 2)]
    [InlineData(0, 50)]
    [InlineData(1, 50)]
    [InlineData(2, 50)]
    [InlineData(3, 50)]
    [InlineData(30, 50)]
    public async Task TestBacktestingProgress_Multiple_Symbols(int daysPerSplit, int warmupCandlesCount)
    {
        Strategy.ExecuteStrategyDelegate = symbols =>
        {
            _ = symbols;
        };

        // --- Generate fake SymbolData splitter
        var data = GenerateSymbolDataList(new DateTime(2023, 1, 1), 500, daysPerSplit, warmupCandlesCount);

        await Engine.RunAsync(data);

        Assert.Equal(100, Engine.GetProgress());
    }

    /// <summary>
    /// Generates fake symbol data list
    /// </summary>
    /// <returns></returns>
    private List<List<ISymbolData>> GenerateSymbolDataList(DateTime startingDate, int totalCandlesCount,
        int daysPerSplit, int warmupCandlesCount)
    {
        var symbolDataSplitter = new SymbolDataSplitterV1(daysPerSplit, warmupCandlesCount, startingDate, true);

        var generatedSymbolsData = GenerateFakeSymbolsData(["BTCUSDT", "ETHUSDT", "SOLUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.M15, CandlestickInterval.M30, CandlestickInterval.H1],
            startingDate.AddHours(-warmupCandlesCount), totalCandlesCount);

        return symbolDataSplitter.SplitAsync(generatedSymbolsData).Result;
    }
    
    /// <summary>
    /// Generates fake single symbol data list
    /// </summary>
    /// <returns></returns>
    private List<List<ISymbolData>> GenerateSingleSymbolDataList(DateTime startingDate, int totalCandlesCount,
        int daysPerSplit, int warmupCandlesCount)
    {
        var symbolDataSplitter = new SymbolDataSplitterV1(daysPerSplit, warmupCandlesCount, startingDate, true);

        var generatedSymbolsData = GenerateFakeSymbolsData(["BTCUSDT"],
            [CandlestickInterval.M5, CandlestickInterval.M15, CandlestickInterval.M30, CandlestickInterval.H1],
            startingDate.AddHours(-warmupCandlesCount), totalCandlesCount);

        return symbolDataSplitter.SplitAsync(generatedSymbolsData).Result;
    }
}