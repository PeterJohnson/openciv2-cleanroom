using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A05 §9 landing and coastal targets. These helpers select directions/orders;
/// actual sea movement and cargo landing remain U04/U02/U05 operations.</summary>
public sealed class NavalTargets(GameState world)
{
    public int? BestLandingDirection(UnitSlot slot)
    {
        MapKnowledge.RequireLocal(world);
        var actor = world.Entities.Units.At(slot);
        int owner = actor.Owner.Value, best = -1;
        int? answer = null;
        for (int direction = 0; direction <= 8; ++direction)
        {
            var candidate = direction == 8 ? actor.Position : Offset(actor.Position, MapNeighborhoods.Directions[direction]);
            if (!world.Map.Geometry.InBounds(candidate) || !world.Map.TileAt(candidate).Ocean
                || direction != 8 && world.Map.UnitOwner(candidate) is not null)
            { continue; }
            int score = 0;
            foreach (var delta in MapNeighborhoods.Directions)
            {
                var land = Offset(candidate, delta);
                if (!world.Map.Geometry.InBounds(land) || world.Map.TileAt(land).Ocean
                    || world.Map.UnitOwner(land) is not null)
                { continue; }
                int claimant = world.Map.ClaimantOrFertility(land);
                if (claimant <= 7 && world.Globals.HumanPlayers.Contains(claimant))
                {
                    var treaty = world.Civilizations[owner].Treaties[claimant];
                    if (treaty.Ceasefire || treaty.Peace)
                    { continue; }
                }
                if (world.Map.CityOwner(land) is { } cityOwner && cityOwner != owner
                    && world.Map.UnitOwner(land) is null && DiplomaticAttitudes.Hostile(world, owner, cityOwner)
                    && direction == 8)
                { score += 12; }
                ++score;
            }
            if (score >= best)
            { best = score; answer = direction; }
        }
        return answer;
    }

    public void NearShoreGoto(UnitSlot slot, int task, MapPoint target)
    {
        MapKnowledge.RequireLocal(world);
        int actorRegion = world.Map.TileAt(world.Entities.Units.At(slot).Position).Region;
        int targetRegion = world.Map.TileAt(target).Region;
        var selected = target;
        for (int i = 0; i < 20; ++i)
        {
            var candidate = Offset(target, MapNeighborhoods.City[i]);
            if (!world.Map.Geometry.InBounds(candidate) || !world.Map.TileAt(candidate).Ocean
                || world.Map.TileAt(candidate).Region != actorRegion)
            { continue; }
            bool match = false;
            foreach (var delta in MapNeighborhoods.Directions)
            {
                var land = Offset(candidate, delta);
                if (world.Map.Geometry.InBounds(land) && !world.Map.TileAt(land).Ocean
                    && world.Map.TileAt(land).Region == targetRegion && world.Map.CityOwner(land) is null)
                { match = true; }
            }
            if (match)
            { selected = candidate; break; }
        }
        UnitOrders.SetGoto(ref world.Entities.Units.At(slot), task, selected);
    }

    private MapPoint Offset(MapPoint origin, MapPoint delta) =>
        MapNeighborhoods.Offset(world.Map.Geometry, origin, delta, world.Globals.Rules.FlatWorld);
}
