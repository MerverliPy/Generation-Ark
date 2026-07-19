using System;
using System.Collections.Generic;
using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Commands;

public sealed class SimCommandQueue
{
    private readonly SortedSet<QueuedCommand> _pending = new(QueuedCommandComparer.Instance);
    private readonly List<CommandResult> _results = new();

    public IReadOnlyList<CommandResult> Results => _results;
    public int PendingCount => _pending.Count;

    public void Enqueue(ISimCommand command, SimTick currentTick)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.TargetTick <= currentTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                command.TargetTick,
                "Commands must target a future tick.");
        }

        if (!_pending.Add(new QueuedCommand(command)))
        {
            throw new InvalidOperationException($"Duplicate command ordering key for command {command.Id}.");
        }
    }

    public void ApplyForTick(SimContext context)
    {
        while (_pending.Count > 0)
        {
            QueuedCommand next = _pending.Min;
            if (next.Command.TargetTick > context.Tick)
            {
                return;
            }

            _pending.Remove(next);

            if (next.Command.TargetTick < context.Tick)
            {
                throw new InvalidOperationException(
                    $"Command {next.Command.Id} became past due at tick {context.Tick}.");
            }

            CommandValidation validation = next.Command.Validate(context.World);
            if (!validation.IsValid)
            {
                _results.Add(new CommandResult(
                    next.Command.Id,
                    context.Tick,
                    false,
                    validation.Reason));
                continue;
            }

            next.Command.Apply(context);
            _results.Add(new CommandResult(next.Command.Id, context.Tick, true, null));
        }
    }

    private readonly record struct QueuedCommand(ISimCommand Command);

    private sealed class QueuedCommandComparer : IComparer<QueuedCommand>
    {
        public static QueuedCommandComparer Instance { get; } = new();

        public int Compare(QueuedCommand x, QueuedCommand y)
        {
            int byTick = x.Command.TargetTick.CompareTo(y.Command.TargetTick);
            if (byTick != 0)
            {
                return byTick;
            }

            int bySequence = x.Command.Sequence.CompareTo(y.Command.Sequence);
            if (bySequence != 0)
            {
                return bySequence;
            }

            return x.Command.Id.CompareTo(y.Command.Id);
        }
    }
}
