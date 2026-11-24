using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Session;

public static partial class LocalPlayCommands
{
    public const string Fortify = "unit.fortify";
    public const string Research = "research.choose";
    public const string EndTurn = "turn.end";

    private static void RegisterTurnCommands(SessionRuntime runtime, IPreferenceWriter preferences, RateDecisionPreferences choices, HallOfFame hall)
    {
        runtime.RegisterCommand(Define(Fortify, "Fortify an owned land unit and spend its remaining movement.",
            [new("unit", ValueKind.Unit)], [new("unit", ValueKind.Unit)], FortifyUnit, ValidateUnit));
        runtime.RegisterCommand(Define(Research, "Select an available advance by its listed ID.",
            [new("advance")], [new("advance")], ChooseResearch));
        runtime.RegisterCommand(new(
            new(1, EndTurn, "Finish the player phase and advance the local turn.",
                AccessClass.Player, new([]), new([new("turn")])),
            (actor, _, world) => new(ValidatePhase(actor, world)),
            _ => new EndPlayerPhase(preferences, choices, hall)));
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
        SelectReadyUnit(context);
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
        if (!ResearchChoices(context.World, context.Process.Rules.Definitions, owner).Contains(advance))
        {
            return false;
        }

        context.World.Civilizations[owner].Researching = (short)advance;
        context.Effect("research.selected", new([new("advance", new CommandValue.Integer(advance))]));
        return true;
    }

    internal static int[] ResearchChoices(GameState world, RuleTables rules, int owner) =>
        Core.Research.Options(world, rules, owner).Where(a => a != CivilizationKnowledge.FutureTechnology).ToArray();

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
    private sealed class EndPlayerPhase(IPreferenceWriter preferences, RateDecisionPreferences choices, HallOfFame hall) : IContinuation
    {
        private bool confirmed, movementChecked, globalAdvanced, openingFinished, financeFinished, activated;
        private PendingOutcomeOperation? movementOutcome, finalOutcome;
        private TurnOpeningOperation? opening;
        private GlobalTurnOperation? global;
        private bool upkeepCommitted;
        private CityPass.Operation? cityPass;
        private CitySlot researchCity;
        private int cityOwner;
        private CityHumanMaintenance? cityMaintenance;
        private ResearchOperation? research;
        private CityProduction.Operation? production;
        private int foodSurplus;
        private ResearchDecision? pendingResearch;
        private InteractionSpec? researchIntelligence;
        private bool cityPassFinished, maintenanceFinished, assessed;
        private int startGold;
        private CivilizationFinanceOperation? finance;
        private GovernmentTurn? governmentTurn;
        private GovernmentPicker? governmentSelection;
        private string deferredFailure = "";
        private GameState? activeWorld;
        private OperationContext? currentContext;
        private OperationContext Current => currentContext ?? throw new InvalidOperationException("No active turn step");

        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            currentContext = context;
            try
            { return ResumeStep(context, input); }
            finally { currentContext = null; }
        }

        public void Dispose()
        {
            if (activeWorld is { } world)
            {
                world.Production.CivilizationTurnInProgress = false;
            }

            activeWorld = null;
            research?.Dispose();
            research = null;
            production?.Dispose();
            production = null;
            pendingResearch = null;
            governmentTurn = null;
            governmentSelection = null;
            movementOutcome?.Dispose();
            global?.Dispose();
            opening?.Dispose();
            finalOutcome?.Dispose();
        }

