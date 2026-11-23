using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V04 attack permission only. The owning movement/combat operation executes an allowed attack.</summary>
public sealed class AttackConfirmationOperation(AttackConfirmation.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private AttackWarning? pending;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed attack confirmation presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : AttackConfirmationInteraction.Reply(input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: AttackConfirmationInteraction.Prompt(pending, recipient(pending.Actor.Civilization)));
            }
            var result = new OwnedList<NamedValue>([new("blocked", new CommandValue.Boolean(operation.Blocked))]);
            context.Effect("diplomacy.attack-confirmed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
internal static class AttackConfirmationInteraction
{
    internal static int? Reply(OperationInput input) => input.Kind switch
    {
        OperationInputKind.Choice => input.Choice,
        OperationInputKind.Cancel or OperationInputKind.Timeout => 0,
        _ => null
    };
    internal static InteractionSpec Prompt(AttackWarning request, ParticipantId recipient)
    {
        string consequence = request.Kind switch
        {
            AttackWarningKind.Peace => "This will violate your peace treaty.",
            AttackWarningKind.TributeCeasefire => "This will violate your ceasefire and tribute agreement.",
            AttackWarningKind.Ceasefire => "This will violate your ceasefire.",
            _ => "This will anger their civilization."
        };
        return new($"Attack the {request.Victim.Plural}? {consequence}", recipient, null, new([0, 1]))
        { Options = new([new(0, "Cancel"), new(1, "Proceed")]) };
    }
}
