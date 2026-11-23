using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

/// <summary>Ordered inputs and receipts for the authored local game. Controller identities are
/// session capabilities. The log retains their role and the command or interaction identity.</summary>
public abstract record LocalReplayStep
{
    private protected LocalReplayStep() { }

    public sealed record Submit(ControllerKind Controller, CommandRequest Request, CommandId Command,
        CommandStatus Status, string Code) : LocalReplayStep;
    public sealed record Reply(ControllerKind Controller, InteractionId Interaction, int Choice,
        InputReceipt Receipt) : LocalReplayStep;
    public sealed record TextReply(ControllerKind Controller, InteractionId Interaction, int Choice,
        string Text, InputReceipt Receipt) : LocalReplayStep;
    public sealed record Cancel(ControllerKind Controller, CommandId Command, InputReceipt Receipt) : LocalReplayStep;
    public sealed record Advance(CommandId Command, CommandStatus Status, string Code,
        InteractionId? Interaction, int Effects) : LocalReplayStep;
    public sealed record Retire(CommandId Command) : LocalReplayStep;
    public sealed record Save(string Sha256) : LocalReplayStep;
    public sealed record Load(OwnedList<byte> Bytes, string Status) : LocalReplayStep;
}

public sealed record LocalReplayTape(string ScriptSha256, OwnedList<LocalReplayStep> Steps);
