# Generation Ark — Deterministic Clock Milestone 1

This solution implements the first executable slice of the simulation-clock specification:

- Integer authoritative ticks
- Pause, speed control, and single stepping
- Frame-rate-independent tick accumulation
- No dropped ticks when a frame budget is exceeded
- Stable system execution by phase, order, and system ID
- Tick-targeted deterministic command ordering
- Canonical per-tick state checksums
- Engine-independent deterministic test harness

## Requirements

- .NET 8 SDK or newer

## Run

```bash
dotnet build GenerationArk.sln
dotnet run --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj -c Release
```

The test project intentionally uses a small built-in test runner and no NuGet test framework. This keeps the milestone dependency-free and runnable in restricted or offline environments.

## Current acceptance coverage

- Pausing prevents advancement.
- A requested single step advances exactly one tick.
- Different render-frame patterns produce identical state.
- Speed changes do not change tick outcomes.
- System registration order does not change execution order.
- Duplicate system IDs fail startup validation.
- Commands execute by target tick, sequence, then command ID.
- Tick-budget limits preserve backlog rather than dropping ticks.

## Deliberately deferred

The next milestone adds:

1. Serializable deterministic scheduler
2. Counter-based domain-isolated random service
3. Save/load continuity
4. Replay command serialization
5. Component-level desynchronization reports
6. Ten-year headless soak runner

The map, entity model, and job framework should remain blocked until those clock acceptance gates pass.
