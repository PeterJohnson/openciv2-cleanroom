using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core.Ai;

public sealed partial class TerrainWorkerTactics
{
    private UnitOrder? ThresholdChoice(Terrain terrain, MapFeatures features)
    {
        if ((features & (MapFeatures.CitySite | MapFeatures.Irrigation | MapFeatures.Mine)) != 0)
        {
            return null;
        }

        var work = Work(terrain);
        int mine = unchecked((sbyte)work.Mining.AiGovernmentThreshold), irrigate = unchecked((sbyte)work.Irrigation.AiGovernmentThreshold);
        return mine != 0 && mine < irrigate ? UnitOrder.Mine : irrigate != 0 ? UnitOrder.Irrigate : null;
    }
    private SpecialUnitChoice Colonist(UnitTacticsContext c)
    {
        var reference = Actor(c).Home ?? c.HomeCity;
        if (reference is { } retained && world.Map.TileAt(world.Entities.Cities.At(retained).Position).Region != c.Region)
        {
            reference = c.HomeCity;
        }

        if (reference is { } city && Distance(c.Position, world.Entities.Cities.At(city).Position) > 4)
        { return Goto(c, world.Entities.Cities.At(city).Position, AiTaskIds.ReturnHome); }
        if (!world.Map.TileAt(c.Position).Features.HasFlag(MapFeatures.Road) && RoadAllowed(c, c.Position))
        { Actor(c).AiTask = AiTaskIds.RoadWork; return Order(c, UnitOrder.Road); }
        if (c.HomeCity is { } home && c.EnemyZone)
        {
            return Goto(c, world.Entities.Cities.At(home).Position, AiTaskIds.ReturnHome);
        }

        if (c.HomeDistance < 3)
        {
            var features = world.Map.TileAt(c.Position).Features;
            if (ThresholdChoice(c.Terrain, features) is { } order)
            {
                return Order(c, order);
            }

            if (features.HasFlag(MapFeatures.Pollution))
            {
                return Order(c, UnitOrder.Clean);
            }

            if (!features.HasFlag(MapFeatures.Railroad) && Knows(c, AdvanceIds.Railroad))
            { Actor(c).AiTask = AiTaskIds.RoadWork; return Order(c, UnitOrder.Road); }
            c.CitySelector = c.HomeCity;
            for (int i = 0; i < 20; ++i)
            {
                var p = Offset(Home(c).Position, MapNeighborhoods.Radius[i]);
                if (!world.Map.Geometry.InBounds(p))
                {
                    continue;
                }

                var tile = world.Map.TileAt(p);
                if (ThresholdChoice(tile.Terrain, tile.Features) is { } job)
                { return Goto(c, p, job == UnitOrder.Mine ? AiTaskIds.MineDestination : AiTaskIds.IrrigationDestination); }
                if (RoadAllowed(c, p) && (!tile.Features.HasFlag(MapFeatures.Road)
                    || !tile.Features.HasFlag(MapFeatures.Railroad) && Knows(c, AdvanceIds.Railroad)))
                {
                    return Goto(c, p, AiTaskIds.RoadDestination);
                }
            }
        }
        else if (!world.Map.TileAt(c.Position).Features.HasFlag(MapFeatures.Railroad) && Knows(c, AdvanceIds.Railroad))
        { Actor(c).AiTask = AiTaskIds.RoadWork; return Order(c, UnitOrder.Road); }
        return new(SpecialUnitRoute.Continue);
    }
}
