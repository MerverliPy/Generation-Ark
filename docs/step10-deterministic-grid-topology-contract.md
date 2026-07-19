# Generation Ark — Step 10 Milestone Contract

## Deterministic One-Deck Grid and Room Topology Foundation

**Status:** Implemented candidate — owner validation pending
**Baseline:** Step 9 deterministic entity lifecycle, owner-validated at `62/62` tests
**Repository:** `/home/calvin/Generation-Ark/generation-ark-clock`
**Required SDK:** .NET SDK `8.0.129` at `/usr/bin/dotnet`
**Dependency rule:** Preserve all Step 1–9 behavior and validation evidence.

## 1. Milestone decision

Step 10 will implement a deterministic, engine-independent, rectangular one-deck grid and canonical room-topology model.

This is the narrowest dependency-first gameplay foundation after Step 9. It creates authoritative spatial state required by later entity placement, resources, reservations, movement, pathfinding, and jobs without implementing those systems now.

## 2. Entry gate

Implementation must not begin until the Step 9 source boundary has been cleaned, revalidated, reviewed, and preserved or committed.

Required preflight:

```bash
cd /home/calvin/Generation-Ark/generation-ark-clock

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"

git status --short \
  > "/home/calvin/Generation-Ark/generation-ark-step9-source-state-before-cleanup-${STAMP}.txt"

git restore -- ':(glob)**/bin/**' ':(glob)**/obj/**'
git clean -fd -- ':(glob)**/bin/**' ':(glob)**/obj/**'

dotnet build GenerationArk.sln -c Release --no-restore
dotnet run \
  --project GenerationArk.Simulation.Tests/GenerationArk.Simulation.Tests.csproj \
  -c Release --no-build

git status --short
```

Required result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
...
62/62 tests passed.
```

Generated `bin/` and `obj/` paths must not be staged.

## 3. In scope

Step 10 includes only:

1. One immutable rectangular deck size.
2. Canonical row-major cell identifiers and coordinate conversion.
3. Stable map-cell definition identifiers and startup validation.
4. Authoritative cell-state storage independent of Unity.
5. Canonical cell iteration independent of write or registration order.
6. Buffered cell changes applied only by the existing Commit authority.
7. Full-batch validation before any map mutation is applied.
8. Deterministic four-neighbor room/region topology.
9. Stable room identifiers derived from canonical cell membership.
10. Canonical map persistence and restore.
11. Map state and derived topology in deterministic diagnostics.
12. Replay, frame-pattern, save/load, and topology-churn equivalence tests.

## 4. Explicitly out of scope

Step 10 must not implement:

- multiple decks, vertical links, or three-dimensional coordinates
- entity positions or cell occupancy indexes
- colonists or resource-object entities
- movement or movement interpolation
- navigation costs, pathfinding, path requests, or path caches
- reservations
- jobs, toils, work selection, or hauling
- room atmosphere, oxygen, pressure, temperature, or resource networks
- door, wall, furniture, or construction gameplay entities
- fog of war, visibility, line of sight, or lighting
- procedural map generation or random map construction
- Unity tilemaps, GameObjects, rendering, input, or presentation adapters
- save migration across incompatible map-format versions

## 5. Core deterministic model

### 5.1 Grid dimensions

The map has one fixed `Width` and `Height` for the lifetime of a loaded world.

Rules:

- both dimensions must be positive
- `Width * Height` must use checked arithmetic
- the total cell count must fit the selected cell-ID representation
- resizing a live map is not supported in Step 10

Suggested value types:

```csharp
public readonly record struct GridPosition(int X, int Y);

public readonly record struct MapCellId(int Value)
{
    public static MapCellId FromPosition(GridPosition position, int width, int height);
    public GridPosition ToPosition(int width, int height);
}
```

Canonical mapping:

```text
CellId = Y * Width + X
```

Canonical map iteration is ascending `MapCellId`.

### 5.2 Cell definitions

Each cell stores one stable definition identifier.

Suggested types:

```csharp
public readonly record struct MapCellDefinitionId(int Value);

public sealed record MapCellDefinition(
    MapCellDefinitionId Id,
    bool ParticipatesInRoomTopology);
```

Rules:

- IDs are explicit and stable; registration order is irrelevant
- duplicate IDs fail startup validation
- runtime hash codes, culture-sensitive strings, reflection order, and Unity object order are forbidden
- cell definitions are build data; authoritative saves reference definitions by stable ID
- missing definition IDs fail load validation with an explicit error

Step 10 does not define movement cost, atmosphere permeability, cover, lighting, or construction semantics.

### 5.3 Map state

Suggested authoritative state:

```csharp
public sealed class MapState
{
    public int Width { get; }
    public int Height { get; }
    public int CellCount { get; }

