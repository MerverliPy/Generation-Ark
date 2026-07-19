namespace GenerationArk.Simulation.Core;

public enum SimPhase : byte
{
    CommandApply = 0,
    PreSimulation = 10,
    ShipInfrastructure = 20,
    ResourceNetworks = 30,
    AgentState = 40,
    AgentDecision = 50,
    AgentAction = 60,
    SocialAndInstitutions = 70,
    Narrative = 80,
    Commit = 90,
    Diagnostics = 100
}
