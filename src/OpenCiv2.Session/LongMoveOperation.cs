using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private U04 choice to continue or stop repeated automated movement.</summary>
public sealed class LongMoveOperation(LongMove.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private LongMoveQuestion? pending;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed long-move presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : AttackConfirmationInteraction.Reply(input));
            if (pending is { } question)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: new("Stop this unit's automated movement?", recipient(question.Actor),
                    new CommandValue.Unit(question.Unit), new([0, 1]))
                { Options = new([new(0, "Continue"), new(1, "Stop")]) });
            }
            context.Effect("unit.long-move.completed", OwnedList<NamedValue>.Empty);
            return completed = new();
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
