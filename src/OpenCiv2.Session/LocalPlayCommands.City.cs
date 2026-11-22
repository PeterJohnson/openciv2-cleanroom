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
        runtime.RegisterCommand(Define(Production, "Choose a listed production item for an owned city.",
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

        // The bounded profile supplies an accepted player name and has no default-name catalog.
        // C01 creates before U01 erases the founder, avoiding the last-settler elimination branch.
        var stacks = new StackOperations(world, new(100000, 64));
        var units = new UnitCreation(world, context.Process, stacks, new LocalCreation(world, rules, stacks));
        var creator = new CityCreation(world, context.Process, new NameCatalog([], StringComparer.OrdinalIgnoreCase.Equals), units,
            () => throw new InvalidOperationException("City capacity cannot be reached in the one-city profile"));
        if (creator.Create(unit.Owner, point) is not { } slot)
        {
            return new(CommandStatus.Failed, Code: "city_capacity");
        }
        ref var city = ref world.Entities.Cities.At(slot);
        city.Name = name;
        var token = city.Token;
        civ.TotalCitySize = 1;

        EraseLocalUnit(world, rules, target.Slot);
        tile.Features &= ~(MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad);
        for (int observer = 1; observer < EntityLimits.Civilizations; observer++)
        {
            map.Remembered(observer, point) = tile.Features;
        }

        LocalCityEconomy.RememberCity(world, ref city);
        LocalCityEconomy.Recalculate(world, context.Process.Rules, slot);
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

        LocalCityEconomy.Toggle(world, context.Process.Rules, target.Slot, index);
        return Commit(context, "city.workers", new NamedValue("city", new CommandValue.City(target)));
    }

    private static OperationStep ChangeProduction(OperationContext context, CommandRequest request)
    {
        var target = CityArgument(request);
        int type = Number(request, "type");
        var world = context.World;
        if (!LocalGameRules.ProductionAvailable(world, context.Process.Rules.Definitions, type, target.Slot))
        {
            return new(CommandStatus.Failed, Code: "production_not_available");
        }

        var production = new ProductionOrders(world, context.Process.Rules.Definitions,
            _ => throw new InvalidOperationException("Automatic production requires its A02 policy"),
            _ => throw new InvalidOperationException("The authored profile has no AI wonder change path"));
        // This atomic authored-profile command starts a selection transaction. Both admitted
        // choices are units on Chieftain; general P06 previews retain their opening snapshot.
        production.SaveBuildState(target.Slot);
        production.Set(target.Slot, type);
        return Commit(context, "city.production", new NamedValue("city", new CommandValue.City(target)));
    }

    internal static UnitSlot? CreateUnit(SessionRuntime runtime, int type, MapPoint position, CitySlot? home) =>
        CreateUnit(runtime.World, runtime.Process, type, position, home);

    private static UnitSlot? CreateUnit(GameState world, ProcessContext process, int type, MapPoint position, CitySlot? home)
    {
        var stacks = new StackOperations(world, new(100000, 64));
        var creator = new UnitCreation(world, process, stacks, new LocalCreation(world, process.Rules.Definitions, stacks));
        var slot = creator.Create(new((byte)type), new(LocalGameRules.Owner), position);
        if (slot is { } created)
        {
            world.Entities.Units.At(created).Home = home;
        }
        return slot;
    }

    private sealed class LocalCreation(GameState world, RuleTables rules, StackOperations stacks)
        : IUnitCreationEffects, IUnitRevelationEffects
    {
        public void RevealSurroundings(int slot, bool allowContact) =>
            new UnitRevelation(world, rules, stacks, this).Reveal(slot, allowContact);

        public void Notice(UnitCreationNotice notice)
        {
            if (notice != UnitCreationNotice.TooManyUnits)
            {
                throw new InvalidOperationException("Creation tutorials require their session presentation service");
            }
            // The production caller reports capacity refusal with its own city result.
        }

        public void RevealOrigin(MapPoint position) =>
            throw new InvalidOperationException("Foreign contact is outside the authored local profile");

        public void Meet(int actingCivilization, int otherCivilization, MapPoint position) =>
            throw new InvalidOperationException("Diplomacy requires its owning session service");

        public void SynchronizeCreation() => throw new InvalidOperationException("Transport is unavailable");
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
