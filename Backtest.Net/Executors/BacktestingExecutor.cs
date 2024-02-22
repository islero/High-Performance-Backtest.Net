using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;

namespace Backtest.Net.Executors
{
    /// <summary>
    /// High-Performance Backtesting Executor
    /// </summary>
    public sealed class BacktestingExecutor
    {
        // --- Properties
        public bool IsRunning { get; private set; } // Checks whether or not backtesting is currently running
        private DateTime StartDateTime { get; } // Backtesting Start DateTime
        private int WarmupCandlesCount { get; } // The amount of warmup candles count
        private ITrade Trade { get; } // Handles Virtual Trades
        private IStrategy Strategy { get; } // The Backtesting Strategy
        private bool CorrectEndIndex { get; } // Makes sure the end index are the same for all symbols and timeframes
        private CandlestickInterval? WarmupTimeframe { get; } // The timeframe must be warmed up and all lower timeframes accordingly, if null - will be set automatically
        private int DaysPerSplit { get; } // How many days in one split range should exist
        private ISymbolDataSplitter? Splitter { get; set;  } // Splits entire history on smaller pieces
        private IEngine? Engine { get; set;  } // The backtesting engine itself, performs backtesting, passes prepared history into strategy

        // --- Delegates
        public Action<BacktestingEventStatus>? OnBacktestingEvent; // Notifies subscribed objects about backtesting events

        // --- Constructors
        public BacktestingExecutor(DateTime startDateTime, int daysPerSplit, int warmupCandlesCount, 
            ITrade trade, IStrategy strategy, bool correctEndIndex = false, CandlestickInterval? warmupTimeframe = null)
        {
            StartDateTime = startDateTime;
            DaysPerSplit = daysPerSplit;
            WarmupCandlesCount = warmupCandlesCount;
            Trade = trade;
            Strategy = strategy;
            CorrectEndIndex = correctEndIndex;
            WarmupTimeframe = warmupTimeframe;
        }

        // --- Methods
        /// <summary>
        /// Performing the actual backtesting process
        /// </summary>
        /// <returns></returns>
        public async Task PerformAsync(IEnumerable<ISymbolData> symbolsData, CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            // --- Triggering On Started Backtesting Status
            NotifyBacktestingEvent(BacktestingEventStatus.Started);

            // --- Create and Select DataSplitter version
            Splitter = new SymbolDataSplitterV1(DaysPerSplit, WarmupCandlesCount, StartDateTime, CorrectEndIndex, WarmupTimeframe);

            // --- Create and Select Engine version
            Engine = new EngineV2(WarmupCandlesCount, Trade, Strategy);

            // --- Split Symbols Data
            NotifyBacktestingEvent(BacktestingEventStatus.SplitStarted);
            var symbolDataParts = await Splitter.SplitAsync(symbolsData);
            NotifyBacktestingEvent(BacktestingEventStatus.SplitFinished);

            // --- Run Engine
            NotifyBacktestingEvent(BacktestingEventStatus.EngineStarted);
            await Engine.RunAsync(symbolDataParts, cancellationToken);
            NotifyBacktestingEvent(BacktestingEventStatus.EngineFinished);

            IsRunning = false;
            // --- Triggering On Finished Backtesting Status
            NotifyBacktestingEvent(BacktestingEventStatus.Finished);
        }

        /// <summary>
        /// Notifies Subscribed Objects about backtesting status
        /// </summary>
        /// <param name="eventStatus"></param>
        /// <returns></returns>
        private void NotifyBacktestingEvent(BacktestingEventStatus eventStatus)
        {
            OnBacktestingEvent?.Invoke(eventStatus);
        }
    }
}
