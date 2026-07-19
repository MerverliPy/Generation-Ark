#!/usr/bin/env python3
"""Behavioral and static reference for Step 10 deterministic grid and room topology."""

from __future__ import annotations

import json
import re
import sys
from collections import deque
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]

REQUIRED_FILES = (
    "GenerationArk.Simulation/Map/GridPosition.cs",
    "GenerationArk.Simulation/Map/MapCellId.cs",
    "GenerationArk.Simulation/Map/MapCellDefinitionId.cs",
    "GenerationArk.Simulation/Map/MapCellDefinition.cs",
    "GenerationArk.Simulation/Map/MapCellDefinitionRegistry.cs",
    "GenerationArk.Simulation/Map/MapCellMutation.cs",
    "GenerationArk.Simulation/Map/MapCellMutationComparer.cs",
    "GenerationArk.Simulation/Map/MapMutationValidationException.cs",
    "GenerationArk.Simulation/Map/MapState.cs",
    "GenerationArk.Simulation/Map/RoomId.cs",
    "GenerationArk.Simulation/Map/RoomTopology.cs",
    "GenerationArk.Simulation/Map/RoomTopologyBuilder.cs",
    "GenerationArk.Simulation/Persistence/MapCellStateSnapshot.cs",
    "GenerationArk.Simulation/Persistence/MapStateSnapshot.cs",
    "GenerationArk.Simulation/Persistence/MapStateSerializer.cs",
    "GenerationArk.Simulation.Tests/MapTopologyMilestoneTests.cs",
)
REQUIRED_TESTS = (
    "CellIdsUseCanonicalRowMajorCoordinates",
    "InvalidGridDimensionsAndCoordinatesFailFast",
    "CellIterationIsCanonicalRegardlessOfWriteOrder",
    "DuplicateMapCellDefinitionIdsFailFast",
    "MapMutationsRemainInvisibleUntilCommit",
    "ConflictingMapMutationBatchFailsBeforePartialApplication",
    "RoomTopologyUsesCardinalConnectivityAndStableRoomIds",
    "RoomTopologySplitAndMergeRebuildsDeterministically",
    "MapStateSaveLoadRoundTripIsCanonical",
    "MapReplayFramePatternsAndTopologyChurnMatchChecksums",
)
FORBIDDEN = (
    "UnityEngine",
    "System.Random",
    "DateTime.Now",
    "DateTime.UtcNow",
    "Time.time",
    "Time.deltaTime",
    "Time.fixedDeltaTime",
    "GetHashCode()",
)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"FAIL: {message}")


@dataclass(frozen=True, order=True)
class CellId:
    value: int

    @staticmethod
    def from_position(x: int, y: int, width: int, height: int) -> "CellId":
        if width <= 0 or height <= 0:
            raise ValueError("dimensions must be positive")
        if not (0 <= x < width and 0 <= y < height):
            raise ValueError("position out of bounds")
        value = y * width + x
        if value > 2_147_483_647:
            raise OverflowError("cell id overflow")
        return CellId(value)

    def to_position(self, width: int, height: int) -> tuple[int, int]:
        if width <= 0 or height <= 0:
            raise ValueError("dimensions must be positive")
        count = width * height
        if not (0 <= self.value < count):
            raise ValueError("cell id out of bounds")
        return self.value % width, self.value // width


def build_topology(
    width: int,
    height: int,
    definitions: list[int],
    topology_ids: set[int],
) -> tuple[tuple[int, tuple[int, ...]], ...]:
    count = width * height
    if len(definitions) != count:
        raise ValueError("wrong cell count")
    visited = [False] * count
    rooms: list[tuple[int, tuple[int, ...]]] = []
    for seed in range(count):
        if visited[seed] or definitions[seed] not in topology_ids:
            continue
        visited[seed] = True
        queue: deque[int] = deque([seed])
        members: list[int] = []
        while queue:
            current = queue.popleft()
            members.append(current)
            x, y = current % width, current // width
            for nx, ny in ((x, y - 1), (x + 1, y), (x, y + 1), (x - 1, y)):
                if not (0 <= nx < width and 0 <= ny < height):
                    continue
                neighbor = ny * width + nx
                if visited[neighbor] or definitions[neighbor] not in topology_ids:
                    continue
                visited[neighbor] = True
                queue.append(neighbor)
        canonical = tuple(sorted(members))
        rooms.append((canonical[0], canonical))
    return tuple(sorted(rooms))


def canonical_json(width: int, height: int, cells: list[tuple[int, int]]) -> str:
    return json.dumps(
        {
            "schemaVersion": 1,
            "width": width,
            "height": height,
            "cells": [
                {"cellId": cell_id, "definitionId": definition_id}
                for cell_id, definition_id in sorted(cells)
            ],
        },
        separators=(",", ":"),
    )


