using Backtest.Net.Candlesticks;
using Backtest.Net.Engines;
using Backtest.Net.SymbolsData;
using Backtest.Net.Timeframes;
using Models.Net.Enums;

namespace Backtest.Tests.EngineTests;

/// <summary>
/// Testing backtesting Engine V9
/// </summary>
public class EngineV9Tests : EngineTestsV2
{
    /// <summary>
    /// Initializing Engine V9
    /// </summary>
    public EngineV9Tests()
    {
        WarmupCandlesCount = 2;
        Trade = new TestTrade();
        Strategy = new TestStrategyV2();

        EngineV2 = new EngineV9(WarmupCandlesCount, true, false)
        {
            OnTick = OnTickMethodV2
        };
    }
    
    [Fact]
    public async Task TestCurrentCandleOhlcConsolidation()
    {
        var tokenSource = new CancellationTokenSource();

        Strategy.ExecuteStrategyDelegateV2 = symbols =>
        {
            // --- Checking candles order
            var symbolsList = symbols.ToList();
                
            if (symbolsList.Count == 0) Assert.Fail("There is no symbols exist in test data");
                
            foreach (var symbol in symbolsList)
            {
                var firstTimeframe = symbol.Timeframes.First();
                var lastTimeframe = symbol.Timeframes.Last();
                var targetCandle = lastTimeframe.Candlesticks.First();
                
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

            //tokenSource.Cancel();
        };

        // --- Generate fake SymbolData splitter
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