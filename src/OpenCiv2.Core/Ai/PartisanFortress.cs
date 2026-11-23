using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A04 §5 partisan counter and second-nearest-city fortress decision.
/// Only the retained context flag changes; map improvements remain intact.</summary>
public sealed class PartisanFortress(GameState world)
{
    public void Apply(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        ref var unit = ref world.Entities.Units.At(c.Unit);
        if (c.Type == UnitTypeIds.Partisans && unit.MovesUsed == 0)
        {
            if (!c.EnemyZone && unchecked((sbyte)unit.Counter) < 15)
            { unit.Counter = unchecked((byte)(unit.Counter + 1)); }
            if (c.EnemyZone && unit.Counter != 0)
            { unit.Counter = unchecked((byte)(unit.Counter - 1)); }
            if (unchecked((sbyte)unit.Counter) > 9)
            { c.Fortress = false; }
        }
        if (!c.Fortress)
        { return; }
        CitySlot? selected = null;
        int best = 9999;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied || c.NearestCity?.Value == i || world.Map.TileAt(city.Position).Region != c.Region)
            { continue; }
            int distance = world.Map.Geometry.WeightedDistance(c.Position, city.Position, world.Globals.Rules.FlatWorld);
            if (distance < best || distance == best && city.Owner.Value == c.Owner)
            { best = distance; selected = new((ushort)i); }
        }
        if (c.HomeCity != c.NearestCity && (selected is null || world.Entities.Cities.At(selected.Value).Owner.Value != c.Owner))
        { c.Fortress = false; }
    }
}
