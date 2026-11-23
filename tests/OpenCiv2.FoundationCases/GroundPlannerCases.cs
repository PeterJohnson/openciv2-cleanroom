using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class GroundPlannerFixture
    {
        public RevelationFixture Data { get; } = new();
        public UnitMissionFixture Mission { get; }
        public GameState World => Data.World;
        public MapPoint Point => new(20, 20);
        public CombatWorkspace CombatState { get; } = new();
        public GameplayRandom Random { get; private set; } = null!;
        public GroundPlannerFixture()
        {
            Mission = new(data: Data);
            Data.Stacks.Detach(1, false); Data.Unit(1) = default; World.Entities.Units.SetEnd(1);
            Data.Stacks.Detach(0, false); World.Map.Allocate(new(80, 48));
            foreach (ref var tile in World.Map.Tiles)
            { tile.Terrain = Terrain.Grassland; tile.Owner = 1; tile.RevealedTo = CivilizationSet.Only(1); }
            Data.Stacks.AttachAtFront(0, Point, false); Data.Unit(0).Type = new(0);
            Data.Unit(0).LastDirection = byte.MaxValue; Data.Unit(0).Visibility = CivilizationSet.Only(1);
            Data.Rules.Units[0] = new() { Role = RuleRole.Defend, Domain = RuleDomain.Land, Attack = 1, Defense = 2, Movement = 6, HitPoints = 10, Firepower = 1, CostRows = 2 };
            Data.Rules.Units[4] = Data.Rules.Units[0] with { Attack = 2, Defense = 1, CostRows = 3 };
            Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1, DefenseFactor = 2, Food = 2 };
            Data.Rules.Terrain[(int)Terrain.Ocean] = Data.Rules.Terrain[(int)Terrain.Grassland];
            Data.AddCity(0, 1, Point);
            World.Globals.HumanPlayers = CivilizationSet.Only(3); World.Multiplayer.LocalHuman = 3;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            World.Civilizations[1].ContinentPolicy[0] = (byte)RuleRole.Defend;
        }
        public MapPoint At(int direction) => MapNeighborhoods.Offset(World.Map.Geometry, Point, MapNeighborhoods.Directions[direction], true);
        public UnitTacticsContext Context() => new(World, Data.Rules, new(0));
        public void Enemy(int direction = 0) => Data.AddUnit(1, 2, 4, At(direction));
        public GroundMovementPlanner.Operation Begin(UnitTacticsContext context, IEnumerable<Choice>? choices = null)
        {
            Random = new(new ControlledChoices(new(choices ?? GroundBaseChoices())), "ground-planner");
            var planner = new GroundMovementPlanner(World, Data.Rules, Data.Stacks,
                new(World, Data.Rules, Data.Stacks, CombatState), new(World, Data.Rules, Data.Stacks, CombatState), Mission.Missions, Random);
            return planner.Begin(context, "plan");
        }
        public GroundMoveChoice Complete(UnitTacticsContext? context = null, IEnumerable<Choice>? choices = null)
        {
            using var op = Begin(context ?? Context(), choices);
            for (int i = 0; !op.Completed && i < 100; ++i) { Equal(null, op.Resume()); }
            Equal(true, op.Completed); Random.RequireChoicesConsumed(); Mission.Lifecycle.Random.RequireChoicesConsumed();
            return op.Result!;
        }
    }
    private static Choice GroundChoice(string site, int direction, int hi) => Choice.Create(new("A04." + site, "plan", direction), new SamplingLaw.SecondaryRange(0, hi), ChoiceCode.Secondary(0));
    private static IEnumerable<Choice> GroundBaseChoices(int hi = 4, IEnumerable<int>? directions = null) =>
        (directions ?? Enumerable.Range(0, 8)).Select(k => GroundChoice("ground-base", k, hi));

    private static void GroundPlannerTiesOrdersAndRoles()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new GroundPlannerFixture(); var c = f.Context();
            c.Hold = variant == 1; c.HasOrders = variant == 2; f.Data.Unit(0).AiTask = 91;
            if (variant == 3) f.Data.Unit(0).LastDirection = 127;
            var result = f.Complete(c);
            Equal(variant == 2 ? null : (int?)(variant is 1 or 3 ? 8 : 0), result.Direction);
            Equal(variant == 3 ? -999 : 8, result.Score); Equal(result.Score, result.UnscaledScore);
            Equal((byte)(variant == 2 ? 91 : 57), f.Data.Unit(0).AiTask);
            Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal(f.Point, f.Data.Unit(0).Position);
            if (result.Direction is { } direction) TacticOrders(f.Data).Direction(c, direction);
            Equal(variant == 0 ? UnitOrder.AiGoTo : variant is 1 or 3 ? UnitOrder.Fortifying : UnitOrder.None, f.Data.Unit(0).Orders);
        }
        foreach (bool stronger in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = 3 };
            f.World.Civilizations[1].ContinentFlags[0].StrongerEnemy = stronger;
            var c = f.Context(); f.Complete(c); Equal((int)(stronger ? RuleRole.Defend : RuleRole.Attack), c.Role);
            Equal(RuleRole.Defend, f.Data.Rules.Units[0].Role);
        }
    }

    private static void GroundPlannerBaseAndAir()
    {
        foreach (bool attack in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Data.Unit(0).Visibility = default;
            if (attack) f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Attack };
            f.Data.Rules.Terrain[(int)Terrain.Grassland] = f.Data.Rules.Terrain[(int)Terrain.Grassland] with { MovementCost = 3 };
            Equal(attack ? -6 : -2, f.Complete(choices: GroundBaseChoices(2)).Score);
        }
        foreach (int owner in new[] { -1, 8 })
        {
            var f = new GroundPlannerFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Air };
            foreach (ref var tile in f.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; tile.Owner = owner < 0 ? null : (byte)owner; }
            f.World.Civilizations[1].SubversionCount = -8; f.World.Civilizations[1].Attitude[0] = 8;
            var choices = GroundBaseChoices().Concat(Enumerable.Range(0, 8).Select(k => GroundChoice("ground-air", k, 2)));
            Equal(-6, f.Complete(choices: choices).Score); // Base draw is still consumed before the Air replacement and owner penalty.
        }
    }

    private static void GroundPlannerAdmission()
    {
        var ocean = new GroundPlannerFixture(); foreach (int k in Enumerable.Range(0, 8)) ocean.World.Map.TileAt(ocean.At(k)).Terrain = Terrain.Ocean;
        Equal(8, ocean.Complete(choices: []).Direction);
        var sea = new GroundPlannerFixture(); sea.Data.Rules.Units[0] = sea.Data.Rules.Units[0] with { Domain = RuleDomain.Sea };
        Equal(8, sea.Complete(choices: []).Direction);
        foreach (bool ignore in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Data.AddUnit(1, 2, 4, new(24, 20));
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { IgnoreZonesOfControl = ignore } };
            var c = f.Context(); c.EnemyZone = true;
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = default };
            Equal(0, f.Complete(c, GroundBaseChoices(directions: Enumerable.Range(0, 8).Where(k => ignore || k != 1))).Direction);
        }
        var occupied = new GroundPlannerFixture(); occupied.Data.AddUnit(1, 1, 4, occupied.At(1)); occupied.Data.AddUnit(2, 1, 4, occupied.At(1));
        Equal(0, occupied.Complete(choices: GroundBaseChoices(directions: Enumerable.Range(0, 8).Where(k => k != 1))).Direction);
    }

    private static void GroundPlannerFriendlyAndAggregates()
    {
        var f = new GroundPlannerFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Attack };
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { CostRows = 6 };
        f.Data.AddUnit(1, 1, 4, f.At(1)); var result = f.Complete();
        Equal(1, result.Direction); Equal(10, result.Score); // 4*6/(1+1), then subtract actor defense 2.
        var aggregates = new GroundPlannerFixture(); aggregates.Data.Rules.Units[4] = aggregates.Data.Rules.Units[4] with { CostRows = 127, Attack = 128 };
        for (int i = 1; i <= 20; ++i) aggregates.Data.AddUnit(i, 1, 4, aggregates.At(1));
        Equal(2159, aggregates.Data.Stacks.ProductionCost(1, aggregates.Data.Rules));
        Equal(-2560, aggregates.Data.Stacks.Attack(1, aggregates.Data.Rules));
        var zero = new GroundPlannerFixture(); zero.Data.Rules.Units[4] = zero.Data.Rules.Units[4] with { CostRows = 255 };
        zero.Data.AddUnit(1, 1, 4, zero.At(0));
        using var failed = zero.Begin(zero.Context(), [GroundChoice("ground-base", 0, 4)]);
        Throws<DivideByZeroException>(() => failed.Resume()); Throws<InvalidOperationException>(() => failed.Resume()); zero.Random.RequireChoicesConsumed();
    }

    private static void GroundPlannerTerritoryAndCity()
    {
        foreach (sbyte credit in new sbyte[] { 0, -1 })
        {
            var f = new GroundPlannerFixture(); f.World.Map.TileAt(f.At(0)).Claimant = 2;
            f.World.Civilizations[2].Reputation = 6; f.World.Civilizations[1].ReputationCredit[1] = credit;
            f.World.Civilizations[1].ReputationCredit[2] = -100; f.World.Civilizations[1].Treaties[2].Peace = true;
            f.World.Civilizations[1].Attitude[2] = 60;
            Equal(credit == 0 ? 1 : 0, f.Complete().Direction);
        }
        var city = new GroundPlannerFixture(); city.Data.AddCity(1, 2, city.At(0)); var c = city.Context(); c.HasOrders = c.Hold = true;
        var capture = city.Complete(c); Equal(0, capture.Direction); Equal(999, capture.Score); Equal(false, c.HasOrders); Equal(false, c.Hold);
        TacticOrders(city.Data).Direction(c, capture.Direction!.Value); Equal(city.At(0), city.Data.Unit(0).Destination);
        Equal(city.Point, city.Data.Unit(0).Position); Equal(UnitOrder.AiGoTo, city.Data.Unit(0).Orders);
    }

    private static void GroundPlannerExplorationAndFood()
    {
        foreach (bool scenario in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); foreach (ref var tile in f.World.Map.Tiles) tile.RevealedTo = default;
            f.World.Globals.Status.Scenario = scenario; f.World.Scenario.Flags.WholeMapReveal = true;
            Equal(scenario ? 8 : 32, f.Complete().Score);
        }
        foreach (bool doubled in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.World.Globals.Multiplayer = GameMode.Hotseat; f.World.Multiplayer.Saved.DoubleProduction = doubled;
            var c = f.Context(); c.SettlerType = true;
            Equal(doubled ? 32 : 16, f.Complete(c).Score);
        }
    }

    private static void GroundPlannerActualPreview()
    {
        foreach (bool guard in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Enemy(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = 8 };
            f.World.Civilizations[1].ContinentFlags[0].StrongerEnemy = true;
            var c = f.Context(); c.Guard = guard; var result = f.Complete(c);
            Equal(guard ? 1 : 0, result.Direction); Equal(guard ? 8 : 512, result.Score); Equal(!guard, result.Attack);
            Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal((byte)0, f.Data.Unit(1).Damage);
            Equal(Terrain.Grassland, f.CombatState.Terrain);
        }
        var zero = new GroundPlannerFixture(); zero.Enemy(); zero.Data.Rules.Units[0] = zero.Data.Rules.Units[0] with { CostRows = 0 };
        zero.CombatState.AggregateTreatyNotice = true;
        using var failed = zero.Begin(zero.Context(), [GroundChoice("ground-base", 0, 4)]);
        Throws<DivideByZeroException>(() => failed.Resume()); Equal(false, zero.CombatState.AggregateTreatyNotice);
        Throws<InvalidOperationException>(() => failed.Resume()); zero.Random.RequireChoicesConsumed();
    }

    private static void GroundPlannerScaledAttack()
    {
        foreach (int cost in new[] { 8, 16 })
        {
            var f = new GroundPlannerFixture(); f.Enemy(); f.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
            f.Data.Rules.Units[9] = f.Data.Rules.Units[0] with { Attack = 8, CostRows = (byte)cost };
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
            f.World.Civilizations[1].ContinentFlags[0].StrongerEnemy = true; f.Data.Unit(0).MovesUsed = 5;
            var result = f.Complete(); Equal(cost == 8 ? 0 : 8, result.Direction); Equal(cost == 8, result.Attack);
            Equal(2, result.AttackClass); Equal(cost == 8 ? 132 : 136, result.Score); Equal(cost == 8 ? 132 : 68, result.UnscaledScore);
        }
    }

    private static void GroundPlannerPeacefulDiplomat()
    {
        var f = new GroundPlannerFixture(); f.Enemy(); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Diplomacy };
        f.World.Civilizations[1].Treaties[2].Peace = true; f.Data.Unit(0).MovesUsed = 5;
        var result = f.Complete(); Equal(0, result.Direction); Equal(60, result.Score); Equal(60, result.UnscaledScore);
        Equal(true, result.Attack); Equal(2, result.AttackClass);
    }

    private static void GroundPlannerRetainedBribery()
    {
        foreach (bool accept in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Enemy();
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Attack };
            f.World.Civilizations[1].ContinentPolicy[0] = (byte)RuleRole.Attack;
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Multiplayer.LocalHuman = 1;
            f.World.Globals.Difficulty = Difficulty.King; f.World.Civilizations[1].Treaties[2].Peace = true;
            using var op = f.Begin(f.Context(), GroundBaseChoices().Append(GroundChoice("ground-opportunistic-bribe", 0, 7)));
            var pending = (UnitMissionDecision.Bribe)op.Resume()!; Equal(75, pending.Offer.Price); Equal(pending, op.Resume());
            Equal(null, op.Resume(accept ? 1 : 0)); Equal(true, op.Completed);
            Equal(1, op.Result!.Direction); Equal(8, op.Result.Score); Equal(false, op.Result.Attack);
            Equal((byte)(accept ? 1 : 2), f.Data.Unit(1).Owner.Value); Equal(accept ? 925 : 1000, f.World.Civilizations[1].Gold);
            Equal(accept, f.Mission.Order.Contains("publish")); f.Random.RequireChoicesConsumed(); f.Mission.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    private static void GroundPlannerFallbackAndFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Enemy();
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Attack };
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Defense = 80 };
            f.Data.Unit(1).Flags.Veteran = true;
            f.World.Map.TileAt(f.At(0)).Terrain = Terrain.Forest;
            f.Data.Rules.Terrain[(int)Terrain.Forest] = new() { MovementCost = 1, DefenseFactor = 4 };
            f.World.Civilizations[1].ContinentPolicy[0] = (byte)RuleRole.Attack; f.Mission.FailPublish = fail;
            var choices = GroundBaseChoices(directions: fail ? [0] : null).Append(GroundChoice("ground-fallback-bribe", 0, 3));
            using var op = f.Begin(f.Context(), choices);
            if (fail)
            {
                Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume());
                Equal(false, op.Completed);
            }
            else
            {
                Equal(null, op.Resume()); Equal(true, op.Completed); Equal(1, op.Result!.Direction); Equal(8, op.Result.Score);
            }
            Equal((byte)1, f.Data.Unit(1).Owner.Value); Equal(925, f.World.Civilizations[1].Gold);
            Equal(1, f.Mission.Order.Count(s => s == "publish")); f.Random.RequireChoicesConsumed();
        }
    }

    private static void GroundPlannerHumanAttackAndAirGate()
    {
        foreach (bool ranged in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Enemy(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = 8 };
            f.World.Civilizations[1].ContinentFlags[0].StrongerEnemy = true;
            f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Civilizations[2].Reputation = 7;
            f.World.Civilizations[1].Attitude[2] = 30;
            if (ranged) f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air, Range = 1 };
            var result = f.Complete(choices: GroundBaseChoices().Append(GroundChoice("ground-attack-attitude", 0, 19)));
            Equal(ranged ? 1 : 0, result.Direction); Equal(!ranged, result.Attack);
        }
    }

    private static void GroundPlannerPrivateSessionAndDisposal()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Enemy();
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Attack };
            f.World.Civilizations[1].ContinentPolicy[0] = (byte)RuleRole.Attack;
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Multiplayer.LocalHuman = 1;
            f.World.Globals.Difficulty = Difficulty.King; f.World.Civilizations[1].Treaties[2].Peace = true;
            var choices = GroundBaseChoices(directions: dispose ? [0] : null).Append(GroundChoice("ground-opportunistic-bribe", 0, 7));
            using var op = f.Begin(f.Context(), choices);
            using var session = new GroundMovementOperation(op, c => new((ulong)c));
            var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(1, "planner"), new(default, new(1)), default, (_, _) => { }, () => { });
            var prompt = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, prompt.Status);
            Equal(new ParticipantId(1), prompt.Interaction!.Recipient); Sequence(new[] { 0, 1 }, prompt.Interaction.Choices);
            if (dispose)
            {
                session.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(1));
                Equal((byte)2, f.Data.Unit(1).Owner.Value); Equal(1000, f.World.Civilizations[1].Gold);
            }
            else
            {
                var result = session.Resume(context, new(OperationInputKind.Choice, 1)); Equal(CommandStatus.Completed, result.Status);
                Equal(new CommandValue.Integer(1), result.Result!.Single(v => v.Name == "direction").Value);
                Equal((byte)1, f.Data.Unit(1).Owner.Value);
            }
            f.Random.RequireChoicesConsumed();
        }
    }

    private static void GroundPlannerHostilityScans()
    {
        foreach (bool targetHostile in new[] { false, true })
        {
            var f = new GroundPlannerFixture(); f.Enemy();
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Attack };
            f.World.Civilizations[1].ContinentPolicy[0] = (byte)RuleRole.Attack;
            f.World.Globals.HumanPlayers = CivilizationSet.Only(2).With(3);
            f.World.Globals.Difficulty = Difficulty.Prince; f.World.Civilizations[1].Treaties[2].Peace = true;
            f.Data.Unit(1).Orders = UnitOrder.Fortified;
            f.World.Civilizations[targetHostile ? 2 : 1].Treaties[3].War = true;
            IEnumerable<Choice> choices = GroundBaseChoices();
            if (targetHostile) choices = choices.Append(GroundChoice("ground-opportunistic-bribe", 0, 7));
            Equal(1, f.Complete(choices: choices).Direction);
            Equal((byte)(targetHostile ? 1 : 2), f.Data.Unit(1).Owner.Value);
        }
        var all = new GroundPlannerFixture(); all.Enemy();
        all.Data.Rules.Units[0] = all.Data.Rules.Units[0] with { Role = RuleRole.Attack };
        all.World.Civilizations[1].ContinentPolicy[0] = (byte)RuleRole.Attack;
        all.World.Globals.HumanPlayers = CivilizationSet.Only(3).With(4);
        all.World.Globals.Difficulty = Difficulty.Prince; all.World.Civilizations[1].Treaties[2].Peace = true;
        all.Data.Unit(1).Orders = UnitOrder.Fortified; all.World.Civilizations[2].Treaties[3].War = true;
        Equal(1, all.Complete().Direction); Equal((byte)2, all.Data.Unit(1).Owner.Value); // First any-human scan passes; the second all-human scan rejects.
    }

    private static void GroundPlannerCoastAirbaseAndPole()
    {
        var sea = new GroundPlannerFixture(); sea.Data.Rules.Units[0] = sea.Data.Rules.Units[0] with { Domain = RuleDomain.Sea };
        foreach (ref var tile in sea.World.Map.Tiles) tile.Terrain = Terrain.Ocean;
        sea.World.Map.TileAt(sea.Point).Features &= ~MapFeatures.CitySite;
        var home = new MapPoint(28, 20); sea.World.Entities.Cities.At(new(0)).Position = home;
        sea.World.Map.TileAt(home).Terrain = Terrain.Grassland; sea.World.Map.TileAt(home).Features |= MapFeatures.CitySite;
        sea.World.Map.TileAt(new(29, 19)).Region = 9; sea.World.Map.Regions[9] = sea.World.Map.Regions[9] with { Water = 100 };
        sea.World.Map.TileAt(new(28, 22)).Region = 8; sea.World.Map.Regions[8] = sea.World.Map.Regions[8] with { Water = 1 };
        sea.World.Map.TileAt(new(26, 20)).Region = 7; sea.World.Map.Regions[7] = sea.World.Map.Regions[7] with { Water = 5 };
        sea.World.Map.Regions[0] = sea.World.Map.Regions[0] with { Water = 2 };
        Equal(18, sea.Complete().Score); // C01's last visited sea 0 supplies the bonus; its largest sea is 9.

        var air = new GroundPlannerFixture(); air.Data.Rules.Units[0] = air.Data.Rules.Units[0] with { Domain = RuleDomain.Air };
        air.World.Map.TileAt(air.At(0)).Features = MapFeatures.Fortification | MapFeatures.CitySite;
        air.World.Map.TileAt(air.At(0)).Owner = 2;
        var airChoices = GroundBaseChoices().Concat(Enumerable.Range(0, 8).Select(k => GroundChoice("ground-air", k, 2)));
        Equal(1, air.Complete(choices: airChoices).Direction);

        var pole = new GroundPlannerFixture(); var c = pole.Context(); c.Position = new(20, 0);
        var result = pole.Complete(c, GroundBaseChoices(directions: Enumerable.Range(1, 5)));
        Equal(2, result.Direction); Equal(8, result.Score); // Row-zero candidates are divided by three; first interior tie wins.
    }
}
