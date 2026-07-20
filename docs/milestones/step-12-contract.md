# Generation Ark -- Step 12 Deterministic Cardinal Pathfinding and Buffered Movement Intent Contract

**Prepared:** 2026-07-20 UTC
**Canonical repository:** `https://github.com/MerverliPy/Generation-Ark`
**Baseline branch:** `main`
**Baseline commit:** `3ae149614f6131630756dc78cb528d4b8972c150`
**Required SDK:** `/usr/bin/dotnet` `8.0.129`
**Dependency gate:** Step 11 owner-validated at `82/82 tests passed.`
**Expected Step 12 gate:** `92/92 tests passed.`
**Status:** Proposed contract; implementation is not authorized by this document alone.

## 1. Decision

Step 12 is **Deterministic Cardinal Pathfinding and Buffered Movement Intent**.

Step 12 adds deterministic cardinal route calculation and one-cell authoritative movement while preserving the accepted Step 11 spatial index as the sole source of entity location.

The historical `step11/deterministic-pathfinding-movement` branch is design evidence only. It must not be merged, rebased, or cherry-picked wholesale because its `MovementAgentState.CurrentCell` duplicates the accepted `WorldState.Spatial` authority.

## 2. Objective

For the same initial state, seed, command stream, map state, traversal configuration, movement intents, and tick sequence, Step 12 must produce identical:

- selected routes;
- accepted and rejected movement intents;
- entity positions after each Commit;
- obstruction re-path behavior;
- canonical save payloads;
- component and global checksums;
- replay checkpoints and final state.

Simulation ticks and the existing phase pipeline remain authoritative. Unity frames and wall-clock time cannot control movement outcomes.

## 3. Authoritative state boundaries

### 3.1 Spatial authority

`WorldState.Spatial` remains the sole authoritative source of current entity position.

Movement code must read current position through the spatial index and update it only through the existing buffered position-mutation and Commit path.

No movement component, intent, route object, cache, diagnostic record, or Unity adapter state may contain or act as a second authoritative current-cell value.

### 3.2 Movement intent

An active positioned entity may have zero or one authoritative movement intent containing exactly:

- the target `EntityId`;
- one canonical destination `MapCellId`;
- stable mutation or creation sequence metadata required for deterministic ordering.

Movement intent must not contain:

- current position;
- Unity transforms;
- floating-point coordinates;
- wall-clock timestamps;
- frame counters;
- mutable route collections;
- hidden random state.

Intent creation, replacement, and clearing remain buffered and invisible until Commit.

A dedicated movement-intent mutation path is preferred. Step 12 does not authorize a repository-wide generic component-replacement mutation.

### 3.3 Route state

Computed routes are transient deterministic results.

Routes are not persisted, checksummed, or restored as authoritative state. After save/load, replay restoration, or obstruction changes, routes are recalculated from:

- the accepted spatial position;
- destination intent;
- immutable map dimensions;
- current canonical map definitions;
- the deterministic traversal policy.

Diagnostic route traces may be retained only if they cannot affect authoritative outcomes.

## 4. Deterministic traversal policy

Step 12 must define an engine-neutral traversal policy that maps stable `MapCellDefinitionId` values to traversable or blocked status.

Requirements:

- the policy is fully determined by simulation configuration;
- configuration iteration uses explicit stable ordering;
- unknown required definition IDs fail validation;
- the policy cannot query Unity objects, rendering state, wall-clock time, frame time, networking, unordered mutable collections, or external callbacks;
- `ParticipatesInRoomTopology` is not silently interpreted as walkability;
- occupancy limits, entity collision, reservations, doors, and dynamic actor avoidance remain excluded.

A cell is eligible for pathfinding only when it exists and its current definition is traversable under this policy.

## 5. Cardinal pathfinding

Pathfinding uses a deterministic breadth-first search over four-directional grid neighbors.

Requirements:

