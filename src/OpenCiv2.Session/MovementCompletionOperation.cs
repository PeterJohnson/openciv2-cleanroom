using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private decisions across the actual U02 arrival/finish children.</summary>
public sealed class MovementCompletionOperation(MovementCompletion.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> captureWindow, Func<HutCityVisit, InteractionSpec> hutWindow) : IContinuation
{
    private MovementCompletionDecision? pending;
    private InteractionSpec? capturePrompt, hutPrompt;
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
                MovementCompletionDecision.Capture capture => CityCaptureInteraction.Reply(capture.Decision, input),
                MovementCompletionDecision.Incident => EspionageInteraction.Reply(input) is { } choice ? new(choice) : null,
                null => null,
                _ => AttackConfirmationInteraction.Reply(input) is { } choice ? new(choice) : null
            };
            pending = operation.Resume(reply);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    MovementCompletionDecision.Capture capture => CityCaptureInteraction.Prompt(context, capture.Decision, recipient, captureWindow, ref capturePrompt),
                    MovementCompletionDecision.Incident incident => EspionageInteraction.Prompt(incident.Question, recipient(incident.Question.Victim.Civilization)),
                    MovementCompletionDecision.Hut hut => hutPrompt ??= hutWindow(hut.Visit) with { Recipient = recipient(hut.Visit.Actor) },
                    MovementCompletionDecision.LongMove move => LongMoveInteraction.Prompt(move.Question, recipient(move.Question.Actor)),
                    _ => throw new InvalidOperationException("Unknown movement completion decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value))]);
            context.Effect("movement.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
