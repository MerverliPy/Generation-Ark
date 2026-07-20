# Generation Ark — Deterministic Diagnostics Milestone Contract

**Document version:** 0.1  
**Prepared:** 2026-07-19 UTC  
**Repository target:** `~/Generation-Ark/generation-ark-clock`  
**Dependency gate:** Clock, scheduler, and counter-based random service validated in Release with `26/26` tests  
**Active milestone:** Step 6 — deterministic diagnostics

## 1. Milestone objective

Add deterministic diagnostic facilities that can:

1. Produce a canonical global checksum at an exact simulation tick.
2. Produce stable named component checksums that narrow a divergence.
3. Record an inspectable tick trace without changing authoritative outcomes.
4. Compare expected and actual observations and report the first divergent tick.
5. Preserve the existing scheduler and random-service behavior and their locked vectors.

This milestone is diagnostic infrastructure only. It must not introduce map, entity, job, Unity adapter, replay-runner, or general persistence implementation.

## 2. Compatibility constraints

The implementation must be generated against the exact locally validated source tree.

Mandatory constraints:

- Preserve all current clock, command, scheduler, and random APIs unless an additive integration point is required.
- Preserve the existing `StableHash64` byte encoding and `StateChecksum` behavior unless a deliberate checksum-format version migration is documented.
- Preserve the locked random algorithm vector and random algorithm version `1`.
- Do not hash profiling duration, wall-clock timestamps, allocation counts, localized strings, rendering data, or diagnostic buffers.
- Do not use dictionary, hash-set, reflection, file-system, or registration order as canonical iteration order.
- Do not use runtime hash codes.
- Do not modify `VALIDATION.md` automatically.
- Do not add external package dependencies.
- Do not include `bin/`, `obj/`, `.git/`, IDE metadata, or user-specific paths in the patch.
- Diagnostics must never affect scheduling, random output, command ordering, system ordering, or tick advancement.

## 3. Canonical checksum model

### 3.1 Checksum format version

Introduce an explicit checksum format version. Initial value:

```text
ChecksumFormatVersion = 1
```

The version is diagnostic/save/replay metadata. Any incompatible change to canonical field inclusion, field ordering, byte encoding, or component composition requires a new version.

### 3.2 Global checksum

The global checksum must be produced from canonical authoritative state in a fixed sequence.

The exact integration must preserve the already validated checksum inputs, including at minimum:

- Current tick
- Root simulation seed
- Random algorithm version
- Scheduler state and ordering metadata
- Command sequencing/state already represented by the current implementation
- Existing authoritative test-world state

Future authoritative state categories must be appended through explicit, stable contributors rather than discovery.

### 3.3 Component checksums

Provide stable component identifiers using ordinal string semantics. Foundation component IDs:

```text
clock
commands
scheduler
random
world
```

Only components supported by the current source tree should be emitted. Future systems may add stable IDs such as `resources`, `colonists`, `jobs-reservations`, `relationships`, `institutions`, and `narrative`.

Required ordering:

```text
ComponentId using StringComparer.Ordinal
```

Component registration order must not affect results. Duplicate component IDs must fail validation.

### 3.4 Composition rule

The global checksum may either:

A. Continue hashing canonical state directly while component hashes are calculated in parallel, or  
B. Hash a versioned, sorted sequence of `(ComponentId, ComponentChecksum)` pairs.

The implementation must select the option compatible with the existing `StateChecksum` contract. It must not silently change existing checksum values merely to introduce component hashes.

## 4. Tick trace model

The trace is diagnostic and excluded from authoritative checksums.

A completed tick trace should be able to contain:

- Tick
- Ordered systems executed, including phase and stable system ID
- Commands applied or rejected, when current command APIs expose this deterministically
- Scheduled events executed, when current scheduler APIs expose this deterministically
- Random requests captured through the existing random-request tracing seam
- Queue sizes or counts available without altering execution
- The completed global checksum
- Sorted component checksums
- Deterministic failure information when a tick aborts

