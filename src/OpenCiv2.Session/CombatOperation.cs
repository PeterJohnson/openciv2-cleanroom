using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U06 local execution with private attack, casualty, help and capture decisions.
/// The movement caller supplies authority admission and required report/city window adapters.</summary>
public sealed class CombatOperation(CombatExecution.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence, Func<CaptureCityVisit, InteractionSpec> city) : IContinuation
{
    private CombatDecision? pending;
    private InteractionSpec? intelligenceWindow, cityWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed combat presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            ResearchReply? reply = null;
            if (pending is CombatDecision.Continuation continuation)
            { reply = CombatContinuationInteraction.Reply(continuation.Decision, input); }
            else if (pending is CombatDecision.Help help)
            { if (AllyHelpInteraction.Reply(help.Decision, input) is { } answer) { reply = new(answer); } }
            else if (input.Kind == OperationInputKind.Choice && input.Choice is { } answer)
            { reply = new(answer); }
            else if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
            { reply = new(0); }
            pending = operation.Resume(pending is null ? null : reply);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    CombatDecision.Attack attack => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
                    CombatDecision.Incident incident => EspionageInteraction.Prompt(incident.Question, recipient(incident.Question.Victim.Civilization)),
                    CombatDecision.Help help => AllyHelpInteraction.Prompt(help.Decision, recipient, intelligence, ref intelligenceWindow),
                    CombatDecision.Continuation child => CombatContinuationInteraction.Prompt(context, child.Decision, recipient, city, ref cityWindow),
                    _ => throw new InvalidOperationException("Unknown combat decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value))]);
            context.Effect("combat.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
