using System;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Commands;

/// <summary>Simple deterministic command used by the foundation test harness.</summary>
public sealed class AddCounterCommand : ISimCommand
{
    public AddCounterCommand(
        CommandId id,
        SimTick targetTick,
        long sequence,
        string counterKey,
        long delta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(counterKey);
        Id = id;
        TargetTick = targetTick;
        Sequence = sequence;
        CounterKey = counterKey;
        Delta = delta;
    }

    public CommandId Id { get; }
    public SimTick TargetTick { get; }
    public long Sequence { get; }
    public string CounterKey { get; }
    public long Delta { get; }

    public CommandValidation Validate(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return CommandValidation.Valid;
    }

    public void Apply(SimContext context)
    {
        context.World.AddToCounter(CounterKey, Delta);
        context.World.AppendTrace($"command:{Id}@{context.Tick}:{CounterKey}:{Delta}");
    }
}
