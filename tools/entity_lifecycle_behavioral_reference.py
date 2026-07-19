#!/usr/bin/env python3
"""Static behavioral-reference checks preserving Step 9 entity lifecycle under Step 10."""
from __future__ import annotations

from pathlib import Path
import re
import sys

ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]

REQUIRED_FILES = (
    "GenerationArk.Simulation/State/EntityId.cs",
    "GenerationArk.Simulation/State/EntityRegistry.cs",
    "GenerationArk.Simulation/State/EntityLifecycleState.cs",
    "GenerationArk.Simulation/State/ComponentTypeId.cs",
    "GenerationArk.Simulation/State/ComponentRegistration.cs",
    "GenerationArk.Simulation/State/ComponentRegistry.cs",
    "GenerationArk.Simulation/State/MutationBuffer.cs",
    "GenerationArk.Simulation/Persistence/EntityStateSerializer.cs",
    "GenerationArk.Simulation.Tests/EntityLifecycleMilestoneTests.cs",
)
REQUIRED_TESTS = (
    "EntityIdsAreMonotonicAndNeverReused",
    "EntityIterationIsCanonicalRegardlessOfInsertionOrder",
    "DuplicateEntityAndComponentTypeRegistrationFailsFast",
    "StructuralMutationsRemainInvisibleUntilCommit",
    "CreatedEntitiesActivateAtNextPreSimulationPhase",
    "CommitAppliesMutationsInStableBufferedOrder",
    "DestroyEntityRemovesComponentsAndCancelsOwnedEvents",
    "ConflictingMutationBatchFailsBeforePartialApplication",
    "EntityStateSaveLoadRoundTripIsCanonical",
    "EntityLifecycleReplayFramePatternsAndChurnSoakMatchChecksums",
)
FORBIDDEN = (
    "DateTime.Now",
    "DateTime.UtcNow",
    "Time.time",
    "Time.deltaTime",
    "Time.fixedDeltaTime",
    "System.Random",
    "UnityEngine.Random",
    "using UnityEngine",
    "GetHashCode()",
)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"FAIL: {message}")


for relative in REQUIRED_FILES:
    require((ROOT / relative).is_file(), f"missing required file {relative}")

program = (ROOT / "GenerationArk.Simulation.Tests/Program.cs").read_text(encoding="utf-8")
tests = (ROOT / "GenerationArk.Simulation.Tests/EntityLifecycleMilestoneTests.cs").read_text(encoding="utf-8")
runner = (ROOT / "GenerationArk.Simulation/Core/SimulationRunner.cs").read_text(encoding="utf-8")
checksum = (ROOT / "GenerationArk.Simulation/Diagnostics/StateChecksum.cs").read_text(encoding="utf-8")
checksum_version = (ROOT / "GenerationArk.Simulation/Diagnostics/ChecksumFormatVersion.cs").read_text(encoding="utf-8")
mutation = (ROOT / "GenerationArk.Simulation/State/MutationBuffer.cs").read_text(encoding="utf-8")
serializer = (ROOT / "GenerationArk.Simulation/Persistence/EntityStateSerializer.cs").read_text(encoding="utf-8")

require(len(re.findall(r"^\s*\(nameof\(", program, re.MULTILINE)) == 72,
        "Program.cs must register exactly 72 tests after Step 10 integration")
for name in REQUIRED_TESTS:
    require(tests.count(f"void {name}(") == 1, f"test method {name} is missing or duplicated")
    require(program.count(f"EntityLifecycleMilestoneTests.{name}") == 2,
            f"test registration {name} is missing or duplicated")

require("phase == SimPhase.PreSimulation" in runner and "ActivatePendingEntities" in runner,
        "PreSimulation activation hook is missing")
require("phase == SimPhase.Commit" in runner and "CommitMutations" in runner,
        "shared structural commit hook is missing")
require("ValidateEntityInvariants" in runner,
        "Diagnostics entity invariant validation is missing")
require('new("entities")' in checksum and "WriteEntityChecksum" in checksum,
        "entity checksum contributor is missing")
require("Current => new(3)" in checksum_version,
        "checksum format version must be 3 after map/topology integration")
require("ValidateBatch" in mutation and "FindConflicts" in mutation,
        "prevalidated mutation commit is missing")
require("retiredEntityIds" in serializer and "lifecycleState" in serializer,
        "canonical entity persistence fields are missing")

step9_files = [
    path for path in (ROOT / "GenerationArk.Simulation").rglob("*.cs")
    if path.name in {Path(item).name for item in REQUIRED_FILES}
    or path.parent.name in {"State", "Persistence"}
]
for path in step9_files:
    text = path.read_text(encoding="utf-8")
    for token in FORBIDDEN:
        require(token not in text, f"forbidden token {token!r} in {path.relative_to(ROOT)}")

print("PASS: Step 9 entity lifecycle invariants remain preserved under Step 10.")
