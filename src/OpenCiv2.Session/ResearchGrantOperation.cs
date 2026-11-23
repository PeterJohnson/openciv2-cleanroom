using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V03 grant continuation with private government and actual V05 reception choices.</summary>
public sealed class ResearchGrantOperation(ResearchGrants.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private ResearchGrantDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed research grant presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(ResearchGrantInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction:
                    ResearchGrantInteraction.Prompt(context, pending, recipient, intelligence, ref intelligenceWindow));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var values = new OwnedList<NamedValue>([new("granted", new CommandValue.Boolean(true))]);
            context.Effect("research.granted", values);
            return completed = new(Result: values);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
internal static class ResearchGrantInteraction
{
    internal static int? Reply(ResearchGrantDecision? pending, OperationInput input) => pending switch
    {
        ResearchGrantDecision.Emissary emissary => EmissaryInteraction.Reply(emissary.Decision, input),
        ResearchGrantDecision.Government => AttackConfirmationInteraction.Reply(input),
        _ => null
    };
    internal static InteractionSpec Prompt(OperationContext context, ResearchGrantDecision pending, Func<int, ParticipantId> recipient,
        Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? intelligenceWindow)
    {
        if (pending is ResearchGrantDecision.Emissary emissary)
        { return EmissaryInteraction.Prompt(emissary.Decision, recipient, intelligence, ref intelligenceWindow); }
        var choice = ((ResearchGrantDecision.Government)pending).Choice;
        var rules = context.Process.Rules.Definitions;
        string current = rules.Governments[(int)choice.Current].Identity.Name;
        string proposed = rules.Governments[(int)choice.Proposed].Identity.Name;
        return new($"Begin a revolution from {current} toward {proposed}?", recipient(choice.Civilization), null, new([0, 1]))
        { Options = new([new(0, "Keep current government"), new(1, "Start revolution")]) };
    }
}
