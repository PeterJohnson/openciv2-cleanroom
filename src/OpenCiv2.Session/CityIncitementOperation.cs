using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>V06 revolt offer, nested victims and actual C09 decisions. The city controller consumes a returned fallback.</summary>
public sealed class CityIncitementOperation(CityIncitement.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private RevoltDecision? pending;
    private InteractionSpec? windowPrompt, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed revolt continuation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        ResearchReply? reply = RevoltInteraction.Reply(pending, input);
        pending = operation.Resume(pending is null ? null : reply);
        if (pending is not null)
        {
            context.Publish();
            var prompt = RevoltInteraction.Prompt(context, pending, recipient, window, ref windowPrompt, ref intelligenceWindow, intelligence);
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending); }
        var result = new OwnedList<NamedValue>([new("fallback", new CommandValue.Integer(operation.Fallback is { } mission ? (int)mission : -1))]);
        context.Effect("espionage.incitement.completed", result);
        return completed = new(Result: result);
    }
    public void Dispose() => operation.Dispose();
}

internal static class RevoltInteraction
{
    internal static ResearchReply? Reply(RevoltDecision? pending, OperationInput input) => pending switch
    {
        RevoltDecision.Capture capture => CityCaptureInteraction.Reply(capture.Decision, input),
        RevoltDecision.Lifecycle lifecycle => LifecycleInteraction.ResearchReply(lifecycle.Decision, input),
        _ => AttackConfirmationInteraction.Reply(input) is { } answer ? new(answer) : null
    };
    internal static InteractionSpec Prompt(OperationContext context, RevoltDecision pending, Func<int, ParticipantId> recipient,
        Func<CaptureCityVisit, InteractionSpec> window, ref InteractionSpec? windowPrompt, ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null)
    {
        InteractionSpec prompt;
        switch (pending)
        {
            case RevoltDecision.Offer offer:
                // The unaffordable dialog has no payment options. Session's required choice is
                // only its presentation close action, not another admission or payment branch.
                var options = offer.Request.Options.Count == 0 ? new[] { new InteractionOption(0, "Close") }
                    : offer.Request.Options.Select(o => new InteractionOption(o.Value, o.Text)).ToArray();
                string title = offer.Request.Options.Count == 0
                    ? $"Insufficient gold to incite a revolt in {offer.Request.CityName}. Cost: {offer.Request.Price}."
                    : $"Incite a revolt in {offer.Request.CityName} for {offer.Request.Price}?";
                prompt = new(title, recipient(offer.Request.Actor), new CommandValue.City(offer.Request.City), new(options.Select(o => o.Value)))
                { Options = new(options) };
                break;
            case RevoltDecision.Lifecycle incident:
                prompt = LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence);
                break;
            case RevoltDecision.Capture capture:
                prompt = CityCaptureInteraction.Prompt(context, capture.Decision, recipient, window, ref windowPrompt, ref intelligenceWindow, intelligence);
                break;
            default:
                throw new InvalidOperationException("Unknown revolt decision");
        }
        return prompt;
    }
}
