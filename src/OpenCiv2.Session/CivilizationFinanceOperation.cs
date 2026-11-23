using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V02 §11 session presentation and actual rate child, after F04 assessment.</summary>
public sealed class CivilizationFinanceOperation(IPreferenceWriter preferences, RateDecisionPreferences choices, int startGold) : IContinuation
{
    private CivilizationFinances.Operation? operation;
    private CivilizationNotice? shown;
    private CivilizationRateDecision? rates;
    private string failure = "";

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (LocalPlayCommands.ValidatePhase(context.Actor, context.World, false) is { } error)
        {
            return new(CommandStatus.Failed, Code: error);
        }

        int owner = context.Actor.Civilization.Value;
        operation ??= new(context.World, owner, startGold);
        if (rates is not null)
        {
            var result = rates.Resume(context, input);
            if (result.Status == CommandStatus.AwaitingInteraction)
            {
                return result;
            }

            if (result.Status == CommandStatus.Failed)
            {
                failure = result.Code;
            }

            rates = null;
            operation.Reply(0);
        }
        else if (shown is not null)
        {
            if (input.Kind != OperationInputKind.Choice || input.Choice is not int value
                || (shown.Kind == CivilizationNoticeKind.Population ? value != 0 : value is not (0 or 1)))
            {
                return Decision(shown);
            }

            operation.Reply(value);
            shown = null;
        }
        int oldMilestone = context.World.Civilizations[owner].PopulationMilestone;
        var next = operation.Next();
        if (oldMilestone != context.World.Civilizations[owner].PopulationMilestone)
        {
            context.Effect("civilization.population.milestone", new([new("milestone", new CommandValue.Integer(context.World.Civilizations[owner].PopulationMilestone))]));
        }

        if (next is null)
        {
            return new(failure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, Code: failure);
        }

        if (next.Kind == CivilizationNoticeKind.Rates)
        {
            rates = new(preferences, choices, duringTurn: true);
            return rates.Resume(context, new());
        }
        shown = next;
        context.Effect(next.Kind == CivilizationNoticeKind.Population ? "civilization.population.notice" : "civilization.funds.notice", OwnedList<NamedValue>.Empty);
        return Decision(next);
    }

    private static OperationStep Decision(CivilizationNotice notice)
    {
        bool population = notice.Kind == CivilizationNoticeKind.Population;
        InteractionOption[] options = population ? [new(0, "Continue")] : [new(0, "Keep rates"), new(1, "Edit rates")];
        return new(CommandStatus.AwaitingInteraction, Interaction: new(
            population ? $"Your civilization has reached {notice.Population * 10000:N0} people."
                : "Your treasury is running low. Adjust the tax rate?", null, null,
            new(options.Select(o => o.Value)), Cancellable: false)
        { Options = new(options) });
    }
}
