using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class NuclearTargetFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public NuclearTargets Targets { get; }
        public NuclearTargetFixture()
        {
            World.Globals.HumanPlayers = CivilizationSet.Only(2); World.Multiplayer.LocalHuman = 2;
            Data.Rules.Units[45] = Data.Rules.Units[2] with { Attack = 99, Movement = 12, Range = 1 };
            Data.AddUnit(0, 1, 45, new(2, 2)); Data.AddUnit(1, 1, 45, new(2, 4));
            Data.AddCity(0, 1, new(10, 10)); City(1, 2, new(18, 10));
            World.Civilizations[1].CityCount = 5; World.Civilizations[1].UnitsActive[45] = 2;
            World.Civilizations[1].MilitaryStrength = 100;
            Data.Unit(0).Visibility = CivilizationSet.Only(2).With(3); Data.Unit(0).MovesUsed = 12;
            Data.Unit(0).Orders = UnitOrder.GoTo; Data.Unit(0).Destination = new(4, 4); Data.Unit(0).AiTask = 7;
            Targets = new(World, Data.Rules, Data.Stacks);
        }
        public void City(int slot, int owner, MapPoint p, int size = 20)
        {
            Data.AddCity(slot, owner, p, size); World.Civilizations[owner].MilitaryStrength = 200;
            World.Civilizations[1].Treaties[owner].NuclearThreat = true;
        }
        public void Stack(int owner, MapPoint p, int count)
        { for (int i = 0; i < count; ++i) Data.AddUnit(World.Entities.Units.End, owner, 0, p); }
        public int Choose() => Targets.Choose(new(0));
    }

    // A06 §7: strict unsigned pressure comparisons, fixed enemy missile slot and directed threat/peace gates.
    private static void NuclearTargetAdmission()
    {
        foreach (var (own, other, enemyMissiles, vendetta, allowed) in new (ushort, ushort, byte, bool, bool)[]
        {
            (100, 150, 1, false, false), (100, 151, 1, false, true), (100, 50, 0, false, false),
            (100, 51, 0, false, true), (100, 0, 1, true, true), (50000, 65000, 1, false, false),
            (40000, 65535, 1, false, true)
        })
        {
            var f = new NuclearTargetFixture(); f.World.Civilizations[1].MilitaryStrength = own;
            f.World.Civilizations[2].MilitaryStrength = other; f.World.Civilizations[2].UnitsActive[45] = enemyMissiles;
            f.World.Civilizations[1].Treaties[2].Vendetta = vendetta;
            Equal(allowed ? 4 : 8, f.Choose()); Equal(allowed ? new MapPoint(19, 9) : new(2, 2), f.Data.Unit(0).Position);
            Equal(false, f.World.Civilizations[1].Treaties[2].War);
        }
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new NuclearTargetFixture();
            if (variant == 0) BuildingQueries.SetBuilding(f.World, new(1), BuildingIds.SdiDefense, true);
            if (variant == 1) f.World.Entities.Cities.At(new(1)).Size = 4;
            if (variant == 2) f.World.Entities.Cities.At(new(1)).Size = -128;
            if (variant == 3) f.World.Civilizations[1].Treaties[2].Peace = true;
            if (variant == 4) { f.World.Civilizations[1].Treaties[2].NuclearThreat = false; f.World.Civilizations[2].Treaties[1].NuclearThreat = true; }
            Equal(8, f.Choose()); Equal(new MapPoint(2, 2), f.Data.Unit(0).Position); Equal(false, f.World.Civilizations[1].Treaties[2].Nuked);
        }
    }

    private static void NuclearTargetThresholdAndActualType()
    {
        foreach (int cities in new[] { 0, 1, 5, 10 })
            foreach (int score in new[] { 5, 6, 9, 10 })
            {
                var f = new NuclearTargetFixture(); f.World.Civilizations[1].CityCount = (short)cities;
                f.World.Entities.Cities.At(new(1)).Size = (sbyte)(2 * score);
                Equal(score >= 10 - Math.Max(5 - cities, 0) ? 4 : 8, f.Choose());
            }
        foreach (bool enough in new[] { false, true })
        {
            var f = new NuclearTargetFixture(); f.Data.Rules.Units[20] = f.Data.Rules.Units[45]; f.Data.Unit(0).Type = new(20);
            f.World.Civilizations[1].UnitsActive[45] = enough ? (byte)0 : (byte)20;
            f.World.Civilizations[1].UnitsActive[20] = enough ? (byte)2 : (byte)1;
            Equal(enough ? 4 : 8, f.Choose());
        }
        var absent = new NuclearTargetFixture(); absent.World.Entities.Cities.SetEnd(0); absent.World.Civilizations[1].CityCount = -6;
        Throws<InvalidOperationException>(() => absent.Choose()); Equal(new MapPoint(2, 2), absent.Data.Unit(0).Position);
        absent.World.Civilizations[1].UnitsActive[45] = 1; Equal(8, absent.Choose()); // Count gate precedes the missing-target read.
    }

    private static void NuclearTargetScoringAndTies()
    {
        foreach (int variant in Enumerable.Range(0, 6))
        {
            var f = new NuclearTargetFixture(); f.Data.Rules.Units[45] = f.Data.Rules.Units[45] with { Movement = 30 };
            f.City(2, 3, new(18, 18), variant == 1 ? 16 : 20);
            if (variant == 1) { f.Stack(1, new(19, 9), 3); f.World.Civilizations[1].CityCount = 3; } // One friendly tile subtracts two, not two per member; both scores eight.
            if (variant == 2) f.Stack(0, new(19, 9), 3); // Barbarian occupants do not penalize the score.
            if (variant == 3) f.Stack(4, new(19, 9), 2);
            if (variant == 4) { f.Stack(4, new(19, 9), 2); f.Stack(2, new(19, 11), 150); }
            if (variant == 5) { f.Stack(2, new(19, 9), 2); f.World.Civilizations[1].Treaties[2].Peace = true; }
            int direction = f.Choose(); Equal(variant == 3 || variant == 5 ? new MapPoint(19, 17) : variant is 1 or 2 or 4 ? new(20, 10) : new(19, 9), f.Data.Unit(0).Position);
            Equal(variant is 1 or 2 or 4 ? 5 : 4, direction);
        }
    }

    private static void NuclearTargetReachAndTopology()
    {
        var f = new NuclearTargetFixture(); f.City(2, 3, new(36, 20), 100);
        Equal(4, f.Choose()); Equal(new MapPoint(19, 9), f.Data.Unit(0).Position); // Unreachable better score preserves the first target.
        foreach (bool flat in new[] { false, true })
        {
            var wrapped = new NuclearTargetFixture(); wrapped.World.Globals.Rules.FlatWorld = flat;
            wrapped.World.Map.TileAt(new(10, 10)).Features &= ~MapFeatures.CitySite;
            wrapped.World.Map.TileAt(new(18, 10)).Features &= ~MapFeatures.CitySite;
            wrapped.Data.AddCity(0, 1, new(2, 10)); wrapped.City(1, 2, new(38, 10));
            Equal(flat ? 8 : 4, wrapped.Choose()); Equal(flat ? new MapPoint(2, 2) : new(39, 9), wrapped.Data.Unit(0).Position);
        }
        var noHome = new NuclearTargetFixture(); noHome.World.Entities.Cities.Storage[0].Token = default;
        noHome.Data.Stacks.Relocate(0, new(16, 10), false); Equal(8, noHome.Choose());
        Equal(new MapPoint(16, 10), noHome.Data.Unit(0).Position); // Actor proximity alone supplies no launch reach.
        var damaged = new NuclearTargetFixture(); damaged.Data.Rules.Units[45] = damaged.Data.Rules.Units[45] with { Domain = RuleDomain.Land };
        damaged.Data.Unit(0).Damage = 5; Equal(8, damaged.Choose());
        var nearby = new NuclearTargetFixture(); nearby.Data.Rules.Units[45] = nearby.Data.Rules.Units[45] with { Domain = RuleDomain.Land };
        nearby.Data.Unit(0).Damage = 5; nearby.Data.AddCity(2, 1, new(16, 10));
        Equal(4, nearby.Choose()); // Any owned city supplies reach, using damage-adjusted allowance rather than remaining moves.
    }

    private static void NuclearTargetStagingAndDiplomacy()
    {
        foreach (int variant in Enumerable.Range(0, 3))
        {
            var f = new NuclearTargetFixture();
            f.Data.AddCity(2, 3, new(19, 9), 4); f.Stack(0, new(20, 10), 1);
            f.World.Map.TileAt(new(19, 11)).Terrain = Terrain.Ocean;
            f.World.Map.TileAt(new(19, 11)).Features |= MapFeatures.CitySite | MapFeatures.Fortification; f.World.Map.TileAt(new(19, 11)).Owner = 3;
            f.World.Civilizations[1].Treaties[2].Vendetta = true; f.World.Civilizations[2].Treaties[1].Vendetta = true;
            f.World.Civilizations[2].LastContactTurn[1] = short.MinValue;
            if (variant == 1) f.World.Civilizations[1].Treaties[2].Nuked = true;
            if (variant == 2) f.World.Globals.HumanPlayers = default;
            f.Stack(1, new(2, 2), 1); f.Data.Unit(3).Visibility = CivilizationSet.Only(3);
            Equal(6, f.Choose()); Equal(new MapPoint(19, 11), f.Data.Unit(0).Position);
            Equal(default(CivilizationSet), f.Data.Unit(0).Visibility); Equal(new MapPoint(2, 2), f.Data.Unit(3).Position);
            Equal(CivilizationSet.Only(3), f.Data.Unit(3).Visibility);
            Equal((byte)12, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders);
            Equal(new MapPoint(4, 4), f.Data.Unit(0).Destination); Equal((byte)7, f.Data.Unit(0).AiTask);
            Equal(variant != 2, f.World.Civilizations[1].Treaties[2].Nuked); Equal(false, f.World.Civilizations[2].Treaties[1].Nuked);
            Equal(variant != 0, f.World.Civilizations[1].Treaties[2].Vendetta); Equal(true, f.World.Civilizations[2].Treaties[1].Vendetta);
            Equal(variant == 0, f.World.Civilizations[1].Treaties[2].Incident); Equal(variant == 0, f.World.Civilizations[2].Treaties[1].Incident);
            Equal(variant == 1 ? (short)32766 : short.MinValue, f.World.Civilizations[2].LastContactTurn[1]);
            Equal(false, f.World.Civilizations[1].Treaties[2].War);
        }
        var blocked = new NuclearTargetFixture();
        foreach (var p in new[] { new MapPoint(19, 9), new(20, 10), new(19, 11), new(18, 12), new(17, 11), new(16, 10), new(17, 9), new(18, 8) }) blocked.Stack(0, p, 1);
        Equal(8, blocked.Choose()); Equal(new MapPoint(2, 2), blocked.Data.Unit(0).Position); Equal(false, blocked.World.Civilizations[1].Treaties[2].Nuked);
    }

    private static void NuclearTargetFailuresAndDirection()
    {
        var zero = new NuclearTargetFixture(); zero.Data.Rules.Cosmic = zero.Data.Rules.Cosmic with { RoadMovementMultiplier = 0 };
        Throws<DivideByZeroException>(() => zero.Choose()); Equal(new MapPoint(2, 2), zero.Data.Unit(0).Position);
        var remote = new NuclearTargetFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => remote.Choose()); Equal(false, remote.World.Civilizations[1].Treaties[2].Nuked);
        var f = new NuclearTargetFixture(); var c = new UnitTacticsContext(f.World, f.Data.Rules, new(0));
        int direction = f.Choose(); Equal(new MapPoint(2, 2), c.Position);
        c.Position = f.Data.Unit(0).Position; // A04's specified reload after actual staging.
        Equal(0, TacticOrders(f.Data).Direction(c, direction)); Equal(new MapPoint(18, 10), f.Data.Unit(0).Destination);
        Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders); Equal((byte)12, f.Data.Unit(0).MovesUsed);
    }

    private static void NuclearTargetActualOrderDelivery()
    {
        foreach (bool intercept in new[] { false, true })
        {
            var f = new OrderFixture(); var d = f.Data; var w = d.World; var combat = f.Move.Foreign.E.F;
            combat.ClearDefender(); combat.Missile(); combat.Ocean();
            d.Rules.Units[20] = d.Rules.Units[20] with { Domain = RuleDomain.Air, Range = 1, Movement = 12 };
            d.Unit(0).Orders = UnitOrder.None; d.Unit(0).MovesUsed = 0;
            d.AddUnit(w.Entities.Units.End, 1, 20, new(2, 2)); d.AddCity(2, 1, new(8, 8));
            w.Civilizations[1].UnitsActive[20] = 2; w.Civilizations[1].CityCount = 5; w.Civilizations[1].MilitaryStrength = 100;
            w.Civilizations[2].MilitaryStrength = 200; w.Civilizations[1].Treaties[2].NuclearThreat = true;
            w.Entities.Cities.At(new(0)).Size = 20; w.Globals.HumanPlayers = CivilizationSet.Only(2); w.Multiplayer.LocalHuman = 2;
            if (intercept) { d.AddCity(3, 2, new(16, 8), 4); BuildingQueries.SetBuilding(w, new(3), BuildingIds.SdiDefense, true); }
            var c = new UnitTacticsContext(w, d.Rules, new(0));
            int direction = new NuclearTargets(w, d.Rules, d.Stacks).Choose(new(0)); Equal(4, direction);
            Equal(new MapPoint(13, 7), d.Unit(0).Position); c.Position = d.Unit(0).Position;
            TacticOrders(d).Direction(c, direction); Equal(new MapPoint(12, 8), d.Unit(0).Destination);
            using var move = f.Begin(); f.Complete(move);
            Equal(false, d.Unit(0).Token.Occupied); Equal(intercept ? (sbyte)20 : (sbyte)10, w.Entities.Cities.At(new(0)).Size);
            Equal(true, w.Civilizations[1].Treaties[2].Nuked); Equal(true, w.Civilizations[2].Treaties[1].Incident);
        }
    }
}