        private OperationStep ResumeStep(OperationContext context, OperationInput input)
        {
            var world = context.World;
            if (!confirmed && input.Kind == OperationInputKind.Begin)
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

            if (!confirmed && (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout
                || input.Choice == 0))
            {
                return new(CommandStatus.Cancelled, Code: "cancelled");
            }

            if (!confirmed)
            {
                confirmed = true;
                world.PlayerPhaseActive = false;
            }
            // F04 checks outcomes from the completed movement phase before advancing another turn.
            if (!movementChecked)
            {
                if (movementOutcome is null && world.PendingEndReason != EndReason.None)
                { movementOutcome = new(hall); input = new(); }
                if (movementOutcome is not null)
                {
                    var result = movementOutcome.Resume(context, input);
                    if (!result.Status.Terminal())
                    { return result; }
                    RememberFailure(result);
                    if (movementOutcome.Quit)
                    { return CloseGame(context); }
                    movementOutcome.Dispose();
                    movementOutcome = null;
                }
                movementChecked = true;
            }
            if (!globalAdvanced)
            {
                if (global is null)
                { global = BeginGlobalAdvance(context); input = new(); }
                var result = global.Resume(context, input);
                if (!result.Status.Terminal())
                { return result; }
                RememberFailure(result);
                global.Dispose();
                global = null;
                globalAdvanced = true;
                world.Globals.TurnCiv = (sbyte)context.Actor.Civilization.Value;
                opening = new(hall);
                input = new();
            }
            if (!openingFinished)
            {
                var result = opening!.Resume(context, input);
                if (!result.Status.Terminal())
                { return result; }
                RememberFailure(result);
                if (opening.Quit)
                { return CloseGame(context); }
                opening.Dispose();
                opening = null;
                openingFinished = true;
                input = new();
            }

            ResearchReply? reply = ResearchFlowInteraction.Reply(pendingResearch, input);
            if (!upkeepCommitted)
            {
                startGold = CivilizationFinances.NormalizeTreasury(world, context.Actor.Civilization.Value);
                new CivilizationRecovery(world, context.Process.Rules.Definitions, point =>
                    context.Effect("map.changed", new([new("x", new CommandValue.Integer(point.X)), new("y", new CommandValue.Integer(point.Y))])))
                    .Run(context.Actor.Civilization.Value);
                upkeepCommitted = true;
                activeWorld = world;
                world.Production.CivilizationTurnInProgress = true;
                cityPass = new CityPass(world,
                    new(world, context.Process.Rules, CreateGovernments(context), context.Gameplay))
                    .Begin(context.Actor.Civilization.Value);
            }
            while (true)
            {
                if (production is not null)
                {
                    pendingResearch = production.Resume(reply);
                    reply = null;
                    if (pendingResearch is { } choice)
                    {
                        return ResearchPrompt(context, choice);
                    }
                    if (!production.Finished)
                    { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
                    var result = production.Result!;
                    production.Dispose();
                    production = null;
                    research = AfterProduction(context, researchCity, result);
                }
                if (research is not null)
                {
                    pendingResearch = research.Resume(reply);
                    reply = null;
                    if (pendingResearch is { } request)
                    {
                        return ResearchPrompt(context, request);
                    }
                    if (!research.Finished)
                    { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
                    research.Dispose();
                    research = null;
                    if (cityMaintenance!.Run(researchCity, cityOwner, $"local-pollution/{world.Globals.Turn}/{researchCity.Value}").Count != 0
                        && deferredFailure.Length == 0)
                    { deferredFailure = "city_maintenance_presentation_failed"; }
                    FinishCity(context, researchCity);
                }
                if (cityPass!.NextCity() is not { } slot)
                {
                    break;
                }

                researchCity = slot;
                production = BeginCityProduction(context, slot);
                if (production is null)
                { cityPass.CompleteCity(null); }
            }
            if (!cityPassFinished)
            {
                cityPass!.Finish();
                cityPassFinished = true;
                governmentTurn = new(world, context.Process.Rules.Definitions, context.Actor.Civilization.Value, owner =>
                {
                    if (RecordCompatibility.Find(world.Lifetime) is { } opaque)
                    {
                        opaque.Civilizations[owner] = opaque.Civilizations[owner] with
                        { Flags = (ushort)(opaque.Civilizations[owner].Flags & ~0x40) };
                    }
                });
            }
            if (!maintenanceFinished)
            {
                if (governmentSelection is not null)
                {
                    var result = governmentSelection.Resume(context, input);
                    if (result.Status == CommandStatus.AwaitingInteraction)
                    {
                        return result;
                    }
                    if (result.Status == CommandStatus.Failed)
                    {
                        deferredFailure = result.Code;
                    }
                    governmentSelection = null;
                    governmentTurn!.ChangeCompleted();
                }
                while (governmentTurn!.NextChange() is int requested)
                {
                    int owner = context.Actor.Civilization.Value;
                    PlayerGovernments(context, _ => governmentSelection = new(preferences, choices, duringTurn: true),
                        _ => throw new InvalidOperationException("Turn government selection owns the rate child"))
                        .Change(owner, requested);
                    context.Effect("civilization.government.selection", GovernmentValue(context));
                    if (governmentSelection is not null)
                    {
                        return governmentSelection.Resume(context, new());
                    }
                    governmentTurn.ChangeCompleted();
                }
                new DiplomacyTurn(world, context.Process.Rules.Definitions, context.Gameplay, new AuthoredDiplomacyTurn())
                    .Run(context.Actor.Civilization.Value, $"local-diplomacy/{world.Globals.Turn}");
                context.Effect("civilization.diplomacy.maintained", GovernmentValue(context));
                maintenanceFinished = true;
            }
            if (!assessed)
            {
                AssessCivilization(context);
                assessed = true;
                finance = new(preferences, choices, startGold);
                input = new();
            }
            if (!financeFinished)
            {
                var financialResult = finance!.Resume(context, input);
                if (financialResult.Status == CommandStatus.AwaitingInteraction)
                { return financialResult; }
                RememberFailure(financialResult);
                financeFinished = true;
            }
            if (!activated)
            {
                world.Production.CivilizationTurnInProgress = false;
                activeWorld = null;
                world.Globals.ActiveUnit = null;
                SelectReadyUnit(context);
                activated = true;
                // With no surviving owned units, this profile's movement phase completes immediately.
                // Otherwise its outcome gate is the next accepted End Turn, above.
                if (world.PendingEndReason != EndReason.None && !world.Entities.Units.Scanned.ToArray()
                    .Any(unit => unit.Token.Occupied && unit.Owner == context.Actor.Civilization))
                { finalOutcome = new(hall); input = new(); }
            }
            if (finalOutcome is not null)
            {
                var result = finalOutcome.Resume(context, input);
                if (!result.Status.Terminal())
                { return result; }
                RememberFailure(result);
                if (finalOutcome.Quit)
                { return CloseGame(context); }
                finalOutcome.Dispose();
                finalOutcome = null;
            }
            context.Effect("turn.advanced", TurnValue(world));
            world.PlayerPhaseActive = true;
            context.Publish();
            return new(deferredFailure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed,
                TurnValue(world), deferredFailure);
        }

        private void RememberFailure(OperationStep step)
        {
            if (step.Status == CommandStatus.Failed && deferredFailure.Length == 0)
            { deferredFailure = step.Code; }
        }
        private OperationStep CloseGame(OperationContext context)
        {
            context.World.Production.CivilizationTurnInProgress = false;
            activeWorld = null;
            context.World.PlayerPhaseActive = context.World.TurnLoopActive = false;
            context.Effect("game.closed", OwnedList<NamedValue>.Empty);
            return new(deferredFailure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed, TurnValue(context.World), deferredFailure);
        }

        private sealed class AuthoredDiplomacyTurn : IDiplomacyTurnEffects
        {
            private static InvalidOperationException Outside() => new("The authored profile has no AI diplomacy turn");
            public void ExchangeAdvances(int from, int to) => throw Outside();
            public void Meet(int from, int to) => throw Outside();
            public void WarEnded(int from, int to) => throw Outside();
            public void ReviewRelation(int from, int human) => throw Outside();
            public void SeekWarAlly(int from, int human) => throw Outside();
        }

        private OperationStep ResearchPrompt(OperationContext context, ResearchDecision pending)
        {
            context.Publish();
            return new(CommandStatus.AwaitingInteraction, Interaction: ResearchFlowInteraction.Prompt(context, pending,
                c => c == context.Actor.Civilization.Value ? context.Actor.Participant
                    : throw new InvalidOperationException("The authored profile has no other research recipient"), ref researchIntelligence));
        }

        private static OwnedList<NamedValue> TurnValue(GameState world) =>
            new([new("turn", new CommandValue.Integer(world.Globals.Turn))]);

        private GlobalTurnOperation BeginGlobalAdvance(OperationContext context)
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

            world.HumanWonders.Update(world);
            var budget = new StackBudget(5_000_000, 64);
            var stacks = new StackOperations(world, budget);
            var cityEffects = new CityTurnEffects(context);
            var effects = new GlobalEffects(() => Current);
            var deletion = new UnitDeletion(world, rules, budget, cityEffects);
            var loss = new UnitLoss(world, rules, stacks, new(world, rules, stacks), deletion, cityEffects);
            var lifecycle = cityEffects.Elimination();
            var governments = CreateGovernments(context);
            var policy = new GovernmentPolicy(world, context.Process.Rules, governments, context.Gameplay);
            var war = new DiplomaticWar(world, rules, stacks, new(world, rules, stacks), context.Gameplay, effects);
            var incidents = new EspionageIncidents(world, context.Gameplay, war, new(world, context.Process.Rules, policy),
                governments, effects.Incident);
            var creation = new UnitCreation(world, context.Process, stacks, new LocalCreation(world, rules, stacks));
            var spawning = new BarbarianSpawning(world, rules, context.Gameplay, creation, stacks, loss, lifecycle, incidents, effects);
            var nuclear = new NuclearEffects(world, context.Gameplay, stacks, loss, lifecycle, incidents, effects);
            cityMaintenance = new(world, new(world, context.Gameplay, nuclear, effects), new(world, rules, effects.Lost));
            var turn = new GlobalTurn(world, rules, context.Gameplay, GameplayContent.Builtin.Values.Calendar, spawning,
                new(world, effects.Warming, effects.Redraw), new(world, context.Gameplay),
                new(world, CivilizationScoreOperation.CreateScore(context)), new(world, rules, lifecycle, incidents));
            return new(turn.Begin(context.Process.OrdinaryPreferences.AcceleratedStart, $"local-global/{world.Globals.Turn}"),
                c => c == owner ? context.Actor.Participant : throw new InvalidOperationException("The authored profile has no other decision recipient"));
        }

        private sealed class GlobalEffects(Func<OperationContext> current) : IBarbarianSpawningEffects, IDiplomaticWarEffects,
            INuclearEffects, ICityPollutionEffects
        {
            public void Notice(BarbarianUprisingNotice notice) => current().Effect("barbarians.uprising", new([
                new("owner", new CommandValue.Integer(notice.Owner)), new("kind", new CommandValue.Text(notice.Kind.ToString())),
                new("city", new CommandValue.Text(notice.CityName)), new("unit", new CommandValue.Unit(notice.Unit))]));
            public void Warming(WarmingNotice notice) => current().Effect("world.warming", new([new("kind", new CommandValue.Text(notice.ToString()))]));
            public void Redraw(MapPoint point) => new CityTurnEffects(current()).RedrawTile(point);
            public void Notice(DiplomaticWarNotice notice) => current().Effect("diplomacy.war_notice", new([
                new("kind", new CommandValue.Text(notice.Kind.ToString())), new("first", new CommandValue.Integer(notice.First.Civilization)),
                new("second", new CommandValue.Integer(notice.Second.Civilization))]));
            public void RedrawMaps(int localHuman) => current().Effect("map.refresh", new([]));
            public void MoveCursor(MapPoint point) => current().World.Views[current().World.Multiplayer.LocalHuman] =
                current().World.Views[current().World.Multiplayer.LocalHuman] with { Center = point };
            public void Incident(EspionageIncidentNotice notice) => current().Effect("diplomacy.incident", new([
                new("kind", new CommandValue.Text(notice.Kind.ToString())), new("actor", new CommandValue.Integer(notice.Actor.Civilization)),
                new("victim", new CommandValue.Integer(notice.Victim.Civilization))]));
            public void CenterPrimary(MapPoint point) => MoveCursor(point);
            public void EnsureVisible(MapPoint point, int owner) => CenterPrimary(point);
            public void AnimateNuclear(MapPoint point) => Animate(point);
            public void Animate(MapPoint point) => current().Effect("map.nuclear", new([
                new("x", new CommandValue.Integer(point.X)), new("y", new CommandValue.Integer(point.Y))]));
            public void Notice(NuclearNotice notice) => current().Effect("nuclear.notice", new([
                new("kind", new CommandValue.Text(notice.Kind.ToString())), new("recipient", new CommandValue.Integer(notice.Recipient)),
                new("name", new CommandValue.Text(notice.Name))]));
            public void Notice(CityPollutionNotice notice) => current().Effect("city.pollution", new([
                new("city", new CommandValue.City(new(notice.City, current().World.Entities.Cities.At(notice.City).Token))),
                new("kind", new CommandValue.Text(notice.Kind.ToString()))]));
            public void Lost(ImprovementLostNotice notice) => current().Effect("city.improvement_lost", new([
                new("city", new CommandValue.City(new(notice.City, current().World.Entities.Cities.At(notice.City).Token))),
                new("owner", new CommandValue.Integer(notice.Owner)), new("building", new CommandValue.Integer(notice.Building)),
                new("name", new CommandValue.Text(notice.Name)), new("credit", new CommandValue.Integer(notice.Credit))]));
        }

        private static void AssessCivilization(OperationContext context)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            var budget = new StackBudget(5_000_000, 64);
            var stacks = new StackOperations(world, budget);
            var effects = new CityTurnEffects(context);
            var deletion = new UnitDeletion(world, rules, budget, effects);
            var loss = new UnitLoss(world, rules, stacks, new(world, rules, stacks), deletion, effects);
            var governments = new GovernmentPolicy(world, context.Process.Rules, CreateGovernments(context), context.Gameplay);
            new CivilizationAssessment(world, context.Process.Rules, stacks, CreateProductionOrders(context),
                governments, loss, effects.Elimination(), context.Gameplay)
                .Assess(context.Actor.Civilization.Value, $"local-assessment/{world.Globals.Turn}");
        }

