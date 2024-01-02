using Backtest.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backtest.Net.Engines
{
    /// <summary>
    /// Engine V1
    /// Prepares parts before feeding them into strategy
    /// </summary>
    public class EngineV1 : IEngine
    {


        // --- Methods
        public Task<IEnumerable<ISymbolData>> RunAsync(IEnumerable<IEnumerable<ISymbolData>> symbolDataParts)
        {
            throw new NotImplementedException();
        }
    }
}
