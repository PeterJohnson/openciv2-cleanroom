using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>Trusted C09 capture/incitement continuation, with independent victim/actor routing and a required P06 window.</summary>
public sealed class CityCaptureOperation(CityCapture.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window) : IContinuation
{
    private CityCaptureDecision? pending;
    private InteractionSpec? windowPrompt;
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
        ResearchReply? reply = null;
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } selected)
        { reply = pending is CityCaptureDecision.Technology && selected == 100 ? new(0, 1) : new(selected); }
        else if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout && pending is not CityCaptureDecision.Technology)
        { reply = new(0); }
        pending = operation.Resume(pending is null ? null : reply);
        if (pending is not null)
        {
            context.Publish();
            InteractionSpec prompt;
            switch (pending)
            {
                case CityCaptureDecision.Incident incident:
                    prompt = EspionageInteraction.Prompt(incident.Pretext, recipient(incident.Pretext.Victim.Civilization));
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
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending); }
        var result = new OwnedList<NamedValue>([new("city", new CommandValue.Integer(operation.RemainingCity?.Value ?? -1))]);
        context.Effect("city.capture.completed", result);
        return completed = new(Result: result);
    }
    public void Dispose() => operation.Dispose();
}
