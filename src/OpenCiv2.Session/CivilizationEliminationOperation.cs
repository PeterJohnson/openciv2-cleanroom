using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted V01 destruction continuation. The owning city/combat operation admits the removal;
/// the seat resolver routes each human victim's pretext, including nested last-settler checks.</summary>
public sealed class CivilizationEliminationOperation(CivilizationElimination.Operation operation,
    Func<int, ParticipantId> recipient) : IContinuation
{
    private bool pending;
    private OperationStep? completed;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        EspionagePretext? decision;
        try
        { decision = operation.Resume(pending ? EspionageInteraction.Reply(input) : null); }
        catch { context.Publish(); throw; }
        pending = decision is not null;
        if (decision is not null)
        {
            context.Publish();
            return new(CommandStatus.AwaitingInteraction,
                Interaction: EspionageInteraction.Prompt(decision, recipient(decision.Victim.Civilization)));
        }
        if (operation.Result is null)
        {
            context.Publish();
            return new(CommandStatus.Pending);
        }
        var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result.Value))]);
        context.Effect("civilization.elimination.completed", result);
        return completed = new(Result: result);
    }

    public void Dispose() => operation.Dispose();
}
