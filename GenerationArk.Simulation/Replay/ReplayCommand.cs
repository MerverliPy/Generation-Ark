using System;

namespace GenerationArk.Simulation.Replay;

/// <summary>
/// Serializable accepted-command record used by deterministic replay.
/// AcceptedTick controls when the command is re-submitted; TargetTick, Sequence,
/// and CommandId control authoritative application order inside the simulation.
/// </summary>
public sealed class ReplayCommand
{
    public ReplayCommand(
        long acceptedTick,
        long targetTick,
        long sequence,
        string commandId,
        string commandType,
        string payloadBase64)
    {
        if (acceptedTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(acceptedTick));
        }
        if (targetTick <= acceptedTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetTick),
                targetTick,
                "A replay command must target a tick after its acceptance boundary.");
        }
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }
        if (string.IsNullOrWhiteSpace(commandId))
        {
            throw new ArgumentException("Command ID is required.", nameof(commandId));
        }
        if (string.IsNullOrWhiteSpace(commandType))
        {
            throw new ArgumentException("Command type is required.", nameof(commandType));
        }
        if (payloadBase64 is null)
        {
            throw new ArgumentNullException(nameof(payloadBase64));
        }

        try
        {
            _ = Convert.FromBase64String(payloadBase64);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "Replay command payload must be valid Base64.",
                nameof(payloadBase64),
                exception);
        }

        AcceptedTick = acceptedTick;
        TargetTick = targetTick;
        Sequence = sequence;
        CommandId = commandId;
        CommandType = commandType;
        PayloadBase64 = payloadBase64;
    }

    public long AcceptedTick { get; }

    public long TargetTick { get; }

    public long Sequence { get; }

    public string CommandId { get; }

    public string CommandType { get; }

    public string PayloadBase64 { get; }

    public byte[] DecodePayload()
        => Convert.FromBase64String(PayloadBase64);
}
