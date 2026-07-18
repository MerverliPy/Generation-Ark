namespace GenerationArk.Simulation.Commands;

public readonly record struct CommandValidation(bool IsValid, string? Reason)
{
    public static CommandValidation Valid => new(true, null);
    public static CommandValidation Invalid(string reason) => new(false, reason);
}
