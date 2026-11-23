using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private U02 expulsion/fatigue and nested combat decisions. The owning movement
/// continuation consumes the finish route and performs common finish/cleanup.</summary>
public sealed class ForeignUnitOperation(ForeignUnitMovement.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence, Func<CaptureCityVisit, InteractionSpec> city) : IContinuation
{
    private ForeignUnitDecision? pending;
    private InteractionSpec? intelligenceWindow, cityWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed foreign-unit presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var reply = ForeignUnitInteraction.Reply(pending, input);
            pending = operation.Resume(pending is null ? null : reply);
            if (pending is not null)
            {
                context.Publish();
                var prompt = ForeignUnitInteraction.Prompt(context, pending, recipient, intelligence, city, ref intelligenceWindow, ref cityWindow);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            var result = operation.Result!;
            var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(result.Value)),
                new("finish", new CommandValue.Boolean(result.Finish)), new("x", new CommandValue.Integer(result.Destination.X)), new("y", new CommandValue.Integer(result.Destination.Y))]);
            context.Effect("movement.foreign-unit", values);
            return completed = new(Result: values);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class ForeignUnitInteraction
{
    internal static ResearchReply? Reply(ForeignUnitDecision? pending, OperationInput input) => pending is ForeignUnitDecision.Combat combat ? CombatInteraction.Reply(combat.Decision, input) : input.Kind switch
    {
        OperationInputKind.Choice when input.Choice is { } answer => new(answer),
        OperationInputKind.Cancel or OperationInputKind.Timeout => new(pending is ForeignUnitDecision.Expel ? 2 : 1),
        _ => null
    };
    internal static InteractionSpec Prompt(OperationContext context, ForeignUnitDecision pending, Func<int, ParticipantId> recipient,
        Func<EmissaryIntelligence, InteractionSpec> intelligence, Func<CaptureCityVisit, InteractionSpec> city,
        ref InteractionSpec? intelligenceWindow, ref InteractionSpec? cityWindow) => pending switch
        {
            ForeignUnitDecision.Expel expel => new InteractionSpec($"Expel the {expel.Offer.VictimAdjective} {expel.Offer.TargetName}?",
                recipient(expel.Offer.Actor), null, new([0, 1, 2]))
            { Options = new([new(0, "Expel"), new(1, "Attack"), new(2, "Cancel")]) },
            ForeignUnitDecision.Fatigue fatigue => new InteractionSpec($"Attack with {fatigue.Choice.MovementLeft} of {fatigue.Choice.RoadMultiplier} movement remaining?",
                recipient(fatigue.Choice.Actor), null, new([0, 1]))
            { Options = new([new(0, "Attack"), new(1, "Cancel")]) },
            ForeignUnitDecision.Combat child => CombatInteraction.Prompt(context, child.Decision, recipient, intelligence, city, ref intelligenceWindow, ref cityWindow),
            _ => throw new InvalidOperationException("Unknown foreign-unit decision")
        };
}
