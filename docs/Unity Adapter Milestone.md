# Deterministic Unity Adapter Milestone

## Purpose

This milestone adds the presentation-frame boundary for the deterministic Generation Ark simulation. Unity frame duration may request authoritative ticks, but it never enters `SimContext` and never changes tick contents.

## Implemented scope

- Engine-neutral `UnitySimulationAdapter`
- 30 ticks/second frame accumulation by default
- Supported speed multipliers: `0`, `1`, `4`, `16`, `64`, and `256`
- Configurable maximum ticks per presentation frame
- Fractional-tick retention
- Full backlog retention when the frame budget is exhausted
- Pause/resume without discarding backlog
- Manual stepping of exactly one tick while paused
- Presentation-only throttling at 16×, 64×, and 256×
- Catch-up-aware presentation throttling
- Unity `MonoBehaviour` driver behind `UNITY_5_3_OR_NEWER`
- Unity presentation bridge receiving refresh requests only
- Eight dependency-free executable harness tests
- Dependency-free Python behavioral reference

## Determinism boundary

The following values are non-authoritative and must not be saved or hashed:

- frame sequence
- frame accumulator
- fractional tick remainder
- frame budget telemetry
- presentation refresh decisions
- interpolation state

Only calls to the existing simulation runner's `RunOneTick` method cross into authoritative execution.

## Backlog policy

When a frame requests more ticks than the configured budget:

1. Execute at most the configured tick budget.
2. Retain every unexecuted whole tick in the accumulator.
3. Report the retained backlog through `FrameAdvanceResult`.
4. Continue draining backlog on later frames, including frames with zero elapsed time.
5. Never skip a tick to catch up.

Pausing prevents execution and prevents new wall-clock time from accumulating. Existing backlog remains intact. This is stricter than the illustrative sample in the foundation specification and preserves the no-dropped-ticks invariant.

## Unity integration

The Unity composition root initializes the driver with the engine-independent runner:

```csharp
unitySimulationDriver.Initialize(simulationRunner.RunOneTick);
```

Unity controls call:

```csharp
unitySimulationDriver.SetSpeedMultiplier(64);
unitySimulationDriver.Pause();
unitySimulationDriver.StepOneTick();
unitySimulationDriver.Resume();
```

`Time.unscaledDeltaTime` is consumed only inside `UnitySimulationDriver.Update`. It is converted into a count of `RunOneTick` calls and is never exposed to authoritative systems.

## Acceptance gate

The dependency gate is the already validated diagnostics state:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
34/34 tests passed.
```

After installation, the expected complete harness result is:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
42/42 tests passed.
```

The milestone remains Ready for validation until that result is obtained on the owner environment with .NET SDK `8.0.129`.

## Explicitly out of scope

- Unity project scaffolding, scenes, prefabs, UI Toolkit, or input bindings
- Authoritative Unity physics
- Map, entity, job, toil, or reservation systems
- Replay runner
- General save/load implementation
- Ten-year soak testing
- Presentation snapshot schema for gameplay systems that do not yet exist

Those remain later milestones. Step 8 must add the headless/replay runner, full frame-pattern checksum comparisons, save/load continuity, and soak tests.
