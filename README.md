# High-Performance Backtest.Net

[![NuGet](https://img.shields.io/nuget/v/Backtest.Net.svg)](https://www.nuget.org/packages/Backtest.Net/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Backtest.Net.svg)](https://www.nuget.org/packages/Backtest.Net/)
[![Build Status](https://github.com/islero/High-Performance-Backtest.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/islero/High-Performance-Backtest.Net/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)

A high-performance backtesting engine for algorithmic trading strategies in .NET.

---

## Introduction

**High-Performance Backtest.Net** is a specialized backtesting library designed for quantitative trading applications. It processes multi-timeframe candlestick data across multiple symbols, simulating tick-by-tick strategy execution with proper warmup periods and OHLC handling.

### Key Characteristics

- **Multi-Symbol Support**: Process multiple trading symbols in parallel
- **Multi-Timeframe**: Handle multiple timeframes per symbol with automatic synchronization
- **Performance Optimized**: 10 iteratively optimized engine versions with zero-allocation patterns
- **Data Splitting**: Intelligent data partitioning for memory-efficient large-scale backtests
- **Cancellation Support**: Graceful async cancellation with progress tracking

---

## Features

| Feature | Description |
|---------|-------------|
| **EngineV10** | Latest engine with Span-based iteration, binary search, and parallel processing |
| **SymbolDataSplitter** | Partitions large datasets into memory-efficient chunks |
| **Warmup Handling** | Configurable warmup candle counts per timeframe |
| **OHLC Simulation** | Accurate current-candle OHLC handling during backtest |
| **Progress Tracking** | Real-time progress from 0-100% |
| **SIMD Acceleration** | Leverages SimdLinq for vectorized operations |

### Performance Optimizations

The library implements sophisticated performance techniques:

- **Zero-Allocation Hot Paths**: Uses `Span<T>` and `CollectionsMarshal.AsSpan`
- **Parallel Processing**: `Parallel.ForEach` for symbol-level parallelism
- **Binary Search**: O(log n) candlestick lookups
- **Aggressive Inlining**: `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- **Sealed Classes**: JIT optimization hints

---

## Installation

### Package Manager

```bash
dotnet add package Backtest.Net
```

### PackageReference

```xml
<PackageReference Include="Backtest.Net" Version="4.1.14" />
```

> [!NOTE]
> Requires .NET 9.0 or later.

---

## Quickstart

### Basic Engine Usage

```csharp
using Backtest.Net.Engines;
using Backtest.Net.SymbolsData;
using Backtest.Net.SymbolDataSplitters;
using Backtest.Net.Timeframes;
using Backtest.Net.Enums;

// 1. Prepare your symbol data (candlesticks per timeframe)
// Candlestick properties: OpenTime, Open, High, Low, Close, CloseTime, Volume
var symbolsData = new List<SymbolDataV2>
{
    new SymbolDataV2
    {
        Symbol = "BTCUSDT",
        Timeframes = new List<TimeframeV2>
        {
            new TimeframeV2
            {
                Timeframe = CandlestickInterval.OneMinute,
                Candlesticks = yourOneMinuteCandles
            },
            new TimeframeV2
            {
                Timeframe = CandlestickInterval.OneHour,
                Candlesticks = yourOneHourCandles
            }
        }
    }
};

// 2. Split data for efficient processing
var splitter = new SymbolDataSplitterV2(
    daysPerSplit: 30,
    warmupCandlesCount: 100,
    backtestingStartDateTime: new DateTime(2024, 1, 1)
);

var splitData = await splitter.SplitAsyncV2(symbolsData);

// 3. Create and configure the engine
var engine = new EngineV10(
    warmupCandlesCount: 100,
    sortCandlesInDescOrder: false,
    useFullCandleForCurrent: false
);

// 4. Set up your strategy callback
engine.OnTick = async (symbolData) =>
{
    foreach (var symbol in symbolData)
    {
        var latestCandle = symbol.Timeframes[0].Candlesticks[^1];
        // Your strategy logic here
        Console.WriteLine($"{symbol.Symbol}: Close = {latestCandle.Close}");
    }
};

// 5. Run the backtest
await engine.RunAsync(splitData);

Console.WriteLine($"Progress: {engine.GetProgress()}%");
```

### With Cancellation Support

```csharp
using var cts = new CancellationTokenSource();

engine.OnCancellationFinishedDelegate = () =>
{
    Console.WriteLine("Backtest cancelled gracefully");
};

// Cancel after 30 seconds
cts.CancelAfter(TimeSpan.FromSeconds(30));

await engine.RunAsync(splitData, cts.Token);
```

---

## Engine Versions

| Engine | Description | Use Case |
|--------|-------------|----------|
| `EngineV8` | SymbolDataV2 support | Standard workloads |
| `EngineV9` | Optimized OHLC handling | Memory-sensitive scenarios |
| `EngineV10` | Full optimization suite | **Production recommended** |

> [!TIP]
> Use `EngineV10` for new projects. It provides the best performance through zero-allocation patterns and parallel processing.

---

## Development

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Git

### Build

```bash
git clone https://github.com/islero/High-Performance-Backtest.Net.git
cd High-Performance-Backtest.Net
dotnet build
```

### Test

```bash
dotnet test
```

### Benchmark

```bash
cd benchmarks/Backtest.Net.Benchmarks
dotnet run -c Release
```

### Format

```bash
dotnet format
```

---

## Versioning & Releases

This project follows [Semantic Versioning](https://semver.org/).

| Branch | Version Format | NuGet Feed |
|--------|----------------|------------|
| `master` | `X.Y.Z` (stable) | nuget.org |
| `beta` | `X.Y.Z-beta.N` (prerelease) | nuget.org |

### Release Process

1. Update version in `src/Backtest.Net/Backtest.Net.csproj`
2. Create GitHub Release with tag `vX.Y.Z`
3. CI automatically publishes to NuGet

---

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## License

This project is licensed under the [GNU Lesser General Public License v3.0](LICENSE).

---

## Acknowledgments

- [SimdLinq](https://github.com/Cysharp/SimdLinq) - SIMD-accelerated LINQ operations
- [BenchmarkDotNet](https://benchmarkdotnet.org/) - Performance benchmarking

---

<p align="center">
  Built for the algorithmic trading community
</p>
