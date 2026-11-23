using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V06 escape followed by the actual incident, suspended for the human victim's pretext.
/// The owning mission supplies victim routing; no public player command bypasses admission.</summary>
public sealed class SpyEscapeOperation(CivilizationElimination lifecycle, EspionageIncidents incidents,
    int slot, int mode, int victim, string call, Func<int, ParticipantId> recipient) : IContinuation
{
    private readonly CivilizationElimination.Operation operation = lifecycle.BeginEscape(slot, mode, victim, call, incidents);
    private EspionagePretext? pending;
    private OperationStep? completed;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        int? reply = pending is null ? null : EspionageInteraction.Reply(input);
        try
        { pending = operation.Resume(reply); }
        catch { context.Publish(); throw; }
        if (pending is { } decision)
        {
            context.Publish();
            return new(CommandStatus.AwaitingInteraction, Interaction: EspionageInteraction.Prompt(decision, recipient(decision.Victim.Civilization)));
        }
        if (operation.Result is null)
        { context.Publish(); return new(CommandStatus.Pending); }
        var result = new OwnedList<NamedValue>([new("lost", new CommandValue.Boolean(operation.Result == 1))]);
        context.Effect("espionage.escape.completed", result);
        return completed = new(Result: result);
    }

    public void Dispose() => operation.Dispose();
}

internal static class EspionageInteraction
{
    internal static int? Reply(OperationInput input) => input.Kind switch
    {
        OperationInputKind.Choice => input.Choice,
        OperationInputKind.Cancel or OperationInputKind.Timeout => 0,
        _ => null
    };

    internal static InteractionSpec Prompt(EspionagePretext decision, ParticipantId? recipient = null)
    {
        string response = decision.Allied ? "Cancel the alliance" : "Declare war";
        return new($"Espionage incident\n{decision.Actor.Plural} agents were caught acting against the {decision.Victim.Plural}.\n{response}?",
            recipient, null, new([0, 1]))
        { Options = new([new(0, "Decline"), new(1, response)]) };
    }
}
