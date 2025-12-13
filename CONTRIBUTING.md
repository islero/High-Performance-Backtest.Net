# Contributing to High-Performance Backtest.Net

Thank you for your interest in contributing! This document provides guidelines for contributing to the project.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Code Style](#code-style)
- [Commit Conventions](#commit-conventions)
- [Pull Request Process](#pull-request-process)
- [Issue Guidelines](#issue-guidelines)

---

## Code of Conduct

This project adheres to a Code of Conduct. By participating, you are expected to uphold this code. See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

---

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Git
- A code editor (VS Code, Visual Studio, JetBrains Rider)

### Fork and Clone

```bash
# Fork via GitHub UI, then:
git clone https://github.com/YOUR_USERNAME/High-Performance-Backtest.Net.git
cd High-Performance-Backtest.Net
git remote add upstream https://github.com/islero/High-Performance-Backtest.Net.git
```

### Build and Test

```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run tests
dotnet test

# Run benchmarks (optional)
cd benchmarks/Backtest.Net.Benchmarks
dotnet run -c Release
```

---

## Development Workflow

### Branching Model

| Branch | Purpose |
|--------|---------|
| `master` | Stable releases only |
| `dev` | Integration branch for features |
| `feature/*` | New features |
| `fix/*` | Bug fixes |
| `perf/*` | Performance improvements |

### Workflow

1. **Sync** your fork with upstream `dev`
2. **Create** a feature branch from `dev`
3. **Implement** your changes with tests
4. **Test** locally: `dotnet test`
5. **Push** your branch and open a PR against `dev`

```bash
# Sync with upstream
git fetch upstream
git checkout dev
git merge upstream/dev

# Create feature branch
git checkout -b feature/my-feature

# Make changes, then push
git push origin feature/my-feature
```

---

## Code Style

### General Guidelines

- **Follow existing patterns** in the codebase
- **Use meaningful names** for variables, methods, and classes
- **Keep methods focused** - single responsibility
- **Add XML documentation** for public APIs

### C# Conventions

```csharp
// DO: Use explicit types for clarity in complex scenarios
List<SymbolDataV2> symbolData = GetSymbolData();

// DO: Use var when type is obvious
var engine = new EngineV10(warmupCandlesCount: 100, sortCandlesInDescOrder: false, useFullCandleForCurrent: false);

// DO: Use expression-bodied members for simple properties/methods
public decimal GetProgress() => (decimal)Index / MaxIndex * 100;

// DO: Use primary constructors for simple classes (C# 12+)
public sealed class EngineV10(int warmupCandlesCount, bool sortDesc, bool useFullCandle)
    : EngineV8(warmupCandlesCount, useFullCandle)

// DON'T: Leave commented-out code
// DON'T: Use magic numbers without explanation
// DON'T: Suppress warnings without justification
```

### Performance-Critical Code

When contributing to hot paths:

```csharp
// DO: Use Span<T> for zero-allocation iteration
var span = CollectionsMarshal.AsSpan(list);

// DO: Mark hot methods for inlining
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static void ProcessCandle(...)

// DO: Prefer for loops over LINQ in hot paths
for (var i = 0; i < span.Length; i++) { ... }

// DO: Use sealed classes where inheritance isn't needed
public sealed class CandlestickV2 { ... }
```

### Formatting

- Use 4 spaces for indentation (no tabs)
- Use `dotnet format` before committing
- Keep lines under 120 characters when reasonable

---

## Commit Conventions

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Types

| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `perf` | Performance improvement |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `test` | Adding or updating tests |
| `docs` | Documentation only |
| `chore` | Build process, dependencies, tooling |

### Examples

```bash
feat(engine): add EngineV11 with improved memory pooling

fix(splitter): correct index calculation for edge case

perf(engine): reduce allocations in OHLC handling

test(engine): add coverage for cancellation scenarios

docs: update quickstart example in README
```

---

## Pull Request Process

### Before Submitting

- [ ] Tests pass locally: `dotnet test`
- [ ] Code formatted: `dotnet format`
- [ ] No new warnings introduced
- [ ] Changes are documented (if applicable)
- [ ] Commit messages follow conventions

### PR Template

Your PR should include:

1. **Summary**: What does this PR do?
2. **Motivation**: Why is this change needed?
3. **Testing**: How was this tested?
4. **Breaking Changes**: Any breaking changes? (if applicable)

### Review Process

1. Maintainers will review within 3-5 business days
2. Address feedback in new commits (don't force-push during review)
3. Once approved, maintainer will squash-merge

---

## Issue Guidelines

### Bug Reports

Include:
- .NET SDK version (`dotnet --version`)
- Operating system
- Minimal reproduction steps
- Expected vs actual behavior
- Stack trace (if applicable)

### Feature Requests

Include:
- Use case description
- Proposed API (if applicable)
- Alternatives considered

---

## Questions?

- Open a [Discussion](https://github.com/islero/High-Performance-Backtest.Net/discussions)
- Check existing [Issues](https://github.com/islero/High-Performance-Backtest.Net/issues)

---

Thank you for contributing!
