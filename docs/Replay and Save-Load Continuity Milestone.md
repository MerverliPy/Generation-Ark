# Step 8 — Replay Runner and Save/Load Continuity

Step 8 adds an engine-neutral validation harness around the existing authoritative `RunOneTick` boundary.

## Included

- `HeadlessSimulationRunner` with exact one-tick progress validation
- accepted-command replay records and canonical command ordering
- canonical replay JSON with periodic checksums
- versioned save envelope and canonical JSON
- pending-command preservation across a save boundary
- replay checkpoint mismatch reporting
- frame-pattern checksum equivalence through `UnitySimulationAdapter`
- deterministic two-run soak validation
- ten-year foundation constant: `5,184,000` ticks
- bounded periodic checkpoint retention

## Save boundary

A scenario adapter must place all authoritative state in the save payload, including:

- clock state
- pending commands
- scheduler event IDs, creation sequence, and events
- random seed, algorithm version, and future-result state
- world state and next entity ID

Presentation accumulator and frame sequence are excluded. Loading resumes at an exact authoritative tick boundary.

## Replay boundary

`ReplayCommand.AcceptedTick` determines when an accepted command is re-submitted. The simulation remains responsible for applying queued commands by:

`TargetTick → Sequence → CommandId`

The replay runner never mutates authoritative state directly.

## Soak boundary

The executable milestone test runs a complete ten-year foundation soak twice and compares 120 monthly-equivalent checkpoints plus the final checksum. Process-level performance and memory telemetry remain diagnostic; they must not affect simulation decisions or checksums.

## Out of scope

- map or room topology
- persistent gameplay entities beyond the foundation validation scenario
- jobs, toils, or reservations
- Unity scenes, prefabs, or UI
- cloud saves or compression
- save migration beyond schema/version rejection
