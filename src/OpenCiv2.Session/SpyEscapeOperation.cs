using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V06 escape followed by the actual incident, suspended for the human victim's pretext.
/// The owning mission supplies victim routing; no public player command bypasses admission.</summary>
public sealed class SpyEscapeOperation(CivilizationElimination lifecycle, EspionageIncidents incidents,
    int slot, int mode, int victim, string call, Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private readonly CivilizationElimination.Operation operation = lifecycle.BeginEscape(slot, mode, victim, call, incidents);
    private LifecycleDecision? pending;
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed lifecycle presentation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        var reply = LifecycleInteraction.ResearchReply(pending, input);
        var previous = pending;
        pending = operation.ResumeDecision(reply);
        if (!ReferenceEquals(previous, pending))
        { intelligenceWindow = null; }
        if (pending is { } decision)
        {
            context.Publish();
            return new(CommandStatus.AwaitingInteraction, Interaction: LifecycleInteraction.Prompt(context, decision, recipient, ref intelligenceWindow, intelligence));
        }
        if (operation.Result is null)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
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
