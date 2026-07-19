# Generation Ark — Step 9 Deterministic Entity Lifecycle Milestone Contract

**Prepared:** 2026-07-19 UTC  
**Repository target:** `/home/calvin/Generation-Ark/generation-ark-clock`  
**Owner SDK:** .NET SDK `8.0.129` at `/usr/bin/dotnet`  
**Dependency gate:** Step 8 replay and save/load continuity complete at `52/52`  
**Expected milestone gate:** `62/62 tests passed.`  
**Milestone status:** Implementation candidate prepared; owner-machine compile and `62/62` validation pending

## 1. Decision

Step 9 is **Deterministic Entity Lifecycle Foundation**.

This is the narrow dependency-first milestone required before map, colonist, resource-object, reservation, or job-system implementation. It establishes stable authoritative identity, explicit component registration, canonical entity iteration, buffered structural mutations, activation timing, persistence, and checksum ownership.

Step 9 does not implement gameplay entities. It provides the deterministic lifecycle substrate those later systems require.

## 2. Objective

Add an engine-neutral persistent entity model whose behavior is identical for the same build, seed, initial state, accepted commands, and tick count.

The milestone must guarantee:

- entity IDs are monotonic and never reused within a save
- authoritative iteration is independent of dictionary or insertion order
- component types have explicit stable serialized identifiers
- structural changes occur only through the mutation buffer
- newly created entities become active at the next `PreSimulation` phase
- destroyed entities and their owned scheduled events are removed deterministically
- entity state survives canonical save/load round trips
- entity state contributes to component-level and global checksums
- replay, frame-pattern, and repeated-run validation remain equivalent

## 3. Required implementation

### 3.1 Stable entity identity

Provide or complete:

```text
GenerationArk.Simulation/State/EntityId.cs
GenerationArk.Simulation/State/EntityRegistry.cs
GenerationArk.Simulation/State/EntityLifecycleState.cs
```

Requirements:

- `EntityId` is an unsigned 64-bit stable identifier.
- `EntityId.None`, if provided, is reserved and never assigned to a live entity.
- IDs are allocated monotonically from saved `NextEntityId` state.
- Deleted IDs are permanently retired and never reused.
- overflow uses checked arithmetic and fails before corrupting state.
- lookup collections may use dictionaries, but authoritative iteration returns entity IDs in ascending ordinal order.
- the registry distinguishes at least:
  - `PendingActivation`
  - `Active`
- entities created during Commit are `PendingActivation` for the remainder of that tick.
- pending entities become `Active` at the next tick's `PreSimulation` phase, before ordinary system work begins.

### 3.2 Explicit component registration

Provide or complete:

```text
GenerationArk.Simulation/State/ComponentTypeId.cs
GenerationArk.Simulation/State/IComponentStore.cs
GenerationArk.Simulation/State/ComponentStore.cs
GenerationArk.Simulation/State/ComponentRegistry.cs
GenerationArk.Simulation/State/ComponentRegistration.cs
```

Requirements:

- every authoritative component type has an explicit stable `ComponentTypeId`
- component IDs are serialized values, not reflection order or enum position assumptions
- registration is explicit at startup
- duplicate component IDs fail startup validation
- duplicate runtime component types fail startup validation
- no authoritative component discovery uses reflection scanning
- a component store supports deterministic lookup and canonical entity-ID-ordered iteration
- adding a component to a missing entity fails validation
- adding a duplicate component fails validation
- removing a missing component fails validation unless an operation is explicitly documented as idempotent
- component field serialization and checksum order are explicitly implemented; runtime field order and default JSON reflection order are forbidden as authoritative ordering

Step 9 should use a small explicit registry, not introduce a general-purpose ECS framework.

### 3.3 Buffered structural mutation

Provide or complete:

```text
GenerationArk.Simulation/State/EntityMutation.cs
GenerationArk.Simulation/State/EntityMutationKind.cs
GenerationArk.Simulation/State/MutationBuffer.cs
GenerationArk.Simulation/State/MutationCommitResult.cs
```

The mutation buffer must support:

- create entity with an explicit canonical initial component set
- destroy entity
- add component
- remove component

Requirements:

- structural mutation requests may be enqueued during permitted phases
- authoritative collections are not structurally changed before Commit
- every request receives a monotonically increasing `MutationSequence`
- request production is deterministic because systems and entities already execute in stable order
- Commit processes requests by `MutationSequence`, with every tie-break field compared explicitly
- create-request initial components are ordered by `ComponentTypeId`
- all requests are validated before the first structural change is applied
- a validation failure stops the authoritative tick and reports the exact conflicting requests
- conflicting same-Commit operations are rejected, including:
  - destroying an entity while also adding or removing one of its components
  - duplicate component additions
  - duplicate component removals
  - multiple destroy requests for the same entity
