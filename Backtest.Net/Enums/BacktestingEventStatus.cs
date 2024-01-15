using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backtest.Net.Enums
{
    public enum BacktestingEventStatus
    {
        Started,
        Finished,
        SplitStarted,
        SplitFinished,
        EngineStarted,
        EngineFinished,
        Error
    }
}
