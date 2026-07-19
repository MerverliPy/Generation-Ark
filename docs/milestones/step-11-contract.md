# Generation Ark — Step 11 Deterministic Spatial Entity Index and Position Mutation Contract

**Prepared:** 2026-07-19 UTC  
**Canonical repository:** `https://github.com/MerverliPy/Generation-Ark`  
**Baseline branch:** `main`  
**Baseline commit:** `f6be88e0c7c273899263661e31fb6c9639b006f8`  
**Required SDK:** `/usr/bin/dotnet` `8.0.129`  
**Dependency gate:** Step 10 owner-validated at `72/72 tests passed.`  
**Expected Step 11 gate:** `82/82 tests passed.`  
**Status:** Contract candidate frozen; implementation is not authorized by this document

## 1. Decision

Step 11 is **Deterministic Spatial Entity Index and Position Mutation**.

This is the narrow dependency-first bridge between the accepted entity lifecycle and map/topology foundations. It attaches active entities to canonical map cells and maintains deterministic entity-to-cell and cell-to-entity indexes.

The earlier GitHub issue #1 proposal for pathfinding and movement is not the Step 11 implementation contract. Pathfinding and authoritative movement remain future milestones.

## 2. Objective

For the same initial state, seed, accepted command stream, configuration, and tick sequence, Step 11 must produce identical:

- entity positions;
- per-cell entity membership;
- mutation acceptance or rejection;
- canonical save payloads;
- component and global checksums;
- replay checkpoints and final state.

All authoritative changes remain governed by simulation ticks and the existing Commit phase.

## 3. Required behavior

### 3.1 Position value

Provide a stable engine-neutral position value that contains exactly one canonical `MapCellId`.

Requirements:

- no floating-point coordinates;
- no Unity transform, scene object, or frame-derived state;
- equality and ordering derive only from the stable cell identifier;
- an entity has zero or one authoritative position;
- only live `Active` entities may receive a position;
- every referenced cell must exist in the immutable map dimensions.

### 3.2 Canonical spatial indexes

World state owns one authoritative spatial index with both lookup directions:

- `EntityId -> MapCellId`;
- `MapCellId -> ordered EntityId collection`.

Requirements:

- entities iterate in ascending `EntityId` order;
- cells iterate in ascending row-major `MapCellId` order;
- multiple entities may occupy one cell because occupancy limits and reservations are excluded;
- registration, request, dictionary, and hash iteration order cannot affect results;
- cached reverse membership must be validated against the authoritative forward mapping.

### 3.3 Buffered position mutations

The existing `MutationBuffer` and Commit authority must support:

- set an unpositioned active entity's position;
- move a positioned active entity to another valid cell;
- clear an entity's position.

Each request receives the shared monotonic `MutationSequence`. Entity, map, and position mutations form one atomic Commit batch:

1. establish explicit stable request order;
2. validate the complete combined batch;
3. reject the complete batch before any state change if one request is invalid;
4. apply valid requests through the existing Commit phase;
5. leave every mutation queue empty after successful Commit.

Conflicting same-Commit position operations for one entity are rejected. Position changes remain invisible before Commit.

### 3.4 Lifecycle and topology integration

- Destroying an entity removes its position and reverse-index membership during the same deterministic Commit.
- Pending-activation entities cannot be positioned.
- Activating an entity does not assign a position implicitly.
- Cell-definition and room-topology changes do not move entities.
- A position remains valid when its cell definition changes because walkability and occupancy rules are excluded.
- Invariant validation fails on missing entities, retired entities, invalid cells, duplicate entity membership, or forward/reverse index disagreement.

## 4. Persistence and compatibility

Canonical spatial persistence must include entity-position entries ordered by ascending `EntityId`, with each `MapCellId` serialized in the repository's explicit stable format.

Restore must:

- validate the full spatial payload before accepting it;
- reject duplicate entities, missing/non-active entities, and invalid cells;
- rebuild reverse per-cell membership from canonical entity-position entries;
- never trust serialized cache or collection order;
- produce byte-identical canonical serialize → restore → serialize output.

**Compatibility boundary:** Step 11 implementation is expected to add spatial state to the save contract and therefore requires an explicit schema/version decision before code changes. Silent interpretation of older payloads is forbidden.

## 5. Checksum and diagnostics

Add a stable spatial-state checksum component. It includes:

- positioned entity count;
- each positioned `EntityId` in ascending order;
- its canonical `MapCellId`;
- canonical per-cell membership as a validated derived view.

The global checksum must include the spatial component.

**Compatibility boundary:** the accepted baseline is `ChecksumFormatVersion = 3`. Step 11 implementation must not change it until Calvin explicitly approves the version transition. The implementation contract proposes version `4` if spatial state is added to canonical checksums.

Diagnostics must detect:

- positioned missing, pending, or retired entities;
- invalid cell identifiers;
- duplicate position entries;
- forward/reverse membership disagreement;
- pending position mutations after Commit.

## 6. Exact implementation file scope

Implementation is limited to these files unless a pre-edit inspection proves one is unnecessary or Calvin approves a scope amendment.

New files:

