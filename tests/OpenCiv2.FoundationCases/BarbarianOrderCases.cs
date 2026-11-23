using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static List<Choice> BarbarianRolls(bool leader = false, params int[] directions) =>
        (directions.Length == 0 ? Enumerable.Range(0, 8) : directions).Select(d =>
            Choice.Create(new("W06.order-" + (leader ? "leader" : "military") + "-neighbor", "orders", d),
                new SamplingLaw.SecondaryRange(0, leader ? 3 : 5), ChoiceCode.Secondary(0))).ToList();
    private sealed class BarbarianOrderFixture : IBarbarianOrderEffects, IBarbarianRansomEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public EliminationFixture Life { get; }
        public BarbarianOrders Orders { get; }
        public ClassicTextInput Text { get; } = new();
        public List<string> Effects { get; } = [];
        public List<BarbarianLandingNotice> Notices { get; } = [];
        public bool FailCosmetic;
        public MapPoint Point => Data.Unit(0).Position;
        public BarbarianOrderFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Data = data ?? new(); Life = new(choices, Data);
            var incidents = new IncidentFixture(Life.Random, Data);
            World.Globals.Turn = 101; World.Globals.Year = 200;
            foreach (ref var tile in World.Map.Tiles) { tile.Region = 1; }
            Data.AddUnit(0, 0, 4, new(10, 10)); Data.Unit(0).LastDirection = 255;
            Text.Files["scenario/CITY.TXT"] = "@BARBARIANS\n1\nPay the raiders\n";
            var ransom = new BarbarianRansom(World, Data.Stacks, Life.Loss, Life.Elimination, incidents.Incidents,
                new ClassicRansomText(Text.Reader()), this);
            var pillage = new Pillage(World, Data.Rules, new(World, Data.Rules, _ => { }),
                new(World, Data.Rules, new MovementGoalQueries(World, Data.Rules)), (p, r) => Cosmetic("pillage"));
            Orders = new(World, Data.Rules, Life.Random, Data.Stacks, new(World, Data.Rules, Data.Stacks,
                new(World, Data.Rules, Data.Stacks)), pillage, ransom, Life.Loss, Life.Elimination, incidents.Incidents, this);
        }
        public MapPoint At(int d) => MapNeighborhoods.Offset(World.Map.Geometry, Point, MapNeighborhoods.Directions[d], World.Globals.Rules.FlatWorld);
        public void City(MapPoint point, int owner = 1, int slot = 0)
        {
            Data.AddCity(slot, owner, point); World.Entities.Cities.Storage[slot].Name = "Haven" + slot;
            World.Civilizations[owner].CityCount++; World.Civilizations[owner].TotalCitySize += 4;
        }
        public void Ship()
        {
            foreach (ref var tile in World.Map.Tiles) { tile.Terrain = Terrain.Ocean; tile.Region = 2; }
            Data.Rules.Units[4] = Data.Rules.Units[4] with { Domain = RuleDomain.Sea, Role = RuleRole.SeaTransport, Capacity = 4 };
            Data.AddUnit(1, 0, 0, Point); Data.Unit(1).Orders = UnitOrder.Sleeping;
        }
        public BarbarianOrders.Operation Begin() => Orders.Begin(new(0), "orders");
        public void Complete(BarbarianOrders.Operation op, int? reply = null)
        {
            for (int i = 0; i < 100 && op.Result is null; ++i)
            { var pending = op.Resume(reply); if (reply is null) Equal(null, pending); }
            Equal(true, op.Result is not null); Life.Random.RequireChoicesConsumed();
        }
        public void EnsureVisible(MapPoint point, int observer) => Cosmetic("visible:" + observer);
        public void Redraw(MapPoint point) => Cosmetic("draw");
        public void RefreshStatus() => Cosmetic("status");
        public void Landing(BarbarianLandingNotice notice) { Notices.Add(notice); Cosmetic("landing"); }
        private void Cosmetic(string value)
        { Effects.Add(value); if (FailCosmetic) throw new InvalidOperationException("Presentation unavailable"); }
    }

    private static void BarbarianOrderTiesAndMovement()
    {
        var f = new BarbarianOrderFixture(BarbarianRolls()); var origin = f.Point;
        f.Data.Unit(0).Orders = UnitOrder.Sleeping;
        using var op = f.Begin(); f.Complete(op); Equal(0, op.Result);
        Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders); Equal(f.At(7), f.Data.Unit(0).Destination);
        Equal(origin, f.Point); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        var execution = new OrderFixture();
        // Keep the real dispatcher and all of its real children, with an ordinary empty test map.
        execution.Data.World.Entities.Cities.SetEnd(0); execution.Data.World.Entities.Units.SetEnd(0);
        foreach (ref var tile in execution.Data.World.Map.Tiles) { tile.Features = default; tile.Owner = null; }
        var follow = new BarbarianOrderFixture(BarbarianRolls(), execution.Data);
        follow.Data.Stacks.Relocate(0, new(18, 12), false); follow.Data.Unit(0).LastDirection = 1;
        using var selected = follow.Begin(); follow.Complete(selected);
        Equal(new MapPoint(20, 12), follow.Data.Unit(0).Destination);
        using var move = execution.Begin(); execution.Complete(move);
        Equal(new MapPoint(20, 12), follow.Point); Equal(true, follow.Data.Unit(0).MovesUsed > 0);
    }

    private static void BarbarianOrderHoldAndGarrison()
    {
        foreach (bool garrison in new[] { false, true })
        {
            var f = new BarbarianOrderFixture();
            f.World.Map.TileAt(f.Point).Features |= MapFeatures.Fortification;
            f.Data.Unit(0).Flags.Garrison = garrison;
            using var op = f.Begin(); f.Complete(op);
            Equal(garrison ? UnitOrder.Fortified : UnitOrder.Fortifying, f.Data.Unit(0).Orders);
            Equal((byte)255, f.Data.Unit(0).LastDirection); Equal((byte)6, f.Data.Unit(0).MovesUsed);
        }
        var city = new BarbarianOrderFixture(); city.City(city.Point, 0);
        using var held = city.Begin(); city.Complete(held); Equal(UnitOrder.Fortifying, city.Data.Unit(0).Orders);
        var none = new BarbarianOrderFixture();
        foreach (int d in Enumerable.Range(0, 8)) none.World.Map.TileAt(none.At(d)).Terrain = Terrain.Ocean;
        none.Data.Unit(0).Orders = UnitOrder.GoTo; none.Data.Unit(0).Destination = none.Point;
        using var stopped = none.Begin(); none.Complete(stopped);
        Equal(UnitOrder.None, none.Data.Unit(0).Orders); Equal((byte)6, none.Data.Unit(0).MovesUsed);
    }

    private static void BarbarianOrderPolarAndPeriodic()
    {
        var polar = new BarbarianOrderFixture(); polar.Ship();
        polar.Data.Stacks.Relocate(0, new(10, 0), false); polar.Data.Stacks.Relocate(1, new(10, 0), false);
        using var erased = polar.Begin(); polar.Complete(erased); Equal(1, erased.Result);
        Equal(false, polar.Data.Unit(0).Token.Occupied); Equal(false, polar.Data.Unit(1).Token.Occupied);
        foreach (bool wrongRegion in new[] { false, true })
        {
            var f = new BarbarianOrderFixture(); f.World.Globals.Turn = 100;
            f.City(wrongRegion ? new(14, 10) : new(34, 10));
            if (wrongRegion) f.World.Map.TileAt(new(14, 10)).Region = 3;
            using var op = f.Begin(); f.Complete(op); Equal(1, op.Result); Equal(false, f.Data.Unit(0).Token.Occupied);
        }
        var cadence = new BarbarianOrderFixture(BarbarianRolls()); cadence.City(new(34, 10));
        using var retained = cadence.Begin(); cadence.Complete(retained); Equal(0, retained.Result); Equal(true, cadence.Data.Unit(0).Token.Occupied);
    }

    private static void BarbarianTransportLifetime()
    {
        var thin = new BarbarianOrderFixture(); thin.Ship();
        thin.Data.Rules.Units[0] = thin.Data.Rules.Units[0] with { Role = RuleRole.Diplomacy };
        using var missing = thin.Begin(); thin.Complete(missing); Equal(1, missing.Result); Equal(false, thin.Data.Unit(0).Token.Occupied);
        foreach (byte counter in new byte[] { 30, 127, 255 })
        {
            var f = new BarbarianOrderFixture(); f.Ship(); f.Data.Unit(0).Counter = counter;
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(30, 10);
            using var op = f.Begin(); f.Complete(op);
            Equal(counter == 30 ? 1 : 0, op.Result); Equal(counter != 30, f.Data.Unit(0).Token.Occupied);
            Equal(unchecked((byte)(counter + 1)), f.Data.Unit(0).Counter);
            if (counter != 30) { Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).MovesUsed); }
        }
    }

    private static void BarbarianTransportLanding()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new BarbarianOrderFixture(); f.Ship(); f.FailCosmetic = fail; var origin = f.Point;
            f.City(new(14, 10)); f.World.Map.TileAt(new(14, 10)).Terrain = Terrain.Grassland;
            f.World.Map.TileAt(new(14, 10)).Region = 1;
            ref var land = ref f.World.Map.TileAt(f.At(0)); land.Terrain = Terrain.Grassland; land.Region = 1;
            land.RevealedTo = CivilizationSet.Only(1).With(2);
            using var op = f.Begin(); f.Complete(op);
            Equal(0, op.Result); Equal(origin, f.Point); Equal(origin, f.Data.Unit(1).Position);
            Equal(AiTaskIds.UnloadRaid, f.Data.Unit(0).AiTask); Equal(UnitOrder.None, f.Data.Unit(1).Orders);
            Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(true, f.Data.Unit(0).Visibility.Contains(2));
            Equal(true, f.Data.Unit(1).Visibility.Contains(1)); Equal(false, f.Data.Unit(1).Visibility.Contains(2));
            Equal(1, f.Notices.Count); Equal(1, f.Notices[0].Observer);
            Sequence(new[] { "visible:0", "draw", "landing" }, f.Effects);
            Equal(fail ? 3 : 0, op.PresentationFailures.Count);
        }
    }

    private static void BarbarianTransportEmptyLandingAndAttack()
    {
        var empty = new BarbarianOrderFixture(); empty.Ship();
        empty.Data.Rules.Units[0] = empty.Data.Rules.Units[0] with { Role = RuleRole.Trade };
        empty.City(new(12, 10)); empty.World.Map.TileAt(new(12, 10)).Region = 1;
        empty.World.Map.TileAt(empty.At(0)).Terrain = Terrain.Grassland; empty.World.Map.TileAt(empty.At(0)).Region = 1;
        empty.Data.Unit(0).Orders = UnitOrder.GoTo; empty.Data.Unit(0).Destination = empty.Point;
        using var deleted = empty.Begin(); empty.Complete(deleted);
        Equal(0, deleted.Result); Equal(false, empty.Data.Unit(0).Token.Occupied);
        Equal(UnitOrder.None, empty.Data.Unit(0).Orders); Equal(0, empty.Notices.Count);
        var attack = new BarbarianOrderFixture(); attack.Ship(); attack.City(new(14, 10));
        attack.Data.AddUnit(2, 2, 1, attack.At(0));
        attack.World.Map.TileAt(attack.At(1)).Terrain = Terrain.Grassland;
        using var selected = attack.Begin(); attack.Complete(selected);
        Equal(UnitOrder.AiGoTo, attack.Data.Unit(0).Orders); Equal(attack.At(0), attack.Data.Unit(0).Destination);
        Equal((byte)0, attack.Data.Unit(0).MovesUsed); Equal(0, attack.Notices.Count);
    }

    private static void BarbarianTransportTargets()
    {
        foreach (bool borders in new[] { false, true })
        {
            var f = new BarbarianOrderFixture(); f.Ship();
            f.Data.Stacks.Relocate(0, new(20, 12), false); f.Data.Stacks.Relocate(1, new(20, 12), false);
            f.City(new(4, 12), 2); f.City(new(36, 12), 3, 1);
            foreach (var point in new[] { new MapPoint(4, 12), new MapPoint(36, 12) })
            { f.World.Map.TileAt(point).Terrain = Terrain.Grassland; f.World.Map.TileAt(point).Region = 1; }
            // Equal weighted distances and zero treasury: first city wins.
            f.World.Map.Regions[1] = new(1, 0, 0, borders ? new RegionSet().With(2) : default);
            using var op = f.Begin(); f.Complete(op);
            Equal(borders ? 0 : 1, op.Result);
            if (borders)
            {
                Equal(AiTaskIds.RaidTransport, f.Data.Unit(0).AiTask); Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders);
                Equal(new MapPoint(4, 12), f.Data.Unit(0).Destination); // No noncity shore: A05 fallback.
            }
        }
    }

    private static void BarbarianLeaderOrders()
    {
        var together = new BarbarianOrderFixture();
        together.Data.Rules.Units[4] = together.Data.Rules.Units[4] with { Role = RuleRole.Diplomacy };
        together.Data.AddUnit(1, 0, 0, together.Point);
        using var hold = together.Begin(); together.Complete(hold);
        Equal(UnitOrder.None, together.Data.Unit(0).Orders); Equal((byte)6, together.Data.Unit(0).MovesUsed);
        var follow = new BarbarianOrderFixture();
        follow.Data.Rules.Units[4] = follow.Data.Rules.Units[4] with { Role = RuleRole.Trade };
        follow.Data.AddUnit(1, 0, 0, new(14, 10));
        using var order = follow.Begin(); follow.Complete(order);
        Equal(UnitOrder.GoTo, follow.Data.Unit(0).Orders); Equal(AiTaskIds.LeaderFollow, follow.Data.Unit(0).AiTask);
        Equal(new MapPoint(14, 10), follow.Data.Unit(0).Destination); Equal((byte)0, follow.Data.Unit(0).MovesUsed);
        var gone = new BarbarianOrderFixture(); gone.Data.Rules.Units[4] = gone.Data.Rules.Units[4] with { Role = RuleRole.Diplomacy };
        using var absent = gone.Begin(); gone.Complete(absent); Equal(1, absent.Result);
        var scoring = new BarbarianOrderFixture(BarbarianRolls(true)); scoring.City(new(12, 10));
        scoring.Data.Rules.Units[4] = scoring.Data.Rules.Units[4] with { Role = RuleRole.Trade };
        using var roam = scoring.Begin(); scoring.Complete(roam);
        Equal(new MapPoint(8, 10), scoring.Data.Unit(0).Destination);
    }

    private static void BarbarianMilitaryScoring()
    {
        var f = new BarbarianOrderFixture(BarbarianRolls());
        f.Data.AddUnit(1, 0, 0, f.At(7)); // Rejected only after its random draw.
        f.World.Map.TileAt(f.At(1)).Features |= MapFeatures.Mine;
        f.World.Map.TileAt(f.At(3)).Features |= MapFeatures.Fortification;
        using var selected = f.Begin(); f.Complete(selected); Equal(f.At(3), f.Data.Unit(0).Destination);
        var last = new BarbarianOrderFixture(BarbarianRolls()); last.Data.Unit(0).LastDirection = 2;
        using var repeat = last.Begin(); last.Complete(repeat); Equal(last.At(2), last.Data.Unit(0).Destination);
        foreach (bool diplomat in new[] { false, true })
        {
            var enemy = new BarbarianOrderFixture(BarbarianRolls());
            enemy.Data.Rules.Units[9] = enemy.Data.Rules.Units[0] with { Role = diplomat ? RuleRole.Diplomacy : RuleRole.Attack };
            enemy.Data.AddUnit(1, 2, 9, enemy.At(0));
            enemy.Data.Rules.Units[4] = enemy.Data.Rules.Units[4] with { Abilities = new() { IgnoreZonesOfControl = true } };
            using var op = enemy.Begin(); enemy.Complete(op); Equal(enemy.At(diplomat ? 7 : 0), enemy.Data.Unit(0).Destination);
        }
    }

    private static void BarbarianOrderRansom()
    {
        foreach (int answer in new[] { 0, 1 })
        {
            var f = new BarbarianOrderFixture(); f.City(f.At(0)); f.World.Civilizations[1].Gold = 150;
            using var op = f.Begin();
            var decision = op.Resume() as BarbarianOrderDecision.Ransom;
            Equal(true, decision?.Decision is BarbarianRansomDecision.Offer { Demand: 100 });
            Equal(decision, op.Resume()); Equal(150, f.World.Civilizations[1].Gold);
            f.Complete(op, answer); Equal(answer, op.Result); Equal(answer == 0 ? 150 : 50, f.World.Civilizations[1].Gold);
            Equal(answer == 0, f.Data.Unit(0).Token.Occupied);
            if (answer == 0) Equal(f.At(0), f.Data.Unit(0).Destination);
        }
        var missing = new BarbarianOrderFixture(); missing.City(missing.At(0)); missing.World.Civilizations[1].Gold = 150;
        missing.Text.Files.Clear(); using var stopped = missing.Begin(); missing.Complete(stopped);
        Equal(0, stopped.Result); Equal(UnitOrder.None, missing.Data.Unit(0).Orders); Equal((byte)0, missing.Data.Unit(0).MovesUsed);
    }

    private static void BarbarianOrderActualPillage()
    {
        var f = new BarbarianOrderFixture(BarbarianRolls()); f.City(new(14, 10));
        f.Data.Unit(0).MovesUsed = 4; f.World.Map.TileAt(f.Point).Features |= MapFeatures.Irrigation | MapFeatures.Mine;
        using var op = f.Begin(); f.Complete(op);
        Equal(false, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Mine));
        Equal(true, f.World.Map.TileAt(f.Point).Features.HasFlag(MapFeatures.Irrigation));
        Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        Sequence(new[] { "pillage" }, f.Effects);
        var refused = new BarbarianOrderFixture(BarbarianRolls()); refused.City(new(14, 10));
        refused.World.Globals.HumanPlayers = CivilizationSet.Only(0).With(1);
        refused.World.Civilizations[0].Treaties[1].Peace = true;
        refused.Data.Unit(0).MovesUsed = 4; refused.World.Map.TileAt(refused.Point).Features |= MapFeatures.Mine;
        using var decline = refused.Begin(); Equal(true, decline.Resume() is BarbarianOrderDecision.Pillage);
        Equal((byte)4, refused.Data.Unit(0).MovesUsed); refused.Complete(decline, 0);
        Equal(true, refused.World.Map.TileAt(refused.Point).Features.HasFlag(MapFeatures.Mine));
        Equal((byte)6, refused.Data.Unit(0).MovesUsed); Equal(0, refused.Effects.Count);
    }

    private static void BarbarianOrderPartisans()
    {
        foreach (bool cadence in new[] { false, true })
        {
            var f = new BarbarianOrderFixture(cadence ? BarbarianRolls(false, 0, 2, 3, 4, 5, 6, 7) : BarbarianRolls());
            f.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
            f.City(f.At(1), 2); f.Data.AddUnit(1, 2, 0, f.At(1)); f.World.Globals.Turn = (short)(cadence ? 104 : 101);
            using var op = f.Begin(); f.Complete(op);
            if (cadence) { Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders); Equal(f.At(7), f.Data.Unit(0).Destination); }
            else { Equal(UnitOrder.Fortifying, f.Data.Unit(0).Orders); Equal((byte)6, f.Data.Unit(0).MovesUsed); }
        }
        var leader = new BarbarianOrderFixture(BarbarianRolls());
        leader.Data.Unit(0).Type = new(UnitTypeIds.Partisans);
        leader.Data.AddUnit(1, 2, 3, leader.At(0)); // A Trade target is admissible even for Partisans.
        using var attack = leader.Begin(); leader.Complete(attack);
        Equal(leader.At(0), leader.Data.Unit(0).Destination); Equal((byte)0, leader.Data.Unit(0).MovesUsed);
    }

    private static void BarbarianOrderPursuit()
    {
        foreach (bool rich in new[] { false, true })
        {
            var f = new BarbarianOrderFixture(BarbarianRolls()); f.City(new(10, 4));
            f.World.Civilizations[1].CityCount = 2; f.World.Civilizations[1].Gold = rich ? 100 : 99;
            f.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1 }; f.World.Paths.Owner = 0;
            f.World.Map.CoarseGrid(0).Fill(0); f.World.Map.CoarseGrid(1).Fill(0);
            if (rich) f.World.Map.TileAt(f.At(3)).Features |= MapFeatures.Mine;
            using var op = f.Begin(); f.Complete(op);
            Equal(rich ? (byte)7 : (byte)255, f.Data.Unit(0).LastDirection);
            Equal(f.At(rich ? 3 : 7), f.Data.Unit(0).Destination);
            Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        }
        // Ignore-ZOC is checked on the actual actor type after a candidate's draw.
        foreach (bool ignore in new[] { false, true })
        {
            var f = new BarbarianOrderFixture(BarbarianRolls());
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Abilities = new() { IgnoreZonesOfControl = ignore } };
            f.Data.AddUnit(1, 2, 47, f.At(0)); f.World.Map.TileAt(f.At(7)).Features |= MapFeatures.Fortification;
            using var op = f.Begin(); f.Complete(op);
            Equal(f.At(ignore ? 7 : 5), f.Data.Unit(0).Destination);
        }
    }

    private static void BarbarianTransportCoastAndCustomDomain()
    {
        var f = new BarbarianOrderFixture(); f.Ship(); f.City(new(30, 10));
        f.World.Map.TileAt(new(30, 10)).Terrain = Terrain.Grassland; f.World.Map.TileAt(new(30, 10)).Region = 1;
        f.World.Map.TileAt(new(32, 10)).Terrain = Terrain.Grassland; f.World.Map.TileAt(new(32, 10)).Region = 1;
        f.World.Map.Regions[1] = new(2, 0, 0, new RegionSet().With(2));
        using var coast = f.Begin(); f.Complete(coast);
        Equal(new MapPoint(31, 9), f.Data.Unit(0).Destination); Equal(AiTaskIds.RaidTransport, f.Data.Unit(0).AiTask);
        var custom = new BarbarianOrderFixture(); custom.Ship();
        custom.Data.Rules.Units[4] = custom.Data.Rules.Units[4] with { Domain = RuleDomain.Air, Role = RuleRole.Trade };
        custom.Data.Unit(0).Orders = UnitOrder.GoTo; custom.Data.Unit(0).Destination = new(30, 10);
        using var hold = custom.Begin(); custom.Complete(hold);
        Equal((byte)1, custom.Data.Unit(0).Counter); Equal(UnitOrder.GoTo, custom.Data.Unit(0).Orders);
        var air = new BarbarianOrderFixture(BarbarianRolls());
        air.Data.Rules.Units[4] = air.Data.Rules.Units[4] with { Domain = RuleDomain.Air };
        using var roam = air.Begin(); air.Complete(roam); Equal(air.At(7), air.Data.Unit(0).Destination);
    }

    private static void BarbarianOrderFailureAndCancellation()
    {
        var f = new BarbarianOrderFixture(); f.City(f.At(0)); f.World.Civilizations[1].Gold = 150;
        using var op = f.Begin(); Equal(true, op.Resume() is BarbarianOrderDecision.Ransom);
        op.Dispose(); Equal(150, f.World.Civilizations[1].Gold); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        Throws<ObjectDisposedException>(() => op.Resume());
        var failed = new BarbarianOrderFixture(); failed.Data.Unit(0).Orders = UnitOrder.Sleeping;
        using var missingDraw = failed.Begin(); Throws<ChoiceException>(() => missingDraw.Resume());
        Equal(UnitOrder.None, failed.Data.Unit(0).Orders); Equal((byte)0, failed.Data.Unit(0).MovesUsed);
        Throws<InvalidOperationException>(() => missingDraw.Resume());
        var cancelled = new BarbarianOrderFixture(); cancelled.City(cancelled.At(0)); cancelled.World.Civilizations[1].Gold = 150;
        var context = new OperationContext(cancelled.World, cancelled.Data.Process, cancelled.Life.Random, new PresentationRandom(2, "orders"),
            new(default, new(1)), default, (_, _) => { }, () => { });
        using var session = new BarbarianOrderOperation(cancelled.Begin(), _ => new(1));
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
        Equal(CommandStatus.Completed, session.Resume(context, new(OperationInputKind.Cancel)).Status);
        Equal(UnitOrder.AiGoTo, cancelled.Data.Unit(0).Orders); Equal(150, cancelled.World.Civilizations[1].Gold);
        var remote = new BarbarianOrderFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        remote.Data.Unit(0).Orders = UnitOrder.Sleeping; using var unsupported = remote.Begin();
        Throws<InvalidOperationException>(() => unsupported.Resume()); Equal(UnitOrder.Sleeping, remote.Data.Unit(0).Orders);
    }

    private static void BarbarianOrderPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(728));
        var f = new BarbarianOrderFixture(data: new(runtime.Process, runtime.World));
        f.City(f.At(0)); f.World.Civilizations[1].Gold = 150; ParticipantId victimSeat = default;
        runtime.RegisterCommand(new(new(1, "test.barbarian-orders", "Orders", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new BarbarianOrderOperation(f.Begin(), _ => victimSeat)));
        runtime.BeginClientSession(new()); var caller = SessionClient(runtime, 2); var victim = SessionClient(runtime);
        victimSeat = victim.Identity.Participant;
        var command = caller.Submit(new("test.barbarian-orders", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(null, RequiredResult(caller, command).Interaction); var prompt = RequiredInteraction(victim, command);
        Equal(false, caller.Reply(prompt, 1).Accepted); Equal(true, victim.Reply(prompt, 1).Accepted);
        runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(caller, command).Status);
        Equal(new CommandValue.Integer(1), RequiredResult(caller, command).Result.Single().Value);
        Equal(50, f.World.Civilizations[1].Gold); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal(false, victim.Reply(prompt, 1).Accepted); f.Life.Random.RequireChoicesConsumed();
    }
}
