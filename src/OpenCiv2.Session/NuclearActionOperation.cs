using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted U07 child continuation. Delivery admission remains with the owning gameplay command.</summary>
public sealed class NuclearActionOperation(NuclearEffects.Operation operation, Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private LifecycleDecision? pending;
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed nuclear continuation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        int? reply = LifecycleInteraction.Reply(pending, input);
        pending = operation.Resume(pending is null ? null : reply);
        if (pending is not null)
        {
            context.Publish();
            return new(CommandStatus.AwaitingInteraction, Interaction: LifecycleInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence));
        }
        if (operation.Result is null)
        { context.Publish(); return new(CommandStatus.Pending); }
        var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result.Value))]);
        context.Effect("nuclear.completed", result);
        return completed = new(Result: result);
    }
    public void Dispose() => operation.Dispose();
}
