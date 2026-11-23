using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class SettlementTactics
{
    /// <summary>A03 §5 four-line classifier and actual A01 standing goals.
    /// Rectangular samples intentionally include both coordinate parities.</summary>
    public void ClassifyFortress(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        c.FortressNeed = 0;
        if (!(c.SettlerType || c.Role == (int)RuleRole.Defend)
            || !CivilizationKnowledge.KnowsAdvance(world.Civilizations, c.Owner, AdvanceIds.Construction))
        {
            return;
        }

        int distance = c.HomeCity is { } home
            ? world.Map.Geometry.StepDistance(c.Position, world.Entities.Cities.At(home).Position, world.Globals.Rules.FlatWorld) : c.HomeDistance;
        if (distance == 0 || c.HomeCity != c.NearestCity || c.Fortress || world.Map.AirbaseOwner(c.Position) is not null || c.Terrain == Terrain.Ocean)
        {
            return;
        }

        int oceans = 0, north = 0, south = 0, west = 0, east = 0;
        for (int d = 0; d < 8; ++d)
        {
            var delta = MapNeighborhoods.Directions[d];
            var p = Offset(c.Position, delta);
            if (!world.Map.Geometry.InBounds(p))
            {
                continue;
            }

            if (Fortress(p) || world.Map.CityOwner(p) is not null)
            {
                return;
            }

            if (world.Map.TileAt(p).Ocean)
            {
                if (++oceans > 3)
                {
                    return;
                }

                if (delta.Y < 0)
                {
                    north = 1;
                }

                if (delta.Y > 0)
                {
                    south = 1;
                }

                if (delta.X < 0)
                {
                    west = 1;
                }

                if (delta.X > 0)
                {
                    east = 1;
                }
            }
            if (SharedGoals.PriorityAt(Civ(c), p, (sbyte)RuleRole.Defend) != 0)
            {
                return;
            }
        }
        if (world.Map.TileAt(c.Position).Features.HasFlag(MapFeatures.Road) && distance is > 1 and < 4)
        {
            c.FortressNeed = 1;
        }

        for (int i = -2; i <= 2; ++i)
        {
            Mark(ref north, Offset(c.Position, new(i, -2)));
            Mark(ref south, Offset(c.Position, new(i, 2)));
        }
        for (int i = -2; i <= 2; ++i)
        {
            Mark(ref west, Offset(c.Position, new(-2, i)));
            Mark(ref east, Offset(c.Position, new(2, i)));
        }
        if (north + south > 1 && (west == 0 || east == 0) || west + east > 1 && (north == 0 || south == 0))
        {
            c.FortressNeed = 2;
        }

        if (c.FortressNeed != 0)
        {
            goals.AddStanding(c.Owner, c.Position, c.SettlerType ? (int)RuleRole.Defend : 21,
                c.SettlerType ? 2 : c.FortressNeed + (distance < 4 ? 1 : 0));
        }

        void Mark(ref int marker, MapPoint point)
        {
            if (marker == 0 && world.Map.Geometry.InBounds(point) && (world.Map.TileAt(point).Ocean || Fortress(point)))
            {
                marker = 2;
            }
        }
        bool Fortress(MapPoint point) => (world.Map.TileAt(point).Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification;
    }
}
