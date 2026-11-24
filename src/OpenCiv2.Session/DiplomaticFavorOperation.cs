using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 private favor decisions, including real exchange and grant continuations.</summary>
public sealed class DiplomaticFavorOperation(DiplomaticFavors.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticFavorDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed diplomatic favor cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(DiplomaticFavorInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: DiplomaticFavorInteraction.Prompt(context, pending, recipient, intelligence, ref window));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("diplomacy.favor.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new() : new(CommandStatus.Failed, Code: "favor_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
