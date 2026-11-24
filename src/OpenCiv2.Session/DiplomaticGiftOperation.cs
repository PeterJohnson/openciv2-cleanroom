using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 §4 private gift choices, including retained V03 research/government children.</summary>
public sealed class DiplomaticGiftOperation(DiplomaticGifts.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private DiplomaticGiftDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed diplomatic gift cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(DiplomaticGiftInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: DiplomaticGiftInteraction.Prompt(context, pending, recipient, intelligence, ref window));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("diplomacy.gift.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "gift_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
