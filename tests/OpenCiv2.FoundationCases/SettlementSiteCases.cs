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
    private sealed class SettlementFixture
    {
        public FoundingFixture Founding { get; } = new();
        public RevelationFixture Data => Founding.Data;
        public GameState World => Data.World;
        public SharedGoals Goals { get; }
        public SettlementTactics Policy { get; }
        public StartingPositions Starts = new();
        public SettlementFixture()
        {
            World.Globals.Turn = 2; World.Globals.HumanPlayers = CivilizationSet.Only(2); World.Multiplayer.LocalHuman = 2;
            foreach (ref var tile in World.Map.Tiles) tile.Fertility = 0;
            foreach (ref var civ in World.Civilizations.AsSpan()) SharedGoals.ResetStanding(ref civ);
            Data.Unit(0).Orders = UnitOrder.None; Data.Unit(0).AiTask = 7;
            Goals = new(World, Data.Rules, new MovementGoalQueries(World, Data.Rules));
            Policy = new(World, Data.Rules, Goals, Founding.Founding);
        }
        public MapPoint Position => Data.Unit(0).Position;
        public MapPoint At(int index) => MapNeighborhoods.Offset(World.Map.Geometry, Position, MapNeighborhoods.Radius[index], World.Globals.Rules.FlatWorld);
        public UnitTacticsContext Context() => new(World, Data.Rules, new(0));
        public void Home(MapPoint point, int owner = 1)
        { Data.AddCity(0, owner, point); World.Civilizations[owner].CityCount = 1; World.Civilizations[owner].ContinentCities[1] = 1; }
        public SpecialUnitChoice Choose(UnitTacticsContext? context = null)
        {
            using var op = Policy.Site(context ?? Context(), Starts, "site");
            Equal(null, op.Resume()); return op.Result ?? throw new InvalidOperationException("Unexpected pending site child");
        }
        public void Construction() => World.Civilizations[1].Advances = World.Civilizations[1].Advances.With(AdvanceIds.Construction);
        public CivilizationGoal? Standing(int role) => ((ReadOnlySpan<CivilizationGoal>)World.Civilizations[1].StandingGoals)
            .ToArray().Where(g => g.Role == role).Select(g => (CivilizationGoal?)g).FirstOrDefault();
    }

    // A03 §4: actual founding, no shared finish, including a refused C01 child.
    private static void SettlementFirstCityAndCapacity()
    {
        foreach (bool full in new[] { false, true })
        {
            var f = new SettlementFixture(); f.World.Globals.Turn = 1; f.Starts.Positions[0] = f.Position;
            f.World.Map.TileAt(f.Position).Fertility = 9; f.World.Map.TileAt(f.Position).Features |= MapFeatures.Road;
            f.Goals.AddStanding(1, f.Position, 5, 2); f.Data.Unit(0).MovesUsed = 1;
            var c = f.Context(); c.ForceAttack = true; c.CitySelector = new(12);
            if (full)
            {
                f.World.Entities.Cities.SetEnd(EntityLimits.CreatableCities);
                for (int i = 0; i < EntityLimits.CreatableCities; ++i) f.World.Entities.Cities.Storage[i].Token = new((uint)i + 1);
            }
            using var op = f.Policy.Site(c, f.Starts, "site"); Equal(null, op.Resume());
            Equal(new SpecialUnitChoice(SpecialUnitRoute.ReturnOne), op.Result!.Value);
            Equal(full ? FoundingOutcome.Unavailable : FoundingOutcome.Founded, op.FoundingResult!.Value);
            Equal(null, f.Standing(5)); Equal(new CitySlot(12), c.CitySelector!.Value);
            Equal(full, f.Data.Unit(0).Token.Occupied); Equal(true, c.SettlerKind); Equal(true, c.SettlerType);
            if (full) { Equal((byte)1, f.Data.Unit(0).MovesUsed); Equal((byte)7, f.Data.Unit(0).AiTask); }
            else { Equal("First", f.World.Entities.Cities.At(new(0)).Name); Equal(false, f.World.Map.TileAt(f.Position).Features.HasFlag(MapFeatures.Road)); }
            Equal(null, op.Resume()); f.Founding.Life.Random.RequireChoicesConsumed();
        }
    }

    private static void SettlementSiteEligibilityAndCapturedRegion()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = new SettlementFixture(); f.Home(new(14, 10)); f.World.Map.TileAt(f.Position).Fertility = 15;
            if (variant == 1) f.World.Map.TileAt(new(14, 10)).Region = 2;
            if (variant == 2) f.World.Map.TileAt(f.Position).Terrain = Terrain.Ocean;
            var c = f.Context(); if (variant == 3) c.ForceAttack = true; if (variant == 4) c.EnemyZone = true;
            var result = f.Choose(c);
            Equal(variant == 1 ? SpecialUnitRoute.ReturnOne : SpecialUnitRoute.Continue, result.Route);
            if (variant != 1) Equal((byte)(variant == 0 ? 1 : 0), f.Data.Unit(0).AuxiliaryTimer);
            Equal(new CitySlot(0), c.CitySelector!.Value);
        }
        var roles = new SettlementFixture(); var context = roles.Context(); roles.Data.Unit(0).Type = new(4);
        roles.World.Map.TileAt(roles.Position).Fertility = 0;
        roles.Choose(context); Equal(true, context.SettlerKind); Equal(false, context.SettlerType);
        var worker = new SettlementFixture(); worker.Data.Unit(0).Flags.Colonist = true; worker.Home(worker.Position);
        var wc = worker.Context(); worker.Choose(wc); Equal(false, wc.SettlerKind); Equal(true, wc.SettlerType);
        foreach (bool nearestSameRegion in new[] { false, true })
        {
            var f = new SettlementFixture(); f.Home(new(20, 10)); f.Data.AddCity(1, 2, new(14, 10));
            f.World.Map.TileAt(new(20, 10)).Region = (byte)(nearestSameRegion ? 2 : 1);
            f.World.Map.TileAt(new(14, 10)).Region = (byte)(nearestSameRegion ? 1 : 2);
            f.World.Map.TileAt(f.Position).Fertility = 15;
            var c = f.Context(); Equal(new CitySlot(1), c.NearestCity!.Value); Equal(new CitySlot(0), c.HomeCity!.Value);
            Equal(nearestSameRegion ? SpecialUnitRoute.Continue : SpecialUnitRoute.ReturnOne, f.Choose(c).Route);
        }
    }

    private static void SettlementSiteTiesAndRadius()
    {
        var f = new SettlementFixture(); f.World.Map.TileAt(f.Position).Fertility = 8;
        f.World.Map.TileAt(f.At(0)).Fertility = f.World.Map.TileAt(f.At(1)).Fertility = 12;
        var c = f.Context(); Equal(new SpecialUnitChoice(SpecialUnitRoute.Direction, 0), f.Choose(c));
        Equal(AiTaskIds.FoundSite, f.Data.Unit(0).AiTask); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        TacticOrders(f.Data).Direction(c, 0); Equal(UnitOrder.AiGoTo, f.Data.Unit(0).Orders); Equal(f.At(0), f.Data.Unit(0).Destination);
        var outer = new SettlementFixture(); outer.World.Map.TileAt(outer.Position).Fertility = 8;
        outer.World.Map.TileAt(outer.At(8)).Fertility = outer.World.Map.TileAt(outer.At(19)).Fertility = 12;
        Equal(SpecialUnitRoute.Continue, outer.Choose().Route); Equal(UnitOrder.GoTo, outer.Data.Unit(0).Orders);
        Equal(outer.At(8), outer.Data.Unit(0).Destination); Equal(AiTaskIds.FoundSite, outer.Data.Unit(0).AiTask);
        var excluded = new SettlementFixture(); excluded.World.Map.TileAt(excluded.Position).Fertility = 8;
        excluded.World.Map.TileAt(excluded.At(0)).Fertility = 15; excluded.World.Map.TileAt(excluded.At(0)).Region = 2;
        excluded.World.Map.TileAt(excluded.At(1)).Fertility = 15; excluded.World.Map.TileAt(excluded.At(1)).Terrain = Terrain.Ocean;
        excluded.World.Map.TileAt(excluded.At(2)).Fertility = 9;
        Equal(new SpecialUnitChoice(SpecialUnitRoute.Direction, 2), excluded.Choose());
    }

    private static void SettlementSiteTimerAndRegionalCount()
    {
        foreach (var (timer, regional, expected, found) in new (byte, byte, byte, bool)[]
        { (3,1,4,false), (4,1,5,true), (127,1,128,false), (255,1,0,false), (4,0,4,true) })
        {
            var f = new SettlementFixture(); f.Home(new(14, 10)); f.World.Map.TileAt(f.Position).Fertility = 9;
            f.World.Civilizations[1].ContinentCities[1] = regional; f.Data.Unit(0).AuxiliaryTimer = timer;
            Equal(found ? SpecialUnitRoute.ReturnOne : SpecialUnitRoute.Continue, f.Choose().Route);
            if (!found) Equal(expected, f.Data.Unit(0).AuxiliaryTimer);
            else Equal((short)2, f.World.Civilizations[1].CityCount);
        }
        var selected = new SettlementFixture(); selected.Home(new(14, 10)); selected.World.Map.TileAt(selected.Position).Fertility = 8;
        selected.World.Map.TileAt(selected.At(0)).Fertility = 9; selected.Data.Unit(0).AuxiliaryTimer = 4;
        Equal(SpecialUnitRoute.Continue, selected.Choose().Route); Equal((byte)4, selected.Data.Unit(0).AuxiliaryTimer);
    }

    private static void SettlementSiteHutsAndStandingGoals()
    {
        var f = new SettlementFixture(); f.Data.Unit(0).Type = new(4); f.World.Map.TileAt(f.Position).Fertility = 8;
        f.World.Map.TileAt(f.At(0)).Fertility = f.World.Map.TileAt(f.At(1)).Fertility = 12;
        Equal(SpecialUnitRoute.Continue, f.Choose().Route); Equal(new CivilizationGoal(f.At(0), 5, 2), f.Standing(5)!.Value);
        Equal((byte)7, f.Data.Unit(0).AiTask); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        var signed = new SettlementFixture(); signed.Data.Unit(0).Type = new(4);
        signed.World.Map.TileAt(signed.Position).Fertility = 8; signed.World.Map.TileAt(signed.At(0)).Fertility = 12;
        var sc = signed.Context(); sc.Role = 255; Equal(SpecialUnitRoute.Continue, signed.Choose(sc).Route);
        Equal(new CivilizationGoal(signed.At(0), 5, 2), signed.Standing(5)!.Value);
        // Find a real W01 hut candidate; no replacement hut predicate is injected.
        var hut = new SettlementFixture(); hut.Data.Unit(0).Type = new(4);
        MapPoint origin = default; int direction = -1;
        for (int y = 3; y < 20 && direction < 0; ++y)
            for (int x = 4 + (y & 1); x < 34 && direction < 0; x += 2)
                for (int d = 0; d < 8 && direction < 0; ++d)
                {
                    var q = MapNeighborhoods.Offset(hut.World.Map.Geometry, new(x, y), MapNeighborhoods.Directions[d], true);
                    if (hut.World.Map.HutCandidate(q)) { origin = new(x, y); direction = d; }
                }
        Equal(true, direction >= 0); hut.Data.Stacks.Relocate(0, origin, false); hut.World.Map.TileAt(origin).Fertility = 8;
        hut.World.Map.TileAt(hut.At(direction)).Owner = null;
        hut.World.Map.TileAt(hut.At((direction + 7) & 7)).Fertility = 15;
        var hc = hut.Context(); hc.EnemyZone = true;
        Equal(new SpecialUnitChoice(SpecialUnitRoute.Direction, direction), hut.Choose(hc)); Equal(AiTaskIds.Hut, hut.Data.Unit(0).AiTask);
    }

    private static void SettlementAdvancedHumanTail()
    {
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new SettlementFixture(); f.Home(new(18, 10), 2); f.World.Globals.Difficulty = Difficulty.Prince;
            f.World.Civilizations[2].NumAdvances = 2; f.World.Map.TileAt(f.Position).Fertility = 9;
            f.World.Map.TileAt(f.At(8)).Fertility = 12;
            var c = f.Context(); Equal(6, c.NearestDistance);
            if (variant == 1) f.World.Globals.Difficulty = Difficulty.Chieftain;
            if (variant == 2) f.World.Civilizations[1].NumAdvances = 2;
            if (variant == 3) { c.ForceAttack = true; f.World.Map.TileAt(f.Position).Claimant = 1; }
            Equal(variant is 0 or 3 ? SpecialUnitRoute.ReturnOne : SpecialUnitRoute.Continue, f.Choose(c).Route);
            Equal(variant is 1 or 2, f.Data.Unit(0).Token.Occupied);
            if (variant is 1 or 2) { Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(f.At(8), f.Data.Unit(0).Destination); }
        }
    }

    private static void SettlementSiteFailureAndDisposal()
    {
        var f = new SettlementFixture(); var context = f.Context(); f.World.Globals.Multiplayer = GameMode.Tcp;
        using var op = f.Policy.Site(context, f.Starts, "site"); Throws<InvalidOperationException>(() => op.Resume());
        f.World.Globals.Multiplayer = GameMode.SinglePlayer; Throws<InvalidOperationException>(() => op.Resume());
        var disposed = f.Policy.Site(f.Context(), f.Starts, "site"); disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Resume());
        // The start-position read is reached after clearing a standing goal; a failed read must not retry that prefix.
        f.World.Globals.Turn = 1; f.World.Civilizations[1].Tribe = -1; f.World.Map.TileAt(f.Position).Fertility = 9;
        f.Goals.AddStanding(1, f.Position, 5, 2);
        using var invalid = f.Policy.Site(f.Context(), f.Starts, "site"); Throws<IndexOutOfRangeException>(() => invalid.Resume());
        Equal(null, f.Standing(5)); f.World.Civilizations[1].Tribe = 0; Throws<InvalidOperationException>(() => invalid.Resume());
    }

    private static void SettlementFortressRoadAndLiveHome()
    {
        foreach (bool settler in new[] { false, true })
        {
            var f = new SettlementFixture(); f.Home(new(16, 10)); f.Construction(); f.World.Map.TileAt(f.Position).Features |= MapFeatures.Road;
            var c = f.Context(); c.SettlerType = settler; c.Role = settler ? 21 : 1;
            Equal(4, c.HomeDistance); f.Policy.ClassifyFortress(c); Equal(1, c.FortressNeed);
            Equal(new CivilizationGoal(f.Position, (sbyte)(settler ? 1 : 21), 2), f.Standing(settler ? 1 : 21)!.Value);
            Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal((byte)7, f.Data.Unit(0).AiTask);
            f.World.Entities.Cities.At(new(0)).Position = f.Position; f.Policy.ClassifyFortress(c); Equal(0, c.FortressNeed);
        }
    }

    private static void SettlementFortressEarlyExits()
    {
        foreach (int variant in Enumerable.Range(0, 7))
        {
            var f = new SettlementFixture(); f.Home(new(16, 10)); f.Construction(); f.World.Map.TileAt(f.Position).Features |= MapFeatures.Road;
            if (variant == 0) f.World.Civilizations[1].Advances = default;
            if (variant == 1) f.World.Map.TileAt(f.Position).Features |= MapFeatures.CitySite | MapFeatures.Fortification;
            if (variant == 2) f.World.Map.TileAt(f.At(0)).Features |= MapFeatures.Fortification;
            if (variant == 3) f.Data.AddCity(1, 2, f.At(0));
            if (variant == 4) f.World.Civilizations[1].Goals[0] = new(f.At(0), 1, -2);
            if (variant == 5) for (int d = 0; d < 4; ++d) f.World.Map.TileAt(f.At(d)).Terrain = Terrain.Ocean;
            if (variant == 6) f.World.Map.TileAt(f.Position).Terrain = Terrain.Ocean;
            var c = f.Context(); c.SettlerType = true; c.FortressNeed = 9;
            f.Policy.ClassifyFortress(c); Equal(0, c.FortressNeed); Equal(null, f.Standing(1));
        }
    }

    private static void SettlementFortressLinesAndParity()
    {
        foreach (int variant in Enumerable.Range(0, 3))
        {
            var f = new SettlementFixture(); f.Home(new(16, 10)); f.Construction();
            if (variant == 0) { f.World.Map.TileAt(f.At(3)).Terrain = Terrain.Ocean; f.World.Map.TileAt(f.At(7)).Terrain = Terrain.Ocean; }
            if (variant == 1) f.World.Map.TileAt(new(13, 9)).Terrain = Terrain.Ocean; // Sample (12,9) shares this tile.
            if (variant == 2) f.World.Map.TileAt(new(8, 8)).Features = MapFeatures.Fortification;
            var c = f.Context(); c.Role = 1; f.Policy.ClassifyFortress(c); Equal(2, c.FortressNeed);
            Equal(new CivilizationGoal(f.Position, 21, 3), f.Standing(21)!.Value);
        }
        var blocked = new SettlementFixture(); blocked.Home(new(16, 10)); blocked.Construction();
        foreach (var p in new[] { new MapPoint(8, 8), new(12, 8), new(8, 12), new(12, 12) }) blocked.World.Map.TileAt(p).Terrain = Terrain.Ocean;
        var bc = blocked.Context(); bc.Role = 1; blocked.Policy.ClassifyFortress(bc); Equal(0, bc.FortressNeed);
        var wrapped = new SettlementFixture(); wrapped.World.Globals.Rules.FlatWorld = false;
        wrapped.Data.Stacks.Relocate(0, new(0, 10), false); wrapped.Home(new(6, 10)); wrapped.Construction();
        wrapped.World.Map.TileAt(new(38, 8)).Terrain = Terrain.Ocean;
        var wc = wrapped.Context(); wc.SettlerType = true; wrapped.Policy.ClassifyFortress(wc); Equal(2, wc.FortressNeed);
        Equal(new CivilizationGoal(wrapped.Position, 1, 2), wrapped.Standing(1)!.Value);
    }

    private static void SettlementParatrooperSelectorJoin()
    {
        var f = new SettlementFixture(); var para = new ParadropFixture(data: f.Data);
        foreach (ref var tile in f.World.Map.Tiles) tile.Fertility = 0;
        f.Data.AddCity(10, 1, new(8, 8)); var c = f.Context(); Equal(new CitySlot(10), c.HomeCity!.Value);
        Equal(null, c.CitySelector); Equal(SpecialUnitRoute.Continue, f.Choose(c).Route);
        Equal(c.NearestCity, c.CitySelector);
        f.Data.AddCity(11, 1, c.Position); // The consumer retains A03's selector despite a newer nearest city.
        using var returning = ParaPolicy(para).Begin(c, "para"); CompletePara(para, returning);
        Equal(SpecialUnitRoute.Finish, returning.Result!.Value.Route); Equal(new MapPoint(8, 8), f.Data.Unit(0).Destination);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(AiTaskIds.CityTarget, f.Data.Unit(0).AiTask);
        Equal(0, TacticOrders(f.Data).Finish(c.Unit, c.Domain));
    }
}