- no partial best-effort Commit is permitted after an internal consistency failure
- the mutation buffer is empty after a successful Commit

### 3.4 Entity destruction cleanup

Destroying an entity during Commit must deterministically:

1. validate that the entity exists
2. remove it from pending or active entity indexes
3. remove all registered authoritative components in ascending `ComponentTypeId` order
4. cancel scheduled events owned by that `EntityId`
5. remove cadence-bucket and system-index membership through explicit cleanup hooks
6. emit a deterministic lifecycle event
7. retain the retired ID as non-reusable

Cleanup hooks must be explicitly registered and ordered. Unity object destruction, scene discovery, finalizers, or garbage-collection timing must not participate in authoritative cleanup.

### 3.5 World-state integration

`WorldState` must own the entity registry, component registry/stores, next-ID state, pending activation state, and mutation buffer integration.

The simulation runner must invoke lifecycle work at fixed phases:

- `PreSimulation`: activate entities committed during the previous tick
- `Commit`: validate and apply buffered structural mutations
- `Diagnostics`: checksum and invariant validation only; no authoritative mutation

The runner must not expose Unity types, wall-clock values, runtime hash codes, or unordered collection iteration to entity logic.

## 4. Canonical persistence

Provide or complete:

```text
GenerationArk.Simulation/Persistence/EntityStateSerializer.cs
GenerationArk.Simulation/Persistence/ComponentStateSerializer.cs
GenerationArk.Simulation/Persistence/EntityStateSnapshot.cs
```

The canonical entity save payload must include:

- next entity ID
- all live entities by ascending `EntityId`
- lifecycle state for each entity
- pending activation ordering
- components by ascending `ComponentTypeId`
- component payload fields in serializer-defined order
- entity/component schema version

Rules:

- saves occur only at a completed authoritative tick boundary
- the mutation buffer must be empty at save time
- pending activation state must be preserved because an entity created during the prior Commit may not yet be active
- load validates all entity IDs, component IDs, duplicate components, lifecycle states, and next-ID monotonicity before accepting the payload
- canonical serialize → deserialize → serialize must produce byte-identical UTF-8 JSON or the repository's already-established canonical payload format
- unknown required component types fail load with an actionable error
- silent component dropping is forbidden

## 5. Checksum and diagnostics integration

Add an entity-state checksum contributor with a stable component identifier.

The canonical entity checksum includes:

- next entity ID
- live entity count
- each entity ID in ascending order
- each entity lifecycle state
- each component type ID in ascending order
- each component's authoritative fields in explicit serializer order
- pending activation state

It excludes:

- Unity objects
- render transforms
- animation state
- cached presentation data
- object references or memory addresses
- dictionary capacity and internal bucket state
- profiling and wall-clock measurements

Invariant diagnostics must detect at least:

- duplicate live entity IDs
- reused retired IDs
- next entity ID not greater than every assigned ID
- component attached to a missing entity
- unknown component type ID
- active/pending index disagreement
- non-empty mutation buffer after Commit
- owned scheduled event remaining after its owner is destroyed

## 6. Public lifecycle events

Emit stable data records for:

- entity created
- entity activated
- component added
- component removed
- entity destroyed
- mutation rejected

Event ordering must follow the committed mutation sequence and explicit cleanup order. Event payloads contain stable IDs and reason codes, not runtime type names or localized text.

## 7. Required tests

Add exactly ten Step 9 tests:

1. `EntityIdsAreMonotonicAndNeverReused`
2. `EntityIterationIsCanonicalRegardlessOfInsertionOrder`
3. `DuplicateEntityAndComponentTypeRegistrationFailsFast`
4. `StructuralMutationsRemainInvisibleUntilCommit`
5. `CreatedEntitiesActivateAtNextPreSimulationPhase`
6. `CommitAppliesMutationsInStableBufferedOrder`
7. `DestroyEntityRemovesComponentsAndCancelsOwnedEvents`
8. `ConflictingMutationBatchFailsBeforePartialApplication`
9. `EntityStateSaveLoadRoundTripIsCanonical`
10. `EntityLifecycleReplayFramePatternsAndChurnSoakMatchChecksums`

