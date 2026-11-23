using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class AirFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public AirTactics Air { get; }
        public AirFixture(bool large = false)
        {
            if (large) World.Map.Allocate(new(80, 64));
            World.Globals.HumanPlayers = CivilizationSet.Only(2); World.Multiplayer.LocalHuman = 2;
            Data.Rules.Units[2] = Data.Rules.Units[2] with
            { Role = RuleRole.AirSuperiority, Movement = 12, Attack = 12, Defense = 1, Firepower = 1, CostRows = 1 };
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Defense = 1, Firepower = 1, CostRows = 4 };
            Data.Rules.Terrain[(int)Terrain.Grassland] = Data.Rules.Terrain[(int)Terrain.Grassland] with { DefenseFactor = 2 };
            Data.AddUnit(0, 1, 2, new(10, 10)); Data.Unit(0).Orders = UnitOrder.None;
            Data.Unit(0).AiTask = 7; Data.Unit(0).Visibility = CivilizationSet.Only(2);
            Data.AddCity(0, 1, new(10, 10));
            Air = new(World, Data.Rules, Data.Stacks, new(World, Data.Rules));
        }
        public UnitTacticsContext Context() => new(World, Data.Rules, new(0));
        public SpecialUnitChoice Choose() => Air.Choose(Context());
        public void Enemy(int slot = 1, int type = 0, MapPoint? point = null, int owner = 2)
        { Data.AddUnit(slot, owner, type, point ?? new(12, 10)); Data.Unit(slot).Visibility = CivilizationSet.Only(1); }
        public void Expended()
        {
            Data.Rules.Units[2] = Data.Rules.Units[2] with { Abilities = new() { DestroyedAfterAttack = true } };
            World.Civilizations[1].Treaties[2].War = true;
        }
        public void RemoveHome() { World.Entities.Cities.Storage[0].Token = default; World.Map.TileAt(new(10, 10)).Features &= ~MapFeatures.CitySite; }
    }

    // A06 §2: recovery uses live fuel arithmetic, ordinary city distance and the actual GoHome child.
    private static void AirFuelRecovery()
    {
        foreach (var (range, counter, spent, order, recover) in new (byte, byte, byte, UnitOrder, bool)[]
        {
            (1,0,9,UnitOrder.None,true), (1,0,6,UnitOrder.None,false), (1,0,9,UnitOrder.GoTo,false),
            (2,1,0,UnitOrder.GoTo,true), (2,255,0,UnitOrder.None,false), (128,128,0,UnitOrder.None,false),
            (0,100,12,UnitOrder.None,false)
        })
        {
            var f = new AirFixture(); f.RemoveHome(); f.Data.AddCity(1, 1, new(12, 10));
            f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Range = range, Role = RuleRole.Trade };
            f.Data.Unit(0).Counter = counter; f.Data.Unit(0).MovesUsed = spent; f.Data.Unit(0).Orders = order;
            Equal(SpecialUnitRoute.Finish, f.Choose().Route); Equal(recover ? AiTaskIds.AirReturn : (byte)7, f.Data.Unit(0).AiTask);
        }
        var carrier = new AirFixture(); carrier.RemoveHome(); carrier.Data.AddCity(1, 1, new(14, 10));
        carrier.Data.Rules.Units[2] = carrier.Data.Rules.Units[2] with { Range = 1 };
        carrier.Data.Unit(0).MovesUsed = 6;
        carrier.Data.Rules.Units[1] = carrier.Data.Rules.Units[1] with { Abilities = new() { CarryAircraft = true } };
        carrier.Data.AddUnit(1, 1, 1, new(11, 9));
        Equal(SpecialUnitRoute.Finish, carrier.Choose().Route); Equal(new MapPoint(11, 9), carrier.Data.Unit(0).Destination);
        Equal(AiTaskIds.AirReturn, carrier.Data.Unit(0).AiTask); Equal((byte)6, carrier.Data.Unit(0).MovesUsed);
    }

    private static void AirExpendedSelection()
    {
        var tie = new AirFixture(); tie.Expended(); tie.Enemy(); tie.Enemy(2, point: new(10, 14));
        tie.Data.Unit(2).Visibility = default; tie.Data.Unit(0).MovesUsed = 12;
        var c = tie.Context(); var choice = tie.Air.Choose(c);
        Equal(new SpecialUnitChoice(SpecialUnitRoute.Direction, 4), choice);
        Equal(new MapPoint(11, 13), c.Position); Equal(c.Position, tie.Data.Unit(0).Position);
        Equal(default(CivilizationSet), tie.Data.Unit(0).Visibility); Equal((byte)12, tie.Data.Unit(0).MovesUsed);
        Equal((byte)7, tie.Data.Unit(0).AiTask); TacticOrders(tie.Data).Direction(c, choice.Direction);
        Equal(new MapPoint(10, 14), tie.Data.Unit(0).Destination); Equal(UnitOrder.AiGoTo, tie.Data.Unit(0).Orders);
        foreach (bool zeroAirbase in new[] { false, true })
        {
            var f = new AirFixture(); f.Expended(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { CostRows = 2 };
            int owner = zeroAirbase ? 0 : 2; f.World.Civilizations[1].Treaties[owner].War = true;
            f.Enemy(owner: owner); f.Enemy(2, owner: owner);
            if (zeroAirbase) f.World.Map.TileAt(new(12, 10)).Features |= MapFeatures.CitySite | MapFeatures.Fortification;
            Equal(zeroAirbase ? 4 : 8, f.Choose().Direction); // Absent airbase is nonzero; only owner zero uses total stack cost here.
        }
        var noWar = new AirFixture(); noWar.Expended(); noWar.Enemy(); noWar.World.Civilizations[1].Treaties[2].War = false;
        Equal(8, noWar.Choose().Direction); Equal(new MapPoint(10, 10), noWar.Data.Unit(0).Position);
        var aircraft = new AirFixture(); aircraft.Expended(); aircraft.Enemy(type: 2);
        Equal(8, aircraft.Choose().Direction);
    }

    private static void AirExpendedModifiersAndRedistribution()
    {
        foreach (byte hold in new byte[] { 0, 255 })
        {
            var f = new AirFixture(); f.Expended(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { CostRows = 1, Capacity = hold };
            f.Enemy(); f.World.Map.TileAt(new(12, 10)).Terrain = Terrain.Ocean;
            f.World.Map.TileAt(new(12, 10)).Features |= MapFeatures.Fortification;
            Equal(hold == 0 ? 4 : 8, f.Choose().Direction); // Ocean and fortress each double before signed hold is added.
        }
        var spread = new AirFixture(); spread.Expended(); spread.Data.AddUnit(1, 1, 2, new(10, 10));
        spread.Data.Unit(1).Visibility = CivilizationSet.Only(2);
        spread.Data.AddCity(1, 1, new(12, 14)); spread.Data.AddCity(2, 1, new(12, 10));
        var c = spread.Context(); Equal(new SpecialUnitChoice(SpecialUnitRoute.Direction), spread.Air.Choose(c));
        Equal(new MapPoint(12, 14), c.Position); Equal(c.Position, spread.Data.Unit(0).Position);
        Equal(new MapPoint(10, 10), spread.Data.Unit(1).Position); Equal(CivilizationSet.Only(2), spread.Data.Unit(1).Visibility);
        var single = new AirFixture(); single.Expended(); single.Data.AddCity(1, 1, new(12, 14));
        Equal(8, single.Choose().Direction); Equal(new MapPoint(10, 10), single.Data.Unit(0).Position);
    }

    private static void AirExpendedBlockedTarget()
    {
        var f = new AirFixture(); f.Expended(); f.Enemy(point: new(14, 10));
        f.Data.Rules.Units[9] = f.Data.Rules.Units[0] with { CostRows = 20 };
        f.Enemy(2, 9, new(18, 10));
        for (int d = 0; d < 8; ++d)
        {
            var p = MapNeighborhoods.Offset(f.World.Map.Geometry, new(18, 10), MapNeighborhoods.Directions[d], true);
            f.Data.AddUnit(3 + d, 0, 0, p);
        }
        Equal(8, f.Choose().Direction); Equal(new MapPoint(10, 10), f.Data.Unit(0).Position); // No retry of the lower-scoring target.
        var outOfRange = new AirFixture(); outOfRange.Expended(); outOfRange.Enemy(point: new(20, 10));
        Equal(8, outOfRange.Choose().Direction);
    }

    private static void AirSuperiorityAdmissionAndStrength()
    {
        foreach (int variant in Enumerable.Range(0, 8))
        {
            var f = new AirFixture(); f.Enemy();
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Defense = variant == 4 ? (byte)9 : (byte)8 };
            if (variant == 0) f.Data.Unit(1).Visibility = default;
            if (variant == 1) f.World.Civilizations[1].Treaties[2].Ceasefire = true;
            if (variant == 2) f.World.Civilizations[1].Treaties[2].Peace = true;
            if (variant == 3) f.World.Civilizations[1].Treaties[2].Alliance = true;
            if (variant == 5) f.Data.Unit(1).Flags.Veteran = true;
            if (variant == 6) { f.Data.Unit(1).Flags.Veteran = true; f.Data.Unit(0).Flags.Veteran = true; }
            if (variant == 7) { f.Data.Unit(1).Damage = 9; f.Data.Unit(0).Damage = 9; }
            bool admitted = variant is 3 or 6 or 7;
            Equal(admitted ? SpecialUnitRoute.Finish : SpecialUnitRoute.Continue, f.Choose().Route);
            Equal(admitted ? AiTaskIds.Strike : (byte)7, f.Data.Unit(0).AiTask); // Equal strength qualifies; damage is not subtracted.
        }
        var air = new AirFixture(); air.Data.Rules.Units[2] = air.Data.Rules.Units[2] with { Attack = 0, Firepower = 0 };
        air.Enemy(type: 2); air.Enemy(2, 2); Equal(SpecialUnitRoute.Finish, air.Choose().Route);
        Equal(AiTaskIds.Strike, air.Data.Unit(0).AiTask);
    }

    private static void AirSuperiorityStackGates()
    {
        var land = new AirFixture(); land.Enemy(); land.Enemy(2);
        Equal(SpecialUnitRoute.Continue, land.Choose().Route);
        foreach (int variant in Enumerable.Range(0, 3))
        {
            var f = new AirFixture(); f.Data.Rules.Units[1] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Sea, Capacity = 2, Defense = 6 };
            f.Enemy(type: 1);
            if (variant == 1) f.Enemy(2, 0); // One ship plus passengers avoids solitary-transport defense doubling.
            if (variant == 2) f.Enemy(2, 1);
            Equal(variant == 1 ? SpecialUnitRoute.Finish : SpecialUnitRoute.Continue, f.Choose().Route);
        }
    }

    private static void AirSuperiorityAdjustedTies()
    {
        var f = new AirFixture(); f.Enemy(1, 2, new(10, 8), 3); f.Enemy(2, 2, new(14, 10), 2);
        Equal(SpecialUnitRoute.Finish, f.Choose().Route); Equal(new MapPoint(14, 10), f.Data.Unit(0).Destination);
        // AI distance one and human distance two both score one; later adjusted ties retain the human's raw distance.
        var range = new AirFixture(); range.Data.Rules.Units[9] = range.Data.Rules.Units[2] with { Range = 1 };
        range.Enemy(1, 9, new(12, 10), 3); range.Enemy(2, 2, new(10, 12), 3);
        range.Choose(); Equal(new MapPoint(12, 10), range.Data.Unit(0).Destination);
        var stack = new AirFixture(); stack.Enemy(1, 2, new(12, 10), 3); stack.Enemy(2, 2, new(10, 12), 3); stack.Enemy(3, 2, new(10, 12), 3);
        stack.Choose(); Equal(new MapPoint(12, 10), stack.Data.Unit(0).Destination);
    }

    private static void AirStrikeReturnAndBasing()
    {
        foreach (byte movement in new byte[] { 6, 9 })
        {
            var f = new AirFixture(); f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Range = 1, Movement = movement };
            f.Enemy(type: 2, point: new(14, 10));
            Equal(SpecialUnitRoute.Finish, f.Choose().Route);
            Equal(movement == 9 ? AiTaskIds.Strike : AiTaskIds.StageAirStrike, f.Data.Unit(0).AiTask);
            Equal(movement == 9 ? new MapPoint(14, 10) : new(10, 10), f.Data.Unit(0).Destination);
        }
        var stranded = new AirFixture(); stranded.RemoveHome(); stranded.Data.AddCity(1, 1, new(30, 10));
        stranded.Data.Rules.Units[2] = stranded.Data.Rules.Units[2] with { Range = 1, Movement = 3 };
        stranded.Enemy(type: 2, point: new(18, 10)); stranded.Choose();
        Equal(AiTaskIds.Strike, stranded.Data.Unit(0).AiTask); Equal(new MapPoint(18, 10), stranded.Data.Unit(0).Destination); // Beyond reach when home is unreachable.
        var bases = new AirFixture(); bases.Data.Rules.Units[2] = bases.Data.Rules.Units[2] with { Movement = 3 };
        bases.Enemy(type: 2, point: new(20, 6)); bases.Data.AddCity(1, 1, new(14, 10)); bases.Data.AddCity(2, 1, new(12, 8));
        bases.Data.AddCity(3, 1, new(18, 8)); // A better base outside reach is excluded.
        bases.Choose(); Equal(AiTaskIds.StageAirStrike, bases.Data.Unit(0).AiTask);
        Equal(new MapPoint(14, 10), bases.Data.Unit(0).Destination); // Equal base scores retain the first city.
        var blocked = new AirFixture(); blocked.Enemy(type: 2, point: new(14, 10));
        blocked.Enemy(2, point: new(12, 10), owner: 3); blocked.Data.Unit(2).Visibility = default;
        blocked.Choose(); Equal(AiTaskIds.StageAirStrike, blocked.Data.Unit(0).AiTask);
        Equal(new MapPoint(10, 10), blocked.Data.Unit(0).Destination); // The sole nearer return candidate has a foreign occupant.
    }

    private static void AirObjectiveChainAndVendetta()
    {
        foreach (bool vendetta in new[] { false, true })
        {
            var f = new AirFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(3);
            f.Enemy(type: 2, point: new(30, 10)); f.Data.AddCity(1, 1, new(14, 10)); f.Data.AddCity(2, 1, new(10, 14));
            int slot = 3;
            foreach (var p in new[] { new MapPoint(24, 14), new(18, 14), new(14, 14), new(12, 14) })
            { f.Data.AddCity(slot, 3, p); f.World.Entities.Cities.Storage[slot++].Flags.Airlifted = true; }
            f.World.Civilizations[1].Treaties[3].Peace = true; f.World.Civilizations[1].Treaties[3].Vendetta = vendetta;
            f.Choose(); Equal(AiTaskIds.StageAirStrike, f.Data.Unit(0).AiTask);
            Equal(vendetta ? new MapPoint(10, 14) : new(14, 10), f.Data.Unit(0).Destination);
        }
    }

    private static void AirMissingHomeReturnAndHold()
    {
        foreach (int distance in new[] { 1, 2 })
        {
            var f = new AirFixture(); f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { RoadMovementMultiplier = 1 };
            f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Range = 100, Movement = 101 }; f.Data.Unit(0).Counter = 1;
            var c = f.Context(); f.World.Entities.Cities.Storage[0].Token = default; // Captured dh=0; live recovery scan has no city.
            f.Enemy(type: 2, point: new(10 + 2 * distance, 10));
            var choice = f.Air.Choose(c); // reach=9999; only the distance-two target admits a nearer noncity neighbor.
            Equal(distance == 1 ? SpecialUnitRoute.Direction : SpecialUnitRoute.Finish, choice.Route);
            Equal(distance == 1 ? AiTaskIds.AirHold : AiTaskIds.Strike, f.Data.Unit(0).AiTask);
            Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(new MapPoint(10, 10), f.Data.Unit(0).Position);
        }
    }

    private static void AirCityRaidScoringAndOrders()
    {
        foreach (bool inherited in new[] { false, true })
        {
            var f = new AirFixture(); f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Role = RuleRole.Attack, Range = 1 };
            f.Data.AddCity(1, 2, new(12, 10), 10); f.Data.AddCity(2, 2, new(10, 12), 10);
            f.Enemy(); f.Enemy(2, point: new(10, 12));
            var c = f.Context(); c.EnemyZone = false; c.HasOrders = inherited;
            Equal(inherited ? SpecialUnitRoute.PlanMove : SpecialUnitRoute.Finish, f.Air.Choose(c).Route);
            Equal(new MapPoint(10, 12), f.Data.Unit(0).Destination); Equal(AiTaskIds.CityRaid, f.Data.Unit(0).AiTask);
        }
        var multiple = new AirFixture(); multiple.Data.Rules.Units[2] = multiple.Data.Rules.Units[2] with { Role = RuleRole.Attack, Range = 1 };
        multiple.Data.AddCity(1, 2, new(14, 10), 10); multiple.Data.AddCity(2, 2, new(10, 14), 20);
        multiple.World.Map.TileAt(new(14, 10)).Terrain = Terrain.Ocean;
        multiple.World.Map.TileAt(new(10, 14)).Region = 2;
        multiple.Enemy(point: new(14, 10)); multiple.Enemy(2, point: new(10, 14));
        multiple.Data.AddUnit(3, 1, 0, new(15, 9)); multiple.Data.AddUnit(4, 1, 0, new(16, 10));
        multiple.Choose(); Equal(new MapPoint(14, 10), multiple.Data.Unit(0).Destination);
        foreach (bool zero in new[] { false, true })
        {
            var f = new AirFixture(); f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Role = RuleRole.Attack, Range = 1 };
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Counter = zero ? (byte)0 : (byte)1;
            var c = f.Context(); c.EnemyZone = true;
            Equal(zero ? SpecialUnitRoute.PlanMove : SpecialUnitRoute.Continue, f.Air.Choose(c).Route); Equal(zero, c.HasOrders);
        }
    }

    private static void AirCityRaidGatesAndScenario()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new AirFixture(); f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Role = RuleRole.Attack, Range = 3 };
            f.Data.AddCity(1, 2, new(14, 10), 20);
            if (variant != 0) f.Enemy(point: new(14, 10));
            if (variant == 1) f.World.Civilizations[1].Treaties[2].Peace = true;
            if (variant == 2) f.World.Civilizations[1].Treaties[2].Ceasefire = true;
            if (variant == 3) f.Data.Unit(0).MovesUsed = 9; // Multi-turn reach is ample, but remaining movement is one tile.
            if (variant == 4) f.World.Civilizations[1].Treaties[2].Alliance = true;
            Equal(variant == 4 ? SpecialUnitRoute.Finish : SpecialUnitRoute.Continue, f.Choose().Route);
        }
        var scenario = new AirFixture(true); scenario.Data.Rules.Units[2] = scenario.Data.Rules.Units[2] with { Role = RuleRole.Attack, Range = 1, Movement = 3 };
        scenario.World.Globals.Status.Scenario = true; scenario.World.Scenario.Flags.HistoricalAi = true;
        scenario.World.Globals.Turn = 1; scenario.World.Scenario.ObjectiveCiv = 1; scenario.Data.AddUnit(1, 1, 0, new(45, 53));
        var c = scenario.Context(); c.EnemyZone = true;
        Equal(SpecialUnitRoute.PlanMove, scenario.Air.Choose(c).Route); Equal(true, c.HasOrders);
        Equal(new MapPoint(45, 53), scenario.Data.Unit(0).Destination); // No foreign owner, city or normal reach requirement.
    }

    private static void AirCapturedTypeAndFailures()
    {
        var f = new AirFixture(); f.Expended(); f.Enemy(); var c = f.Context();
        f.Data.Rules.Units[20] = f.Data.Rules.Units[2] with { Attack = 99, Role = RuleRole.Trade }; f.Data.Unit(0).Type = new(20);
        Equal(SpecialUnitRoute.Direction, f.Air.Choose(c).Route); Equal(new MapPoint(13, 9), f.Data.Unit(0).Position);
        var skip = new AirFixture(); skip.Data.Rules.Units[2] = skip.Data.Rules.Units[2] with { Attack = 99, Role = RuleRole.Attack };
        var captured = skip.Context(); skip.Data.Rules.Units[20] = skip.Data.Rules.Units[2] with { Attack = 1, Range = 1, Abilities = new() { DestroyedAfterAttack = true } };
        skip.Data.Unit(0).Type = new(20); skip.Enemy(); Equal(SpecialUnitRoute.Continue, skip.Air.Choose(captured).Route);
        var zero = new AirFixture(); zero.Data.Rules.Cosmic = zero.Data.Rules.Cosmic with { RoadMovementMultiplier = 0 };
        Throws<DivideByZeroException>(() => zero.Choose()); Equal((byte)7, zero.Data.Unit(0).AiTask);
        var remote = new AirFixture(); var local = remote.Context(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => remote.Air.Choose(local)); Equal((byte)7, remote.Data.Unit(0).AiTask);
    }

    private static void AirRecoveryActualOrderExecution()
    {
        var f = new OrderFixture(); var d = f.Data; var w = d.World;
        d.Rules.Units[20] = d.Rules.Units[2] with { Domain = RuleDomain.Air, Role = RuleRole.AirSuperiority, Range = 1, Movement = 12 };
        d.Unit(0).Type = new(20); d.Unit(0).Orders = UnitOrder.None; d.Unit(0).MovesUsed = 9;
        d.AddCity(2, 1, new(20, 12)); var c = new UnitTacticsContext(w, d.Rules, new(0));
        Equal(SpecialUnitRoute.Finish, new AirTactics(w, d.Rules, d.Stacks, new(w, d.Rules)).Choose(c).Route);
        Equal(AiTaskIds.AirReturn, d.Unit(0).AiTask); Equal(new MapPoint(20, 12), d.Unit(0).Destination);
        TacticOrders(d).Finish(c.Unit, c.Domain);
        using var move = f.Begin(); f.Complete(move);
        Equal(new MapPoint(20, 12), d.Unit(0).Position); Equal((byte)12, d.Unit(0).MovesUsed); Equal(UnitOrder.None, d.Unit(0).Orders);
    }
}
