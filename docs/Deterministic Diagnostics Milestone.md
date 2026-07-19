# Deterministic Diagnostics Milestone

## Status

Prepared against the locally validated clock, scheduler, and deterministic random-service source state.

## Scope

This milestone adds:

- Checksum format version `1`
- Canonical global checksum preservation
- Stable ordinal component checksum IDs
- Component checksum contributors with duplicate-ID rejection
- Built-in `clock`, `random`, `scheduler`, and `world` component hashes
- Detailed component checkpoints alongside the existing global checksum history
- Bounded tick traces
- Ordered system, command, scheduled-event, and random-request records
- Queue-size observations
- Deterministic failure records without stack traces or timing data
- Earliest-divergence detection
- Stable changed and missing component reporting
- Optional caller-supplied build/save metadata

## Compatibility

The existing global checksum byte sequence remains unchanged. Component hashes are calculated in parallel and do not feed back into authoritative state. Trace data, profiling data, wall-clock values, and presentation data remain excluded from checksums.

The scheduler records an event immediately before its handler executes. The runner records a system immediately before its `Tick` call. A failed tick is stopped and receives a deterministic failure trace; it is not continued.

## Validation gate

The milestone is complete only after a Release build succeeds with zero warnings and errors and the executable harness reports:

```text
34/34 tests passed.
```

The scheduler, random, and diagnostics behavioral references must also pass.
