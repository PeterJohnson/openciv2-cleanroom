using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U05 target continuation with actual V04 attack permission and C09 capture decisions.</summary>
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
            ResearchReply? reply = pending is ParadropDecision.Capture capture
                ? CityCaptureInteraction.Reply(capture.Decision, input)
                : AttackConfirmationInteraction.Reply(input) is { } answer ? new(answer) : null;
            pending = operation.Resume(pending is null ? null : reply);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    ParadropDecision.Attack attack => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
                    ParadropDecision.Capture captured => CityCaptureInteraction.Prompt(context, captured.Decision, recipient, window, ref windowPrompt, ref intelligenceWindow, intelligence),
                    _ => throw new InvalidOperationException("Unknown paradrop decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            var result = new OwnedList<NamedValue>([new("landed", new CommandValue.Boolean(operation.Landed))]);
            context.Effect("movement.paradrop.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
