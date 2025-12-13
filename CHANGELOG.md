# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Repository restructure with standard .NET layout (src/, tests/, benchmarks/)
- GitHub Actions CI/CD workflows
- Comprehensive documentation (README, CONTRIBUTING, etc.)
- EditorConfig and code style configuration
- Directory.Build.props for centralized build configuration

### Changed

### Deprecated

### Removed

### Fixed

### Security

---

## [4.1.11] - 2024-XX-XX

### Added
- Volume property to CandlestickV2

### Changed
- Upgraded engine to V10 with performance optimizations

---

## [4.1.0] - 2024-XX-XX

### Added
- EngineV10 with zero-allocation patterns
- Span-based iteration for hot paths
- Binary search for candlestick lookups

### Changed
- Optimized OHLC handling to eliminate Clone() operations
- Improved parallel processing in index incrementing

### Performance
- Reduced memory allocations in hot paths
- Single-pass High/Low computation without intermediate lists

---

<!-- Version comparison links -->
[Unreleased]: https://github.com/islero/High-Performance-Backtest.Net/compare/v4.1.11...HEAD
[4.1.11]: https://github.com/islero/High-Performance-Backtest.Net/compare/v4.1.0...v4.1.11
[4.1.0]: https://github.com/islero/High-Performance-Backtest.Net/releases/tag/v4.1.0
