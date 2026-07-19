using System.Collections.Generic;
using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Diagnostics;

public sealed record DesyncReport(
    SimTick FirstDivergentTick,
    TickChecksum? Expected,
    TickChecksum? Actual,
    IReadOnlyList<DesyncComponentDifference> ComponentDifferences,
    SimulationTraceEntry? ExpectedTrace,
    SimulationTraceEntry? ActualTrace,
    DesyncMetadata? ExpectedMetadata,
    DesyncMetadata? ActualMetadata);