def static_checks() -> None:
    for relative in REQUIRED_FILES:
        require((ROOT / relative).is_file(), f"missing required file {relative}")

    program = (ROOT / "GenerationArk.Simulation.Tests/Program.cs").read_text(encoding="utf-8")
    tests = (ROOT / "GenerationArk.Simulation.Tests/MapTopologyMilestoneTests.cs").read_text(encoding="utf-8")
    runner = (ROOT / "GenerationArk.Simulation/Core/SimulationRunner.cs").read_text(encoding="utf-8")
    world = (ROOT / "GenerationArk.Simulation/State/WorldState.cs").read_text(encoding="utf-8")
    mutation = (ROOT / "GenerationArk.Simulation/State/MutationBuffer.cs").read_text(encoding="utf-8")
    checksum = (ROOT / "GenerationArk.Simulation/Diagnostics/StateChecksum.cs").read_text(encoding="utf-8")
    checksum_version = (ROOT / "GenerationArk.Simulation/Diagnostics/ChecksumFormatVersion.cs").read_text(encoding="utf-8")
    map_state = (ROOT / "GenerationArk.Simulation/Map/MapState.cs").read_text(encoding="utf-8")
    serializer = (ROOT / "GenerationArk.Simulation/Persistence/MapStateSerializer.cs").read_text(encoding="utf-8")

    registrations = re.findall(r"^\s*\(nameof\(([^)]+)\)", program, re.MULTILINE)
    require(len(registrations) == 72, "Program.cs must register exactly 72 tests")
    require(len(set(registrations)) == 72, "Program.cs test registrations must be unique")
    require(tuple(item.rsplit(".", 1)[-1] for item in registrations[-10:]) == REQUIRED_TESTS,
            "the final ten registrations must match the Step 10 contract exactly")
    for name in REQUIRED_TESTS:
        require(tests.count(f"void {name}(") == 1, f"test method {name} is missing or duplicated")
        require(program.count(f"MapTopologyMilestoneTests.{name}") == 2,
                f"test registration {name} is missing or duplicated")

    require("Current => new(3)" in checksum_version,
            "checksum format version must advance to 3")
    require('new("map-topology")' in checksum and "world.Map.WriteChecksum" in checksum,
            "map-specific global and component checksum integration is missing")
    require("CommitMutations" in runner and "phase == SimPhase.Commit" in runner,
            "existing Commit phase is not applying shared structural mutations")
    require("public MapState Map" in world and "Map.ValidateInvariants()" in world,
            "WorldState map integration or invariant validation is missing")
    require("EnqueueSetCellDefinition" in mutation and "world.Map.ValidateMutationBatch" in mutation
            and "world.Map.ApplyValidatedMutations" in mutation,
            "map changes are not using the existing validated mutation authority")
    require("RoomTopologyBuilder.Build(this)" in map_state,
            "topology is not reconstructed from authoritative cell state")
    require('writer.WriteNumber("schemaVersion"' in serializer
            and 'writer.WriteNumber("width"' in serializer
            and 'writer.WriteNumber("height"' in serializer
            and '.OrderBy(static item => item.CellId)' in serializer,
            "canonical map serialization fields or ordering are missing")

    scoped_files = [
        *(ROOT / "GenerationArk.Simulation/Map").glob("*.cs"),
        *(ROOT / "GenerationArk.Simulation/Persistence").glob("Map*.cs"),
    ]
    for path in scoped_files:
        text = path.read_text(encoding="utf-8")
        for token in FORBIDDEN:
            require(token not in text, f"forbidden token {token!r} in {path.relative_to(ROOT)}")
        require(re.search(r"\b(?:float|double)\b", text) is None,
                f"authoritative floating-point type in {path.relative_to(ROOT)}")

    direct_writes = re.findall(r"_cellDefinitions\[[^]]+\]\s*=", map_state)
    require(len(direct_writes) == 2,
            "cell storage writes must remain limited to restore construction and validated Commit application")
    require("ApplyValidatedMutations" in map_state,
            "validated Commit application method is missing")
    print("PASS step10-static-integration")


def main() -> int:
    static_checks()

    assert CellId.from_position(2, 1, 4, 3) == CellId(6)
    assert CellId(11).to_position(4, 3) == (3, 2)
    print("PASS row-major-coordinate-round-trip")

    definitions = [0] * 9
    for cell in (0, 4, 5, 8):
        definitions[cell] = 1
    assert build_topology(3, 3, definitions, {1}) == (
        (0, (0,)),
        (4, (4, 5, 8)),
    )
    print("PASS cardinal-connectivity-and-stable-room-ids")

    full = [1] * 5
    assert build_topology(5, 1, full, {1}) == ((0, (0, 1, 2, 3, 4)),)
    split = full.copy()
    split[2] = 0
    assert build_topology(5, 1, split, {1}) == ((0, (0, 1)), (3, (3, 4)))
    split[2] = 1
    assert build_topology(5, 1, split, {1}) == ((0, (0, 1, 2, 3, 4)),)
    print("PASS deterministic-split-and-merge")

    before = [0, 0, 0]
    batch = [(0, 1), (1, 1), (1, 2)]
    targets = [cell for cell, _ in batch]
    try:
        if len(targets) != len(set(targets)):
            raise ValueError("conflicting cell writes")
        after = before.copy()
        for cell, definition in sorted(batch):
            after[cell] = definition
    except ValueError:
        after = before
    assert after == before
    print("PASS conflicting-batch-is-atomic")

    cells = [(3, 1), (0, 0), (2, 1), (1, 0)]
    assert canonical_json(2, 2, cells) == canonical_json(2, 2, list(reversed(cells)))
    print("PASS canonical-map-serialization")

    print("PASS: Step 10 map/topology behavioral reference")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
