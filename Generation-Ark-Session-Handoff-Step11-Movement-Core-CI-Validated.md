# Generation Ark — Session Handoff: Step 11 Movement Core CI Validated

## Status

Step 11 movement-core implementation is active on a draft pull request. The current branch has a successful Release build and an exact 81/81 test-harness result in GitHub Actions. Movement persistence, replay continuity, frame-pattern equivalence, dynamic-obstruction soak, and owner validation are not yet complete.

This file is the authoritative baseline for the next Step 11 phase.

## Repository and branch

- Repository: `MerverliPy/Generation-Ark`
- Base branch: `main`
- Active branch: `step11/deterministic-pathfinding-movement`
- Draft pull request: `#3`
- Validated branch head: `9badf518f0b12c875ccb7642d510593b548fa1a5`
- Step 10 validated main commit: `f6be88e0c7c273899263661e31fb6c9639b006f8`
- Step 10 source commit: `03ac1cfd8cab11011c205bedf4ee3bae88265784`

## Validation evidence

- Workflow: `Step 11 Validation`
- Workflow run: `29697124585`
- Job: `release-validation`
- Restore: passed
- Release build: passed
- Warnings-as-errors gate: passed
- Exact console harness: passed
- Test result: `81/81`
- Runner OS: Ubuntu 24.04
- Target framework: .NET 8

The pull request must remain draft and unmerged until all remaining Step 11 gates and owner validation are complete.

## Preserved backup

Do not remove or overwrite:

`/home/calvin/Generation-Ark/generation-ark-clock-before-git-sync-20260719T164917Z.tar.gz`

## Completed Step 11 movement core

### Deterministic pathfinding

- Deterministic cardinal pathfinding is implemented.
- Neighbor traversal and tie-breaking use canonical `MapCellId` ordering.
- Blocked cells are avoided.
- Blocked destinations fail deterministically.
- Repathing after obstruction changes is deterministic.
- A 100-agent concurrent-route test is registered and passing.

### Movement state

`MovementAgentState` is the authoritative movement component and includes:

- current cell
- destination cell
- movement revision
- canonical registration
- canonical serialization and deserialization
- checksum participation

### Movement planning

`AuthoritativeMovementPlanner`:

- advances exactly one canonical cell per planning call
- does not mutate authoritative world state directly
- produces Commit-bound replacement intent
- reuses the existing world, mutation, Commit, save, replay, and checksum architecture

## Component replacement infrastructure

Replacement support was added to the existing component and mutation pipeline.

### Component store

`ComponentStore.Replace(EntityId, object)`:

- rejects null values
- requires the exact registered runtime type
- requires the component to already exist
- replaces the stored value without changing component identity

### Component registry

`ComponentRegistry.Replace(EntityRegistry, EntityId, ComponentValue)`:

- validates arguments
- requires the entity to exist
- delegates to the registered component store

### Mutation kind

`EntityMutationKind.ReplaceComponent = 5`

Existing enum numeric values were preserved:

- `CreateEntity = 1`
- `DestroyEntity = 2`
- `AddComponent = 3`
- `RemoveComponent = 4`
- `ReplaceComponent = 5`

### Mutation buffer semantics

`MutationBuffer` now supports `EnqueueReplace` and applies replacements through the existing Commit path.

Validation requires:

- the entity exists
- the component type is registered
- the replacement runtime type exactly matches the registration
- the entity already owns the component

Conflict rules reject atomically:

- replace + replace for the same entity/component
- add + replace for the same entity/component
- remove + replace for the same entity/component
- destroy + replace for the same entity
- any multiple structural component mutations targeting the same entity/component in one batch

Replacement remains invisible until Commit.

## Focused replacement tests

The following tests are registered and passing:

- `ReplacementRemainsInvisibleUntilCommitAndThenApplies`
- `ConflictingReplacementsRejectAtomically`
- `MovementReplacementChangesCanonicalChecksum`

The checksum test compares states at the same tick so divergence is attributable to movement state rather than clock state.

## CI infrastructure

`.github/workflows/step11-validation.yml` runs on the Step 11 branch and pull request.

It performs:

1. checkout
2. .NET 8 setup
3. restore
4. Release build
5. build-diagnostics artifact upload
6. exact console harness execution

The workflow preserves the real build exit status and uploads `step11-build.log` even when compilation fails.

A small test-namespace array `Reverse` extension delegates explicitly to LINQ to avoid .NET array overload resolution selecting the in-place `void` overload in existing map tests.

## Current test count

The authoritative registered test count is:

`81/81`

This supersedes the Step 10 `72/72` baseline for the active Step 11 branch only. It does not complete Step 11.

## Next authoritative work: movement persistence and replay continuity

Continue from validated head `9badf518f0b12c875ccb7642d510593b548fa1a5`.

Implement and validate movement continuity through the existing persistence and replay pipelines. Do not create parallel infrastructure.

Required next gates:

### 1. Save/load continuity

- Persist `MovementAgentState` through the existing entity/component snapshot pipeline.
- Restore authoritative current cell, destination, and revision exactly.
- Verify canonical serialized bytes or canonical restored state.
- Verify uninterrupted execution and save/load-resumed execution produce identical final checksums.

### 2. Replay equivalence

- Record movement-driving commands or deterministic movement inputs through the existing replay log.
- Re-run from the same seed and initial state.
- Verify checkpoint and final checksum equivalence.
- Ensure replacement mutation sequencing remains stable across replay.

### 3. Frame-pattern equivalence

- Run the same movement scenario under multiple frame budgets and frame patterns.
- Verify identical tick outcomes, movement positions, revisions, and canonical checksums.
- Confirm no frame-rate-dependent path planning or movement advancement exists.

### 4. Dynamic-obstruction deterministic soak

- Include at least 100 concurrently moving agents.
- Apply deterministic obstruction changes over a long run.
- Require deterministic repathing and completion/failure behavior.
- Compare repeated runs and varied frame patterns.
- Retain bounded checkpoints.
- Verify final checksum equivalence.

### 5. Owner validation

After CI is green for all Step 11 gates, owner validation must still produce retained evidence showing:

- exact branch and commit SHA
- .NET SDK version
- Release build result
- zero warnings and zero errors
- exact final test count
- soak and continuity evidence
- backup preservation

Do not mark the PR ready, merge it, close issue `#1`, or claim Step 11 complete before owner evidence exists.

## Explicit exclusions

Do not begin or introduce:

- jobs or toils
- reservation systems
- needs or routines
- atmosphere simulation
- Unity presentation work
- a second simulation runner
- a second simulation clock
- a second world root
- a parallel Commit pipeline
- a parallel save/load format
- a parallel checksum pipeline

## Guardrails

- Preserve deterministic ordering everywhere.
- Preserve all existing enum numeric values.
- Preserve the Step 10 validated behavior and tests.
- Reuse the existing mutation, Commit, persistence, replay, scheduler, and checksum systems.
- Treat GitHub CI as development evidence, not owner validation.
- Keep PR #3 draft and unmerged until every remaining gate passes.
- Keep the preserved backup intact.

## Immediate next step

Add movement save/load continuity tests and the minimum persistence integration needed to make them pass. Then run the existing `Step 11 Validation` workflow and act only on concrete build or test evidence.
