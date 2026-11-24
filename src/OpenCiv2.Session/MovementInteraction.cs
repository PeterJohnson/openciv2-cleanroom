using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Shared private interaction routing for U02 movement and its U04 order parent.</summary>
public sealed class MovementInteraction(Func<int, ParticipantId> recipient,
    Func<CityEspionageVisit, InteractionSpec> visit, Func<EmissaryIntelligence, InteractionSpec> intelligence,
    Func<CaptureCityVisit, InteractionSpec> capture, Func<HutCityVisit, InteractionSpec> hut)
{
    private InteractionSpec? visitWindow, intelligenceWindow, captureWindow, hutWindow;
    private DiplomaticContactDecision? contactDecision;
    private InteractionSpec? contactWindow;
    public ResearchReply? Reply(MovementDecision? pending, OperationInput input)
    {
        return pending switch
        {
            MovementDecision.Land { Decision: LandEntryDecision.City city } => CityMissionInteraction.Reply(city.Decision, input),
            MovementDecision.Land { Decision: LandEntryDecision.Unit unit } => Reply(UnitMissionInteraction.Reply(unit.Decision, input)),
            MovementDecision.ForeignUnit unit => ForeignUnitInteraction.Reply(unit.Decision, input),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Contact contact } => DiplomaticContactInteraction.Reply(contact.Decision, input),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Capture city } => CityCaptureInteraction.Reply(city.Decision, input),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Grant grant } => ResearchGrantInteraction.ResearchReply(grant.Decision, input),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Lifecycle lifecycle } => LifecycleInteraction.ResearchReply(lifecycle.Decision, input),
            MovementDecision.ForeignCity { Decision: ForeignCityDecision.Lifecycle lifecycle } => LifecycleInteraction.ResearchReply(lifecycle.Decision, input),
            MovementDecision.Land { Decision: LandEntryDecision.Caravan caravan } => Reply(CaravanInteraction.Reply(caravan.Decision, input)),
            null => null,
            _ => Reply(AttackConfirmationInteraction.Reply(input))
        };
    }
    public InteractionSpec Prompt(OperationContext context, MovementDecision pending)
    {
        return pending switch
        {
            MovementDecision.Land { Decision: LandEntryDecision.Caravan trade } => CaravanInteraction.Prompt(context, trade.Decision, recipient, ref intelligenceWindow, intelligence),
            MovementDecision.Land { Decision: LandEntryDecision.Unit unit } => UnitMissionInteraction.Prompt(context, unit.Decision, recipient, ref intelligenceWindow, intelligence),
            MovementDecision.Land { Decision: LandEntryDecision.City city } => CityMissionInteraction.Prompt(context, city.Decision, recipient, visit, capture, ref visitWindow, ref captureWindow, ref intelligenceWindow, intelligence),
            MovementDecision.ForeignUnit unit => ForeignUnitInteraction.Prompt(context, unit.Decision, recipient, intelligence, capture, ref intelligenceWindow, ref captureWindow),
            MovementDecision.Landfall landfall => LandfallInteraction.Prompt(landfall.Choice, recipient(landfall.Choice.Actor)),
            MovementDecision.ForeignCity { Decision: ForeignCityDecision.Attack attack } => AttackConfirmationInteraction.Prompt(attack.Warning, recipient(attack.Warning.Actor.Civilization)),
            MovementDecision.ForeignCity { Decision: ForeignCityDecision.Lifecycle incident } => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Contact contact } => ContactPrompt(context, contact.Decision),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Capture city } => CityCaptureInteraction.Prompt(context, city.Decision, recipient, capture, ref captureWindow, ref intelligenceWindow, intelligence),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Grant grant } => ResearchGrantInteraction.Prompt(context, grant.Decision, recipient, intelligence, ref intelligenceWindow),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Lifecycle incident } => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
            MovementDecision.Finish { Decision: MovementCompletionDecision.Hut city } => hutWindow ??= hut(city.Visit) with { Recipient = recipient(city.Visit.Actor) },
            MovementDecision.Finish { Decision: MovementCompletionDecision.LongMove move } => LongMoveInteraction.Prompt(move.Question, recipient(move.Question.Actor)),
            _ => throw new InvalidOperationException("Unknown movement decision")
        };
    }
    private static ResearchReply? Reply(int? answer) => answer is { } selected ? new(selected) : null;
    private InteractionSpec ContactPrompt(OperationContext context, DiplomaticContactDecision decision)
    {
        if (!ReferenceEquals(contactDecision, decision))
        { contactWindow = null; }
        contactDecision = decision;
        return DiplomaticContactInteraction.Prompt(context, decision, recipient, intelligence, ref contactWindow);
    }

}
