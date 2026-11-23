using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Admitted V06 city mission child, with actor questions and independently routed nested victims.</summary>
public sealed class CityEspionageDamageOperation(CityEspionageDamage.Operation operation, Func<int, ParticipantId> recipient) : IContinuation
{
    private CityEspionageDecision? pending;
    private OperationStep? completed;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        int? reply = input.Kind switch
        {
            OperationInputKind.Choice => input.Choice,
            OperationInputKind.Cancel or OperationInputKind.Timeout => pending is CityEspionageDecision.Incident ? 0 : -1,
            _ => null
        };
        try
        { pending = operation.Resume(pending is null ? null : reply); }
        catch { context.Publish(); throw; }
        if (pending is not null)
        {
            context.Publish();
            InteractionSpec prompt;
            if (pending is CityEspionageDecision.Incident incident)
            { prompt = EspionageInteraction.Prompt(incident.Pretext, recipient(incident.Pretext.Victim.Civilization)); }
            else
            {
                var question = (CityEspionageDecision.Question)pending;
                var options = question.Targets is { } targets
                    ? new OwnedList<InteractionOption>(targets.Select(t => new InteractionOption(t.Building, t.Name)))
                    : new([new(0, question.Kind == CityEspionageQuestion.SpecificSabotage ? "Random target" : "Cancel"),
                        new(1, question.Kind == CityEspionageQuestion.SpecificSabotage ? "Choose target" : "Proceed")]);
                string title = question.Kind switch
                {
                    CityEspionageQuestion.TreatyBreach => $"Risk a treaty incident at {question.Name}?",
                    CityEspionageQuestion.SpecificSabotage => $"Choose a sabotage target in {question.Name}?",
                    CityEspionageQuestion.SabotageTarget => $"Sabotage target in {question.Name}",
                    CityEspionageQuestion.HardSabotage => $"Attempt difficult sabotage in {question.Name}?",
                    _ => throw new InvalidOperationException("Unknown city mission question")
                };
                prompt = new(title, recipient(question.Actor), new CommandValue.City(question.City), new(options.Select(o => o.Value))) { Options = options };
            }
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending); }
        context.Effect("espionage.city-damage.completed", new([]));
        return completed = new();
    }
    public void Dispose() => operation.Dispose();
}
