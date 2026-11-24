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
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: values)
                : new(CommandStatus.Failed, values, "research_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class ResearchFlowInteraction
{
    internal static ResearchReply? Reply(ResearchDecision? pending, OperationInput input) => pending switch
    {
        ResearchDecision.Goal goal => ResearchGoalInteraction.Reply(goal.Decision, input),
        ResearchChoiceNotice when input.Kind == OperationInputKind.Choice && input.Choice == 100 => new(0, 2),
        ResearchChoiceNotice help when input.Kind == OperationInputKind.Choice && input.Choice is { } encoded
            && help.Options.Any(o => o.Identity == encoded - ResearchGoalInteraction.HelpBase) => new(encoded - ResearchGoalInteraction.HelpBase, 1),
        ResearchDecision.Grant grant => ResearchGrantInteraction.ResearchReply(grant.Decision, input),
        ResearchChoiceNotice choice when input.Kind == OperationInputKind.Choice && input.Choice is { } value
            && choice.Options.Any(o => o.Identity == value) => new(value),
        _ => null
    };
    internal static InteractionSpec Prompt(OperationContext context, ResearchDecision pending, Func<int, ParticipantId> recipient,
        ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null)
    {
        if (pending is ResearchDecision.Goal goal)
        {
            return ResearchGoalInteraction.Prompt(context, goal.Decision, recipient);
        }

        if (pending is ResearchDecision.Grant grant)
        {
            return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient,
            intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow);
        }
        var choice = (ResearchChoiceNotice)pending;
        var options = choice.Options.Select(o => new InteractionOption(o.Identity, ResearchGoalInteraction.Label(o))).ToList();
        options.Add(new(100, "Research goal"));
        options.AddRange(choice.Options.Select(o => new InteractionOption(ResearchGoalInteraction.HelpBase + o.Identity, $"Help: {ResearchGoalInteraction.Label(o)}")));
        return new("Choose the next advance.", recipient(choice.Civilization), null,
            new(options.Select(o => o.Value)), Cancellable: false)
        { Options = new(options) };
    }
}
