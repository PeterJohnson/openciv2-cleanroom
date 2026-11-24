using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private decisions across the actual U02 arrival/finish children.</summary>
public sealed class MovementCompletionOperation(MovementCompletion.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> captureWindow, Func<HutCityVisit, InteractionSpec> hutWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private MovementCompletionDecision? pending;
    private InteractionSpec? capturePrompt, hutPrompt, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed movement completion presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            ResearchReply? reply = pending switch
            {
                MovementCompletionDecision.Contact contact => DiplomaticContactInteraction.Reply(contact.Decision, input),
                MovementCompletionDecision.Capture capture => CityCaptureInteraction.Reply(capture.Decision, input),
                MovementCompletionDecision.Grant grant => ResearchGrantInteraction.ResearchReply(grant.Decision, input),
                MovementCompletionDecision.Lifecycle lifecycle => LifecycleInteraction.ResearchReply(lifecycle.Decision, input),
                null => null,
                _ => AttackConfirmationInteraction.Reply(input) is { } choice ? new(choice) : null
            };
            var previous = pending;
            pending = operation.Resume(reply);
            if (!ReferenceEquals(previous, pending))
            { intelligenceWindow = null; }
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    MovementCompletionDecision.Contact contact => DiplomaticContactInteraction.Prompt(context, contact.Decision, recipient,
                        intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow),
                    MovementCompletionDecision.Capture capture => CityCaptureInteraction.Prompt(context, capture.Decision, recipient, captureWindow, ref capturePrompt, ref intelligenceWindow, intelligence),
                    MovementCompletionDecision.Grant grant => ResearchGrantInteraction.Prompt(context, grant.Decision, recipient,
                        intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow),
                    MovementCompletionDecision.Lifecycle incident => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
                    MovementCompletionDecision.Hut hut => hutPrompt ??= hutWindow(hut.Visit) with { Recipient = recipient(hut.Visit.Actor) },
                    MovementCompletionDecision.LongMove move => LongMoveInteraction.Prompt(move.Question, recipient(move.Question.Actor)),
                    _ => throw new InvalidOperationException("Unknown movement completion decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value))]);
            context.Effect("movement.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, Result: result, Code: "movement_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
