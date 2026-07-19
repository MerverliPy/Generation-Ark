using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Random;

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

internal sealed class ScheduleDuringPhaseSystem : ISimSystem
{
    private readonly SimPhase _targetPhase;
    private readonly string _payload;
    private readonly string _guardCounter;

    public ScheduleDuringPhaseSystem(
        string id,
        SimPhase sourcePhase,
        int order,
        SimPhase targetPhase,
        string payload)
    {
        Id = new SystemId(id);
        Phase = sourcePhase;
        Order = order;
        _targetPhase = targetPhase;
        _payload = payload;
        _guardCounter = $"scheduled:{id}";
    }

    public SimPhase Phase { get; }
    public int Order { get; }
    public SystemId Id { get; }

    public void Tick(SimContext context)
    {
        if (context.World.GetCounter(_guardCounter) != 0)
        {
            return;
        }

        context.World.SetCounter(_guardCounter, 1);
        context.Scheduler.ScheduleAt(
            context.Tick,
            _targetPhase,
            priority: 0,
            eventData: new GenerationArk.Simulation.Scheduling.ScheduledEventData(
                new GenerationArk.Simulation.Scheduling.ScheduledEventTypeId("test.trace"),
                owner: null,
                payload: _payload));
    }
}

internal sealed class RandomDrawingSystem : ISimSystem
{
    private static readonly RandomDomainId Domain = new("test.diagnostics");

    public SimPhase Phase => SimPhase.AgentDecision;
    public int Order => 0;
    public SystemId Id { get; } = new("test.random-draw");

    public void Tick(SimContext context)
    {
        var scope = new RandomScope(
            Domain,
            Owner: 11,
            Purpose: 22,
            Occurrence: unchecked((ulong)context.Tick.Value));
        ulong value = context.Random.UInt64(scope, counter: 0);
        context.World.SetCounter("random-low", unchecked((long)value));
    }
}
