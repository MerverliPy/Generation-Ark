namespace GenerationArk.Simulation.Random;

/// <summary>
/// Stable serialized random-domain identifiers. Values are save/replay format data.
/// </summary>
public static class RandomDomains
{
    public static RandomDomainId ScenarioGeneration => new("scenario-generation");
    public static RandomDomainId ColonistGeneration => new("colonist-generation");
    public static RandomDomainId HouseholdFormation => new("household-formation");
    public static RandomDomainId BirthOutcomes => new("birth-outcomes");
    public static RandomDomainId HealthEvents => new("health-events");
    public static RandomDomainId JobSelectionTieBreaking => new("job-selection-tie-breaking");
    public static RandomDomainId EquipmentFailures => new("equipment-failures");
    public static RandomDomainId IncidentSelection => new("incident-selection");
    public static RandomDomainId IncidentConsequences => new("incident-consequences");
    public static RandomDomainId InstitutionalSuccession => new("institutional-succession");
    public static RandomDomainId CulturalChange => new("cultural-change");
}
