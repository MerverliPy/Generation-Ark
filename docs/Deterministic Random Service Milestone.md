# Generation Ark — Deterministic Random Service Milestone

## Scope

This milestone adds authoritative, reproducible randomness without a shared mutable stream.
Every output is a pure function of:

1. Root simulation seed
2. Random algorithm version
3. Stable domain ID
4. Owner ID
5. Purpose ID
6. Occurrence ID
7. Operation-local counter
8. Internal rejection-sampling lane

Unrelated requests therefore cannot consume or perturb one another.

## Algorithm contract

`CounterBasedRandomV1` first hashes the complete key with the project-owned `StableHash64`
byte encoding, then applies the fixed SplitMix64 finalizer. All integer overflow is deliberate
unsigned wraparound. Version `1` is save/replay format data; changing constants, field order,
byte order, string encoding, or output extraction requires a new algorithm version.

The executable tests lock the first public vector:

```text
Seed:       0x0123456789ABCDEF
Domain:     colonist-generation
Owner:      42
Purpose:    7
Occurrence: 3
Counter:    0
UInt64:     0xC2140C0AEA994494
UInt32:     0xC2140C0A
Range[-1000,1001): 4
Chance(17/101): false
```

## Domains and scopes

`RandomDomainId` is an ordinal string identifier, not an enum position or runtime hash.
`RandomDomains` defines the initial project-owned IDs. A `RandomScope` combines the domain
with stable owner, purpose, and occurrence keys. Gameplay code must derive those keys from
saved authoritative identifiers rather than frame numbers or object addresses.

## Ranges and probabilities

`Range` uses 64-bit rejection sampling. A rejected sample advances an internal lane belonging
only to the same public scope/counter request, so retries cannot affect another operation.
Naive modulo-only range mapping is not used.

`Chance` accepts integer numerator/denominator ratios. Zero and certainty are handled exactly;
a zero denominator and numerator greater than denominator are rejected.

## Persistence

The service has no shared mutable draw position. `RandomStateSnapshot` therefore stores only:

- Root seed
- Algorithm version

`RandomStateSnapshotJson` provides a dependency-free JSON round trip. Restore rejects an
unsupported version instead of silently changing future outcomes.

## Simulation integration

`SimContext.Random` exposes the authoritative service to systems. `SimulationRunner` owns the
service instance and includes its metadata when recording diagnostic checksums. Canonical
checksums include root seed and algorithm version, while request traces remain diagnostic and
are excluded.

## Request tracing

`RandomRequestTrace` records the seed, version, complete scope, counter, operation kind,
parameters, and result. Supplying a tracer must not alter any generated value. The trace is
intended for first-divergence diagnostics and is not authoritative save state.

## Acceptance tests

The executable test runner covers:

- Fixed vectors and repeatability
- Domain and owner isolation
- Unrelated-draw isolation
- JSON save/load continuity and version rejection
- Range bounds and cursor counter ownership
- Probability edge cases and invalid inputs
- Canonical checksum integration
- Diagnostic request tracing without output changes
