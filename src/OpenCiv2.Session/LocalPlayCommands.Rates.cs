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

    internal static OperationStep ApplyRates(OperationContext context, RateValues values, IPreferenceWriter preferences)
    {
        int owner = context.Actor.Civilization.Value, tax = values.Tax, science = values.Science;
        if (!CivilizationRates.Allowed(context.World, owner, tax, science))
        {
            return new(CommandStatus.Failed, Code: "rates_not_available");
        }
        var rates = new CivilizationRates(context.World, context.Process, new StackOperations(context.World, new(5_000_000, 64)));
        bool saved = rates.Accept(owner, values,
            () => preferences.Write(PreferenceFiles.Capture(context.Process)));
        var result = Commit(context, "civilization.rates", new("tax", new CommandValue.Integer(tax)),
            new("science", new CommandValue.Integer(science)), new("luxury", new CommandValue.Integer(values.Luxury)));
        return saved ? result : result with { Status = CommandStatus.Failed, Code = "preferences_output_failed" };
    }
}
