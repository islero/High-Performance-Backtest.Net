# Roadmap

This document outlines the planned development direction for High-Performance Backtest.Net.

> [!NOTE]
> This roadmap is subject to change based on community feedback and priorities.

---

## Current Focus

### v4.2.0 - Stability & Public Release

- [ ] Fix remaining test failures
- [ ] Address build warnings
- [ ] Resolve models.net dependency for public consumers
- [ ] Add LICENSE file
- [ ] Publish to nuget.org

---

## Near-Term Goals

### v5.0.0 - API Refinements

- [ ] Consolidate engine versions (deprecate V1-V7)
- [ ] Improve generic type support
- [ ] Add result aggregation helpers
- [ ] Enhanced progress reporting with ETA

### Developer Experience

- [ ] Comprehensive API documentation
- [ ] Additional code samples
- [ ] Performance tuning guide

---

## Long-Term Vision

### Future Considerations

- **Memory Pooling**: ArrayPool/MemoryPool for large-scale backtests
- **Streaming Support**: Process data as it arrives (live-like mode)
- **Result Persistence**: Built-in trade logging and analysis
- **Python Bindings**: PyPI package via Python.NET
- **Cloud-Native**: Azure Functions / AWS Lambda samples

---

## Completed

### v4.1.x

- [x] EngineV10 with optimized performance
- [x] SymbolDataSplitterV2
- [x] Parallel processing support
- [x] Cancellation token support
- [x] SIMD acceleration via SimdLinq
- [x] Repository restructure

---

## Contributing to the Roadmap

Have ideas? We welcome input!

- Open a [Discussion](https://github.com/islero/High-Performance-Backtest.Net/discussions) for feature ideas
- Vote on existing feature requests with thumbs up
- Submit PRs for items you'd like to implement

---

*Last updated: 2024*
