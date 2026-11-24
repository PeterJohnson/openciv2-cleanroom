using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string EditRates = "civilization.rates.edit";
    public const string Rates = "civilization.rates";

    private static void RegisterRateCommands(SessionRuntime runtime, IPreferenceWriter preferences, RateDecisionPreferences choices)
    {
        runtime.RegisterCommand(Define(Rates, "Set tax and science in tenths; the remainder is luxury.",
            [new("tax"), new("science")], [new("tax"), new("science"), new("luxury")],
            (context, request) => ChangeRates(context, request, preferences)));
        runtime.RegisterCommand(new(
            new(1, EditRates, "Preview and edit civilization rates, with balancing and holds.", AccessClass.Player,
                new([]), new([new("tax"), new("science"), new("luxury")])),
            (actor, _, world) => new(ValidatePhase(actor, world)),
            _ => new CivilizationRateDecision(preferences, choices)));
    }

    private static OperationStep ChangeRates(OperationContext context, CommandRequest request, IPreferenceWriter preferences)
    {
        int tax = Number(request, "tax"), science = Number(request, "science");
        return ApplyRates(context, new(tax, science, unchecked(10 - tax - science)), preferences);
    }

    internal static OperationStep ApplyRates(OperationContext context, RateValues values, IPreferenceWriter preferences) =>
        CivilizationRateDecision.Accept(context, context.Actor.Civilization.Value, values, preferences);
}