    public MapCellDefinitionId GetCellDefinition(MapCellId cell);
    public IEnumerable<MapCellId> EnumerateCellsCanonical();
}
```

The implementation may use arrays internally because row-major cell IDs provide a direct canonical index. Public APIs must validate all cell IDs and positions.

### 5.4 Buffered cell mutation

Map edits must use the existing mutation/Commit authority rather than introducing a second independent commit phase.

Initial mutation kind:

```csharp
SetCellDefinition
```

Suggested mutation record:

```csharp
public readonly record struct MapCellMutation(
    MapCellId Cell,
    MapCellDefinitionId Definition,
    long Sequence);
```

Commit rules:

1. Capture requests during permitted earlier phases.
2. Keep current map state visible until Commit.
3. Validate the entire batch before applying any mutation.
4. Reject invalid cells, missing definitions, and multiple writes to the same cell in one batch.
5. Sort valid mutations by `MapCellId`, then mutation kind, then insertion sequence.
6. Apply the batch atomically.
7. Rebuild affected topology deterministically after successful application.
8. Publish the new state to the next tick.

A rejected batch must leave both cell state and topology unchanged.

## 6. Room topology contract

### 6.1 Meaning of a room in Step 10

A Step 10 room is a deterministic connected component of cells whose definitions have `ParticipatesInRoomTopology = true`.

This is spatial topology only. It does not imply enclosure, atmosphere, ownership, temperature, pressure, habitability, or gameplay purpose.

### 6.2 Connectivity

Connectivity is cardinal only:

```text
North, East, South, West
```

Diagonal cells are not connected.

### 6.3 Canonical construction

Topology reconstruction must:

1. scan seed cells in ascending `MapCellId`
2. traverse neighbors in the documented cardinal order
3. avoid iteration over unordered collections
4. assign each region a stable ID equal to its minimum member `MapCellId`
5. store region cells in ascending `MapCellId`
6. expose regions in ascending `RoomId`

Suggested type:

```csharp
public readonly record struct RoomId(int Value);
```

A room ID is derived topology, not an entity ID. Room IDs may change when a mutation splits or merges regions.

### 6.4 Derived-state policy

Room topology is reconstructable derived authoritative state:

- saves persist dimensions and cell definition IDs
- load reconstructs topology canonically
- checksums include canonical room IDs and room membership
- topology caches or lookup indexes are never serialized as authoritative ordering
- a rebuild after load must produce the same topology and checksum as an uninterrupted run

## 7. Integration boundary

### 7.1 `WorldState`

Add map state as an explicit authoritative member of `WorldState`.

### 7.2 `SimContext` and mutation authority

Extend the existing mutation abstraction only as required to request map-cell changes. Do not add an alternate runner, alternate clock, or alternate Commit phase.

### 7.3 Tick integration

No new simulation phase is required.

- requests may be generated in permitted phases
- cell state remains unchanged until `Commit`
- Commit validates and applies the batch
- room topology is rebuilt before Commit completes
- Diagnostics observes the completed map and topology state

### 7.4 Persistence

Add canonical map snapshots and serializers under `GenerationArk.Simulation/Persistence/`.

Canonical serialization order:

1. map-format version
2. width
3. height
4. cell definitions in ascending `MapCellId`

Room topology is rebuilt, not trusted from serialized cache data.

### 7.5 Checksums and diagnostics

Advance `ChecksumFormatVersion` from `2` to `3`.

Include in canonical checksum order:

1. map-format version
2. width and height
3. each cell by ascending `MapCellId`
4. each cell's stable definition ID
5. each room by ascending `RoomId`
6. each room member by ascending `MapCellId`

Add a map-specific component checksum identifier so desync reports can distinguish map/topology divergence from entity-state divergence.

## 8. Proposed source boundary

Exact filenames may be adjusted to match existing repository conventions, but the implementation boundary should remain equivalent.

```text
GenerationArk.Simulation/Map/
  GridPosition.cs
  MapCellId.cs
  MapCellDefinitionId.cs
  MapCellDefinition.cs
  MapCellDefinitionRegistry.cs
  MapCellMutation.cs
  MapCellMutationComparer.cs
  MapMutationValidationException.cs
  MapState.cs
  RoomId.cs
  RoomTopology.cs
  RoomTopologyBuilder.cs

GenerationArk.Simulation/Persistence/
  MapStateSnapshot.cs
  MapStateSerializer.cs

GenerationArk.Simulation.Tests/
  MapTopologyMilestoneTests.cs

docs/
  step10-deterministic-grid-topology-contract.md

tools/
  map_topology_behavioral_reference.py
