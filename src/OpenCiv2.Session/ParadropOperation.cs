using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U05 target continuation with actual V04 permission, C09 capture and W03 contact decisions.</summary>
public sealed class ParadropOperation(Paradrop.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private ParadropDecision? pending;
    private InteractionSpec? windowPrompt, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed paradrop presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = pending;
            pending = operation.Resume(ParadropInteraction.Reply(pending, input));
            if (!ReferenceEquals(previous, pending))
            { intelligenceWindow = null; }
            if (pending is not null)
            {
                context.Publish();
                var prompt = ParadropInteraction.Prompt(context, pending, recipient, window,
                    ref windowPrompt, ref intelligenceWindow, intelligence);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("landed", new CommandValue.Boolean(operation.Landed))]);
            context.Effect("movement.paradrop.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, Result: result, Code: "paradrop_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class ParadropInteraction
{
    internal static ResearchReply? Reply(ParadropDecision? pending, OperationInput input) => pending switch
    {
        ParadropDecision.Contact contact => DiplomaticContactInteraction.Reply(contact.Decision, input),
        ParadropDecision.Capture capture => CityCaptureInteraction.Reply(capture.Decision, input),
        ParadropDecision.Attack => AttackConfirmationInteraction.Reply(input) is { } answer ? new(answer) : null,
        _ => null
    };
    internal static InteractionSpec Prompt(OperationContext context, ParadropDecision pending, Func<int, ParticipantId> recipient,
        Func<CaptureCityVisit, InteractionSpec> window, ref InteractionSpec? windowPrompt, ref InteractionSpec? intelligenceWindow,
        Func<EmissaryIntelligence, InteractionSpec>? intelligence) => pending switch
        {
            ParadropDecision.Attack attack => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
            ParadropDecision.Capture capture => CityCaptureInteraction.Prompt(context, capture.Decision, recipient, window, ref windowPrompt, ref intelligenceWindow, intelligence),
            ParadropDecision.Contact contact => DiplomaticContactInteraction.Prompt(context, contact.Decision, recipient,
                intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow),
            _ => throw new InvalidOperationException("Unknown paradrop decision")
        };
}
