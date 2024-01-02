using Backtest.Net.Enums;
using Backtest.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backtest.Net.SymbolDataSplitters
{
    public abstract class SymbolDataSplitterBase : ISymbolDataSplitter
    {
        // --- Properties
        public CandlestickInterval? WarmupTimeframe { get; protected set; } // The timeframe must be warmed up and all lower timeframes accordingly, if null - will be set automatically
        protected int DaysPerSplit { get; } // How many days in one split range should exist
        protected int WarmupCandlesCount { get; } // The amount of warmup candles count
        protected DateTime BacktestingStartDateTime { get; } // Backtesting Start DateTime
        
        // --- Constructors
        public SymbolDataSplitterBase(int daysPerSplit, int warmupCandlesCount, DateTime backtestingStartDateTime, CandlestickInterval? warmupTimeframe = null)
        {
            DaysPerSplit = daysPerSplit;
            WarmupCandlesCount = warmupCandlesCount;
            BacktestingStartDateTime = backtestingStartDateTime;
            WarmupTimeframe = warmupTimeframe;
        }

        // --- Methods
        public abstract Task<IEnumerable<IEnumerable<ISymbolData>>> SplitAsync(IEnumerable<ISymbolData> symbolsData);

        /// <summary>
        /// Returns the candlestick index of the targetDateTime by candle OpenTime, or -1 if the index wasn't found
        /// </summary>
        /// <param name="timeframe"></param>
        /// <returns></returns>
        protected int GetCandlesticksIndexByOpenTime(IEnumerable<ICandlestick> candlesticks, DateTime targetDateTime)
        {
            var candlesticksList = candlesticks.ToList();
            return candlesticksList.FindIndex(candle => candle.OpenTime >= targetDateTime);
        }

        /// <summary>
        /// Returns the candlestick index of the targetDateTime by candle CloseTime, or -1 if the index wasn't found
        /// </summary>
        /// <param name="timeframe"></param>
        /// <returns></returns>
        protected int GetCandlesticksIndexByCloseTime(IEnumerable<ICandlestick> candlesticks, DateTime targetDateTime)
        {
            var candlesticksList = candlesticks.ToList();
            return candlesticksList.FindIndex(candle => candle.CloseTime >= targetDateTime);
        }

        /// <summary>
        /// Returns warmup candlestick index, if historical data is larger or smaller than WarmupCandlesCount
        /// </summary>
        /// <param name="backtestingStartDateIndex"></param>
        /// <returns></returns>
        protected int GetWarmupCandlestickIndex(int backtestingStartDateIndex)
        {
            // --- The historical data is bigger than WarmupCandlesCount case
            if (backtestingStartDateIndex > WarmupCandlesCount)
            {
                return backtestingStartDateIndex - WarmupCandlesCount;
            }

            // --- Returning the first index of the candlesticks history
            return 0;
        }

        /// <summary>
        /// Gets WarmupTimeframe, if WarmupTimeframe is null then calculates the WarmupTimeframe automatically based on constant that 
        /// defines how much of the history can be maximum allocated for warmup period
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        protected CandlestickInterval GetWarmupTimeframe(IEnumerable<ISymbolData> symbolsData)
        {
            // --- Return WarmupTimeframe value if it's not null
            if (WarmupTimeframe.HasValue)
                return WarmupTimeframe.Value;

            // --- Setting the lowest symbolsData timeframe
            CandlestickInterval potentialWarmupTimeframe = symbolsData.Min(x => x.Timeframes.Min(y => y.Timeframe));

            foreach (ISymbolData symbol in symbolsData)
            {
                foreach (var timeframe in symbol.Timeframes)
                {
                    // --- No need to perform calculations for same or lower timeframes that has been already set
                    if (timeframe.Timeframe <= potentialWarmupTimeframe)
                        continue;

                    if (timeframe.Candlesticks.Count() > WarmupCandlesCount)
                    {
                        DateTime warmupCandlesCountDate = timeframe.Candlesticks.ElementAt(WarmupCandlesCount).OpenTime;

                        // --- Checking if warming up by using this timeframe will not exceed backtesting start date time
                        if (warmupCandlesCountDate < BacktestingStartDateTime && timeframe.Timeframe > potentialWarmupTimeframe)
                        {
                            potentialWarmupTimeframe = timeframe.Timeframe;
                        }
                    }
                }
            }

            // --- It basically returns the highest timeframe that after warming up not exceed backtesting starting date
            return potentialWarmupTimeframe;
        }

        /// <summary>
        /// Checks if all the symbols reached history end
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        protected bool AreAllSymbolDataReachedHistoryEnd(IEnumerable<ISymbolData> symbolsData)
        {
            return symbolsData.All(x => x.Timeframes.Any(tf => tf.NoMoreHistory));
        }

        /// <summary>
        /// Quick not a very accurate way to perform a validation, maybe it will not be necessary in future, and I'll remove it
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        protected bool QuickSymbolDataValidation(IEnumerable<ISymbolData> symbolsData)
        {
            foreach (var symbol in symbolsData)
            {
                var priorTimeframe = symbol.Timeframes.First();
                foreach (var timeframe in symbol.Timeframes.Skip(1))
                {
                    // --- Validating that timeframes are sorted by ascending
                    if (timeframe.Timeframe < priorTimeframe.Timeframe)
                    {
                        // --- Timeframes aren't sorted
                        return false;
                    }

                    // --- Validating that candles are sorted by ascending
                    if (timeframe.Candlesticks.Skip(1).First().OpenTime < timeframe.Candlesticks.First().OpenTime)
                    {
                        // --- Candles aren't sorted (very rough check, but it's quicker)
                        return false;
                    }

                    priorTimeframe = timeframe;
                }
            }

            // --- Validation is passed
            return true;
        }

        /// <summary>
        /// Checks for symbol or timeframe duplicates
        /// </summary>
        /// <param name="symbolsData"></param>
        /// <returns></returns>
        protected bool IsThereSymbolTimeframeDuplicates(IEnumerable<ISymbolData> symbolsData)
        {
            bool symbolDuplicatesExist = symbolsData.GroupBy(x => x.Symbol).Any(symbol => symbol.Count() > 1);
            var timeframeDuplicatesExist = false;
            foreach (var symbol in symbolsData)
            {
                if (!timeframeDuplicatesExist)
                {
                    timeframeDuplicatesExist = symbol.Timeframes.GroupBy(timeframe => timeframe.Timeframe).Any(interval => interval.Count() > 1);
                    break;
                }
            }

            return symbolDuplicatesExist || timeframeDuplicatesExist;
        }

        /// <summary>
        /// Adding days per split to ongoing backtesting time
        /// </summary>
        /// <param name="ongoingBacktestingTime"></param>
        /// <param name="isLastSymbol"></param>
        /// <returns></returns>
        protected DateTime AddDaysToOngoingBacktestingTime(DateTime ongoingBacktestingTime, bool isLastSymbol)
        {
            if (isLastSymbol)
                return ongoingBacktestingTime.AddDays(DaysPerSplit);

            return ongoingBacktestingTime;
        }
    }
}