        private void FinishCity(OperationContext context, CitySlot slot)
        {
            var world = context.World;
            new CityTerrainGoals(world, context.Process.Rules.Definitions).Publish(slot);
            int balance = unchecked(world.Mood.Happy - world.Mood.Unhappy);
            world.Mood.City = null;
            world.Calculation.Processing = false;
            cityPass!.CompleteCity(balance);
        }

        private CityProduction.Operation? BeginCityProduction(OperationContext context, CitySlot slot)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            ref var city = ref world.Entities.Cities.At(slot);
            cityOwner = city.Owner.Value;
            ref var civ = ref world.Civilizations[city.Owner.Value];
            if ((city.TurnsCaptured - 1 & 63) == (world.Globals.Turn & 63))
            {
                city.Founder = city.Owner;
            }
            city.Flags.ImprovementSold = false;
            city.Flags.SettlerNearby = city.Flags.Investigated = false;
            CityTarget target = new(slot, city.Token);
            int before = city.Size;
            int previousFood = city.FoodBox;
            var food = CreateFood(context);
            if (food.Run(slot))
            {
                return null;
            }

            if (city.Size != before)
            {
                civ.TotalCitySize = city.Size;
                LocalCityEconomy.RememberCity(world, ref city);
                context.Effect("city.population", new([new("city", new CommandValue.City(target)),
                    new("size", new CommandValue.Integer(city.Size))]));
            }