```

Likely existing files requiring integration edits:

```text
GenerationArk.Simulation/Core/SimContext.cs
GenerationArk.Simulation/Core/SimulationRunner.cs
GenerationArk.Simulation/Diagnostics/ChecksumComponentId.cs
GenerationArk.Simulation/Diagnostics/ChecksumFormatVersion.cs
GenerationArk.Simulation/Diagnostics/StateChecksum.cs
GenerationArk.Simulation/Persistence/* world/save envelope integration files
GenerationArk.Simulation/State/MutationBuffer.cs
GenerationArk.Simulation/State/WorldState.cs
GenerationArk.Simulation.Tests/Program.cs
GenerationArk.Simulation.Tests/SimulationFixture.cs
README.md
```

**Needs confirmation:** Exact persistence envelope and mutation interface filenames must be taken from the Step 9 repository before implementation. Do not invent parallel abstractions when an existing extension point is available.

## 9. Required acceptance tests

Add exactly these ten Step 10 milestone tests unless repository inspection shows a necessary naming adjustment:

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

Expected complete harness after Step 10:

```text
72/72 tests passed.
```

### 9.1 Test requirements

`CellIdsUseCanonicalRowMajorCoordinates`

- verifies exact row-major mapping and inverse conversion
- verifies canonical first and last cell IDs

`InvalidGridDimensionsAndCoordinatesFailFast`

- rejects zero/negative dimensions
- rejects overflow
- rejects out-of-bounds coordinates and cell IDs

`CellIterationIsCanonicalRegardlessOfWriteOrder`

- constructs equivalent maps through different mutation request orders
- requires identical canonical iteration and checksum

`DuplicateMapCellDefinitionIdsFailFast`

- registers definitions in different orders
- rejects duplicate stable IDs before simulation starts

`MapMutationsRemainInvisibleUntilCommit`

- confirms requests do not alter current-phase or pre-Commit state
- confirms successful Commit publishes all changes together

`ConflictingMapMutationBatchFailsBeforePartialApplication`

- includes at least one valid request and one conflict/invalid request
- verifies no cell or topology change occurs

`RoomTopologyUsesCardinalConnectivityAndStableRoomIds`

- proves diagonal separation
- proves room ID equals the minimum member cell ID
- proves room and member iteration order

`RoomTopologySplitAndMergeRebuildsDeterministically`

- closes and opens connector cells in different request orders
- requires identical room IDs, membership, and checksums

`MapStateSaveLoadRoundTripIsCanonical`

- permutes snapshot array order before restore
- reconstructs topology rather than trusting serialized cache order
- requires byte/canonical JSON equivalence according to existing persistence convention

`MapReplayFramePatternsAndTopologyChurnMatchChecksums`

- applies the same accepted map-change command log under multiple frame patterns
- includes repeated room splits and merges
- includes save/load continuation
- requires identical checkpoint and final checksums
- verifies bounded diagnostic/checkpoint retention

## 10. Behavioral and static gates

The Step 10 installer/validation flow must:

1. verify payload integrity
2. require the exact Step 9 `62/62` dependency gate before first application
3. preserve a pre-apply backup and rollback script
4. run all existing behavioral references
5. run `map_topology_behavioral_reference.py`
6. build Release with zero warnings and zero errors
7. run the full executable harness
8. fail unless the exact result is `72/72 tests passed.`
9. verify expected source files and integration edits
10. record the post-validation repository status

Static checks must reject authoritative use of:

- `UnityEngine` in simulation map code
- `System.Random` or `UnityEngine.Random`
- wall-clock gameplay APIs
- runtime hash codes as stable IDs or order keys
- authoritative `float` or `double` cell/topology state
- unordered dictionary/hash-set enumeration as simulation order
- direct cell-state changes outside the controlled mutation/Commit path

## 11. Completion criteria

Step 10 is Done only when owner-machine evidence records:

```text
Active SDK: 8.0.129
Step 9 dependency gate: 62/62 passed
Behavioral references: PASS
Build succeeded.
    0 Warning(s)
    0 Error(s)
Complete harness: 72/72 tests passed
Step 10 deterministic grid/topology compile and test gate: PASS
```

The validation transcript, archive checksums, backup path, rollback path, and post-validation source status must be preserved in the next handoff.

## 12. Next milestone boundary

Step 10 does not authorize simultaneous implementation of occupancy, pathfinding, reservations, and jobs.

After Step 10, select one narrow dependency-first milestone. Recommended next candidate:

```text
Step 11 — Deterministic Spatial Entity Index and Position Mutation
```

That milestone would connect active entities to cells and maintain canonical per-cell entity indexes, while still excluding pathfinding, reservations, and jobs.