- no diagonal movement;
- validate source and destination cells before searching;
- source position comes from `WorldState.Spatial`;
- gather valid cardinal neighbors and visit them by ascending `MapCellId`;
- do not depend on hash, dictionary, registration, task, or request order;
- destination equal to source returns the single source cell;
- blocked destination returns no path;
- unreachable destination returns no path;
- blocked intermediate cells are excluded;
- equivalent requests produce identical cell sequences;
- route output includes source and destination;
- route output is immutable to callers.

Parallel route calculations may be used only when their inputs are immutable and their scheduling cannot affect authoritative ordering or results.

## 6. Movement execution

During each authorized movement update:

1. read the entity's accepted destination intent;
2. read its current position from `WorldState.Spatial`;
3. calculate the canonical route from current position;
4. if no route exists, leave position unchanged;
5. if already at destination, leave position unchanged and clear intent only if the contract implementation explicitly requires it;
6. otherwise enqueue one position mutation to route cell index `1`;
7. apply the position through the existing Commit phase.

An entity may advance at most one cardinal cell per simulation tick.

Movement must never mutate `SpatialEntityIndex` directly.

Obstruction changes visible before route calculation must affect that tick's route. Changes still buffered and not yet committed remain invisible.

## 7. Conflict and failure behavior

The complete Commit batch must be validated before any mutation is applied.

Reject deterministically when:

- intent targets a missing, pending, or retired entity;
- the entity has no accepted spatial position;
- destination cell is invalid;
- traversal configuration is missing or invalid;
- multiple movement-intent operations target one entity in the same Commit;
- movement and destruction target the same entity incompatibly;
- more than one position operation targets the same entity in the same Commit;
- movement attempts to bypass the existing spatial Commit path.

A rejected batch leaves entity, component, map, spatial, scheduler, intent, persistence, and diagnostic state unchanged.

## 8. Persistence and compatibility

Persistence includes canonical movement intents ordered by ascending `EntityId`.

Restore must:

- validate the full movement-intent payload before accepting it;
- reject duplicate entities;
- reject missing or non-active entities;
- reject invalid destination cells;
- rebuild runtime route calculations rather than restoring route caches;
- produce byte-identical canonical serialize → restore → serialize output.

Step 12 implementation must explicitly propose its save-schema transition before implementation. This contract does not yet authorize a schema-version change.

## 9. Checksums and diagnostics

The authoritative checksum includes canonical movement intents but not transient calculated routes.

Checksum contribution order:

1. intent count;
2. each `EntityId` in ascending order;
3. its destination `MapCellId`;
4. any explicit stable intent version or sequence field approved by the implementation scope.

The implementation must explicitly propose any checksum format transition before changing `ChecksumFormatVersion`. This contract does not yet authorize a checksum-version change.

Diagnostics must identify:

- invalid movement-intent ownership;
- invalid destinations;
- positioned/unpositioned authority disagreement;
- pending intent or position mutations after Commit;
- attempts to introduce duplicate current-position state;
- traversal-policy configuration errors.

## 10. Proposed implementation file scope

Implementation is not authorized until this scope, save transition, checksum transition, and public APIs receive separate approval.

Proposed new files:

```text
GenerationArk.Simulation/Movement/MovementIntent.cs
GenerationArk.Simulation/Movement/MovementIntentMutation.cs
GenerationArk.Simulation/Movement/MovementIntentMutationComparer.cs
GenerationArk.Simulation/Movement/MovementIntentStore.cs
GenerationArk.Simulation/Movement/TraversalPolicy.cs
GenerationArk.Simulation/Movement/DeterministicPathfinder.cs
GenerationArk.Simulation/Movement/AuthoritativeMovementSystem.cs
GenerationArk.Simulation/Persistence/MovementIntentStateSnapshot.cs
GenerationArk.Simulation/Persistence/MovementIntentStateSerializer.cs
GenerationArk.Simulation.Tests/MovementMilestoneTests.cs
tools/movement_behavioral_reference.py
```

