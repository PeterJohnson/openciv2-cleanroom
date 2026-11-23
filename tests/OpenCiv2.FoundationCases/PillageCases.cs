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
    private sealed class PillageFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public MapPoint Point => new(10, 10);
        public Pillage Pillage { get; }
        public GroundPartisans Partisans { get; }
        public List<(MapPoint Point, int Radius)> Redraws { get; } = [];
        public bool FailRedraw;
        public PillageFixture()
        {
            Data.AddUnit(0, 1, 0, Point);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3).With(4);
            World.Civilizations[1].Government = Government.Despotism;
            World.Civilizations[1].Treaties[2].Peace = true;
            Pillage = new(World, Data.Rules, new(World, Data.Rules, _ => { }),
                new(World, Data.Rules, new MovementGoalQueries(World, Data.Rules)),
                (p, r) => { Redraws.Add((p, r)); if (FailRedraw) throw new InvalidOperationException("Drawing failed"); });
            Partisans = new(World, Data.Rules, Pillage);
        }
        public void Features(MapFeatures features) => World.Map.TileAt(Point).Features = MapFeatures.Unit | features;
        public UnitTacticsContext Context() => new(World, Data.Rules, new(0));
        public void Ai(int cityX = 14)
        {
            World.Globals.HumanPlayers = CivilizationSet.Only(2);
            World.Civilizations[1].Treaties[2].Peace = false;
            World.Civilizations[1].Treaties[2].War = true;
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Movement = 3 };
            Data.AddCity(0, 2, new(cityX, 10));
        }
    }

    // W04 core priority is independent of player command admission and does not clear unrelated orders/state.
    private static void PillageFeaturePriority()
    {
        const MapFeatures farm = MapFeatures.Mine | MapFeatures.Irrigation;
        const MapFeatures airbase = MapFeatures.Fortification | MapFeatures.CitySite;
        foreach (var (features, partisan, removed) in new[]
        {
            (farm | airbase | MapFeatures.Road | MapFeatures.Railroad, false, MapFeatures.Mine),
            (MapFeatures.Irrigation | airbase, false, MapFeatures.Irrigation),
            (airbase | MapFeatures.Railroad, false, airbase),
            (MapFeatures.Fortification | MapFeatures.Road, false, airbase),
            (MapFeatures.Road | MapFeatures.Railroad, false, MapFeatures.Railroad),
            (MapFeatures.Railroad, false, MapFeatures.Railroad),
            (MapFeatures.Road, false, MapFeatures.Road),
            (MapFeatures.None, false, MapFeatures.Road),
            (farm | MapFeatures.Road | MapFeatures.Railroad, true, MapFeatures.Railroad),
            (farm | MapFeatures.Road, true, MapFeatures.Road),
            (farm | MapFeatures.Railroad, true, MapFeatures.Mine)
        })
        {
            var f = new PillageFixture(); f.Features(features);
            f.Data.Unit(0).Type = new((byte)(partisan ? UnitTypeIds.Partisans : 0));
            f.Data.Unit(0).Orders = UnitOrder.Clean; f.Data.Unit(0).Counter = 79; f.Data.Unit(0).AiTask = 91;
            using var op = f.Pillage.Begin(new(0)); Equal(null, op.Resume()); Equal(true, op.Applied);
            Equal((features | MapFeatures.Unit) & ~removed, f.World.Map.TileAt(f.Point).Features);
            Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.Clean, f.Data.Unit(0).Orders);
            Equal((byte)79, f.Data.Unit(0).Counter); Equal((byte)91, f.Data.Unit(0).AiTask);
            Sequence(new[] { (f.Point, 1) }, f.Redraws);
        }
        var explicitChoice = new PillageFixture(); explicitChoice.Features(farm | MapFeatures.Road | MapFeatures.Pollution);
        explicitChoice.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
        using var selected = explicitChoice.Pillage.Begin(new(0), farm | MapFeatures.Pollution); selected.Resume();
        Equal(MapFeatures.Unit | MapFeatures.Road, explicitChoice.World.Map.TileAt(explicitChoice.Point).Features);
    }

    private static void PillageConfirmationAndCapture()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Mine); f.Data.AddCity(0, 2, new(30, 10));
            if (variant == 1) f.World.Civilizations[1].Treaties[2].Alliance = true;
            if (variant == 2) f.World.Civilizations[1].Treaties[2].War = true;
            if (variant == 3) f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            using var op = f.Pillage.Begin(new(0)); var pending = op.Resume();
            if (variant == 0)
            {
                Equal(AttackWarningKind.Peace, pending!.Kind); Equal(pending, op.Resume()); op.Resume(0);
            }
            Equal(variant >= 2, op.Applied); Equal(variant >= 2 ? 1 : 0, f.Redraws.Count);
            Equal((byte)(variant >= 2 ? 6 : 0), f.Data.Unit(0).MovesUsed);
            Equal(variant < 2, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Mine));
        }
        var live = new PillageFixture(); live.Features(MapFeatures.Irrigation | MapFeatures.Road);
        live.Data.AddCity(0, 2, new(30, 10)); using var retained = live.Pillage.Begin(new(0));
        Equal(AttackWarningKind.Peace, retained.Resume()!.Kind);
        live.Data.Stacks.Relocate(0, new(20, 10), false);
        live.Data.Unit(0).Owner = new(3); live.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
        live.Data.AddCity(1, 4, new(12, 10)); retained.Resume(1);
        Equal(true, retained.Applied); Equal(MapFeatures.Irrigation, live.World.Map.TileAt(live.Point).Features);
        Equal(MapFeatures.Irrigation, live.World.Map.Remembered(3, live.Point));
        Equal(MapFeatures.Irrigation, live.World.Map.Remembered(4, live.Point));
        Equal(true, live.World.Civilizations[3].Treaties[4].War); Equal(false, live.World.Civilizations[1].Treaties[2].War);
        Equal(4, SharedGoals.PriorityAt(live.World.Civilizations[3], live.Point, (sbyte)RuleRole.Attack));
        Equal(new MapPoint(20, 10), live.Data.Unit(0).Position); Equal((byte)6, live.Data.Unit(0).MovesUsed);
    }

    private static void PillageKnowledgeAndDiplomacy()
    {
        foreach (int owner in new[] { -1, 1, 2 })
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Mine | MapFeatures.Road);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(3);
            f.World.Map.Remembered(1, f.Point) = MapFeatures.Pollution;
            f.World.Map.Remembered(2, f.Point) = MapFeatures.Pollution;
            if (owner >= 0) f.Data.AddCity(0, owner, new(30, 10));
            using var op = f.Pillage.Begin(new(0)); op.Resume();
            var expected = MapFeatures.Unit | MapFeatures.Road;
            Equal(owner < 0 ? MapFeatures.Pollution : expected, f.World.Map.Remembered(1, f.Point));
            Equal(owner == 2 ? expected : MapFeatures.Pollution, f.World.Map.Remembered(2, f.Point));
            if (owner >= 0)
            {
                Equal(true, f.World.Civilizations[1].Treaties[owner].War);
                Equal(true, f.World.Civilizations[owner].Treaties[1].WarThisTurn);
                Equal(false, f.World.Civilizations[1].Treaties[owner].Peace);
            }
            Equal(owner == 2 ? 4 : 0, SharedGoals.PriorityAt(f.World.Civilizations[1], f.Point, (sbyte)RuleRole.Attack));
            Equal(default(CivilizationSet), f.World.Map.TileAt(f.Point).RevealedTo);
        }
    }

    private static void PillageFailureAndSession()
    {
        var cosmetic = new PillageFixture { FailRedraw = true }; cosmetic.Features(MapFeatures.Road);
        using var allowed = cosmetic.Pillage.Begin(new(0)); allowed.Resume();
        Equal(true, allowed.Applied); Equal(1, allowed.PresentationFailures.Count); Equal((byte)6, cosmetic.Data.Unit(0).MovesUsed);
        var failure = new PillageFixture(); failure.Features(MapFeatures.Mine); failure.Ai(); failure.Data.Unit(0).Damage = 1;
        failure.Data.Rules.Cosmic = failure.Data.Rules.Cosmic with { RoadMovementMultiplier = 0 };
        using var failed = failure.Pillage.Begin(new(0)); Throws<DivideByZeroException>(() => failed.Resume());
        Equal(MapFeatures.Unit, failure.World.Map.TileAt(failure.Point).Features);
        Equal(4, SharedGoals.PriorityAt(failure.World.Civilizations[1], failure.Point, (sbyte)RuleRole.Attack));
        Equal((byte)0, failure.Data.Unit(0).MovesUsed); Throws<InvalidOperationException>(() => failed.Resume()); Equal(1, failure.Redraws.Count);
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout, OperationInputKind.Choice })
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Mine); f.Data.AddCity(0, 2, new(14, 10)); var random = WarRandom();
            var context = new OperationContext(f.World, f.Data.Process, random, new PresentationRandom(1, "pillage"), new(default, new(3)), default, (_, _) => { }, () => { });
            using var session = new PillageOperation(f.Pillage.Begin(new(0)), c => new((ulong)c));
            var prompt = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, prompt.Status);
            Equal(new ParticipantId(1), prompt.Interaction!.Recipient); Sequence(new[] { 0, 1 }, prompt.Interaction.Choices);
            var result = session.Resume(context, new(kind, 1)); Equal(CommandStatus.Completed, result.Status);
            Equal(new CommandValue.Boolean(kind == OperationInputKind.Choice), result.Result!.Single().Value); random.RequireChoicesConsumed();
        }
        var disposed = new PillageFixture(); disposed.Features(MapFeatures.Mine); disposed.Data.AddCity(0, 2, new(14, 10));
        var abandoned = disposed.Pillage.Begin(new(0)); abandoned.Resume(); abandoned.Dispose();
        Throws<ObjectDisposedException>(() => abandoned.Resume(1)); Equal((byte)0, disposed.Data.Unit(0).MovesUsed);
        var remote = new PillageFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = remote.Pillage.Begin(new(0)); Throws<InvalidOperationException>(() => rejected.Resume()); Equal(0, remote.Redraws.Count);
    }

    private static void GroundPillageAdmission()
    {
        foreach (int variant in Enumerable.Range(0, 8))
        {
            var f = new PillageFixture(); f.Ai(); f.Features(MapFeatures.Mine); var c = f.Context();
            switch (variant)
            {
                case 1: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Movement = 6 }; break;
                case 2: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = 0 }; break;
                case 3: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Air }; break;
                case 4: f.World.Globals.HumanPlayers = CivilizationSet.Only(3); break;
                case 5: f.World.Civilizations[1].Treaties[2].War = false; break;
                case 6: c.ForceAttack = true; break;
                case 7: f.Features(MapFeatures.Road); f.World.Civilizations[1].ContinentCities[0] = 1; break;
            }
            using var op = f.Partisans.BeginPillage(c); Equal(variant == 0, op is not null); Equal(c.NearestCity, c.CitySelector);
            if (op is not null)
            {
                op.Resume(); Equal(true, op.Applied); Equal((byte)3, f.Data.Unit(0).MovesUsed);
                Equal(0, TacticOrders(f.Data).Finish(c.Unit, c.Domain)); Equal(UnitOrder.Fortifying, f.Data.Unit(0).Orders);
            }
        }
        foreach (bool partisan in new[] { false, true })
        {
            var f = new PillageFixture(); f.Ai(26); f.Features(MapFeatures.Road);
            if (partisan) { f.Data.Unit(0).Type = new(UnitTypeIds.Partisans); f.Data.Rules.Units[9] = f.Data.Rules.Units[0]; }
            using var op = f.Partisans.BeginPillage(f.Context()); Equal(partisan, op is not null);
            if (op is not null) { op.Resume(); Equal(MapFeatures.Unit, f.World.Map.TileAt(f.Point).Features); }
        }
        var airbase = new PillageFixture(); airbase.Ai(); airbase.Features(MapFeatures.Fortification | MapFeatures.CitySite);
        using var raw = airbase.Partisans.BeginPillage(airbase.Context()); Equal(true, raw is not null); raw!.Resume();
        Equal(MapFeatures.Unit, airbase.World.Map.TileAt(airbase.Point).Features);
    }

    private static void PartisanNonmilitaryTargets()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new PillageFixture(); f.Ai(); f.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
            f.World.Civilizations[1].Treaties[3].Alliance = true;
            f.Data.AddUnit(1, 3, 3, new(14, 10)); f.Data.Unit(1).Visibility = CivilizationSet.Only(1);
            f.Data.AddUnit(2, 2, 3, new(12, 10)); f.Data.Unit(2).Visibility = CivilizationSet.Only(1);
            switch (variant)
            {
                case 1: f.World.Civilizations[1].Treaties[3].Peace = true; break;
                case 2: f.Data.Unit(1).Visibility = default; break;
                case 3: f.World.Map.TileAt(new(14, 10)).Region = 1; break;
                case 4: f.Data.Rules.Units[4] = f.Data.Rules.Units[3] with { Role = (RuleRole)255 }; f.Data.Unit(1).Type = new(4); break;
            }
            var c = f.Context(); Equal(PartisanTargetRoute.PlanMove, f.Partisans.Targets(c).Route); Equal(true, c.HasOrders);
            Equal(variant == 0 ? new MapPoint(14, 10) : new MapPoint(12, 10), f.Data.Unit(0).Destination);
            Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal((byte)80, f.Data.Unit(0).AiTask);
        }
        var existing = new PillageFixture(); var retained = existing.Context(); retained.HasOrders = true;
        Equal(PartisanTargetRoute.PlanMove, existing.Partisans.Targets(retained).Route); Equal(UnitOrder.None, existing.Data.Unit(0).Orders);
    }

    private static void PartisanRoadSelection()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new PillageFixture(); f.Ai(18); f.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
            // D1 is closer, D7 is no farther than the original city distance. The last eligible direction wins.
            f.World.Map.TileAt(new(12, 10)).Features = MapFeatures.Road;
            f.World.Map.TileAt(new(10, 8)).Features = MapFeatures.Irrigation;
            if (variant == 1) f.Data.AddCity(1, 1, new(10, 12)); // D3 cancels the earlier selection and ends the scan.
            if (variant == 2) f.Data.AddUnit(1, 1, 0, new(10, 8)); // Occupancy excludes D7.
            if (variant == 3) f.World.Civilizations[1].Treaties[2].Ceasefire = true;
            var c = f.Context(); c.CitySelector = new(0);
            var result = f.Partisans.Targets(c);
            Equal(variant is 1 or 3 ? PartisanTargetRoute.Continue : PartisanTargetRoute.Direction, result.Route);
            if (result.Route == PartisanTargetRoute.Direction)
            {
                Equal(variant == 2 ? 1 : 7, result.Direction); Equal((byte)112, f.Data.Unit(0).AiTask);
                Equal(0, TacticOrders(f.Data).Direction(c, result.Direction)); Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders);
                Equal(variant == 2 ? new MapPoint(12, 10) : new MapPoint(10, 8), f.Data.Unit(0).Destination);
            }
        }
    }
}
