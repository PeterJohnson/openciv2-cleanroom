using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U04 civilization recovery, distinct from reset and GoHome.
/// Query scratch is local; the facility selector is retained across units in this invocation.</summary>
public sealed class CivilizationRecovery(GameState world, RuleTables rules, Action<MapPoint> redraw)
{
    public IReadOnlyList<Exception> Run(int owner, int? incomingFacility = null)
    {
        MapKnowledge.RequireLocal(world);
        List<Exception> failures = [];
        int? facility = incomingFacility;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || unit.Owner.Value != owner)
            {
                continue;
            }

            bool moved = unit.Flags.Moved;
            unit.Flags.Moved = unit.Flags.Paradropped = false;
            var rule = rules.Units[unit.Type.Value];
            if (moved)
            {
                if (rule.Domain == RuleDomain.Air && rule.Range == 0 && !world.Map.TileAt(unit.Position).Features.HasFlag(MapFeatures.CitySite))
                {
                    unit.Damage = unchecked((byte)Arithmetic.ClampAB(unchecked(unit.Damage + UnitMovement.MaximumHitPoints(rules, unit) / 10),
                        0, UnitMovement.MaximumHitPoints(rules, unit) - 1));
                }

                continue;
            }
            if (unit.Damage == 0)
            {
                continue;
            }

            var point = unit.Position;
            var tile = world.Map.TileAt(point);
            var nearest = CityQueries.Nearest(world, point, owner, requestedRegion: tile.Region);
            int distance = nearest.Slot is { } city ? world.Map.Geometry.StepDistance(point,
                world.Entities.Cities.At(city).Position, world.Globals.Rules.FlatWorld) : nearest.Distance;
            int factor = (tile.Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification ? 2 : 1;
            bool Has(int building) => building is >= BuildingIds.First and <= BuildingIds.Last
                && BuildingQueries.HasBuilding(world, nearest.Slot ?? throw new InvalidOperationException("Recovery facility requires a selected city"), building);
            if (distance < 4 && rule.Domain == RuleDomain.Land)
            {
                factor += Has(BuildingIds.Barracks) ? 2 : 1;
            }

            bool full = false;
            if (distance == 0)
            {
                facility = rule.Domain switch
                {
                    RuleDomain.Land => BuildingIds.Barracks,
                    RuleDomain.Air => BuildingIds.Airport,
                    RuleDomain.Sea => BuildingIds.PortFacility,
                    _ => facility
                };
                if (Has(facility ?? throw new InvalidOperationException("Unsupported recovery domain requires a retained facility selector")))
                { full = true; factor = unchecked(factor * 2); }
            }
            if (distance == 0)
            {
                factor = unchecked(factor * 2);
            }

            int amount = unchecked(factor * (UnitMovement.MaximumHitPoints(rules, unit) / 10));
            if (full && distance == 0)
            {
                amount = unit.Damage;
            }

            if (rule.Domain == RuleDomain.Air && world.Map.AirbaseOwner(point) is > 0)
            {
                amount = unit.Damage;
            }

            unit.Damage = unchecked((byte)(unit.Damage - Math.Min(amount, unit.Damage)));
            if (unit.Damage == 0 && unit.Orders == UnitOrder.Sleeping && (rule.Domain != RuleDomain.Land || !tile.Ocean))
            {
                unit.Orders = UnitOrder.None;
            }

            if (world.Multiplayer.LocalHuman == owner || unit.Visibility.Contains(world.Multiplayer.LocalHuman)
                || unit.Owner.Value == world.Multiplayer.LocalHuman)
            {
                try
                { redraw(unit.Position); }
                catch (Exception error) { failures.Add(error); }
            }
        }
        return failures.AsReadOnly();
    }
}
