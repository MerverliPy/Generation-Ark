using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GenerationArk.Simulation.Replay;

public sealed class FramePattern
{
    private readonly ReadOnlyCollection<FramePatternStep> _steps;

    public FramePattern(string name, IEnumerable<FramePatternStep> steps)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Frame pattern name is required.", nameof(name));
        }
        if (steps is null)
        {
            throw new ArgumentNullException(nameof(steps));
        }

        FramePatternStep[] stepArray = steps.ToArray();
        if (stepArray.Length == 0)
        {
            throw new ArgumentException("Frame pattern must contain at least one step.", nameof(steps));
        }

        Name = name;
        _steps = Array.AsReadOnly(stepArray);
    }

    public string Name { get; }

    public IReadOnlyList<FramePatternStep> Steps => _steps;
}
