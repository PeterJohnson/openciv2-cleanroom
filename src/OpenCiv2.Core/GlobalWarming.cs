using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum WarmingNotice { FearWarming, GlobalWarming }

/// <summary>W05 local warming pressure and terrain transformation. F04 invokes Update
/// before advancing a nonzero turn. Notices/redraw are cosmetic, not gameplay callbacks.</summary>
public sealed class GlobalWarming(GameState world, Action<WarmingNotice> notice, Action<MapPoint> redraw)
{
    public IReadOnlyList<Exception> Update()
    {
        MapKnowledge.RequireLocal(world);
        List<Exception> failures = [];
        int humans = world.Globals.HumanPlayers.Count;
        int pollution = world.Globals.PollutedTiles - world.Globals.WarmingPollution + world.Globals.WarmingPollution / 2;
        int solar = 0;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            if (world.Entities.Cities.Storage[i].Token.Occupied && BuildingQueries.HasBuilding(world, new((ushort)i), BuildingIds.SolarPlant))
            { ++solar; }
        }
        if (humans > 1)
        { pollution = unchecked(pollution + humans - 1) / humans; }
        int target = unchecked(2 * pollution - 4 * world.Globals.GlobalWarmings - solar);
        int pressure = world.Globals.WarmingCounter;
        pressure = unchecked((sbyte)(pressure + Math.Sign(target - pressure)));
        world.Globals.WarmingCounter = (sbyte)Math.Clamp(pressure, 0, 99);
        if (world.Globals.WarmingCounter == 12 && pollution > 6 && !world.Multiplayer.Saved.AutoplayActive)
        { Present(() => notice(WarmingNotice.FearWarming), failures); }
        int severity = world.Globals.GlobalWarmings;
        if (world.Globals.WarmingCounter > 16)
        {
            world.Globals.GlobalWarmings = unchecked((sbyte)(severity + 1));
            Transform(severity, failures);
            world.Globals.WarmingCounter = 0;
        }
        world.Globals.WarmingPollution = 0;
        return failures.AsReadOnly();
    }

    public IReadOnlyList<Exception> Transform(int severity)
    {
        MapKnowledge.RequireLocal(world);
        List<Exception> failures = [];
        Transform(severity, failures);
        return failures.AsReadOnly();
    }

    private void Transform(int severity, List<Exception> failures)
    {
        if (!world.Multiplayer.Saved.AutoplayActive)
        { Present(() => notice(WarmingNotice.GlobalWarming), failures); }
        for (int i = 0; i < world.Map.Tiles.Length; ++i)
        {
            var point = world.Map.Geometry.Coordinates(i);
            var terrain = world.Map.TileAt(point).Terrain;
            if (terrain > Terrain.Forest)
            { continue; }
            int ocean = 0;
            // W05's twenty visits: D[0..7], eight centers, then D[4], D[3], D[2], D[1].
            for (int visit = 0; visit < 20; ++visit)
            {
                var offset = visit < 8 ? MapNeighborhoods.Directions[visit]
                    : visit < 16 ? new MapPoint(0, 0) : MapNeighborhoods.Directions[20 - visit];
                var neighbor = MapNeighborhoods.Offset(world.Map.Geometry, point, offset, world.Globals.Rules.FlatWorld);
                if (world.Map.Geometry.InBounds(neighbor) && world.Map.TileAt(neighbor).Ocean)
                { ++ocean; }
            }
            bool coastal = ocean >= unchecked(7 - severity);
            if (!coastal && (uint)(unchecked(3 * point.X - 3 * point.Y) & 7) != unchecked((uint)severity))
            { continue; }
            if (world.Map.Owner(point) is null)
            { world.Map.Mutate(new TileChange.Owner(point, 0), synchronize: true); }
            var changed = coastal ? terrain == Terrain.Forest ? Terrain.Jungle : Terrain.Swamp
                : terrain < Terrain.Grassland ? Terrain.Desert : Terrain.Plains;
            world.Map.Mutate(new TileChange.Terrain(point, changed), synchronize: true);
            if (coastal)
            {
                world.Map.Mutate(new TileChange.Features(point, MapFeatures.Irrigation | MapFeatures.Mine, false), synchronize: true);
                for (int observer = 1; observer < EntityLimits.Civilizations; ++observer)
                {
                    if (world.Map.IsRevealedTo(point, observer)
                        && (world.Map.Remembered(observer, point) & (MapFeatures.Irrigation | MapFeatures.Mine)) != 0)
                    {
                        MapKnowledge.Remember(world, observer, point);
                        if (world.Multiplayer.LocalHuman == observer)
                        { Present(() => redraw(point), failures); }
                    }
                }
            }
            else if (world.Map.IsRevealedTo(point, world.Multiplayer.LocalHuman))
            { Present(() => redraw(point), failures); }
        }
    }
    private static void Present(Action action, List<Exception> failures)
    {
        try
        { action(); }
        catch (Exception error) { failures.Add(error); }
    }
}
