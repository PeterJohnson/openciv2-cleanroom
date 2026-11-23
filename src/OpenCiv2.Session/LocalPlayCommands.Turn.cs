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

    private static void RegisterTurnCommands(SessionRuntime runtime, IPreferenceWriter preferences, LocalRateChoices choices)
    {
        runtime.RegisterCommand(Define(Fortify, "Fortify an owned land unit and spend its remaining movement.",
            [new("unit", ValueKind.Unit)], [new("unit", ValueKind.Unit)], FortifyUnit, ValidateUnit));
        runtime.RegisterCommand(Define(Research, "Select an available advance by its listed ID.",
            [new("advance")], [new("advance")], ChooseResearch));
        runtime.RegisterCommand(new(
            new(1, EndTurn, "Finish the player phase and advance the local turn.",
                AccessClass.Player, new([]), new([new("turn")])),
            (actor, _, world) => new(ValidatePhase(actor, world)),
            _ => new EndPlayerPhase(preferences, choices)));
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
    private sealed class EndPlayerPhase(IPreferenceWriter preferences, LocalRateChoices choices) : IContinuation
    {
        private bool upkeepCommitted;
        private int nextCity;
        private CitySlot researchCity;
        private ResearchOperation? research;
        private CityProduction.Operation? production;
        private int foodSurplus;
        private bool largeBalancedCity, cityDisorder;
        private ResearchChoiceNotice? pendingResearch;
        private bool cityPassFinished, maintenanceFinished;
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
            research = null;
            production = null;
            pendingResearch = null;
            governmentTurn = null;
            governmentSelection = null;
        }

        private OperationStep ResumeStep(OperationContext context, OperationInput input)
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

            ResearchReply? reply = null;
            if (pendingResearch is { } pending)
            {
                if (input.Kind != OperationInputKind.Choice || input.Choice is not int selected
                    || !pending.Options.Any(o => o.Identity == selected))
                {
                    return ResearchDecision(pending);
                }

                reply = new(selected);
            }
            if (!upkeepCommitted)
            {
                AdvanceStart(context);
                upkeepCommitted = true;
                activeWorld = world;
                world.Production.CivilizationTurnInProgress = true;
                BeginCityPass(context);
                nextCity = world.Entities.Cities.End - 1;
            }
            while (true)
            {
                if (production is not null)
                {
                    pendingResearch = production.Resume(reply);
                    reply = null;
                    if (pendingResearch is { } choice)
                    {
                        return ResearchDecision(choice);
                    }

                    var result = production.Result!;
                    production = null;
                    research = AfterProduction(context, researchCity, result);
                }
                if (research is not null)
                {
                    pendingResearch = research.Resume(reply);
                    reply = null;
                    if (pendingResearch is { } request)
                    {
                        return ResearchDecision(request);
                    }

                    research = null;
                    MaintainBuildings(context, researchCity);
                    FinishCity(context, researchCity);
                }
                if (nextCity < 0)
                {
                    break;
                }

                var slot = new CitySlot((ushort)nextCity--);
                if (world.Entities.Cities.At(slot).Token.Occupied && world.Entities.Cities.At(slot).Owner == context.Actor.Civilization)
                {
                    world.Entities.Cities.At(slot).Flags.Airlifted = false;
                    researchCity = slot;
                    production = BeginCityProduction(context, slot);
                }
            }
            if (!cityPassFinished)
            {
                FinishCityPass(context);
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
            AssessCivilization(context);
            world.Production.CivilizationTurnInProgress = false;
            activeWorld = null;
            world.Globals.ActiveUnit = null;
            SelectReadyUnit(context);
            context.Effect("turn.advanced", TurnValue(world));
            world.PlayerPhaseActive = true;
            context.Publish();
            return new(deferredFailure.Length == 0 ? CommandStatus.Completed : CommandStatus.Failed,
                TurnValue(world), deferredFailure);
        }

        private sealed class AuthoredDiplomacyTurn : IDiplomacyTurnEffects
        {
            private static InvalidOperationException Outside() => new("The authored profile has no AI diplomacy turn");
            public void ExchangeAdvances(int from, int to) => throw Outside();
            public void Meet(int from, int to) => throw Outside();
            public void WarEnded(int from, int to) => throw Outside();
            public void ReviewRelation(int from, int human) => throw Outside();
            public void MilitaryAid(int owner) => throw Outside();
            public void SeekWarAlly(int from, int human) => throw Outside();
        }

        private static OperationStep ResearchDecision(ResearchChoiceNotice pending) =>
            new(CommandStatus.AwaitingInteraction,
                Interaction: new("Choose the next advance.", null, null, new(pending.Options.Select(o => o.Identity)), Cancellable: false));

        private static OwnedList<NamedValue> TurnValue(GameState world) =>
            new([new("turn", new CommandValue.Integer(world.Globals.Turn))]);

        private static void AdvanceStart(OperationContext context)
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
            world.HumanWonders.Update(world);
            new PowerRanking(world, context.Gameplay).Publish($"local-power/{world.Globals.Turn}");
            ref var globals = ref world.Globals;
            ++globals.Turn;
            if (globals.Turn > 199)
            {
                ++globals.PeaceTurns;
            }

            globals.Year = (short)Calendar.Year(globals.Turn, globals, world.Scenario, GameplayContent.Builtin.Values.Calendar);
            globals.TransientFlags.EndOfTurn = false;
            ResetUnits(world);
            DiplomaticAttitudes.MaintainReputation(world, rules);
            globals.TurnCiv = (sbyte)owner;
            ref var treasury = ref world.Civilizations[owner].Gold;
            treasury = Math.Max(treasury, 0);

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

        private static void BeginCityPass(OperationContext context)
        {
            var world = context.World;
            int owner = context.Actor.Civilization.Value;
            world.Civilizations[owner].GovernmentScore = default;
            world.Production.WonderNotices[owner] = default;
            world.Globals.TransientFlags.AttitudeDirty = false;
            // F04's best-shield publication and additional support/mood score terms
            // remain an explicit owner-specification gap; do not invent their scale.
        }

        private void FinishCity(OperationContext context, CitySlot slot)
        {
            var world = context.World;
            new CityTerrainGoals(world, context.Process.Rules.Definitions).Publish(slot);
            int balance = unchecked(world.Mood.Happy - world.Mood.Unhappy);
            world.Mood.City = null;
            world.Calculation.Processing = false;
            ref var city = ref world.Entities.Cities.At(slot);
            largeBalancedCity |= balance == 0 && city.Size > 5;
            cityDisorder |= city.Flags.Disorder;
            for (int observer = 1; observer < EntityLimits.Civilizations; ++observer)
            {
                if (world.Civilizations[city.Owner.Value].Treaties[observer].Alliance
                    || world.Globals.WonderCity[WonderIds.Apollo] != -1)
                {
                    city.RememberedSize[observer] = unchecked((byte)city.Size);
                }
            }
        }

        private void FinishCityPass(OperationContext context)
        {
            var world = context.World;
            int owner = context.Actor.Civilization.Value;
            ref var scores = ref world.Civilizations[owner].GovernmentScore;
            scores[(int)Government.Anarchy] = unchecked((short)(scores[(int)Government.Anarchy]
                + 2 * scores[(int)Government.Despotism]));
            new GovernmentPolicy(world, context.Process.Rules, CreateGovernments(context), context.Gameplay)
                .AfterCities(owner, largeBalancedCity, cityDisorder);
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
            }
        }

        private CityProduction.Operation? BeginCityProduction(OperationContext context, CitySlot slot)
        {
            var world = context.World;
            var rules = context.Process.Rules.Definitions;
            ref var city = ref world.Entities.Cities.At(slot);
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

        private static void MaintainBuildings(OperationContext context, CitySlot slot)
        {
            var world = context.World;
            // This profile has no pollution-producing population/industry or nuclear plant.
            // C08 follows the completed research operation at the human-city upkeep boundary.
            new BuildingMaintenance(world, context.Process.Rules.Definitions, notice => context.Effect("city.improvement_lost", new([
                new("city", new CommandValue.City(new(notice.City, world.Entities.Cities.At(notice.City).Token))),
                new("owner", new CommandValue.Integer(notice.Owner)), new("building", new CommandValue.Integer(notice.Building)),
                new("name", new CommandValue.Text(notice.Name)), new("credit", new CommandValue.Integer(notice.Credit))]))).Run(slot);
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
            public void RevolutionStarting(int civilization) => throw OutsideProfile();
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