            LocalCityEconomy.Recalculate(world, context.Process.Rules, slot);
            foodSurplus = food.Accumulate(slot, previousFood);
            return CreateProduction(context).Begin(slot, $"local-production/{world.Globals.Turn}/{slot.Value}");
        }

        private ResearchOperation AfterProduction(OperationContext context, CitySlot slot, CityProductionResult production)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            ref var city = ref world.Entities.Cities.At(slot);
            CityTarget target = new(slot, city.Token);
            if (production.Unit is { Outcome: UnitProductionOutcome.Completed, Unit: { } produced })
            {
                context.Effect("unit.produced", new([new("unit", new CommandValue.Unit(produced)), new("city", new CommandValue.City(target))]));
                context.CityProductionCompleted?.Invoke(target, world.Entities.Units.At(produced.Slot).Type.Value);
            }
            else if (production.Unit?.Outcome == UnitProductionOutcome.CreationFailed)
            {
                context.Effect("city.production_spent", new([new("city", new CommandValue.City(target))]));
            }
            else if (production is { Outcome: CityProductionOutcome.Completed, Improvement: { } improvement })
            {
                context.CityProductionCompleted?.Invoke(target, -improvement);
            }

            RetireUnsupportedUnits(context, slot);

            world.Calculation.Reallocating = world.Calculation.Processing = true;
            world.Calculation.JobFound = false;
            world.Calculation.JobPriority = 0;
            LocalCityEconomy.Recalculate(world, context.Process.Rules, slot);
            world.Calculation.Reallocating = false;
            var moodEffects = new CityTurnEffects(context);
            var governments = CreateGovernments(context);
            new CityMoodTurn(world, rules, governments, moodEffects.ReconsiderProduction, moodEffects).Run(slot, foodSurplus);
            return CreateResearch(context).AddPoints(city.Owner.Value, city.Science, $"local-research/{world.Globals.Turn}/{slot.Value}");
        }

        private static GovernmentChanges CreateGovernments(OperationContext context) =>
            new(context.World, context.Process.Rules, new(context.World, new(5_000_000, 64)),
                _ => throw new InvalidOperationException("The authored profile has no government selection path"),
                _ => throw new InvalidOperationException("The authored profile has no government rate decision path"),
                _ => throw new InvalidOperationException("The authored profile has no AI government path"));

        private WonderEffects CreateWonders(OperationContext context)
        {
            var world = context.World;
            return new(world, context.Process.Rules.Definitions, notice => Current.Effect("unit.upgraded", new([
                new("unit", new CommandValue.Unit(new(notice.Unit, world.Entities.Units.At(notice.Unit).Token))),
                new("old", new CommandValue.Integer(notice.OldType.Value)), new("new", new CommandValue.Integer(notice.NewType.Value))])),
                point => new CityTurnEffects(Current).RedrawTile(point));
        }

        private ResearchFlow CreateResearch(OperationContext context)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            var governments = CreateGovernments(context);
            var researchEffects = new AuthoredResearchEffects(() => Current);
            var researchPrompts = new ResearchGovernmentPrompts(world, governments, researchEffects);
            var grants = new ResearchGrants(world, rules, context.Gameplay, CreateWonders(context), researchPrompts.Run, researchEffects);
            return new ResearchFlow(world, context.Process.Rules, context.Gameplay, grants, researchPrompts,
                CreateProductionOrders(context),
                new CityEconomy(world, context.Process.Rules, new(world, new(5_000_000, 64))), researchEffects, researchEffects);
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

        // The admitted ordinary advances0..2 have no grant notices, emissary, Barracks or
        // first-discovery wonder effects. Completion/next-selection remain session continuations.
        private sealed class AuthoredResearchEffects(Func<OperationContext> current) : IResearchGrantEffects, IResearchGovernmentEffects, IResearchChoiceEffects, IResearchCompletionEffects
        {
            public bool DiplomacyActive => false;
            public int NoticeMode => 0;
            public CitySlot? SelectedCity => null;
            public void Completed(ResearchCompletionNotice notice) => current().Effect("research.completed", new([new("advance", new CommandValue.Integer(notice.Advance))]));
            public void Selected(int civilization, int advance) => current().Effect("research.selected", new([new("advance", new CommandValue.Integer(advance))]));
            public void Ability(ResearchAbilityNotice notice) => current().Effect("research.ability", new([new("ability", new CommandValue.Text(notice.Ability.ToString()))]));
            public void FirstCivilization(int civilization) => current().Effect("research.first_civilization", new([]));
            public void Advice(ResearchAdvice advice) => current().Effect("research.advice", new([new("advance", new CommandValue.Integer(advice.Recommendation))]));
            public void CloseAdvice() { }
            public void Encyclopedia(ResearchTopic topic, int identity) => throw OutsideProfile();
            public ResearchReply GoalChoice(ResearchGoalNotice notice) => throw OutsideProfile();
            public int GoalHelp(ResearchTopic topic, int identity, int prerequisite) => throw OutsideProfile();
            public void GoalPath(ResearchPathNotice notice) => throw OutsideProfile();
            private static InvalidOperationException OutsideProfile() => new("The authored profile has no research-grant presentation path");
            public void Acquired(AdvanceAcquisitionNotice notice) => throw OutsideProfile();
            public bool ReceiveEmissary(int civilization) => throw OutsideProfile();
            public void EmissaryKnowledge(int civilization, int advance) => throw OutsideProfile();
            public void ResetDiplomacy() => throw OutsideProfile();
            public void Encyclopedia(int advance) => throw OutsideProfile();
            public void BarracksSold(BarracksSaleNotice notice) => throw OutsideProfile();
            public void GoldenAge(GoldenAgeNotice notice) => throw OutsideProfile();
            public void WonderExpired(WonderExpiryNotice notice) => throw OutsideProfile();
            public void RefreshStatus() => throw OutsideProfile();
            public int ConfirmGovernment(ResearchGovernmentChoice choice) => throw OutsideProfile();
            public void RevolutionStarting(int civilization) => current().Effect("government.revolution_started",
                new([new("civilization", new CommandValue.Integer(civilization))]));
            public void Tutorial(int civilization, ResearchTutorial tutorial) => throw OutsideProfile();
        }

        private CityProduction CreateProduction(OperationContext context)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            var values = GameplayContent.Builtin.Values;
            var effects = new AuthoredProductionEffects(() => Current);
            var orders = CreateProductionOrders(context);
            return new(world, rules, CreateUnitProduction(context), orders, CreateResearch(context), CreateWonders(context),
                new(world, rules, values.Spaceship, values.Calendar),
                new(world, rules, values.Spaceship, values.Calendar, city => orders.Set(city, 99), effects),
                values.Spaceship, new(world, context.Process.Rules, new(world, new(5_000_000, 64))), effects);
        }

        private static ProductionOrders CreateProductionOrders(OperationContext context)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            var values = GameplayContent.Builtin.Values;
            var budget = new StackBudget(5_000_000, 64);
            var stacks = new StackOperations(world, budget);
            var effects = new CityTurnEffects(context);
            var airlift = new AiProductionAirlift(world, rules, stacks, new(world, rules, budget, effects), context.Gameplay,
                notice => context.Effect(notice.ShotDown ? "unit.airlift_shot" : "unit.airlift_seen", new([
                    new("city", new CommandValue.City(new(notice.Destination, world.Entities.Cities.At(notice.Destination).Token))),
                    new("name", new CommandValue.Text(notice.Name))])));
            // Alder Valley has no CITYPREF content; the general policy accepts the installed list explicitly.
            var chooser = new CityProductionChoice(world, context.Process.Rules, new(), new(world, context.Process.Rules, stacks), stacks,
                new(world, rules, values.Spaceship, values.Calendar), airlift);
            int sequence = 0;
            return new(world, rules, city => chooser.Choose(city, $"local-choice/{world.Globals.Turn}/{city.Value}/{sequence++}"),
                _ => throw new InvalidOperationException("The authored profile has no AI wonder notice"));
        }

        private sealed class AuthoredProductionEffects(Func<OperationContext> current) : ICityProductionEffects, ISpaceshipConstructionEffects
        {
            public bool WindowShown => false;
            private void Item(string name, CitySlot city, int item) => current().Effect(name, new([
                new("city", new CommandValue.City(new(city, current().World.Entities.Cities.At(city).Token))),
                new("improvement", new CommandValue.Integer(item))]));
            public void DrawProduction(CitySlot city) => current().Publish();
            public void DrawImprovements(CitySlot city) => current().Publish();
            public void InvalidBuild(CitySlot city, int improvement, string name) => Item("city.invalid_build", city, improvement);
            public void BadSpace(CitySlot city, int improvement, string name) => Item("city.bad_space", city, improvement);
            public void Manhattan(CitySlot city, int recipient) => throw OutsideProfile();
            public void CompletionSound(CitySlot city, int improvement) => Item("city.construction_sound", city, improvement);
            public void Built(ImprovementCompletionNotice notice) => Item("city.improvement_built", notice.City.Slot, notice.Improvement);
            public void WonderMovie(int wonder) => throw OutsideProfile();
            public void CapitalMoved(CitySlot city, int owner, string name, string adjective) => Item("city.capital_moved", city, BuildingIds.Palace);
            public void RedrawMaps() => current().Publish();
            public void Astronauts() => throw OutsideProfile();
            public void AlmostWonder(AlmostWonderNotice notice) => throw OutsideProfile();
            public int ChoosePart(SpaceshipPartChoice choice) => throw OutsideProfile();
            public void NoFurther(int civilization, int part) => throw OutsideProfile();
            public void Launched(SpaceshipLaunchNotice notice) => throw OutsideProfile();
            public void ShowShip(int civilization) => throw OutsideProfile();
            private static InvalidOperationException OutsideProfile() => new("The authored profile has no wonder or spaceship presentation path");
        }

        private static CityUnitProduction CreateUnitProduction(OperationContext context)
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
            public void ReconsiderProduction(CitySlot city) => CreateProductionOrders(context).Set(city, 99);
            public void ChooseTradeCargo(CitySlot city, int owner, UnitSlot unit) =>
                throw new InvalidOperationException("The authored profile has no caravan production path");
            public bool AbandonCity(CitySlot city, UnitType type) =>
                throw new InvalidOperationException("The authored profile cannot build settlers");
            public void Upgraded(CitySlot city, UnitType oldType, UnitType newType) => context.Effect("city.production_upgraded", new([
                CityValue(city), new("oldType", new CommandValue.Integer(oldType.Value)), new("newType", new CommandValue.Integer(newType.Value))]));
            public void Built(CityTarget city, UnitType type) => context.Effect("unit.built", new([
                new("city", new CommandValue.City(city)), new("type", new CommandValue.Integer(type.Value))]));
            public void AlmostWonder(AlmostWonderNotice notice) => context.Effect("city.wonder_almost", new([
                CityValue(notice.City), new("owner", new CommandValue.Integer(notice.Owner)),
                new("wonder", new CommandValue.Integer(notice.Wonder))]));

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
            private int Eliminate(int owner, int killer) => Elimination().Check(owner, killer, "local-elimination/" + eliminationCall++);
            public CivilizationElimination Elimination()
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
                return elimination;
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
