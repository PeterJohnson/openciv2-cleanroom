using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private attack confirmation for core W04 pillage; command admission belongs to its caller.</summary>
public sealed class PillageOperation(Pillage.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private AttackWarning? pending;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed pillage presentation cannot resume"); }
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
            var result = new OwnedList<NamedValue>([new("applied", new CommandValue.Boolean(operation.Applied!.Value))]);
            context.Effect("unit.pillaged", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
