using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private W04 improvement selection and actual nested attack warnings.</summary>
public sealed class PillageCommandOperation(PillageCommand.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private PillageDecision? pending;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed pillage command presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            int? answer = pending switch
            {
                PillageDecision.Attack => AttackConfirmationInteraction.Reply(input),
                PillageDecision.Menu => input.Kind switch
                { OperationInputKind.Choice => input.Choice, OperationInputKind.Cancel or OperationInputKind.Timeout => -1, _ => null },
                _ => null
            };
            pending = operation.Resume(answer);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    PillageDecision.Attack attack => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
                    PillageDecision.Menu menu => Menu(menu),
                    _ => throw new InvalidOperationException("Unknown pillage decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            var result = new OwnedList<NamedValue>([new("outcome", new CommandValue.Text(operation.Result!.Value.ToString()))]);
            context.Effect("unit.pillage-command-completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private InteractionSpec Menu(PillageDecision.Menu menu)
    {
        var options = menu.Choices.Select((choice, i) => new InteractionOption(i, choice.Feature.ToString())).ToList();
        options.Add(new(-1, "Cancel"));
        return new("Choose an improvement to pillage", recipient(menu.Owner), null, new(options.Select(option => option.Value)))
        { Options = new(options) };
    }
    public void Dispose() => operation.Dispose();
}
