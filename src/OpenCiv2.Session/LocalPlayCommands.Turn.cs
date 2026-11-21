using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Fortify = "unit.fortify";
    public const string Research = "research.choose";
    public const string EndTurn = "turn.end";

    private static void RegisterTurnCommands(SessionRuntime runtime)
    {
        runtime.RegisterCommand(Define(Fortify, "Fortify an owned land unit and spend its remaining movement.",
            [new("unit", ValueKind.Unit)], [new("unit", ValueKind.Unit)], FortifyUnit, ValidateUnit));
        runtime.RegisterCommand(Define(Research, "Select an available advance by its listed ID.",
            [new("advance")], [new("advance")], ChooseResearch));
        runtime.RegisterCommand(new(
            new(1, EndTurn, "Finish the player phase and advance the local turn.",
                AccessClass.Player, new([]), new([new("turn")])),
            (actor, _, world) => new(ValidatePhase(actor, world)),
            _ => new EndPlayerPhase()));
    }

    private static OperationStep FortifyUnit(OperationContext context, CommandRequest request)
    {
        var target = UnitArgument(request);
        ref var unit = ref context.World.Entities.Units.At(target.Slot);
        if (context.World.Map.TileAt(unit.Position).Ocean || unit.Orders != UnitOrder.None
            || LandMovement.Left(context.World, context.Process.Rules.Definitions, unit) == 0)
        {
            return new(CommandStatus.Failed, Code: "order_not_available");
        }

        unit.Orders = UnitOrder.Fortifying;
        Exhaust(context.World, context.Process.Rules.Definitions, ref unit);
        SelectReadyUnit(context.World, context.Process.Rules.Definitions);
        return Commit(context, "unit.fortifying", new NamedValue("unit", new CommandValue.Unit(target)));
    }

    private static void Exhaust(GameState world, RuleTables rules, ref Unit unit) =>
        unit.MovesUsed = unchecked((byte)LandMovement.Allowance(world, rules, unit));

    private static OperationStep ChooseResearch(OperationContext context, CommandRequest request)
    {
        int advance = Number(request, "advance");
        return SetResearch(context, advance)
            ? new(Result: new([new("advance", new CommandValue.Integer(advance))]))
            : new(CommandStatus.Failed, Code: "research_not_available");
    }

    private static bool SetResearch(OperationContext context, int advance)
    {
        int owner = context.Actor.Civilization.Value;
        if (!LocalResearch.Available(context.World, context.Process.Rules.Definitions, owner, advance))
        {
            return false;
        }

        context.World.Civilizations[owner].Researching = (short)advance;
        context.Effect("research.selected", new([new("advance", new CommandValue.Integer(advance))]));
        return true;
    }

    internal static int[] ResearchChoices(GameState world, RuleTables rules, int owner) =>
        Enumerable.Range(0, rules.Advances.Length)
            .Where(a => LocalResearch.Available(world, rules, owner, a)).ToArray();

    internal static void SelectReadyUnit(GameState world, RuleTables rules)
    {
        var pool = world.Entities.Units;
        var current = world.Globals.ActiveUnit;
        var origin = current is { } slot && pool.Live(slot) ? pool.At(slot).Position : LocalGameRules.StartingTile;
        int minimumDistance = int.MaxValue;
        UnitSlot? best = null;
        for (ushort i = 0; i < pool.End; i++)
        {
            ref var unit = ref pool.Storage[i];
            if (!unit.Token.Occupied || unit.Owner.Value != world.Globals.TurnCiv
                || unit.Orders is UnitOrder.Fortified or UnitOrder.Sleeping
                || LandMovement.Left(world, rules, unit) == 0)
            {
                continue;
            }

            int distance = world.Map.Geometry.WeightedDistance(origin, unit.Position, world.Globals.Rules.FlatWorld);
            if (distance < minimumDistance || (distance == minimumDistance && current?.Value == i))
            {
                minimumDistance = distance;
                best = new(i);
            }
        }

        world.Globals.ActiveUnit = best;
        if (best is { } selected)
        {
            ref var unit = ref pool.At(selected);
            if (unit.Orders == UnitOrder.None)
            {
                unit.AuxiliaryTimer = 0;
            }

            if (unit.MovesUsed == 0)
            {
                LocalVisibility.RevealAround(world, unit, rules.Units[unit.Type.Value]);
            }
        }

        world.Globals.TransientFlags.EndOfTurn = best is null;
    }

    /// <summary>Local F04 mode adapter. The controller supplies the end-phase and research decisions.</summary>
    private sealed class EndPlayerPhase : IContinuation
    {
        private bool upkeepCommitted;

        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            var world = context.World;
            if (input.Kind == OperationInputKind.Begin)
            {
                if (ValidatePhase(context.Actor, world) is { } error)
                {
                    return new(CommandStatus.Failed, Code: error);
                }

                if (world.Globals.Turn == short.MaxValue)
                {
                    return new(CommandStatus.Failed, Code: "turn_limit_reached");
                }

                return new(CommandStatus.AwaitingInteraction,
                    Interaction: new("End the current player turn?", null, null, new([0, 1])));
            }

            if (!upkeepCommitted && (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
                || input.Choice == 0))
            {
                return new(CommandStatus.Cancelled, Code: "cancelled");
            }

            if (upkeepCommitted)
            {
                if (input.Choice is not int selected || !SetResearch(context, selected))
                {
                    return ResearchDecision(context);
                }
            }
            else
            {
                bool needsResearch = Advance(context);
                upkeepCommitted = true;
                if (needsResearch && ResearchDecision(context) is { Interaction: not null } waiting)
                {
                    return waiting;
                }
            }

            world.PlayerPhaseActive = true;
            context.Publish();
            return new(Result: TurnValue(world));
        }

        private static OperationStep ResearchDecision(OperationContext context)
        {
            var world = context.World;
            int owner = context.Actor.Civilization.Value;
            var choices = ResearchChoices(world, context.Process.Rules.Definitions, owner);
            return choices.Length == 0 || world.Civilizations[owner].Researching >= 0 ? new() :
                new(CommandStatus.AwaitingInteraction,
                    Interaction: new("Choose the next advance.", null, null, new(choices), Cancellable: false));
        }

        private static OwnedList<NamedValue> TurnValue(GameState world) =>
            new([new("turn", new CommandValue.Integer(world.Globals.Turn))]);

        private static bool Advance(OperationContext context)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            int owner = context.Actor.Civilization.Value;
            foreach (ref var unit in world.Entities.Units.Scanned)
            {
                if (unit.Token.Occupied && unit.Owner.Value == owner)
                {
                    Exhaust(world, rules, ref unit);
                }
            }
            world.PlayerPhaseActive = false;
            world.Multiplayer.ResumeCiv = world.Multiplayer.Saved.ResumeCiv = 0;

            // Global advance and all-unit reset precede civilization recovery and its city pass.
            ref var globals = ref world.Globals;
            ++globals.Turn;
            if (globals.Turn > 199)
            {
                ++globals.PeaceTurns;
            }

            globals.Year = (short)Calendar.Year(globals.Turn, globals, world.Scenario, GameplayContent.Builtin.Values.Calendar);
            globals.TransientFlags.EndOfTurn = false;
            ResetUnits(world);
            globals.TurnCiv = (sbyte)owner;
            ref var treasury = ref world.Civilizations[owner].Gold;
            treasury = Math.Max(treasury, 0);

            bool needsResearch = false;
            for (int i = world.Entities.Cities.End - 1; i >= 0; i--)
            {
                ref var city = ref world.Entities.Cities.Storage[i];
                if (city.Token.Occupied && city.Owner.Value == owner)
                {
                    needsResearch |= CityTurn(context, new((ushort)i));
                }
            }

            globals.ActiveUnit = null;
            SelectReadyUnit(world, rules);
            context.Effect("turn.advanced", TurnValue(world));
            return needsResearch;
        }

        private static void ResetUnits(GameState world)
        {
            foreach (ref var tile in world.Map.Tiles)
            {
                tile.Features &= ~MapFeatures.Unit;
            }

            var units = world.Entities.Units.Scanned;
            for (int i = 0; i < units.Length; i++)
            {
                ref var unit = ref units[i];
                if (!unit.Token.Occupied)
                {
                    continue;
                }

                world.Map.TileAt(unit.Position).Features |= MapFeatures.Unit;
                unit.MovesUsed = unit.AuxiliaryTimer = 0;
                unit.Flags.Waiting = false;
                if (!unit.Flags.BorderIntruder && (world.Globals.Turn & 3) == (i & 3))
                {
                    unit.Flags.BorderWarned = false;
                }

                unit.Flags.BorderIntruder = unit.Flags.Moved = unit.Flags.Paradropped = false;
                if (unit.Orders == UnitOrder.Fortifying)
                {
                    unit.Orders = UnitOrder.Fortified;
                }
            }
        }

        private static bool CityTurn(OperationContext context, CitySlot slot)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            ref var city = ref world.Entities.Cities.At(slot);
            ref var civ = ref world.Civilizations[city.Owner.Value];
            CityTarget target = new(slot, city.Token);
            int before = city.Size;
            if (city.FoodBox < 0)
            {
                // Every allowed city center feeds a size-one city. The profile has no food routes or homed settlers.
                --city.Size;
                city.FoodBox = 0;
            }
            else if (city.FoodBox >= (city.Size + 1) * rules.Cosmic.FoodBoxRows)
            {
                if (city.Size < rules.Cosmic.AqueductSize)
                {
                    ++city.Size;
                }

                city.FoodBox = 0;
            }

            if (city.Size != before)
            {
                civ.TotalCitySize = city.Size;
                LocalCityEconomy.RememberCity(world, ref city);
                context.Effect("city.population", new([new("city", new CommandValue.City(target)),
                    new("size", new CommandValue.Integer(city.Size))]));
            }

            int support = LocalCityEconomy.Recalculate(world, rules, slot);
            int surplus = city.TotalFood - city.Size * rules.Cosmic.FoodPerCitizen;
            city.FoodBox = unchecked((short)(city.FoodBox + surplus));
            int type = city.Production;
            city.ShieldBox += (short)Math.Max(0, city.TotalShields - support);
            int cost = rules.Units[type].CostRows * rules.Cosmic.ShieldBoxRows;
            if (city.ShieldBox >= cost)
            {
                city.ShieldBox -= (short)cost;
                if (CreateUnit(world, context.Process, type, city.Position, slot) is { } created)
                {
                    city.ShieldBox = 0;
                    var token = world.Entities.Units.At(created).Token;
                    context.Effect("unit.produced", new([new("unit", new CommandValue.Unit(new(created, token))),
                        new("city", new CommandValue.City(target))]));
                }
                else
                {
                    context.Effect("city.production_spent", new([new("city", new CommandValue.City(target))]));
                }
            }

            while (LocalCityEconomy.Support(world, slot) > city.TotalShields)
            {
                RemoveUnsupportedUnit(context, slot, city.Position);
            }

            support = LocalCityEconomy.Recalculate(world, rules, slot);
            city.Flags.MoodOnEdge = city.Happy == 0 && city.TotalShields > support && surplus >= 0;
            city.Flags.Celebrating = city.Size > 2 && city.Happy >= (city.Size + 1) / 2;
            civ.Gold = unchecked(civ.Gold + city.Tax);
            if (LocalResearch.Accumulate(world, rules, city.Owner.Value, city.Science) is int advance)
            {
                context.Effect("research.completed", new([new("advance", new CommandValue.Integer(advance))]));
                return true;
            }

            return city.Science > 0 && civ.ResearchProgress != 0 && civ.Researching < 0;
        }

        private static void RemoveUnsupportedUnit(OperationContext context, CitySlot home, MapPoint city)
        {
            var world = context.World;
            var pool = world.Entities.Units.Scanned;
            int best = -1, distance = -1;
            for (int i = 0; i < pool.Length; i++)
            {
                ref readonly var unit = ref pool[i];
                if (!unit.Token.Occupied || unit.Home != home)
                {
                    continue;
                }

                int score = world.Map.Geometry.WeightedDistance(city, unit.Position, world.Globals.Rules.FlatWorld);
                if (score > distance)
                {
                    best = i;
                    distance = score;
                }
            }

            UnitTarget target = new(new((ushort)best), pool[best].Token);
            EraseLocalUnit(world, context.Process.Rules.Definitions, target.Slot);
            context.Effect("unit.unsupported", new([new("unit", new CommandValue.Unit(target))]));
        }
    }
}
