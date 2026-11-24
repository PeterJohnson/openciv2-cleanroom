using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>Trusted C09 capture/incitement continuation, with independent victim/actor routing and a required P06 window.</summary>
public sealed class CityCaptureOperation(CityCapture.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private CityCaptureDecision? pending;
    private InteractionSpec? windowPrompt, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed capture continuation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        var reply = CityCaptureInteraction.Reply(pending, input);
        var previous = pending;
        pending = operation.Resume(pending is null ? null : reply);
        if (!ReferenceEquals(previous, pending))
        { intelligenceWindow = null; }
        if (pending is not null)
        {
            context.Publish();
            return new(CommandStatus.AwaitingInteraction, Interaction: CityCaptureInteraction.Prompt(context, pending, recipient, window, ref windowPrompt, ref intelligenceWindow, intelligence));
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        var result = new OwnedList<NamedValue>([new("city", new CommandValue.Integer(operation.RemainingCity?.Value ?? -1))]);
        context.Effect("city.capture.completed", result);
        return completed = operation.PresentationFailures.Count == 0 ? new(Result: result) : new(CommandStatus.Failed, result, "city_capture_presentation_failed");
    }
    public void Dispose() => operation.Dispose();
}

internal static class CityCaptureInteraction
{
    internal static ResearchReply? Reply(CityCaptureDecision? decision, OperationInput input)
    {
        if (decision is CityCaptureDecision.Goal goal)
        {
            return ResearchGoalInteraction.Reply(goal.Decision, input);
        }

        if (decision is CityCaptureDecision.Lifecycle lifecycle)
        { return LifecycleInteraction.ResearchReply(lifecycle.Decision, input); }
        if (decision is CityCaptureDecision.Grant grant)
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        ResearchReply? reply = null;
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } selected)
        { reply = decision is CityCaptureDecision.Technology && selected == 100 ? new(0, 1) : new(selected); }
        else if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout && decision is not CityCaptureDecision.Technology)
        { reply = new(0); }
        return reply;
    }
    internal static InteractionSpec Prompt(OperationContext context, CityCaptureDecision pending, Func<int, ParticipantId> recipient,
        Func<CaptureCityVisit, InteractionSpec> window, ref InteractionSpec? windowPrompt,
        ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null)
    {
        InteractionSpec prompt;
        switch (pending)
        {
            case CityCaptureDecision.Goal goal:
                return ResearchGoalInteraction.Prompt(context, goal.Decision, recipient);
            case CityCaptureDecision.Grant grant:
                return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient,
                    intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow);
            case CityCaptureDecision.Lifecycle incident:
                prompt = LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence);
                break;
            case CityCaptureDecision.Window visit:
                prompt = windowPrompt ??= window(visit.Request) with { Recipient = recipient(visit.Request.Owner) };
                break;
            case CityCaptureDecision.Palace palace:
                prompt = new($"Move the Palace to {palace.Choice.CityName} for {palace.Choice.Cost}?", recipient(palace.Choice.Owner), null, new([0, 1]))
                { Options = new([new(0, "Decline"), new(1, "Move Palace")]) };
                break;
            case CityCaptureDecision.Technology technology:
                var choice = technology.Choice;
                var options = choice.Options.Select(a => new InteractionOption(a.Identity, a.Name)).ToList();
                if (options.Count == 0)
                { options.Add(new(choice.Recommendation, context.Process.Rules.Definitions.Advances[choice.Recommendation].Identity.Name)); }
                options.Add(new(100, "Research goal"));
                prompt = new("Choose captured technology", recipient(choice.Civilization), null, new(options.Select(o => o.Value)), Cancellable: false)
                { Options = new(options) };
                break;
            default:
                throw new InvalidOperationException("Unknown city capture decision");
        }
        return prompt;
    }
}
