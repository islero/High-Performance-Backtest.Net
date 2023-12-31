using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private DateTime EndDateTime { get; } // Backtesting End DateTime
        private int WarmupCandlesCount { get; } // The amount of warmup candles count
        private CandlestickInterval? WarmupTimeframe { get; } // The timeframe must be warmed up and all lower timeframes accordingly, if null - will be set automatically

        // --- Delegates
        public Action OnBacktestingStarted; // Notifies subscribed objects about start of the backtesting
        public Action OnBacktestingFinished; // Notifies subscribed objects about the end of the backtesting

        // --- Constructors
        public BacktestingExecutor(DateTime startDateTime, DateTime endDateTime, int warmupCandlesCount, CandlestickInterval? warmupTimeframe = null)
        {
            StartDateTime = startDateTime;
            EndDateTime = endDateTime;
            WarmupCandlesCount = warmupCandlesCount;
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
            // --- Triggering On Backtesting Started action delegate
            OnBacktestingStarted();



            IsRunning = false;
            // --- Triggering On Backtesting Finished action delegate
            OnBacktestingFinished();
        }
    }
}
