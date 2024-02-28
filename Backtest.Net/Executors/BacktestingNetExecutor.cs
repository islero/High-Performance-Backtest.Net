using System.Reflection;
using Backtest.Net.Engines;
using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using Backtest.Net.SymbolDataSplitters;

namespace Backtest.Net.Executors
{
    /// <summary>
    /// High-Performance Backtesting Executor
    /// </summary>
    public sealed class BacktestingNetExecutor
    {
        // --- Properties
        public static bool IsRunning { get; private set; } // Checks whether or not backtesting is currently running
        private DateTime StartDateTime { get; } // Backtesting Start DateTime
        private int WarmupCandlesCount { get; } // The amount of warmup candles count
        private bool CorrectEndIndex { get; } // Makes sure the end index are the same for all symbols and timeframes
        private CandlestickInterval? WarmupTimeframe { get; } // The timeframe must be warmed up and all lower timeframes accordingly, if null - will be set automatically
        private int DaysPerSplit { get; } // How many days in one split range should exist
        private ISymbolDataSplitter? Splitter { get; set;  } // Splits entire history on smaller pieces
        private IEngine? Engine { get; set;  } // The backtesting engine itself, performs backtesting, passes prepared history into strategy

        // --- Delegates
        public Action<BacktestingEventStatus, string?>? OnBacktestingEvent; // Notifies subscribed objects about backtesting events
        public required Func<IEnumerable<ISymbolData>, Task> OnTick { get; set; }

        // --- Constructors
        public BacktestingNetExecutor(DateTime startDateTime, int daysPerSplit, int warmupCandlesCount, 
            bool correctEndIndex = false, CandlestickInterval? warmupTimeframe = null)
        {
            StartDateTime = startDateTime;
            DaysPerSplit = daysPerSplit;
            WarmupCandlesCount = warmupCandlesCount;
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
            NotifyBacktestingEvent(BacktestingEventStatus.Started,
                Assembly.GetExecutingAssembly().GetName().Version?.ToString());

            // --- Create and Select DataSplitter version
            Splitter = new SymbolDataSplitterV1(DaysPerSplit, WarmupCandlesCount, StartDateTime, CorrectEndIndex,
                WarmupTimeframe);

            // --- Create and Select Engine version
            Engine = new EngineV4(WarmupCandlesCount)
            {
                OnTick = OnTick
            };

            // --- Split Symbols Data
            NotifyBacktestingEvent(BacktestingEventStatus.SplitStarted, Splitter.GetType().Name);
            var symbolDataParts = await Splitter.SplitAsync(symbolsData);
            NotifyBacktestingEvent(BacktestingEventStatus.SplitFinished, Splitter.GetType().Name);

            // --- Run Engine
            NotifyBacktestingEvent(BacktestingEventStatus.EngineStarted, Engine.GetType().Name);
            await Engine.RunAsync(symbolDataParts, cancellationToken);
            NotifyBacktestingEvent(BacktestingEventStatus.EngineFinished, Engine.GetType().Name);

            IsRunning = false;
            // --- Triggering On Finished Backtesting Status
            NotifyBacktestingEvent(BacktestingEventStatus.Finished,
                Assembly.GetExecutingAssembly().GetName().Version?.ToString());
        }
        
        /*
        /// <summary>
        /// On Tick Action that passes data into strategy
        /// </summary>
        /// <param name="symbolData"></param>
        /// <returns></returns>
        private async Task OnTick(IEnumerable<ISymbolData> symbolData)
        {
            var signals = await Strategy.Execute(symbolData);
            foreach (var signal in signals)
            {
                _ = await Trade.ExecuteSignal(signal);
            }
        }*/

        /// <summary>
        /// Notifies Subscribed Objects about backtesting status
        /// </summary>
        /// <param name="eventStatus"></param>
        /// <param name="additionalDetails"></param>
        /// <returns></returns>
        private void NotifyBacktestingEvent(BacktestingEventStatus eventStatus, string? additionalDetails = default)
        {
            OnBacktestingEvent?.Invoke(eventStatus, additionalDetails);
        }
    }
}