Proposed existing integration files:

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
docs/milestones/step-12-contract.md
```

No dependency, project-file, Unity adapter, scheduler, random-service, entity-ID, room-topology, or unrelated component changes are authorized.

Generic `ReplaceComponent`, historical `MovementAgentState.CurrentCell`, and direct reuse of the old Step 11 movement branch remain excluded.

## 11. Required tests

Add exactly ten Step 12 tests:

1. `CardinalPathUsesAscendingCellTieBreaking`
2. `BlockedAndUnreachableDestinationsReturnNoPath`
3. `PathfindingInputOrderDoesNotChangeRoute`
4. `MovementIntentDoesNotDuplicateSpatialPosition`
5. `MovementIntentRemainsInvisibleUntilCommit`
6. `MovementAdvancesAtMostOneCanonicalCellPerTick`
7. `BlockedNextCellRepathsDeterministically`
8. `ConflictingMovementIntentsRejectBeforePartialMutation`
9. `MovementSaveLoadAndReplayMatchUninterruptedState`
10. `MovementFramePatternsAndObstructionChurnRepeatChecksums`

The complete executable gate increases from `82/82` to `92/92`.

The final test must run the same scenario at least twice and cover:

- destination changes;
- canonical tie-breaking;
- one-cell movement;
- committed obstruction changes;
- blocked and restored routes;
- save/load continuity;
- replay checkpoints;
- multiple Unity frame patterns;
- bounded retained diagnostics.

Repeated runs must produce identical canonical payloads, movement-intent checksums, spatial checksums, checkpoint checksums, and final global checksum.

## 12. Validation sequence

From the verified checkout:

```bash
cd /home/calvin/Generation-Ark

/usr/bin/dotnet restore GenerationArk.sln
/usr/bin/dotnet build GenerationArk.sln -c Release --no-restore

/usr/bin/dotnet run \
  --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj \
  -c Release \
  --no-build

python3 tools/spatial_index_behavioral_reference.py
python3 tools/movement_behavioral_reference.py
```

Required dependency result before implementation:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
82/82 tests passed.
spatial index behavioral reference: PASS
```

Required final result after an authorized Step 12 implementation:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
92/92 tests passed.
movement behavioral reference: PASS
```

Run the complete final harness at least twice from identical inputs and preserve byte-identical authoritative output.

Static guards must reject:

- `System.Random` or Unity randomness;
- wall-clock or Unity-frame movement control;
- floating-point authoritative movement state;
- a second current-position field outside `WorldState.Spatial`;
- runtime hash codes as identifiers or ordering keys;
- unordered collection iteration as authoritative order;
- direct spatial mutation outside Commit;
- persistence of transient route caches;
- Unity dependencies in simulation, persistence, pathfinding, or diagnostics.

## 13. Explicit exclusions

Step 12 does not include:

- autonomous AI decision-making;
- jobs, work givers, toils, priorities, or scheduling;
- velocity, acceleration, interpolation, or continuous movement;
- diagonal or multi-cell-per-tick movement;
- collision, occupancy limits, stacking rules, or reservations;
- doors as traversal state;
- entity avoidance or crowd steering;
- atmosphere, needs, construction, resources, combat, or vehicles;
- Unity transforms, GameObjects, animation, navigation meshes, rendering, or input;
- path-cost weighting, terrain costs, A*, heuristics, or hierarchical pathfinding;
- network prediction or rollback;
- generic component replacement;
- merging or rebasing the historical Step 11 movement branch.

## 14. Approval boundaries

Separate explicit approval is required before:

- implementing Step 12;
- accepting the proposed implementation file scope;
- adding public movement or traversal interfaces;
- changing save or snapshot schemas;
- changing `ChecksumFormatVersion`;
- adding generic component replacement;
- expanding the milestone beyond this contract.