Wall-clock measurements may be recorded separately for profiling, but they must not participate in equality, canonical serialization, checksums, replay decisions, or desynchronization decisions.

Trace retention must be bounded by an explicit capacity. Capacity affects only retained diagnostics, never simulation state.

## 5. Desynchronization report

A comparison facility must identify the earliest differing observation from two ordered checkpoint streams.

Required report fields:

- First divergent tick
- Expected global checksum
- Actual global checksum
- Checksum format version on both sides
- Sorted component-level differences
- Expected and actual trace details available for that tick
- Build/save/replay identifiers only when supplied explicitly by the caller
- Clear indication when a component exists on only one side

Comparison rules:

- Checkpoints are ordered by tick.
- Duplicate ticks are invalid.
- Component IDs use ordinal comparison.
- Missing checkpoints are reported explicitly rather than treated as a checksum value.
- Report generation must be deterministic for the same inputs.

## 6. Proposed additive source surface

Exact names may be adjusted to fit the validated repository, but the milestone should remain small and explicit.

Likely diagnostics files:

```text
GenerationArk.Simulation/Diagnostics/
  ChecksumFormatVersion.cs
  ChecksumComponentId.cs
  ComponentChecksum.cs
  TickChecksum.cs
  IStateChecksumContributor.cs
  StateChecksumWriter.cs
  SimulationTrace.cs
  SimulationTraceEntry.cs
  SimulationTraceBuffer.cs
  DesyncComponentDifference.cs
  DesyncReport.cs
  DesyncDetector.cs
```

Likely test file:

```text
GenerationArk.Simulation.Tests/DiagnosticsMilestoneTests.cs
```

Do not add a general reflection-based contributor registry.

## 7. Required executable tests

The diagnostics patch should add eight focused tests, bringing the expected harness total from `26/26` to `34/34`:

1. `CanonicalChecksumIsRepeatableForIdenticalState`
2. `ComponentRegistrationOrderDoesNotAffectChecksums`
3. `DuplicateComponentIdsFailValidation`
4. `DiagnosticTracingDoesNotAlterAuthoritativeOutcomes`
5. `TickTracePreservesStableExecutionOrder`
6. `TraceRetentionIsBoundedWithoutChangingChecksums`
7. `DesyncDetectionFindsTheFirstDivergentTick`
8. `DesyncReportIdentifiesChangedAndMissingComponents`

Names may be adapted to the current harness conventions, but coverage must remain equivalent.

Existing `26/26` tests must remain unchanged in behavior and continue to pass.

## 8. Behavioral reference

Add a dependency-free Python reference only for behavior that can be locked independently of repository-specific C# APIs.

The reference should validate:

- Ordinal sorting of component IDs
- Versioned component checksum composition, only if composition option B is selected
- Earliest-divergence selection
- Stable ordering of component differences
- Missing-component reporting

Do not invent a new global checksum vector until the exact current `StableHash64` and `StateChecksum` implementations are available.

## 9. Validation gate

The milestone is **Done** only after the project owner obtains all of the following from the exact working tree:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
...
34/34 tests passed.
```

Additional required checks:

- Existing scheduler behavioral reference: PASS
- Existing random behavioral reference: PASS
- Diagnostics behavioral reference: PASS, if added
- `git diff --check`: PASS
- No external `PackageReference`
- No forbidden randomness, wall-clock gameplay input, runtime hash-code use, or reflection-based authoritative ordering
- Reverse patch check succeeds
- Patch does not include `VALIDATION.md`, `bin/`, or `obj/`

## 10. Patch-generation prerequisites

Before producing the diagnostics patch, capture the exact validated repository source—including untracked scheduler and random files—using the companion script:

```bash
chmod +x /home/calvin/Generation-Ark/collect-generation-ark-diagnostics-baseline.sh

bash /home/calvin/Generation-Ark/collect-generation-ark-diagnostics-baseline.sh \
  /home/calvin/Generation-Ark/generation-ark-clock \
  /home/calvin/Generation-Ark
```

Upload the generated baseline archive and its `.sha256` file. The archive is read-only with respect to the repository and excludes build output and Git internals.
