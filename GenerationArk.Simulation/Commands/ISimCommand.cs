using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.State;

namespace GenerationArk.Simulation.Commands;

public interface ISimCommand
{
    CommandId Id { get; }
    SimTick TargetTick { get; }
    long Sequence { get; }

    CommandValidation Validate(WorldState world);
    void Apply(SimContext context);
}
