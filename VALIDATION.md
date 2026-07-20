# Validation

This document distinguishes accepted owner-machine evidence from checks that have not yet been run. A milestone is complete only when its owner-validation record and current repository state support that claim.

## Current accepted baseline

Step 11 — Deterministic Spatial Entity Index and Position Mutation is the latest accepted milestone.

Authoritative post-merge receipt:

```text
Owner validation date: 2026-07-19 America/Chicago
Canonical main: 251350da02ed76f84aa882fc277c27dcb7d3a9bd
Required SDK: /usr/bin/dotnet 8.0.129
Step 10 dependency harness: 72/72 tests passed
Spatial behavioral reference: PASS
Release build: 0 warnings, 0 errors
Step 11 final harness: 82/82 tests passed twice with byte-identical output
ChecksumFormatVersion: 4
PASS: Step 11 deterministic spatial-index compile and test gate cleared.
```

Step 10 remains the accepted dependency record in `docs/milestones/step-10-owner-validation.md`.

## Canonical full verification command

Run from the verified repository checkout with .NET SDK `8.0.129`:

```bash
cd /home/calvin/Generation-Ark/generation-ark-clock

# Required once for a clean checkout or after generated build assets are absent.
/usr/bin/dotnet restore GenerationArk.sln

/usr/bin/dotnet build GenerationArk.sln -c Release --no-restore
/usr/bin/dotnet run \
  --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj \
  -c Release --no-build
```

The accepted Step 10 result is:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
72/72 tests passed.
```

A current run must be reported as a new receipt. This document does not claim that the command was rerun for a documentation-only change.

## Step 11 owner validation completed

The accepted Step 11 contract is documented in `docs/milestones/step-11-contract.md`:

```text
Step 11 — Deterministic Spatial Entity Index and Position Mutation
Dependency gate: 72/72
Expanded gate: 82/82
ChecksumFormatVersion: 4
Implementation status: Accepted after post-merge owner-machine validation on canonical main 251350da02ed76f84aa882fc277c27dcb7d3a9bd
```

The following approval was received before implementation:

- the exact Step 11 scope and file list;
- the save schema/version transition;
- the checksum format transition from version `3` to proposed version `4`;
- the ten-test expansion to `82/82`;
- use of a verified checkout aligned to current GitHub `main`.

The Step 11 save boundary is explicit: envelope JSON now requires `spatialStateBase64`; a missing field is rejected rather than interpreted as an older save. A null value represents an explicitly spatially empty simulation, while a spatial snapshot uses `SpatialStateSnapshot` schema version `1`.

The earlier GitHub issue #1 proposal for pathfinding and movement is broader than the implemented Step 11 scope and is historical evidence only. Pathfinding and movement remain excluded unless separately authorized.

## Validation integrity rules

- Do not infer acceptance from filenames, timestamps, archives, or generated build outputs.
- Do not report a build or test pass without a current transcript.
- Run the dependency gate before applying a milestone payload.
- Run every milestone behavioral reference required by its contract.
- Build Release with warnings treated as errors.
- Run the complete executable harness after focused checks.
- Repeat deterministic checks where the contract requires identical authoritative outputs.
- Compare final Git state with the baseline and report every changed file.
- Preserve owner transcripts, relevant hashes, backup/rollback locations, and unresolved failures.
- Treat `bin/`, `obj/`, archives, extracted packages, patches, and backups as generated or recovery evidence, not canonical source.
