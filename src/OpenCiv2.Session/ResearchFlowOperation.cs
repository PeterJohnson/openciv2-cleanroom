using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V03 completion/accumulation with private selection, grant and government decisions.</summary>
public sealed class ResearchFlowOperation(ResearchOperation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private ResearchDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed research presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(ResearchFlowInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction:
                    ResearchFlowInteraction.Prompt(context, pending, recipient, ref intelligenceWindow, intelligence));
            }
            if (!operation.Finished)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var values = new OwnedList<NamedValue>([new("advance", new CommandValue.Integer(operation.Result ?? -1))]);
            context.Effect("research.flow.completed", values);
            return completed = new(Result: values);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class ResearchFlowInteraction
{
    internal static ResearchReply? Reply(ResearchDecision? pending, OperationInput input) => pending switch
    {
        ResearchDecision.Grant grant => ResearchGrantInteraction.ResearchReply(grant.Decision, input),
        ResearchChoiceNotice choice when input.Kind == OperationInputKind.Choice && input.Choice is { } value
            && choice.Options.Any(o => o.Identity == value) => new(value),
        _ => null
    };
    internal static InteractionSpec Prompt(OperationContext context, ResearchDecision pending, Func<int, ParticipantId> recipient,
        ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null)
    {
        if (pending is ResearchDecision.Grant grant)
        {
            return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient,
            intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow);
        }
        var choice = (ResearchChoiceNotice)pending;
        return new("Choose the next advance.", recipient(choice.Civilization), null,
            new(choice.Options.Select(o => o.Identity)), Cancellable: false)
        {
            Options = new(choice.Options.Select(o => new InteractionOption(o.Identity,
            o.FutureNumber is { } number ? $"{o.Name} {number}" : o.Name)))
        };
    }
}
