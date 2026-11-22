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
        UnitMovement.Exhaust(world, rules, ref unit);

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
        var best = UnitActivation.NextSelectable(world, rules, current, LocalGameRules.StartingTile,
            searchMode: 0, world.Multiplayer.LocalHuman);

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
            int previousFood = city.FoodBox;
            var food = CreateFood(context);
            if (food.Run(slot))
            {
                return false;
            }

            if (city.Size != before)
            {
                civ.TotalCitySize = city.Size;
                LocalCityEconomy.RememberCity(world, ref city);
                context.Effect("city.population", new([new("city", new CommandValue.City(target)),
                    new("size", new CommandValue.Integer(city.Size))]));
            }

            LocalCityEconomy.Recalculate(world, context.Process.Rules, slot);
            int surplus = food.Accumulate(slot, previousFood);
            var production = CreateProduction(context).Run(slot, $"local-production/{world.Globals.Turn}/{slot.Value}");
            if (production is { Outcome: UnitProductionOutcome.Completed, Unit: { } produced })
            {
                context.Effect("unit.produced", new([new("unit", new CommandValue.Unit(produced)),
                    new("city", new CommandValue.City(target))]));
            }
            else if (production.Outcome == UnitProductionOutcome.CreationFailed)
            {
                context.Effect("city.production_spent", new([new("city", new CommandValue.City(target))]));
            }

            RetireUnsupportedUnits(context, slot);

            LocalCityEconomy.Recalculate(world, context.Process.Rules, slot);
            var moodEffects = new CityTurnEffects(context);
            var governments = new GovernmentChanges(world, context.Process.Rules, new(world, new(5_000_000, 64)),
                _ => throw new InvalidOperationException("The authored profile has no government selection path"),
                _ => throw new InvalidOperationException("The authored profile has no government rate decision path"),
                _ => throw new InvalidOperationException("The authored profile has no AI government path"));
            new CityMoodTurn(world, rules, governments, moodEffects.ReconsiderProduction, moodEffects).Run(slot, surplus);
            if (LocalResearch.Accumulate(world, rules, city.Owner.Value, city.Science) is int advance)
            {
                context.Effect("research.completed", new([new("advance", new CommandValue.Integer(advance))]));
                return true;
            }

            return city.Science > 0 && civ.ResearchProgress != 0 && civ.Researching < 0;
        }

        private static void RetireUnsupportedUnits(OperationContext context, CitySlot city)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            var budget = new StackBudget(1000000, 64);
            var stacks = new StackOperations(world, budget);
            var effects = new CityTurnEffects(context);
            var deletion = new UnitDeletion(world, rules, budget, effects);
            var loss = new UnitLoss(world, rules, stacks, new(world, rules, stacks), deletion, effects);
            new CityRetirement(world, rules, deletion, loss, effects).Run(city);
        }

        private static CityUnitProduction CreateProduction(OperationContext context)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            var budget = new StackBudget(1000000, 64);
            var stacks = new StackOperations(world, budget);
            var effects = new CityTurnEffects(context);
            var deletion = new UnitDeletion(world, rules, budget, effects);
            var loss = new UnitLoss(world, rules, stacks, new(world, rules, stacks), deletion, effects);
            var removal = new CityRemoval(world, rules, stacks, loss, target =>
                context.Effect("city.removed", new([new("city", new CommandValue.City(target))])));
            var creation = new UnitCreation(world, context.Process, stacks, new LocalCreation(world, rules, stacks));
            return new(world, rules, creation, deletion, removal, context.Gameplay, effects, effects);
        }

        private static CityFood CreateFood(OperationContext context)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            var budget = new StackBudget(1000000, 64);
            var stacks = new StackOperations(world, budget);
            var effects = new CityTurnEffects(context);
            var deletion = new UnitDeletion(world, rules, budget, effects);
            var loss = new UnitLoss(world, rules, stacks, new(world, rules, stacks), deletion, effects);
            var removal = new CityRemoval(world, rules, stacks, loss, target =>
                context.Effect("city.removed", new([new("city", new CommandValue.City(target))])));
            return new(world, rules, removal, loss, effects, effects);
        }

        private sealed class CityTurnEffects(OperationContext context)
            : ICityRetirementEffects, IUnitLossEffects, IUnitDeletionEffects, ICityFoodEffects, ICityFoodContinuations,
                ISpyEscapeEffects, ICivilizationEliminationEffects, ICityMoodEffects,
                ICityUnitProductionContinuations, ICityUnitProductionEffects
        {
            private CivilizationElimination? elimination;
            private int eliminationCall;
            private NamedValue CityValue(CitySlot city) =>
                new("city", new CommandValue.City(new(city, context.World.Entities.Cities.At(city).Token)));

            public void Famine(CitySlot city, FamineCause cause, string? detail) =>
                context.Effect("city.famine", new([CityValue(city), new("cause", new CommandValue.Text(cause.ToString())),
                    new("detail", new CommandValue.Text(detail ?? ""))]));
            public void GrowthHalted(CitySlot city, int facility) =>
                context.Effect("city.growth_halted", new([CityValue(city), new("facility", new CommandValue.Integer(facility))]));
            public void FoodShortage(CitySlot city) => context.Effect("city.food_shortage", new([CityValue(city)]));
            public void RedrawCity(CitySlot city) => RedrawTile(context.World.Entities.Cities.At(city).Position);
            public void MoodNotice(CityMoodNotice notice)
            {
                if (!context.World.Multiplayer.Saved.AutoplayActive)
                {
                    context.Effect("city.mood", new([CityValue(notice.City),
                        new("kind", new CommandValue.Text(notice.Kind.ToString())),
                        new("government", new CommandValue.Integer((int)notice.Government))]));
                }
            }
            public void ReconsiderProduction(CitySlot city) =>
                throw new InvalidOperationException("Automatic production requires its A02 policy");
            public void ChooseTradeCargo(CitySlot city, int owner, UnitSlot unit) =>
                throw new InvalidOperationException("The authored profile has no caravan production path");
            public void Purchase(CitySlot city, int owner, int netShields) =>
                throw new InvalidOperationException("The authored profile has no AI purchase path");
            public bool AbandonCity(CitySlot city, UnitType type) =>
                throw new InvalidOperationException("The authored profile cannot build settlers");
            public void Upgraded(CitySlot city, UnitType oldType, UnitType newType) => context.Effect("city.production_upgraded", new([
                CityValue(city), new("oldType", new CommandValue.Integer(oldType.Value)), new("newType", new CommandValue.Integer(newType.Value))]));
            public void Built(CityTarget city, UnitType type) => context.Effect("unit.built", new([
                new("city", new CommandValue.City(city)), new("type", new CommandValue.Integer(type.Value))]));

            public void SupportShortage(CitySlot city, UnitTarget selected, UnitType type) =>
                context.Effect("unit.support_shortage", new([new("unit", new CommandValue.Unit(selected)),
                    new("type", new CommandValue.Integer(type.Value))]));

            public void UnitRemoved(CitySlot city, UnitTarget selected, bool shortage) =>
                context.Effect(shortage ? "unit.unsupported" : "unit.retired", new([new("unit", new CommandValue.Unit(selected))]));

            // Session publication rebuilds the map/city projections from these committed effects.
            public void UnitsChangedAt(MapPoint position) => RedrawTile(position);
            public void UnitRemoved(MapPoint position) => RedrawTile(position);
            public void RedrawTile(MapPoint position) => context.Effect("map.changed",
                new([new("x", new CommandValue.Integer(position.X)), new("y", new CommandValue.Integer(position.Y))]));
            public void RefreshHomeCity(int city) => context.Effect("city.support_changed",
                new([new("city", new CommandValue.City(new(new((ushort)city), context.World.Entities.Cities.Storage[city].Token)))]));
            public void CheckCivilization(int owner, int killer) => Eliminate(owner, killer);
            int ICityUnitProductionContinuations.CheckCivilization(int owner, int killer) => Eliminate(owner, killer);
            private int Eliminate(int owner, int killer)
            {
                if (elimination is null)
                {
                    var world = context.World;
                    var rules = context.Process.Rules.Definitions;
                    var budget = new StackBudget(1000000, 64);
                    var stacks = new StackOperations(world, budget);
                    var deletion = new UnitDeletion(world, rules, budget, this);
                    var loss = new UnitLoss(world, rules, stacks, new(world, rules, stacks), deletion, this);
                    var escape = new SpyEscape(world, rules, stacks, loss, context.Gameplay,
                        (_, _) => throw new InvalidOperationException("The authored profile has no foreign espionage path"), this);
                    elimination = new(world, deletion, escape,
                        _ => throw new InvalidOperationException("The authored profile has no AI restart path"), this);
                }
                return elimination.Check(owner, killer, "local-elimination/" + eliminationCall++);
            }
            public void Notice(SpyNotice notice) => context.Effect("unit.spy_fate", new([
                new("unit", new CommandValue.Unit(notice.Unit)), new("fate", new CommandValue.Text(notice.Fate.ToString()))]));
            public void Redraw(MapPoint position) => RedrawTile(position);
            public void Destroyed(CivilizationDestroyedNotice notice) => context.Effect("civilization.destroyed", new([
                new("civilization", new CommandValue.Integer(notice.Civilization)), new("killer", new CommandValue.Integer(notice.Killer)),
                new("adjective", new CommandValue.Text(notice.Adjective)), new("killerName", new CommandValue.Text(notice.KillerName))]));
            public void SpaceshipLost(SpaceshipLossNotice notice) => context.Effect("spaceship.lost", new([
                new("civilization", new CommandValue.Integer(notice.Civilization)), new("returning", new CommandValue.Boolean(notice.Returning))]));
            public void RedrawMaps() => context.Effect("map.refresh", new([]));
            public void SynchronizeLoss(MapPoint? redraw) => throw new InvalidOperationException("Transport is unavailable");
            public void SynchronizeDeletion(MapPoint position, CitySlot? home) => throw new InvalidOperationException("Transport is unavailable");
        }
    }
}
