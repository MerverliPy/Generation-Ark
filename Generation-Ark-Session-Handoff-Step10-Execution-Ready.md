# Generation Ark — Session Handoff: Step 10 Execution Ready

**Updated:** 2026-07-19 UTC  
**Authoritative repository:** `/home/calvin/Generation-Ark/generation-ark-clock`  
**Required owner SDK:** `/usr/bin/dotnet` — .NET SDK `8.0.129`  
**Authoritative completed baseline:** Step 9 deterministic entity lifecycle  
**Planned milestone:** Step 10 deterministic one-deck grid and room topology  
**Implementation status:** Not started in this environment — authoritative repository source was not mounted or available through the connected GitHub installation

## Decision

Use the completed Step 9 handoff as the authoritative baseline for Step 10 execution.

Step 10 must implement the existing milestone contract exactly enough to clear the new deterministic acceptance gate while preserving all Step 1–9 behavior.

Do not create parallel clocks, runners, Commit phases, mutation buffers, persistence envelopes, world-state roots, checksum pipelines, replay pipelines, or diagnostic frameworks. Inspect and extend the existing repository abstractions and exact filenames first.

## Preserved authoritative evidence

### Step 9 handoff

```text
Generation-Ark-Session-Handoff-Step9-Entity-Lifecycle-Done.md
SHA-256: 23a033951a56879ee582c9103cfc432d3db71a7586ffa8e2496f3f53cca78fdd
```

### Step 9 owner validation transcript

```text
generation-ark-step9-entity-lifecycle-owner-validation-20260719T140525Z.txt
SHA-256: 6936d1455d56c9f682105a63673d04dd5c72997d79fd6c08e7ff2c100cbb54bc
```

