using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static RevelationFixture NavalTargetFixture()
    {
        var f = new RevelationFixture();
        foreach (ref var tile in f.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; }
        f.AddUnit(0, 1, 1, new(10, 10));
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2;
        return f;
    }

    private static void NavalLandingCandidatesAndTies()
    {
        var f = NavalTargetFixture(); var targets = new NavalTargets(f.World);
        f.Unit(0).Orders = UnitOrder.Sleeping; f.Unit(0).MovesUsed = 2;
        Equal(8, targets.BestLandingDirection(new(0))); // Occupied center is eligible, even at zero score.
        foreach (ref var tile in f.World.Map.Tiles) { tile.Terrain = Terrain.Grassland; }
        Equal(null, targets.BestLandingDirection(new(0)));
        f.World.Map.TileAt(new(11, 9)).Terrain = Terrain.Ocean;
        Equal(0, targets.BestLandingDirection(new(0)));
        f.AddUnit(1, 1, 1, new(11, 9));
        Equal(null, targets.BestLandingDirection(new(0))); // Noncenter occupancy rejects even an own ship.
        Equal(UnitOrder.Sleeping, f.Unit(0).Orders); Equal((byte)2, f.Unit(0).MovesUsed);
        Equal(new MapPoint(10, 10), f.Unit(0).Position);
    }

    private static void NavalLandingClaimantAndTreaties()
    {
        foreach (int variant in Enumerable.Range(0, 8))
        {
            var f = NavalTargetFixture();
            f.World.Map.TileAt(new(11, 9)).Terrain = Terrain.Grassland;
            f.World.Map.TileAt(new(7, 9)).Terrain = Terrain.Grassland;
            int slot = 1;
            foreach (var p in new[] { new MapPoint(12, 10), new(11, 11), new(10, 12), new(9, 11), new(9, 9), new(10, 8) })
            { f.AddUnit(slot++, 1, 1, p); }
            ref var land = ref f.World.Map.TileAt(new(11, 9));
            land.Claimant = (byte)(variant is 1 or 2 or 3 ? 0 : 2);
            land.Fertility = (byte)(variant == 1 ? 1 : variant == 2 ? 0 : 15);
            f.World.Civilizations[1].Treaties[2].Peace = variant is not (4 or 5);
            f.World.Civilizations[1].Treaties[2].Alliance = variant == 4;
            f.World.Civilizations[1].Treaties[2].Ceasefire = variant == 5;
            if (variant == 6) f.World.Globals.HumanPlayers = CivilizationSet.Only(3);
            if (variant == 7) { land.Claimant = 0; land.Fertility = 8; f.AddUnit(slot, 2, 0, new(11, 9)); }
            Equal(variant is 0 or 5 or 7 ? 5 : 8, new NavalTargets(f.World).BestLandingDirection(new(0)));
        }
    }

    private static void NavalLandingHostileCity()
    {
        foreach (int variant in Enumerable.Range(0, 5))
        {
            var f = NavalTargetFixture(); f.AddCity(0, 2, new(11, 9));
            f.World.Map.TileAt(new(11, 9)).Terrain = Terrain.Grassland;
            f.World.Map.TileAt(new(11, 9)).Claimant = 2;
            f.World.Map.TileAt(new(9, 7)).Terrain = Terrain.Grassland;
            f.World.Civilizations[1].Treaties[2].War = variant is 0 or 4;
            f.World.Civilizations[1].Treaties[2].Contact = variant is 1 or 2;
            f.World.Civilizations[1].Treaties[2].Alliance = variant == 2;
            f.World.Civilizations[1].Attitude[2] = 50;
            if (variant == 4) f.AddUnit(1, 2, 0, new(11, 9));
            Equal(variant is 0 or 1 ? 8 : 7, new NavalTargets(f.World).BestLandingDirection(new(0)));
        }
    }

    private static void NavalNearShoreFirstRegionAndOccupancy()
    {
        var f = new RevelationFixture();
        foreach (ref var tile in f.World.Map.Tiles) { tile.Region = 3; }
        f.AddUnit(0, 1, 1, new(2, 10)); f.World.Map.TileAt(new(2, 10)).Terrain = Terrain.Ocean;
        f.World.Map.TileAt(new(2, 10)).Region = 0;
        f.World.Map.TileAt(new(10, 10)).Region = 1;
        foreach (var p in new[] { new MapPoint(11, 9), new(12, 10), new(11, 11) })
        { f.World.Map.TileAt(p).Terrain = Terrain.Ocean; f.World.Map.TileAt(p).Region = 0; }
        f.World.Map.TileAt(new(11, 9)).Region = 2;
        f.AddUnit(1, 2, 1, new(12, 10)); f.AddUnit(2, 2, 0, new(10, 10));
        f.World.Civilizations[1].Treaties[2].Peace = true;
        f.Unit(0).Counter = 9; f.Unit(0).MovesUsed = 2;
        var targets = new NavalTargets(f.World); targets.NearShoreGoto(new(0), 300, new(10, 10));
        Equal(UnitOrder.GoTo, f.Unit(0).Orders); Equal(new MapPoint(12, 10), f.Unit(0).Destination);
        Equal((byte)44, f.Unit(0).AiTask); Equal((byte)9, f.Unit(0).Counter); Equal((byte)2, f.Unit(0).MovesUsed);
        Equal(new MapPoint(2, 10), f.Unit(0).Position);
        f.AddCity(0, 2, new(10, 10)); // The only matching land is now a city: retain the original target.
        targets.NearShoreGoto(new(0), 80, new(10, 10));
        Equal(new MapPoint(10, 10), f.Unit(0).Destination); Equal((byte)80, f.Unit(0).AiTask);
    }

    private static void NavalNearShoreTopologyAndFallback()
    {
        foreach (bool flat in new[] { false, true })
        {
            var f = new RevelationFixture(); f.World.Globals.Rules.FlatWorld = flat;
            foreach (ref var tile in f.World.Map.Tiles) { tile.Region = 3; }
            f.AddUnit(0, 1, 1, new(36, 10));
            foreach (var p in new[] { new MapPoint(36, 10), new(39, 11) })
            { f.World.Map.TileAt(p).Terrain = Terrain.Ocean; f.World.Map.TileAt(p).Region = 0; }
            f.World.Map.TileAt(new(0, 10)).Region = 1;
            new NavalTargets(f.World).NearShoreGoto(new(0), 10, new(0, 10));
            Equal(flat ? new MapPoint(0, 10) : new(39, 11), f.Unit(0).Destination);
        }
    }

    private static void NavalNearShoreActualMovement()
    {
        var f = new OrderFixture(); var d = f.Data;
        d.World.Globals.HumanPlayers = CivilizationSet.Only(2); d.World.Multiplayer.LocalHuman = 2;
        foreach (ref var tile in d.World.Map.Tiles) { tile.Region = 1; }
        d.Unit(0).Type = new(1);
        foreach (var p in new[] { new MapPoint(18, 12), new(20, 12) })
        { d.World.Map.TileAt(p).Terrain = Terrain.Ocean; d.World.Map.TileAt(p).Region = 0; }
        new NavalTargets(d.World).NearShoreGoto(new(0), 80, new(22, 12));
        Equal(new MapPoint(20, 12), d.Unit(0).Destination); Equal((byte)0, d.Unit(0).MovesUsed);
        using var move = f.Begin(); f.Complete(move);
        Equal(1, move.MovementResult); Equal(new MapPoint(20, 12), d.Unit(0).Position);
        Equal(true, d.Unit(0).Flags.GoalReached); Equal(UnitOrder.None, d.Unit(0).Orders);
        Equal((byte)6, d.Unit(0).MovesUsed);
    }
}
