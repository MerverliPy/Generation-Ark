# Generation Ark — Deterministic Simulation Foundation

This solution implements the deterministic simulation foundation through Step 10:

- Integer authoritative ticks
- Pause, speed control, and single stepping
- Frame-rate-independent tick accumulation
- No dropped ticks when a frame budget is exceeded
- Stable system execution by phase, order, and system ID
- Tick-targeted deterministic command ordering
- Serializable deterministic scheduled events
- Stable event ordering by tick, phase, priority, creation sequence, and ID
- Cancellation by event ID and owner
- Drift-free repeating events
- Same-tick phase scheduling rules
- JSON scheduler snapshots and restoration
- Scheduler state in canonical checksums
- Saved 64-bit root simulation seed
- Stable string random-domain IDs
- Counter-based random scopes keyed by domain, owner, purpose, occurrence, and local counter
- Project-owned versioned SplitMix64-based integer mixing
- Unbiased integer ranges through rejection sampling
- Integer-ratio probability checks
- JSON random metadata snapshots and restoration
- Diagnostic random-request tracing
- Root seed and algorithm version in canonical checksums
- Engine-independent deterministic test harness
- Monotonic deterministic entity lifecycle and structural component mutation
- Canonical entity persistence and lifecycle diagnostics
- Immutable one-deck rectangular grid dimensions
- Checked row-major cell identifiers and stable map-cell definitions
- Buffered atomic cell mutation through the existing Commit authority
- Canonical cardinal room topology with minimum-cell room IDs
- Canonical map persistence, checksum diagnostics, and topology reconstruction

## Requirements

- .NET 8 SDK or newer

## Run

```bash
dotnet build GenerationArk.sln && \
dotnet run \
  --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj \
  -c Release
```

The test project uses a small built-in runner and no NuGet test framework, keeping the milestone dependency-free and usable offline.

## Acceptance coverage

### Clock

- Pausing prevents advancement.
- A requested single step advances exactly one tick.
- Different render-frame patterns produce identical state.
- Speed changes do not change tick outcomes.
- System registration order does not change execution order.
- Duplicate system IDs fail startup validation.
- Commands execute by target tick, sequence, then command ID.
- Tick-budget limits preserve backlog rather than dropping ticks.

### Scheduler

- Events never execute early.
- Equal-tick events execute by priority and creation sequence.
- Snapshot array order cannot change execution order.
- Cancellation works by ID and owner.
- Repeating events do not drift.
- JSON snapshot restoration preserves pending order and future state.
- Same-phase scheduling defers to the next tick.
- Later-phase scheduling may execute in the current tick.
- Pending events participate in authoritative checksums.
- Past-due events and non-positive repeat intervals are rejected.

### Random service

- Identical scopes and counters produce fixed, repeatable output vectors.
- Domain and owner keys isolate output sequences.
- Unrelated draws cannot perturb protected domains.
- JSON save/load preserves root seed, algorithm version, and future outputs.
- Integer ranges remain within bounds and use rejection sampling.
- Probability edge cases and invalid inputs are explicit.
- Root seed and algorithm version participate in canonical checksums.
- Request tracing records scope keys without affecting outputs.

## Deterministic diagnostics milestone

- Checksum format version metadata
- Canonical global checksum compatibility
- Stable named component hashes
- Detailed component checkpoint history
- Duplicate component-ID validation
- Bounded tick traces
- Ordered system, command, scheduled-event, and random-request records
- Queue-size observations
- First-divergent-tick detection
- Changed and missing component reporting

## Deterministic grid and room topology milestone

- Positive immutable rectangular dimensions with checked cell-count arithmetic
- Canonical `CellId = Y * Width + X` mapping and inverse conversion
- Stable explicit map-cell definition IDs independent of registration order
- Shared structural mutation buffer and existing Commit phase
- Full-batch validation before entity or map structural changes are applied
- Cardinal-only connected components with canonical room and member ordering
- Room IDs equal to the minimum member cell ID
- Canonical map JSON snapshots with topology rebuilt after load
- `map-topology` component diagnostics and checksum format version 3
- Replay/frame-pattern topology churn coverage in the executable harness

## Next milestone

The next dependency-first candidate is Step 11: deterministic spatial entity indexes and position mutation. Pathfinding, reservations, jobs, atmosphere, construction, and Unity presentation remain excluded.

## Project Milestones

- Step 10 owner validation: `docs/milestones/step-10-owner-validation.md`
- Step 11 tracking: GitHub issue #1 -- Deterministic pathfinding and movement
