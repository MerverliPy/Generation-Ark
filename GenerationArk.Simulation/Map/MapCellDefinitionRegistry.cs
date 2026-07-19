using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.Map;

public sealed class MapCellDefinitionRegistry
{
    private readonly SortedDictionary<MapCellDefinitionId, MapCellDefinition> _definitions = new();

    public MapCellDefinitionRegistry(IEnumerable<MapCellDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (MapCellDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (!_definitions.TryAdd(definition.Id, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate map-cell definition ID {definition.Id}.");
            }
        }

        if (_definitions.Count == 0)
        {
            throw new ArgumentException(
                "At least one map-cell definition is required.",
                nameof(definitions));
        }
    }

    public int Count => _definitions.Count;

    public IReadOnlyList<MapCellDefinition> Definitions => _definitions.Values.ToArray();

    public bool Contains(MapCellDefinitionId definitionId)
        => _definitions.ContainsKey(definitionId);

    public MapCellDefinition Get(MapCellDefinitionId definitionId)
        => _definitions.TryGetValue(definitionId, out MapCellDefinition? definition)
            ? definition
            : throw new InvalidOperationException(
                $"Unknown map-cell definition ID {definitionId}.");
}
