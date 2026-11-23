using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed class RateDecisionPreferences
{
    public RateHolds Holds { get; set; }
}

/// <summary>V02 decisions stay on the session lane. Presentation renders owned choices;
/// preview and normalization effects survive cancellation of the draft.</summary>
public sealed class CivilizationRateDecision(IPreferenceWriter preferences, RateDecisionPreferences choices, bool duringTurn = false) : IContinuation
{
    private RateDraft? draft;
    private RateReport report;
    private int menu;
    private int government, liveTax, liveScience;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
        {
            return new(CommandStatus.Cancelled, Code: "cancelled");
        }
        if (LocalPlayCommands.ValidatePhase(context.Actor, context.World, !duringTurn) is { } error)
        {
            return new(CommandStatus.Failed, Code: error);
        }
        int owner = context.Actor.Civilization.Value;
        ref var civ = ref context.World.Civilizations[owner];
        var rates = new CivilizationRates(context.World, context.Process,
            new StackOperations(context.World, new(5_000_000, 64)));
        if (draft is null)
        {
            int oldTax = civ.TaxRate, oldScience = civ.ScienceRate;
            draft = rates.Prepare(owner, choices.Holds);
            government = (byte)civ.Government;
            liveTax = civ.TaxRate;
            liveScience = civ.ScienceRate;
            if (oldTax != liveTax || oldScience != liveScience)
            {
                context.Effect("civilization.rates.normalized", Values(draft.Selected));
            }
            Preview(context, rates, owner);
        }
        else
        {
            if ((byte)civ.Government != government || civ.TaxRate != liveTax || civ.ScienceRate != liveScience)
            {
                return new(CommandStatus.Failed, Code: "rates_changed");
            }
            if (input.Kind != OperationInputKind.Choice || input.Choice is not int value)
            {
                return Decision();
            }
            if (menu == 0)
            {
                if (value == 1)
                {
                    return LocalPlayCommands.ApplyRates(context, draft.Selected, preferences);
                }
                menu = value;
            }
            else
            {
                if (menu == 5)
                {
                    foreach (var kind in new[] { RateKind.Tax, RateKind.Science, RateKind.Luxury })
                    {
                        draft.Hold(kind, value == (int)kind + 1 ? 1 : 0);
                    }
                    choices.Holds = draft.Holds;
                }
                else
                {
                    draft.Adjust((RateKind)(menu - 2), value);
                    Preview(context, rates, owner);
                }
                menu = 0;
            }
        }
        return Decision();
    }

    private void Preview(OperationContext context, CivilizationRates rates, int owner)
    {
        var preview = rates.Preview(owner, draft!);
        report = rates.Report(owner, preview);
        context.Effect("civilization.rates.previewed", Values(draft!.Selected));
    }

    private static OwnedList<NamedValue> Values(RateValues values) => new([
        new("tax", new CommandValue.Integer(values.Tax)), new("science", new CommandValue.Integer(values.Science)),
        new("luxury", new CommandValue.Integer(values.Luxury))]);

    private OperationStep Decision()
    {
        var selected = draft!.Selected;
        var held = draft.Holds;
        string hold = held.Tax != 0 ? "Tax" : held.Science != 0 ? "Science" : held.Luxury != 0 ? "Luxury" : "None";
        string prompt = $"Tax {selected.Tax * 10}% · Science {selected.Science * 10}% · Luxury {selected.Luxury * 10}%\n"
            + $"Income {report.Preview.Tax}, upkeep {report.Preview.Upkeep}, science {report.Preview.Science}. "
            + $"Discovery in {report.DiscoveryTurns} turns.\nMaximum {draft.Cap * 10}% · Hold: {hold}.";
        InteractionOption[] options;
        if (menu == 0)
        {
            options = [new(1, "Apply rates"), new(2, "Adjust tax"), new(3, "Adjust science"),
                new(4, "Adjust luxury"), new(5, "Choose a hold")];
        }
        else if (menu == 5)
        {
            prompt += "\nChoose a rate to hold while adjusting another rate.";
            options = [new(0, "No hold"), new(1, "Hold tax"), new(2, "Hold science"), new(3, "Hold luxury")];
        }
        else
        {
            prompt += $"\nChoose {(RateKind)(menu - 2)}; the other rates will balance to 100%.";
            options = Enumerable.Range(0, draft.Cap + 1).Select(value => new InteractionOption(value, $"{value * 10}%")).ToArray();
        }
        return new(CommandStatus.AwaitingInteraction, Interaction: new(prompt, null, null,
            new(options.Select(option => option.Value)))
        { Options = new(options) });
    }
}
