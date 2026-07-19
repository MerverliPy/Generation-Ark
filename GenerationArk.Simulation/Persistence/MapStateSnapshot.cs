using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationArk.Simulation.Persistence;

public sealed class MapStateSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public MapStateSnapshot(
        int schemaVersion,
        int width,
        int height,
        IEnumerable<MapCellStateSnapshot> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        SchemaVersion = schemaVersion;
        Width = width;
        Height = height;
        Cells = cells
            .Select(cell => cell
                ?? throw new ArgumentException(
                    "Map snapshots cannot contain null cell entries.",
                    nameof(cells)))
            .ToArray();
    }

    public int SchemaVersion { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<MapCellStateSnapshot> Cells { get; }
}
