# Generation Ark — Deterministic Scheduler Milestone

## Scope

This milestone extends the validated deterministic clock with an authoritative future-event scheduler.

## Ordering contract

Pending events are ordered by:

1. Due tick, ascending
2. Simulation phase, ascending according to `SimPhaseOrder`
3. Priority, ascending (lower numbers execute first)
4. Creation sequence, ascending
5. Event ID, ascending

The heap never relies on collection insertion order or runtime hash order.

## Phase behavior

- Events execute before systems in their assigned phase.
- An event scheduled for a later phase of the current tick may execute in the current tick.
- An event scheduled for the current or an earlier phase is moved to the same phase on the next tick.
- Events scheduled before the current tick are rejected.
- A tick cannot finish while an event remains due for that tick.

## Repeating events

Repeating events retain their event ID and creation sequence. Their next due tick is calculated from the previous due tick:

`next = previous due tick + interval`

This prevents drift when execution is delayed or presentation frames fluctuate.

## Persistence

`SchedulerSnapshot` stores:

- Current scheduler tick
- Next event ID
- Next creation sequence
- Every active event and its ordering fields
- Event type, owner, payload, and optional repeat interval

`SchedulerSnapshotJson` provides a dependency-free JSON round trip. Snapshot event arrays are sorted canonically when captured and sorted again when restored.

## Cancellation

Events can be cancelled by event ID or stable owner ID. Cancellation is idempotent. Heap entries are invalidated through the authoritative ID index and removed lazily, with deterministic compaction when stale entries accumulate.

## Checksum coverage

Canonical state checksums now include scheduler counters and every pending event in canonical order. Two simulations with identical world counters but different future events therefore produce different authoritative checksums.

## Acceptance tests

The executable test runner covers:

- Events never executing early
- Priority and creation-sequence ordering
- Snapshot array order independence
- Cancellation by ID and owner
- Drift-free repeating events
- JSON snapshot restoration
- Same-phase deferral
- Later-phase current-tick execution
- Scheduler checksum participation
- Rejection of past-due and invalid repeating events
