using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U05 target continuation with actual V04 attack permission and C09 capture decisions.</summary>
public sealed class ParadropOperation(Paradrop.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window) : IContinuation
{
    private ParadropDecision? pending;
    private InteractionSpec? windowPrompt;
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
            bool technology = pending is ParadropDecision.Capture { Decision: CityCaptureDecision.Technology };
            ResearchReply? reply = input.Kind switch
            {
                OperationInputKind.Choice when input.Choice is { } choice => technology && choice == 100 ? new(0, 1) : new(choice),
                OperationInputKind.Cancel or OperationInputKind.Timeout when !technology => new(0),
                _ => null
            };
            pending = operation.Resume(pending is null ? null : reply);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    ParadropDecision.Attack attack => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
                    ParadropDecision.Capture capture => CityCaptureInteraction.Prompt(context, capture.Decision, recipient, window, ref windowPrompt),
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
