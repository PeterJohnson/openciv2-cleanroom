using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>C05 food growth/famine waits for private lifecycle decisions; removal bypasses later F04 city stages.</summary>
public sealed class CityFoodOperation(CityFood.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    public bool Removed => operation.Removed ?? throw new InvalidOperationException("City food processing has not completed");
    private LifecycleDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool disposed, faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        {
            throw new InvalidOperationException("A failed city food presentation cannot resume");
        }

        if (completed is not null)
        {
            return completed;
        }

        try
        {
            var next = operation.ResumeDecision(LifecycleInteraction.ResearchReply(pending, input));
            if (!ReferenceEquals(pending, next))
            {
                intelligenceWindow = null;
            }

            pending = next;
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction,
                    Interaction: LifecycleInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("city.food.completed", OwnedList<NamedValue>.Empty);
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "city_food_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() { disposed = true; operation.Dispose(); }
}
