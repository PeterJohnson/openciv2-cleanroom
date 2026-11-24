using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted V06 theft child; the owning city controller consumes any fallback before completing its command.</summary>
public sealed class CityTechnologyTheftOperation(CityTechnologyTheft.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private CityEspionageDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed theft presentation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        ResearchReply? reply = null;
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } value)
        { reply = pending is CityEspionageDecision.Technology && value == 100 ? new(0, 1) : new(value); }
        else if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout && pending is not CityEspionageDecision.Technology)
        { reply = new(pending is CityEspionageDecision.Lifecycle ? 0 : -1); }
        if (pending is CityEspionageDecision.Goal goal)
        { reply = ResearchGoalInteraction.Reply(goal.Decision, input); }
        if (pending is CityEspionageDecision.Grant grant)
        { reply = ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        if (pending is CityEspionageDecision.Lifecycle lifecycle)
        { reply = LifecycleInteraction.ResearchReply(lifecycle.Decision, input); }
        pending = operation.Resume(pending is null ? null : reply);
        if (pending is not null)
        {
            context.Publish();
            InteractionSpec prompt;
            if (pending is CityEspionageDecision.Goal browsing)
            { prompt = ResearchGoalInteraction.Prompt(context, browsing.Decision, recipient); }
            else if (pending is CityEspionageDecision.Lifecycle incident)
            { prompt = LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence); }
            else if (pending is CityEspionageDecision.Grant granted)
            {
                prompt = ResearchGrantInteraction.Prompt(context, granted.Decision, recipient,
                intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow);
            }
            else if (pending is CityEspionageDecision.Technology technology)
            {
                var choice = technology.Choice;
                var options = choice.Options.Select(a => new InteractionOption(a.Identity, a.Name)).ToList();
                if (options.Count == 0)
                { options.Add(new(choice.Recommendation, context.Process.Rules.Definitions.Advances[choice.Recommendation].Identity.Name)); }
                options.Add(new(100, "Research goal"));
                prompt = new("Choose captured technology", recipient(choice.Civilization), null, new(options.Select(o => o.Value)), Cancellable: false) { Options = new(options) };
            }
            else
            {
                var question = (CityEspionageDecision.Question)pending;
                bool specific = question.Kind == CityEspionageQuestion.SpecificTheft;
                string title = question.Kind switch
                {
                    CityEspionageQuestion.TreatyBreach => $"Risk a treaty incident at {question.Name}?",
                    CityEspionageQuestion.SpecificTheft => $"Choose technology to steal from {question.Name}?",
                    CityEspionageQuestion.HardTheft => $"Attempt another theft from {question.Name}?",
                    _ => throw new InvalidOperationException("Unknown theft question")
                };
                prompt = new(title, recipient(question.Actor), new CommandValue.City(question.City), new([0, 1]))
                { Options = new([new(0, specific ? "Random technology" : "Cancel"), new(1, specific ? "Choose technology" : "Proceed")]) };
            }
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        var values = new OwnedList<NamedValue>([new("fallback", new CommandValue.Integer(operation.Fallback is { } mission ? (int)mission : -1))]);
        context.Effect("espionage.theft.completed", values);
        return completed = operation.PresentationFailures.Count == 0 ? new(Result: values)
            : new(CommandStatus.Failed, Result: values, Code: "technology_theft_presentation_failed");
    }
    public void Dispose() => operation.Dispose();
}
