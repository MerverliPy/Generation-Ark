using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Diagnostics;

public static class DesyncDetector
{
    public static DesyncReport? FindFirstDivergence(
        IEnumerable<TickChecksum> expected,
        IEnumerable<TickChecksum> actual,
        IEnumerable<SimulationTraceEntry>? expectedTrace = null,
        IEnumerable<SimulationTraceEntry>? actualTrace = null,
        DesyncMetadata? expectedMetadata = null,
        DesyncMetadata? actualMetadata = null)
    {
        TickChecksum[] expectedItems = ValidateCheckpoints(expected, nameof(expected));
        TickChecksum[] actualItems = ValidateCheckpoints(actual, nameof(actual));
        IReadOnlyDictionary<long, SimulationTraceEntry> expectedTraceByTick =
            IndexTrace(expectedTrace, nameof(expectedTrace));
        IReadOnlyDictionary<long, SimulationTraceEntry> actualTraceByTick =
            IndexTrace(actualTrace, nameof(actualTrace));

        int expectedIndex = 0;
        int actualIndex = 0;
        while (expectedIndex < expectedItems.Length
            || actualIndex < actualItems.Length)
        {
            TickChecksum? expectedItem = expectedIndex < expectedItems.Length
                ? expectedItems[expectedIndex]
                : null;
            TickChecksum? actualItem = actualIndex < actualItems.Length
                ? actualItems[actualIndex]
                : null;

            if (expectedItem is not null
                && actualItem is not null
                && expectedItem.Tick == actualItem.Tick)
            {
                if (!Equivalent(expectedItem, actualItem))
                {
                    return CreateReport(
                        expectedItem.Tick,
                        expectedItem,
                        actualItem,
                        expectedTraceByTick,
                        actualTraceByTick,
                        expectedMetadata,
                        actualMetadata);
                }

                expectedIndex++;
                actualIndex++;
                continue;
            }

            if (actualItem is null
                || (expectedItem is not null && expectedItem.Tick < actualItem.Tick))
            {
                return CreateReport(
                    expectedItem!.Tick,
                    expectedItem,
                    actual: null,
                    expectedTraceByTick,
                    actualTraceByTick,
                    expectedMetadata,
                    actualMetadata);
            }

            return CreateReport(
                actualItem.Tick,
                expected: null,
                actualItem,
                expectedTraceByTick,
                actualTraceByTick,
                expectedMetadata,
                actualMetadata);
        }

        return null;
    }

    private static TickChecksum[] ValidateCheckpoints(
        IEnumerable<TickChecksum> checkpoints,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(checkpoints, parameterName);
        var items = new List<TickChecksum>();
        foreach (TickChecksum? checkpoint in checkpoints)
        {
            if (checkpoint is null)
            {
                throw new ArgumentException(
                    "Checkpoint collections cannot contain null entries.",
                    parameterName);
            }

            if (items.Count > 0 && items[^1].Tick >= checkpoint.Tick)
            {
                throw new ArgumentException(
                    "Checkpoint ticks must be strictly increasing and unique.",
                    parameterName);
            }

            items.Add(checkpoint);
        }

        return items.ToArray();
    }

    private static IReadOnlyDictionary<long, SimulationTraceEntry> IndexTrace(
        IEnumerable<SimulationTraceEntry>? entries,
        string parameterName)
    {
        var result = new SortedDictionary<long, SimulationTraceEntry>();
        if (entries is null)
        {
            return result;
        }

        foreach (SimulationTraceEntry? entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry, parameterName);
            if (!result.TryAdd(entry.Tick.Value, entry))
            {
                throw new ArgumentException(
                    $"Trace contains duplicate tick {entry.Tick}.",
                    parameterName);
            }
        }

        return result;
    }

    private static bool Equivalent(TickChecksum expected, TickChecksum actual)
        => expected.FormatVersion == actual.FormatVersion
            && expected.GlobalChecksum == actual.GlobalChecksum
            && ComponentDifferences(expected, actual).Length == 0;

    private static DesyncReport CreateReport(
        SimTick tick,
        TickChecksum? expected,
        TickChecksum? actual,
        IReadOnlyDictionary<long, SimulationTraceEntry> expectedTrace,
        IReadOnlyDictionary<long, SimulationTraceEntry> actualTrace,
        DesyncMetadata? expectedMetadata,
        DesyncMetadata? actualMetadata)
    {
        SimulationTraceEntry? expectedEntry = expectedTrace.TryGetValue(
            tick.Value,
            out SimulationTraceEntry? foundExpected)
            ? foundExpected
            : null;
        SimulationTraceEntry? actualEntry = actualTrace.TryGetValue(
            tick.Value,
            out SimulationTraceEntry? foundActual)
            ? foundActual
            : null;
        return new DesyncReport(
            tick,
            expected,
            actual,
            ComponentDifferences(expected, actual),
            expectedEntry,
            actualEntry,
            expectedMetadata,
            actualMetadata);
    }

    private static DesyncComponentDifference[] ComponentDifferences(
        TickChecksum? expected,
        TickChecksum? actual)
    {
        var expectedById = expected?.Components.ToDictionary(
            static item => item.ComponentId,
            static item => item.Checksum)
            ?? new Dictionary<ChecksumComponentId, ulong>();
        var actualById = actual?.Components.ToDictionary(
            static item => item.ComponentId,
            static item => item.Checksum)
            ?? new Dictionary<ChecksumComponentId, ulong>();

        return expectedById.Keys
            .Concat(actualById.Keys)
            .Distinct()
            .OrderBy(static id => id)
            .Select(id =>
            {
                bool hasExpected = expectedById.TryGetValue(id, out ulong expectedValue);
                bool hasActual = actualById.TryGetValue(id, out ulong actualValue);
                return new DesyncComponentDifference(
                    id,
                    hasExpected ? expectedValue : null,
                    hasActual ? actualValue : null);
            })
            .Where(static difference =>
                difference.ExpectedChecksum != difference.ActualChecksum)
            .ToArray();
    }
}
