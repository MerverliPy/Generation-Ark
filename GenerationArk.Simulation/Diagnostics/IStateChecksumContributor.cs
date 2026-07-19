namespace GenerationArk.Simulation.Diagnostics;

public interface IStateChecksumContributor
{
    ChecksumComponentId ComponentId { get; }

    void Write(StateChecksumWriter writer);
}
