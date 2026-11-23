using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted V01 child. Once admitted, bounded creation/partition work completes despite cancellation.</summary>
public sealed class CivilWarOperation(CivilWar.Operation operation, Func<int, ParticipantId>? recipient = null,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed civil-war presentation cannot resume"); }
        if (completed is not null)
        {
            return completed;
        }

        int? result;
        try
        {
            result = operation.Advance(answer: ResearchGrantInteraction.Reply(operation.Pending, input));
            if (operation.Pending is { } decision)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: ResearchGrantInteraction.Prompt(context,
                    decision, recipient ?? (_ => throw new InvalidOperationException("Civil-war research requires participant routing")),
                    intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow));
            }
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
        if (result is null)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(result.Value))]);
        context.Effect("civilization.civil-war.completed", values);
        return completed = new(Result: values);
    }
    public void Dispose() => operation.Dispose();
}
