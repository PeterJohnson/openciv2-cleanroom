using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private V06/V01 decisions during the real global unit reset.</summary>
public sealed class UnitTurnResetOperation(UnitTurnReset.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private LifecycleDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        {
            throw new InvalidOperationException("Failed reset presentation cannot resume");
        }

        if (completed is not null)
        {
            return completed;
        }

        try
        {
            var next = operation.ResumeDecision(pending is null ? null : LifecycleInteraction.ResearchReply(pending, input));
            if (!ReferenceEquals(next, pending))
            {
                intelligenceWindow = null;
            }

            pending = next;
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: LifecycleInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("units.reset", new([]));
            return completed = new();
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
