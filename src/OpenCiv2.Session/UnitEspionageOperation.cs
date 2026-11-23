using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Admitted V06 mission child. The owning movement/event caller supplies participant routing;
/// this continuation is not a public command that bypasses movement or target admission.</summary>
public sealed class UnitEspionageOperation(UnitEspionage.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private UnitMissionDecision? pending;
    private OperationStep? completed;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        int? reply = UnitMissionInteraction.Reply(pending, input);
        try
        { pending = operation.Resume(pending is null ? null : reply); }
        catch { context.Publish(); throw; }
        if (pending is not null)
        {
            context.Publish();
            var prompt = UnitMissionInteraction.Prompt(pending, recipient);
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (operation.Result is null)
        { context.Publish(); return new(CommandStatus.Pending); }
        var result = new OwnedList<NamedValue>([new("stop", new CommandValue.Boolean(operation.Result == 1))]);
        context.Effect("unit.mission.completed", result);
        return completed = new(Result: result);
    }

    public void Dispose() => operation.Dispose();
}

internal static class UnitMissionInteraction
{
    internal static int? Reply(UnitMissionDecision? decision, OperationInput input)
    {
        return input.Kind switch
        {
            OperationInputKind.Choice => input.Choice,
            OperationInputKind.Cancel or OperationInputKind.Timeout => decision is UnitMissionDecision.Menu ? -1 : 0,
            _ => null
        };
    }
    internal static InteractionSpec Prompt(UnitMissionDecision decision, Func<int, ParticipantId> recipient)
    {
        return decision switch
        {
            UnitMissionDecision.Menu menu => new InteractionSpec($"Mission against {menu.Target.Name}", recipient(menu.Actor.Owner),
                new CommandValue.Unit(menu.Target.Unit), new([0, 1]))
            { Options = new([new(0, "Bribe"), new(1, "Sabotage")]) },
            UnitMissionDecision.Bribe bribe => new InteractionSpec($"Bribe {bribe.Offer.Target.Name} for {bribe.Offer.Price} gold?", recipient(bribe.Offer.Actor),
                new CommandValue.Unit(bribe.Offer.Target.Unit), bribe.Offer.Affordable ? new([0, 1]) : new([0]))
            { Options = bribe.Offer.Affordable ? new([new(0, "Decline"), new(1, "Pay")]) : new([new(0, "Cannot afford")]) },
            UnitMissionDecision.Incident incident => EspionageInteraction.Prompt(incident.Pretext, recipient(incident.Pretext.Victim.Civilization)),
            _ => throw new InvalidOperationException("Unknown unit mission decision")
        };
    }
}
