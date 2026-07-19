using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.State;

public sealed class EntityCleanupHookRegistry
{
    private readonly IEntityCleanupHook[] _hooks;

    public EntityCleanupHookRegistry(IEnumerable<IEntityCleanupHook>? hooks = null)
    {
        _hooks = (hooks ?? Array.Empty<IEntityCleanupHook>())
            .Select(hook => hook
                ?? throw new ArgumentException(
                    "Cleanup hooks cannot contain null entries.",
                    nameof(hooks)))
            .OrderBy(static hook => hook.Order)
            .ThenBy(static hook => hook.Id, StringComparer.Ordinal)
            .ToArray();

        for (int index = 0; index < _hooks.Length; index++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(_hooks[index].Id);
            if (index > 0
                && StringComparer.Ordinal.Equals(_hooks[index - 1].Id, _hooks[index].Id))
            {
                throw new InvalidOperationException(
                    $"Duplicate entity cleanup hook ID {_hooks[index].Id}.");
            }
        }
    }

    public IReadOnlyList<IEntityCleanupHook> Hooks => _hooks;

    internal void Cleanup(EntityId entityId, WorldState world)
    {
        foreach (IEntityCleanupHook hook in _hooks)
        {
            hook.Cleanup(entityId, world);
        }
    }
}