The authoritative Step 9 owner result remains:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
...
62/62 tests passed.
PASS: Step 9 deterministic entity lifecycle compile and test gate cleared.
```

### Step 10 milestone contract

```text
Generation-Ark-Step10-Deterministic-Grid-Topology-Milestone-Contract.md
SHA-256: 5afb20f1c5c4763ef3fa5cca37c4761626e2cef3158581e366c8dd8f9c1fdef6
```

## Test-gate transition

Step 10 begins from the owner-validated complete harness:

```text
62/62 tests passed.
```

Step 10 adds ten milestone tests and is complete only at:

```text
72/72 tests passed.
```

Required new tests:

```text
PASS CellIdsUseCanonicalRowMajorCoordinates
PASS InvalidGridDimensionsAndCoordinatesFailFast
PASS CellIterationIsCanonicalRegardlessOfWriteOrder
PASS DuplicateMapCellDefinitionIdsFailFast
PASS MapMutationsRemainInvisibleUntilCommit
PASS ConflictingMapMutationBatchFailsBeforePartialApplication
PASS RoomTopologyUsesCardinalConnectivityAndStableRoomIds
PASS RoomTopologySplitAndMergeRebuildsDeterministically
PASS MapStateSaveLoadRoundTripIsCanonical
PASS MapReplayFramePatternsAndTopologyChurnMatchChecksums
```

No partial or approximate gate is acceptable. The final owner transcript must show zero build warnings, zero build errors, and exactly `72/72 tests passed.`

## Step 10 implementation boundary

Implement only the deterministic one-deck grid and room-topology foundation:

- immutable positive rectangular dimensions
- checked row-major `MapCellId` mapping
- stable explicit map-cell definition IDs
- authoritative engine-independent cell state
- canonical ascending cell iteration
- buffered map-cell changes applied through the existing Commit authority
- full-batch validation before any map state changes
- atomic rejection with no partial cell or topology mutation
- four-neighbor cardinal topology only
- stable room IDs equal to the minimum member cell ID
- canonical room and room-member iteration
- canonical map snapshots and restore
- deterministic topology reconstruction after load
- map and topology checksum participation
- map-specific desynchronization diagnostics
- replay, frame-pattern, save/load, split/merge, and churn equivalence
- checksum format transition from version `2` to `3`

## Explicit exclusions

Do not implement any of the following in Step 10:

- multiple decks, vertical links, or 3D coordinates
- entity position components or occupancy indexes
- colonists or resource-object entities
- movement or interpolation
- navigation cost, pathfinding, path requests, or path caches
- reservations
- jobs, toils, work selection, or hauling
- atmosphere, oxygen, pressure, temperature, or resource networks
- gameplay doors, walls, furniture, or construction entities
- fog of war, visibility, line of sight, or lighting
- procedural map generation or random map construction
- Unity tilemaps, GameObjects, rendering, input, or presentation
- incompatible save-format migration

Do not begin these systems in parallel with Step 10.

## Existing abstractions that must be inspected and extended

Before writing Step 10 code, inspect the current repository and resolve the exact existing extension points for:

```text
GenerationArk.Simulation/Core/SimContext.cs
GenerationArk.Simulation/Core/SimulationRunner.cs
GenerationArk.Simulation/Diagnostics/ChecksumComponentId.cs
GenerationArk.Simulation/Diagnostics/ChecksumFormatVersion.cs
GenerationArk.Simulation/Diagnostics/StateChecksum.cs
GenerationArk.Simulation/Persistence/
GenerationArk.Simulation/State/MutationBuffer.cs
GenerationArk.Simulation/State/WorldState.cs
GenerationArk.Simulation.Tests/Program.cs
GenerationArk.Simulation.Tests/SimulationFixture.cs
README.md
```

The contract's proposed map filenames are a boundary, not permission to duplicate an existing mechanism. Exact persistence envelope, snapshot composition, restore flow, checksum contribution, replay command, and mutation APIs must come from repository inspection.

## Execution blocker recorded in this session

The authoritative path below was not present in the active environment:

```text
/home/calvin/Generation-Ark/generation-ark-clock
```

A filesystem search found no `generation-ark-clock` directory. The connected GitHub installation also contained no Generation Ark repository. Therefore no Step 10 source edits, build, test run, archive, backup, rollback script, or owner validation could be truthfully performed here.

This is an environment/source-access blocker, not a design blocker.

## Non-destructive owner-machine preflight

A companion script is included:

```text
generation-ark-step10-owner-preflight-collect.sh
```

It performs a non-destructive owner-machine check and creates a source/preflight archive suitable for the implementation session. It does not clean, restore, stage, commit, or modify repository source.

Run it on the owner machine:

```bash
chmod +x generation-ark-step10-owner-preflight-collect.sh
./generation-ark-step10-owner-preflight-collect.sh
```

Expected output location:

```text
/home/calvin/Generation-Ark/generation-ark-step10-preflight-<UTCSTAMP>/
/home/calvin/Generation-Ark/generation-ark-step10-preflight-<UTCSTAMP>.tar.gz
/home/calvin/Generation-Ark/generation-ark-step10-preflight-<UTCSTAMP>.tar.gz.sha256
```

The archive should be uploaded into the next implementation environment if the repository itself cannot be mounted.

## Required next execution sequence

1. Obtain the exact Step 9 repository source and git state.
2. Verify the three preserved SHA-256 values above.
3. Inspect existing persistence, mutation, world-state, checksum, replay, and test-harness abstractions.
4. Perform the Step 9 cleanup and dependency gate from the Step 10 contract.
5. Require the exact `62/62 tests passed.` baseline before application.
6. Create pre-apply backup, new-file list, and rollback script.
7. Implement the Step 10 map/topology boundary by extending existing abstractions.
8. Run all existing behavioral references plus the map-topology behavioral reference.
9. Build Release with zero warnings and zero errors.
10. Run the complete harness and require exactly `72/72 tests passed.`
11. Record repository status, validation transcript, archive checksums, backup path, and rollback path.
12. Produce the completion handoff without authorizing excluded Step 11+ systems.

## Completion handoff must preserve

The Step 10 completion handoff must include:

- this Step 9 authoritative baseline and its hashes
- the Step 10 contract and its hash
- the `62/62 → 72/72` test-gate transition
- all explicit Step 10 exclusions
- confirmation that existing abstractions were extended rather than duplicated
- exact source and integration file boundary
- active SDK path and version
- behavioral-reference results
- build warning and error counts
- all 72 test names and final count
- checksum format version transition `2 → 3`
- pre-apply backup and rollback paths
- validation transcript path and SHA-256
- installer/archive hashes
- post-validation repository status
- unresolved facts, if any

## Status summary

- Step 9 deterministic entity lifecycle: **Authoritative and complete**
- Step 9 owner gate: **62/62 passed**
- Step 10 contract: **Preserved and integrity-verified**
- Step 10 target gate: **72/72 passed**
- Step 10 implementation: **Blocked in this environment by missing repository source**
- Parallel gameplay systems: **Not authorized**
- Next action: **Provide/mount the exact owner repository source, then execute the Step 10 contract**
