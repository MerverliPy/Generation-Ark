# Validation

This document distinguishes accepted owner-machine evidence from checks that have not yet been run. A milestone is complete only when its owner-validation record and current repository state support that claim.

## Current accepted baseline

Step 10 — Deterministic Grid and Room Topology is the latest accepted milestone.

Authoritative receipt:

```text
Owner validation date: 2026-07-19 UTC
Required SDK: /usr/bin/dotnet 8.0.129
Pre-install baseline: 45f2074923f5c6ace23248f8307805666b60e35f
Step 9 dependency harness: 62/62 tests passed
Behavioral references: PASS
Release build: 0 warnings, 0 errors
Step 10 final harness: 72/72 tests passed
ChecksumFormatVersion: 3
PASS: Step 10 deterministic grid/topology compile and test gate cleared.
```

See `docs/milestones/step-10-owner-validation.md` for the accepted record.

## Canonical full verification command

Run from the verified repository checkout with .NET SDK `8.0.129`:

```bash
cd /home/calvin/Generation-Ark/generation-ark-clock

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

## Step 11 readiness

The frozen candidate is documented in `docs/milestones/step-11-contract.md`:

```text
Step 11 — Deterministic Spatial Entity Index and Position Mutation
Dependency gate: 72/72
Proposed expanded gate: 82/82
Implementation status: Not authorized
```

Before implementation, explicit approval is required for:

- the exact Step 11 scope and file list;
- the save schema/version transition;
- the checksum format transition from version `3` to proposed version `4`;
- the ten-test expansion to `82/82`;
- use of a verified checkout aligned to current GitHub `main`.

The earlier GitHub issue #1 proposal for pathfinding and movement is broader than the frozen dependency-first Step 11 candidate and is historical evidence only. Pathfinding and movement remain excluded unless separately authorized.

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
