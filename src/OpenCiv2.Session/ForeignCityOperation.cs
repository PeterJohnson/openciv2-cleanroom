using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U02 private occupation/attack permission and U07 nested casualty incidents.</summary>
public sealed class ForeignCityOperation(ForeignCityMovement.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private ForeignCityDecision? pending;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed foreign-city presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : AttackConfirmationInteraction.Reply(input));
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    ForeignCityDecision.Attack attack => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
                    ForeignCityDecision.Incident incident => EspionageInteraction.Prompt(incident.Question, recipient(incident.Question.Victim.Civilization)),
                    _ => throw new InvalidOperationException("Unknown foreign-city decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value)),
                new("proceed", new CommandValue.Boolean(operation.Result.Proceed))]);
            context.Effect("movement.foreign-city", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
