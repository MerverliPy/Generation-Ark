using GenerationArk.Simulation.Core;

namespace GenerationArk.Simulation.Tests;

internal sealed class IncrementSystem : ISimSystem
{
    private readonly string _counter;
    private readonly long _amount;

    public IncrementSystem(string id, SimPhase phase, int order, string counter, long amount)
    {
        Id = new SystemId(id);
        Phase = phase;
        Order = order;
        _counter = counter;
        _amount = amount;
    }

    public SimPhase Phase { get; }
    public int Order { get; }
    public SystemId Id { get; }

    public void Tick(SimContext context)
    {
        context.World.AddToCounter(_counter, _amount);
        context.World.AppendTrace($"system:{Id}@{context.Tick}:{context.Phase}");
    }
}

internal sealed class OrderedMathSystem : ISimSystem
{
    private readonly long _multiplier;
    private readonly long _addition;

    public OrderedMathSystem(string id, int order, long multiplier, long addition)
    {
        Id = new SystemId(id);
        Order = order;
        _multiplier = multiplier;
        _addition = addition;
    }

    public SimPhase Phase => SimPhase.AgentState;
    public int Order { get; }
    public SystemId Id { get; }

    public void Tick(SimContext context)
    {
        const long modulus = 1_000_000_007L;

        long current = context.World.GetCounter("ordered");
        long next = checked(current * _multiplier + _addition) % modulus;

        context.World.SetCounter("ordered", next);
        context.World.AppendTrace($"system:{Id}@{context.Tick}:{context.Phase}");
    }
}
