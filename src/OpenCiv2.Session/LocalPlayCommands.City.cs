using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Found = "city.found";
    public const string Worker = "city.worker";
    public const string Production = "city.production";

    private static void RegisterCityCommands(SessionRuntime runtime)
    {
        runtime.RegisterCommand(Define(Found, "Found your first city with a pioneer and an accepted name.",
            [new("unit", ValueKind.Unit), new("name", ValueKind.Text)], [new("city", ValueKind.City)],
            FoundCity, ValidateUnit));
        runtime.RegisterCommand(Define(Worker, "Toggle a worked radius tile (0–20). Select 20 to reset workers.",
            [new("city", ValueKind.City), new("tile")], [new("city", ValueKind.City)], AssignWorker, ValidateCity));
        runtime.RegisterCommand(Define(Production, "Choose an available unit type for an owned city.",
            [new("city", ValueKind.City), new("type")], [new("city", ValueKind.City)], ChangeProduction, ValidateCity));
    }

    private static CityTarget CityArgument(CommandRequest request) =>
        ((CommandValue.City)CommandDescriptor.Value(request.Arguments, "city")).Value;

    private static string? ValidateCity(CommandActor actor, CommandRequest request, GameState world)
    {
        var target = CityArgument(request);
        var cities = world.Entities.Cities;
        return cities.Live(target.Slot) && cities.At(target.Slot).Token == target.Token
            && cities.At(target.Slot).Owner == actor.Civilization ? null : "city_not_available";
    }

    private static OperationStep FoundCity(OperationContext context, CommandRequest request)
    {
        var world = context.World;
        var rules = context.Process.Rules.Definitions;
        var target = UnitArgument(request);
        var unit = world.Entities.Units.At(target.Slot);
        string name = ((CommandValue.Text)CommandDescriptor.Value(request.Arguments, "name")).Value.Trim();
        if (name.Length is < 1 or > 15 || name.Any(c => c is < ' ' or > '~'))
        {
            return new(CommandStatus.Failed, Code: "invalid_city_name");
        }

        ref var civ = ref world.Civilizations[unit.Owner.Value];
        var map = world.Map;
        var point = unit.Position;
        ref var tile = ref map.TileAt(point);
        if (unit.Type.Value != LocalGameRules.Settler || unit.Orders != UnitOrder.None
            || civ.CityCount != 0 || tile.Ocean || tile.City || tile.Airbase)
        {
            return new(CommandStatus.Failed, Code: "founding_not_available");
        }

        // One-city profile. C01 creates the city before U01 erases the founder, so deletion
        // does not enter its last-settler civilization check.
        const int index = 0;
        CitySlot slot = new(index);
        var cities = world.Entities.Cities;
        cities.SetEnd(1);
        world.Globals.CityEnd = 1;
        var token = context.Process.Tokens.AssignCity();
        ref var city = ref cities.At(slot);
        city = new()
        {
            Token = token,
            Position = point,
            Owner = unit.Owner,
            Founder = unit.Owner,
            Size = 1,
            Name = name,
            Production = LocalGameRules.Guard,
            Improvements = default(ImprovementSet).With(LocalGameRules.Palace),
            Flags = new() { TradeDirty = true }
        };
        civ.CityCount = 1;
        civ.LastCityTurn = world.Globals.Turn;
        civ.CapitalX = (short)point.X;
        civ.TotalCitySize = 1;
        ++civ.UnitsInProduction[LocalGameRules.Guard];

        tile.Features = (tile.Features | MapFeatures.CitySite) & ~MapFeatures.Fortification;
        LocalCityEconomy.ClaimRadius(world, city);
        for (int i = 0; i <= 8; i++)
        {
            var adjacent = i == 8 ? point : LocalCityEconomy.Position(world, point, i);
            if (!map.Geometry.Canonical(adjacent))
            {
                continue;
            }

            var neighbor = map.TileAt(adjacent);
            city.Flags.HydroPossible |= neighbor.River || neighbor.Terrain == Terrain.Mountains;
            if (neighbor.Ocean)
            {
                city.Flags.OceanAccess |= map.Regions[neighbor.Region].Water > 1
                    && (neighbor.Region < 63 || city.Flags.Coastal);
                city.Flags.Coastal = true;
            }
        }

        EraseLocalUnit(world, rules, target.Slot);
        tile.Features &= ~(MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad);
        for (int observer = 1; observer < EntityLimits.Civilizations; observer++)
        {
            map.Remembered(observer, point) = tile.Features;
        }

        LocalCityEconomy.RememberCity(world, ref city);
        LocalCityEconomy.Recalculate(world, rules, slot);
        SelectReadyUnit(world, rules);
        return Commit(context, "city.founded", new NamedValue("city", new CommandValue.City(new(slot, token))));
    }

    private static OperationStep AssignWorker(OperationContext context, CommandRequest request)
    {
        var target = CityArgument(request);
        int index = Number(request, "tile");
        var world = context.World;
        var city = world.Entities.Cities.At(target.Slot);
        if (index is < 0 or > LocalCityEconomy.Center || !LocalCityEconomy.Available(world, city, index))
        {
            return new(CommandStatus.Failed, Code: "worker_tile_not_available");
        }

        LocalCityEconomy.Toggle(world, context.Process.Rules.Definitions, target.Slot, index);
        return Commit(context, "city.workers", new NamedValue("city", new CommandValue.City(target)));
    }

    private static OperationStep ChangeProduction(OperationContext context, CommandRequest request)
    {
        var target = CityArgument(request);
        int type = Number(request, "type");
        var world = context.World;
        if (!LocalGameRules.ProductionAvailable(world, context.Process.Rules.Definitions, type))
        {
            return new(CommandStatus.Failed, Code: "production_not_available");
        }

        ref var city = ref world.Entities.Cities.At(target.Slot);
        ref var counts = ref world.Civilizations[city.Owner.Value].UnitsInProduction;
        --counts[city.Production];
        city.Production = (sbyte)type;
        ++counts[type];
        return Commit(context, "city.production", new NamedValue("city", new CommandValue.City(target)));
    }

    internal static UnitSlot? CreateUnit(SessionRuntime runtime, int type, MapPoint position, CitySlot? home) =>
        CreateUnit(runtime.World, runtime.Process, type, position, home);

    private static UnitSlot? CreateUnit(GameState world, ProcessContext process, int type, MapPoint position, CitySlot? home)
    {
        CivilizationSlot owner = new(LocalGameRules.Owner);
        var pool = world.Entities.Units;
        ref var civ = ref world.Civilizations[owner.Value];
        if (EntityAllocation.ReserveUnit(pool, owner, civ.MilitaryCount, world.Globals.HumanPlayers) is not { } slot)
        {
            return null;
        }

        var rule = process.Rules.Definitions.Units[type];
        world.Globals.UnitEnd = (short)pool.End;
        ++civ.UnitsActive[type];
        ++world.UnitTotals[owner.Value];
        if (rule.Role < RuleRole.Settle)
        {
            ++civ.MilitaryCount;
        }

        ref var unit = ref pool.At(slot);
        unit = new()
        {
            Owner = owner,
            Type = new((byte)type),
            Token = process.Tokens.AssignUnit(),
            Home = home,
            LastDirection = byte.MaxValue,
            Position = new(-1, -1),
            Destination = new(-1, -1)
        };
        new StackOperations(world, new(100000, 64)).AttachAtFront(slot.Value, position, false);
        LocalVisibility.RevealAround(world, unit, rule);
        return slot;
    }

    private static void EraseLocalUnit(GameState world, RuleTables rules, UnitSlot slot) =>
        new UnitDeletion(world, rules, new(100000, 64), LocalDeletion.Instance).Erase(slot.Value, false);

    // This profile has neither transport nor a path to eliminating the last player city.
    private sealed class LocalDeletion : IUnitDeletionEffects
    {
        public static LocalDeletion Instance { get; } = new();
        public void UnitRemoved(MapPoint position) => throw new InvalidOperationException("Use a session effect");
        public void RefreshHomeCity(int city) => throw new InvalidOperationException("Recalculate in the owning phase");
        public void CheckCivilization(int owner, int killer) => throw new InvalidOperationException("Elimination is unavailable");
        public void SynchronizeDeletion(MapPoint position, CitySlot? home) =>
            throw new InvalidOperationException("Transport is unavailable");
    }
}
