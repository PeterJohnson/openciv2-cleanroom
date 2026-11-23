using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private nested interactions for the actual local U02 dispatcher. The player command
/// owns admission, selected-unit updates and its public command result.</summary>
public sealed class MovementOperation(MovementDispatcher.Operation operation, Func<int, ParticipantId> recipient,
    Func<CityEspionageVisit, InteractionSpec> visit, Func<EmissaryIntelligence, InteractionSpec> intelligence,
    Func<CaptureCityVisit, InteractionSpec> capture, Func<HutCityVisit, InteractionSpec> hut) : IContinuation
{
    private MovementDecision? pending;
    private InteractionSpec? visitWindow, intelligenceWindow, captureWindow, hutWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed movement presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            ResearchReply? answer = pending switch
            {
                MovementDecision.Land { Decision: LandEntryDecision.City city } => CityMissionInteraction.Reply(city.Decision, input),
                MovementDecision.Land { Decision: LandEntryDecision.Unit unit } => Reply(UnitMissionInteraction.Reply(unit.Decision, input)),
                MovementDecision.ForeignUnit unit => ForeignUnitInteraction.Reply(unit.Decision, input),
                MovementDecision.Finish { Decision: MovementCompletionDecision.Capture city } => CityCaptureInteraction.Reply(city.Decision, input),
                MovementDecision.Finish { Decision: MovementCompletionDecision.Grant grant } => ResearchGrantInteraction.ResearchReply(grant.Decision, input),
                MovementDecision.Finish { Decision: MovementCompletionDecision.Lifecycle lifecycle } => LifecycleInteraction.ResearchReply(lifecycle.Decision, input),
                MovementDecision.ForeignCity { Decision: ForeignCityDecision.Lifecycle lifecycle } => LifecycleInteraction.ResearchReply(lifecycle.Decision, input),
                MovementDecision.Land { Decision: LandEntryDecision.Caravan caravan } => Reply(CaravanInteraction.Reply(caravan.Decision, input)),
                null => null,
                _ => Reply(AttackConfirmationInteraction.Reply(input))
            };
            pending = operation.Resume(answer);
            if (pending is not null)
            {
                context.Publish();
                InteractionSpec prompt = pending switch
                {
                    MovementDecision.Land { Decision: LandEntryDecision.Caravan trade } => CaravanInteraction.Prompt(context, trade.Decision, recipient, ref intelligenceWindow, intelligence),
                    MovementDecision.Land { Decision: LandEntryDecision.Unit unit } => UnitMissionInteraction.Prompt(context, unit.Decision, recipient, ref intelligenceWindow, intelligence),
                    MovementDecision.Land { Decision: LandEntryDecision.City city } => CityMissionInteraction.Prompt(context, city.Decision, recipient, visit, capture, ref visitWindow, ref captureWindow, ref intelligenceWindow, intelligence),
                    MovementDecision.ForeignUnit unit => ForeignUnitInteraction.Prompt(context, unit.Decision, recipient, intelligence, capture, ref intelligenceWindow, ref captureWindow),
                    MovementDecision.Landfall landfall => LandfallInteraction.Prompt(landfall.Choice, recipient(landfall.Choice.Actor)),
                    MovementDecision.ForeignCity { Decision: ForeignCityDecision.Attack attack } => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
                    MovementDecision.ForeignCity { Decision: ForeignCityDecision.Lifecycle incident } => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
                    MovementDecision.Finish { Decision: MovementCompletionDecision.Capture city } => CityCaptureInteraction.Prompt(context, city.Decision, recipient, capture, ref captureWindow, ref intelligenceWindow, intelligence),
                    MovementDecision.Finish { Decision: MovementCompletionDecision.Grant grant } => ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref intelligenceWindow),
                    MovementDecision.Finish { Decision: MovementCompletionDecision.Lifecycle incident } => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
                    MovementDecision.Finish { Decision: MovementCompletionDecision.Hut city } => hutWindow ??= hut(city.Visit) with { Recipient = recipient(city.Visit.Actor) },
                    MovementDecision.Finish { Decision: MovementCompletionDecision.LongMove move } => LongMoveInteraction.Prompt(move.Question, recipient(move.Question.Actor)),
                    _ => throw new InvalidOperationException("Unknown movement decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var values = new OwnedList<NamedValue>([new("result", new CommandValue.Integer(operation.Result!.Value))]);
            context.Effect("movement.completed", values);
            return completed = new(Result: values);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private static ResearchReply? Reply(int? answer) => answer is { } selected ? new(selected) : null;
    public void Dispose() => operation.Dispose();
}
