using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>Local V06 menu and complete ordinary mission/fallback continuation.
/// Required P06/P07 adapters display visits; each nested decision is routed to its actual recipient.</summary>
public sealed class CityMissionOperation(CityMissions.Operation operation, Func<int, ParticipantId> recipient,
    Func<CityEspionageVisit, InteractionSpec> visit, Func<CaptureCityVisit, InteractionSpec> captureWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private CityMissionDecision? pending;
    private InteractionSpec? visitPrompt, capturePrompt, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed city mission continuation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        var reply = CityMissionInteraction.Reply(pending, input);
        pending = operation.Resume(pending is null ? null : reply);
        if (pending is not null)
        {
            context.Publish();
            var prompt = CityMissionInteraction.Prompt(context, pending, recipient, visit, captureWindow, ref visitPrompt, ref capturePrompt, ref intelligenceWindow, intelligence);
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        context.Effect("espionage.city-mission.completed", new([]));
        return completed = operation.PresentationFailures.Count == 0 ? new()
            : new(CommandStatus.Failed, Code: "city_mission_presentation_failed");
    }
    public void Dispose() => operation.Dispose();
}

internal static class CityMissionInteraction
{
    internal static ResearchReply? Reply(CityMissionDecision? decision, OperationInput input)
    {
        if (decision is CityMissionDecision.Mission { Decision: CityEspionageDecision.Goal goal })
        { return ResearchGoalInteraction.Reply(goal.Decision, input); }
        if (decision is CityMissionDecision.Mission { Decision: CityEspionageDecision.Lifecycle lifecycle })
        { return LifecycleInteraction.ResearchReply(lifecycle.Decision, input); }
        if (decision is CityMissionDecision.Revolt revolt)
        { return RevoltInteraction.Reply(revolt.Decision, input); }
        if (decision is CityMissionDecision.Mission { Decision: CityEspionageDecision.Grant grant })
        { return ResearchGrantInteraction.ResearchReply(grant.Decision, input); }
        bool technology = decision is CityMissionDecision.Mission { Decision: CityEspionageDecision.Technology };
        ResearchReply? reply = null;
        if (input.Kind == OperationInputKind.Choice && input.Choice is { } choice)
        { reply = technology && choice == 100 ? new(0, 1) : new(choice); }
        else if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout && !technology)
        {
            reply = new(decision is CityMissionDecision.Menu
                or CityMissionDecision.Mission { Decision: CityEspionageDecision.Question } ? -1 : 0);
        }
        return reply;
    }
    internal static InteractionSpec Prompt(OperationContext context, CityMissionDecision decision, Func<int, ParticipantId> recipient, Func<CityEspionageVisit, InteractionSpec> visit, Func<CaptureCityVisit, InteractionSpec> captureWindow, ref InteractionSpec? visitPrompt, ref InteractionSpec? capturePrompt, ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null)
    {
        InteractionSpec prompt;
        switch (decision)
        {
            case CityMissionDecision.Menu menu:
                var request = menu.Request;
                var options = new OwnedList<InteractionOption>(request.Options.Select(o => new InteractionOption((int)o.Mission, o.Text)));
                prompt = new($"{request.ActorAdjective} {request.UnitName} at {request.CityName}", recipient(request.Actor),
                    new CommandValue.City(request.City), new(options.Select(o => o.Value)))
                { Options = options };
                break;
            case CityMissionDecision.Revolt revolt:
                prompt = RevoltInteraction.Prompt(context, revolt.Decision, recipient, captureWindow, ref capturePrompt, ref intelligenceWindow, intelligence);
                break;
            case CityMissionDecision.Mission mission:
                prompt = MissionPrompt(context, mission.Decision, recipient, visit, captureWindow, ref visitPrompt, ref capturePrompt, ref intelligenceWindow, intelligence);
                break;
            default:
                throw new InvalidOperationException("Unknown city mission decision");
        }
        return prompt;
    }
    private static InteractionSpec MissionPrompt(OperationContext context, CityEspionageDecision decision, Func<int, ParticipantId> recipient, Func<CityEspionageVisit, InteractionSpec> visit, Func<CaptureCityVisit, InteractionSpec> captureWindow, ref InteractionSpec? visitPrompt, ref InteractionSpec? capturePrompt, ref InteractionSpec? intelligenceWindow, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null)
    {
        switch (decision)
        {
            case CityEspionageDecision.Goal goal:
                return ResearchGoalInteraction.Prompt(context, goal.Decision, recipient);
            case CityEspionageDecision.Grant grant:
                return ResearchGrantInteraction.Prompt(context, grant.Decision, recipient,
                    intelligence ?? ResearchGrantInteraction.RequiredIntelligence, ref intelligenceWindow);
            case CityEspionageDecision.Visit request:
                return visitPrompt ??= visit(request.Request) with { Recipient = recipient(request.Request.Actor) };
            case CityEspionageDecision.Lifecycle incident:
                return LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence);
            case CityEspionageDecision.Technology technology:
                return CityCaptureInteraction.Prompt(context, new CityCaptureDecision.Technology(technology.Choice), recipient, captureWindow, ref capturePrompt, ref intelligenceWindow, intelligence);
            case CityEspionageDecision.Question question:
                var options = question.Targets is { } targets
                    ? new OwnedList<InteractionOption>(targets.Select(t => new InteractionOption(t.Building, t.Name)))
                    : new([new(0, question.Kind switch
                    {
                        CityEspionageQuestion.SpecificSabotage => "Random target",
                        CityEspionageQuestion.SpecificTheft => "Random technology",
                        _ => "Cancel"
                    }), new(1, question.Kind switch
                    {
                        CityEspionageQuestion.SpecificSabotage => "Choose target",
                        CityEspionageQuestion.SpecificTheft => "Choose technology",
                        CityEspionageQuestion.MajorIncident => "Plant device",
                        _ => "Proceed"
                    })]);
                string title = question.Kind switch
                {
                    CityEspionageQuestion.TreatyBreach => $"Risk a treaty incident at {question.Name}?",
                    CityEspionageQuestion.SpecificSabotage => $"Choose a sabotage target in {question.Name}?",
                    CityEspionageQuestion.SabotageTarget => $"Sabotage target in {question.Name}",
                    CityEspionageQuestion.HardSabotage => $"Attempt difficult sabotage in {question.Name}?",
                    CityEspionageQuestion.SpecificTheft => $"Choose technology to steal from {question.Name}?",
                    CityEspionageQuestion.HardTheft => $"Attempt another theft from {question.Name}?",
                    CityEspionageQuestion.MajorIncident => $"Risk a major incident by planting a nuclear device in {question.Name}?",
                    _ => throw new InvalidOperationException("Unknown city mission question")
                };
                return new(title, recipient(question.Actor), new CommandValue.City(question.City), new(options.Select(o => o.Value))) { Options = options };
            default:
                throw new InvalidOperationException("Unknown mission body decision");
        }
    }
}
