using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.Core;

public sealed class SimSystemRegistry
{
    private readonly IReadOnlyDictionary<SimPhase, IReadOnlyList<ISimSystem>> _systemsByPhase;

    public SimSystemRegistry(IEnumerable<ISimSystem> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);

        ISimSystem[] materialized = systems.ToArray();
        Validate(materialized);

        _systemsByPhase = materialized
            .OrderBy(system => SimPhaseOrder.IndexOf(system.Phase))
            .ThenBy(system => system.Order)
            .ThenBy(system => system.Id)
            .GroupBy(system => system.Phase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ISimSystem>)group.ToArray());
    }

    public IReadOnlyList<ISimSystem> ForPhase(SimPhase phase)
        => _systemsByPhase.TryGetValue(phase, out IReadOnlyList<ISimSystem>? systems)
            ? systems
            : Array.Empty<ISimSystem>();

    private static void Validate(IReadOnlyList<ISimSystem> systems)
    {
        var IDs = new HashSet<SystemId>();
        var keys = new HashSet<SystemOrderingKey>();

        foreach (ISimSystem system in systems)
        {
            ArgumentNullException.ThrowIfNull(system);
            _ = SimPhaseOrder.IndexOf(system.Phase);

            if (!IDs.Add(system.Id))
            {
                throw new InvalidOperationException($"Duplicate system ID: {system.Id}.");
            }

            var key = new SystemOrderingKey(system.Phase, system.Order, system.Id);
            if (!keys.Add(key))
            {
                throw new InvalidOperationException($"Duplicate system ordering key: {key}.");
            }
        }
    }

    private readonly record struct SystemOrderingKey(SimPhase Phase, int Order, SystemId Id);
}
