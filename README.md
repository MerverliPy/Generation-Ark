# Generation Ark

Generation Ark is a deterministic colony-ship simulation project.

## Current validated milestone

**Step 10 — Deterministic Grid and Room Topology: COMPLETE**

Owner validation completed on 2026-07-19 using .NET SDK `8.0.129`.

- Baseline commit before installation: `45f2074923f5c6ace23248f8307805666b60e35f`
- Preserved Step 9 dependency gate: `62/62`
- Step 10 final gate: `72/72`
- Release build: `0 warnings`, `0 errors`
- Behavioral references: PASS
- `ChecksumFormatVersion`: `3`

The authoritative owner transcript and milestone record are stored under `docs/milestones/`.

## Next milestone

**Step 11 — Deterministic Pathfinding and Movement**

The next implementation must build on the canonical one-deck grid and room topology. It must not introduce a parallel simulation runner, world root, commit phase, persistence envelope, or checksum pipeline.

Initial acceptance boundary:

- deterministic cardinal pathfinding through walkable cells and doors
- stable tie-breaking independent of insertion or registration order
- blocked-cell avoidance and deterministic re-pathing after obstruction
- movement represented through buffered authoritative mutations
- canonical save/load and checksum participation
- replay and frame-pattern checksum equivalence
- concurrent route stress validation without deadlock

Job selection, toil execution, reservations, needs, atmosphere, and Unity presentation remain outside this milestone unless explicitly added by a later validated contract.

## Repository synchronization status

This GitHub repository was empty when the Step 10 owner evidence was received. The validated owner source currently resides at `/home/calvin/Generation-Ark/generation-ark-clock` and must be pushed from that machine before GitHub can be considered a complete mirror of the executable source tree.
