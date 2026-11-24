using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Commodities = "trade.commodities";
    private static void RegisterCommodityCommand(SessionRuntime runtime) => runtime.RegisterCommand(new(
        new(1, Commodities, "Search known commodity supply and demand after discovering Trade.", AccessClass.Player, new([]), new([])),
        (actor, _, world) => new(ValidatePhase(actor, world)), _ => new CommoditySearchOperation()));
}
