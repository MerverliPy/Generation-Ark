using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Diagnostics;

public sealed class TickChecksum
{
    private readonly ComponentChecksum[] _components;

    public TickChecksum(
        SimTick tick,
        ChecksumFormatVersion formatVersion,
        ulong globalChecksum,
        IEnumerable<ComponentChecksum> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        Tick = tick;
        FormatVersion = formatVersion;
        GlobalChecksum = globalChecksum;
        _components = components
            .OrderBy(static item => item.ComponentId)
            .ToArray();

        for (int index = 1; index < _components.Length; index++)
        {
            if (_components[index - 1].ComponentId == _components[index].ComponentId)
            {
                throw new InvalidOperationException(
                    $"Duplicate checksum component ID: {_components[index].ComponentId}.");
            }
        }
    }

    public SimTick Tick { get; }
    public ChecksumFormatVersion FormatVersion { get; }
    public ulong GlobalChecksum { get; }
    public IReadOnlyList<ComponentChecksum> Components => _components;

    public bool TryGetComponent(
        ChecksumComponentId componentId,
        out ComponentChecksum component)
    {
        foreach (ComponentChecksum item in _components)
        {
            if (item.ComponentId == componentId)
            {
                component = item;
                return true;
            }
        }

        component = default;
        return false;
    }
}