The expected complete harness total increases from `52/52` to `62/62`.

## 8. Churn-soak requirement

The final Step 9 test must execute the same deterministic entity-lifecycle scenario at least twice.

The scenario must include:

- repeated entity creation
- next-tick activation
- component addition and removal
- entity destruction
- owned scheduled-event cancellation
- periodic save/load boundaries
- replay checkpoints
- multiple Unity frame patterns or equivalent Step 8 frame-pattern validation

Acceptance requires:

- identical checkpoint checksums in both runs
- identical final checksum
- identical next entity ID
- identical live and retired entity counts
- no unbounded growth in retained lifecycle events, mutation requests, or destroyed-entity component state

The test must be long enough to exercise substantial ID churn but must not add a second multi-million-tick foundation soak unless performance measurements justify it.

## 9. Validation procedure

Before implementation:

```bash
cd /home/calvin/Generation-Ark/generation-ark-clock

dotnet build GenerationArk.sln -c Release --no-restore
dotnet run --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj -c Release --no-build
```

Required dependency result:

```text
52/52 tests passed.
```

After implementation:

```bash
cd /home/calvin/Generation-Ark/generation-ark-clock

dotnet build GenerationArk.sln -c Release --no-restore
dotnet run --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj -c Release --no-build
```

Required milestone result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
...
62/62 tests passed.
```

Static validation must reject new authoritative uses of:

- `DateTime.Now` or `DateTime.UtcNow`
- `Time.time`, `Time.deltaTime`, or `Time.fixedDeltaTime`
- `System.Random` or `UnityEngine.Random`
- runtime `GetHashCode()` as serialized or ordering input
- unordered dictionary/hash-set iteration in entity processing
- reflection-driven authoritative component discovery or serialization
- Unity dependencies in `State`, `Persistence`, or entity diagnostics code

## 10. Acceptance evidence

Step 9 becomes **Done** only when the owner-machine evidence records:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
...
62/62 tests passed.

PASS: Step 9 deterministic entity lifecycle compile and test gate cleared.
```

The completion handoff must preserve:

- the exact owner validation transcript
- meaningful source state before generated-output cleanup
- generated `bin/` and `obj/` state separately
- milestone archive and SHA-256 values
- canonical save/load evidence
- churn-soak final and checkpoint checksums
- explicit confirmation that no map or job-system implementation was introduced

## 11. Out of scope

Step 9 must not introduce:

- map grids, rooms, decks, tiles, coordinates, or pathfinding
- colonist needs, health, aging, relationships, or households
- resources, storage networks, equipment, or ship infrastructure
- jobs, toils, work givers, priorities, reservations, or movement
- institutions, policies, succession, incidents, or narrative systems
- Unity scenes, prefabs, MonoBehaviours, ScriptableObjects, or UI
- a third-party ECS package
- save migrations, compression, cloud storage, or encryption
- parallel authoritative simulation

Test-only synthetic components and entities are permitted only to validate lifecycle behavior.

## 12. Implementation order

Implement in this order:

1. `EntityId`, lifecycle states, and ordered `EntityRegistry`
2. explicit component registration and deterministic component stores
3. buffered create/add/remove/destroy requests
4. prevalidated Commit semantics and cleanup hooks
5. next-tick activation in `PreSimulation`
6. canonical entity persistence
7. entity checksum contributor and invariant diagnostics
8. ten required tests and deterministic churn soak
9. README and milestone documentation
10. owner Release validation and handoff packaging

Do not begin Step 10 until the `62/62` gate passes and the owner evidence is preserved.

## 13. Step 10 boundary

Step 9 does not preselect Step 10.

After Step 9 completion, choose exactly one narrow consumer of the entity foundation. The likely candidates are:

- deterministic map/deck topology foundation, or
- deterministic reservation foundation without full jobs

The selection must be made in a separate contract after reviewing Step 9 evidence. Full jobs should not begin before both stable entity lifecycle and the required spatial/reservation dependency are complete.

## 14. Assumptions and unresolved implementation details

**Assumption:** Existing `WorldState`, persistence, checksum, scheduler-owner cancellation, and mutation abstractions can be extended without breaking their Step 8 public behavior.

**Needs confirmation during implementation:** Exact existing namespaces and constructor signatures in the owner repository. Preserve current public names where practical rather than replacing validated Step 8 APIs.

**Unresolved:** Whether the canonical authoritative payload uses a dedicated manual JSON writer or the existing canonical serializer. Either is acceptable only if field order is explicit and byte-stable.
