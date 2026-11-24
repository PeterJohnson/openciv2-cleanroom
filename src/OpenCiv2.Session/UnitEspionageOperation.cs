using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Admitted V06 mission child. The owning movement/event caller supplies participant routing;
/// this continuation is not a public command that bypasses movement or target admission.</summary>
public sealed class UnitEspionageOperation(UnitEspionage.Operation operation, Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private UnitMissionDecision? pending;
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
        var reply = UnitMissionInteraction.Reply(pending, input);
        var previous = pending;
        pending = operation.ResumeDecision(pending is null ? null : reply);
        if (!ReferenceEquals(previous, pending))
        { intelligenceWindow = null; }
        if (pending is not null)
        {
            context.Publish();
            var prompt = UnitMissionInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence);
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (operation.Result is null)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        var result = new OwnedList<NamedValue>([new("stop", new CommandValue.Boolean(operation.Result == 1))]);
        context.Effect("unit.mission.completed", result);
        return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
            : new(CommandStatus.Failed, Result: result, Code: "unit_espionage_presentation_failed");
    }

    public void Dispose() => operation.Dispose();
}

internal static class UnitMissionInteraction
{
    internal static ResearchReply? Reply(UnitMissionDecision? decision, OperationInput input)
    {
        if (decision is UnitMissionDecision.Lifecycle lifecycle)
        { return LifecycleInteraction.ResearchReply(lifecycle.Decision, input); }
        return LifecycleInteraction.Selection(input.Kind switch
        {
            OperationInputKind.Choice => input.Choice,
            OperationInputKind.Cancel or OperationInputKind.Timeout => decision is UnitMissionDecision.Menu ? -1 : 0,
            _ => null
        });
    }
    internal static InteractionSpec Prompt(OperationContext context, UnitMissionDecision decision, Func<int, ParticipantId> recipient, ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null)
    {
        return decision switch
        {
            UnitMissionDecision.Menu menu => new InteractionSpec($"Mission against {menu.Target.Name}", recipient(menu.Actor.Owner),
                new CommandValue.Unit(menu.Target.Unit), new([0, 1]))
            { Options = new([new(0, "Bribe"), new(1, "Sabotage")]) },
            UnitMissionDecision.Bribe bribe => new InteractionSpec($"Bribe {bribe.Offer.Target.Name} for {bribe.Offer.Price} gold?", recipient(bribe.Offer.Actor),
                new CommandValue.Unit(bribe.Offer.Target.Unit), bribe.Offer.Affordable ? new([0, 1]) : new([0]))
            { Options = bribe.Offer.Affordable ? new([new(0, "Decline"), new(1, "Pay")]) : new([new(0, "Cannot afford")]) },
            UnitMissionDecision.Lifecycle incident => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
            _ => throw new InvalidOperationException("Unknown unit mission decision")
        };
    }
}
