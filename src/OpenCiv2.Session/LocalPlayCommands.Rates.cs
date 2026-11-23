using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Rates = "civilization.rates";

    private static void RegisterRateCommands(SessionRuntime runtime, IPreferenceWriter preferences)
    {
        runtime.RegisterCommand(Define(Rates, "Set tax and science in tenths; the remainder is luxury.",
            [new("tax"), new("science")], [new("tax"), new("science"), new("luxury")],
            (context, request) => ChangeRates(context, request, preferences)));
    }

    private static OperationStep ChangeRates(OperationContext context, CommandRequest request, IPreferenceWriter preferences)
    {
        int owner = context.Actor.Civilization.Value;
        int tax = Number(request, "tax"), science = Number(request, "science");
        if (!CivilizationRates.Allowed(context.World, owner, tax, science))
        {
            return new(CommandStatus.Failed, Code: "rates_not_available");
        }
        var values = new RateValues(tax, science, 10 - tax - science);
        var rates = new CivilizationRates(context.World, context.Process, new StackOperations(context.World, new(5_000_000, 64)));
        bool saved = rates.Accept(owner, values,
            () => preferences.Write(PreferenceFiles.Capture(context.Process)));
        var result = Commit(context, "civilization.rates", new("tax", new CommandValue.Integer(tax)),
            new("science", new CommandValue.Integer(science)), new("luxury", new CommandValue.Integer(values.Luxury)));
        return saved ? result : result with { Status = CommandStatus.Failed, Code = "preferences_output_failed" };
    }
}
