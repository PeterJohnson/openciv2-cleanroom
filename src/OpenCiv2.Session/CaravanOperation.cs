using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>C07 private city-entry choices and actual nested deletion incidents.</summary>
public sealed class CaravanOperation(CaravanTrade.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private CaravanDecision? pending;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed caravan presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : AttackConfirmationInteraction.Reply(input));
            if (pending is not null)
            {
                context.Publish();
                var prompt = CaravanInteraction.Prompt(pending, recipient);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            var result = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value))]);
            context.Effect("trade.caravan", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class CaravanInteraction
{
    internal static InteractionSpec Prompt(CaravanDecision decision, Func<int, ParticipantId> recipient)
    {
        InteractionSpec prompt;
        if (decision is CaravanDecision.Menu menu)
        {
            var request = menu.Request;
            var options = new OwnedList<InteractionOption>(request.Actions.Select(action => new InteractionOption((int)action,
                action switch { CaravanAction.Deliver => "Deliver", CaravanAction.HelpBuildWonder => "Help build wonder", _ => "Continue" })));
            string demand = request.Demanded is null ? "" : request.Demanded.Value ? " This cargo is demanded." : " This cargo is not demanded.";
            prompt = new($"{request.CargoName} caravan at {request.CityName}.{demand}", recipient(request.Actor),
                new CommandValue.City(request.City), new(options.Select(o => o.Value)))
            { Options = options };
        }
        else if (decision is CaravanDecision.Incident incident)
        { prompt = EspionageInteraction.Prompt(incident.Question, recipient(incident.Question.Victim.Civilization)); }
        else
        { throw new InvalidOperationException("Unknown caravan decision"); }
        return prompt;
    }
}
