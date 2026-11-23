using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

internal sealed class AutomatedOrderInteraction(Func<int, ParticipantId> recipient,
    Func<CityEspionageVisit, InteractionSpec> visit, Func<EmissaryIntelligence, InteractionSpec> intelligence,
    Func<CaptureCityVisit, InteractionSpec> capture, Func<HutCityVisit, InteractionSpec> hut)
{
    private UnitTacticsInteraction tactics = new(recipient, capture, intelligence);
    private MovementInteraction movement = new(recipient, visit, intelligence, capture, hut);
    private AutomatedOrderDecision? current;
    internal AutomatedOrderReply? Reply(AutomatedOrderDecision? pending, OperationInput input) => pending switch
    {
        AutomatedOrderDecision.Tactics choice => tactics.Reply(choice.Decision, input) is { } answer ? new(Tactics: answer) : null,
        AutomatedOrderDecision.Movement move => movement.Reply(move.Decision, input) is { } answer ? new(Movement: answer) : null,
        _ => null
    };
    internal InteractionSpec Prompt(OperationContext context, AutomatedOrderDecision pending)
    {
        if (!ReferenceEquals(current, pending))
        {
            // A pass can visit several units/cities. Only the same pending decision retains a window.
            current = pending;
            tactics = new(recipient, capture, intelligence);
            movement = new(recipient, visit, intelligence, capture, hut);
        }
        return pending switch
        {
            AutomatedOrderDecision.Tactics choice => tactics.Prompt(context, choice.Decision),
            AutomatedOrderDecision.Movement move => movement.Prompt(context, move.Decision),
            _ => throw new InvalidOperationException("Unknown automated order decision")
        };
    }
}