```text
GenerationArk.Simulation/Map/EntityPosition.cs
GenerationArk.Simulation/Map/PositionMutation.cs
GenerationArk.Simulation/Map/PositionMutationComparer.cs
GenerationArk.Simulation/Map/SpatialEntityIndex.cs
GenerationArk.Simulation/Persistence/SpatialStateSnapshot.cs
GenerationArk.Simulation/Persistence/SpatialStateSerializer.cs
GenerationArk.Simulation.Tests/SpatialIndexMilestoneTests.cs
tools/spatial_index_behavioral_reference.py
```

Existing integration files:

```text
GenerationArk.Simulation/State/MutationBuffer.cs
GenerationArk.Simulation/State/MutationCommitResult.cs
GenerationArk.Simulation/State/WorldState.cs
GenerationArk.Simulation/Persistence/SimulationSaveEnvelope.cs
GenerationArk.Simulation/Persistence/SimulationSaveEnvelopeJson.cs
GenerationArk.Simulation/Diagnostics/ChecksumFormatVersion.cs
GenerationArk.Simulation/Diagnostics/StateChecksum.cs
GenerationArk.Simulation.Tests/Program.cs
GenerationArk.Simulation.Tests/SimulationFixture.cs
README.md
VALIDATION.md
docs/milestones/step-11-contract.md
```

No dependency, project-file, Unity adapter, scheduler, random algorithm, entity-ID, map-topology algorithm, or unrelated test changes are authorized.

## 7. Required tests

Add exactly ten Step 11 tests:

1. `SpatialIndexesUseCanonicalCellAndEntityOrder`
2. `PositionMutationsRemainInvisibleUntilCommit`
3. `SetMoveAndClearUpdateBothIndexesAtomically`
4. `InvalidEntityLifecycleAndCellTargetsFailBeforeMutation`
5. `ConflictingPositionBatchFailsBeforePartialApplication`
6. `RequestOrderDoesNotChangeCanonicalSpatialState`
7. `DestroyEntityRemovesSpatialMembershipDuringCommit`
8. `SpatialStateSaveLoadRoundTripIsCanonical`
9. `SpatialStateParticipatesInComponentAndGlobalChecksums`
10. `SpatialReplayFramePatternsAndChurnMatchChecksums`

The existing runner exposes no test filter. The authoritative executable gate therefore remains the complete runner, increasing from `72/72` to `82/82`.

The final churn test must repeat the same scenario and cover set, move, clear, destroy, save/load, replay checkpoints, multiple frame patterns, and bounded retained diagnostics. Both runs must produce identical canonical payloads, component checksums, checkpoint checksums, and final checksum.

## 8. Validation sequence

### 8.1 Baseline dependency gate

From the verified checkout:

```bash
cd /home/calvin/Generation-Ark/generation-ark-clock

# Required once for a clean checkout or after generated build assets are absent.
/usr/bin/dotnet restore GenerationArk.sln

/usr/bin/dotnet build GenerationArk.sln -c Release --no-restore
/usr/bin/dotnet run \
  --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj \
  -c Release --no-build
```

Required result before application:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
72/72 tests passed.
```

### 8.2 Step 11 behavioral reference

```bash
python3 tools/spatial_index_behavioral_reference.py
```

Required result:

```text
spatial index behavioral reference: PASS
```

### 8.3 Final build and complete harness

```bash
/usr/bin/dotnet build GenerationArk.sln -c Release --no-restore
/usr/bin/dotnet run \
  --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj \
  -c Release --no-build
```

Required result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
82/82 tests passed.
```

Run the final harness at least twice from identical inputs and preserve identical authoritative outputs.

Static guards must reject new authoritative use of:

- `System.Random` or Unity randomness;
- wall-clock time or Unity frame timing;
- floating-point position or topology state;
- runtime hash codes as identifiers or ordering keys;
- unordered collection iteration as authoritative order;
- direct spatial mutation outside the existing Commit path;
- Unity dependencies in simulation, persistence, or diagnostics code.

## 9. Explicit exclusions

Step 11 does not include:

- pathfinding or route generation;
- autonomous or velocity-based movement;
- walkability decisions or obstruction re-pathing;
- collision, occupancy limits, stacking rules, or reservations;
- doors as traversal logic;
- jobs, work givers, toils, priorities, or scheduling;
- atmosphere, needs, construction, resources, or combat;
- Unity transforms, GameObjects, presentation, animation, or input;
- new dependencies, concurrency, parallel simulation, or a new ECS;
- save migration policy beyond the separately approved Step 11 schema decision.

## 10. Acceptance and approval boundary

This document freezes the candidate contract only. Step 11 implementation may begin only after Calvin explicitly approves:

1. this scope and exact file list;
2. the save schema/version transition;
3. the checksum format transition from version `3` to proposed version `4`;
4. the `72/72 -> 82/82` gate;
5. implementation in a verified checkout aligned to current GitHub `main`.

Completion requires an owner-machine transcript, behavioral reference PASS, Release build with zero warnings/errors, repeated `82/82` passes, canonical save/load evidence, replay/frame-pattern evidence, exact final diff, backup/rollback locations, and a post-validation handoff.

## 11. Superseded proposal

GitHub issue #1, “Step 11: Deterministic pathfinding and movement,” records an earlier broader proposal and a synchronization blocker that has since been resolved. It remains historical evidence only unless Calvin separately authorizes pathfinding and movement as a later milestone.
