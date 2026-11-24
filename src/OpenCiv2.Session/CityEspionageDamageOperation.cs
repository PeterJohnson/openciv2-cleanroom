using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Admitted V06 city mission child, with actor questions and independently routed nested victims.</summary>
public sealed class CityEspionageDamageOperation(CityEspionageDamage.Operation operation, Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private CityEspionageDecision? pending;
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed lifecycle presentation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        int? reply = input.Kind switch
        {
            OperationInputKind.Choice => input.Choice,
            OperationInputKind.Cancel or OperationInputKind.Timeout => pending is CityEspionageDecision.Lifecycle ? 0 : -1,
            _ => null
        };
        ResearchReply? answer = LifecycleInteraction.Selection(reply);
        if (pending is CityEspionageDecision.Lifecycle lifecycle)
        { answer = LifecycleInteraction.ResearchReply(lifecycle.Decision, input); }
        var previous = pending;
        pending = operation.ResumeDecision(pending is null ? null : answer);
        if (!ReferenceEquals(previous, pending))
        { intelligenceWindow = null; }
        if (pending is not null)
        {
            context.Publish();
            InteractionSpec prompt;
            if (pending is CityEspionageDecision.Lifecycle incident)
            { prompt = LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence); }
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
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        context.Effect("espionage.city-damage.completed", new([]));
        return completed = new();
    }
    public void Dispose() => operation.Dispose();
}
