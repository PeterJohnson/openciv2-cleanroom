using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>W03 knowledge channels for local play. The session publishes changed state to its observers.</summary>
public static class MapKnowledge
{
    /// <summary>U01 single-unit seen set; does not explore, traverse the stack or add the owner's bit.</summary>
    public static bool MarkUnitSeen(GameState world, UnitSlot slot, int observer)
    {
        if (observer < 0)
        { return false; }
        ref var unit = ref world.Entities.Units.At(slot);
        if (unchecked((sbyte)unit.Owner.Value) == observer)
        { return false; }
        bool changed = !unit.Visibility.Contains(observer);
        unit.Visibility = unit.Visibility.With(observer);
        return changed;
    }

    internal static void RequireLocal(GameState world)
    {
        if (world.Globals.Multiplayer >= GameMode.Tcp)
        {
            throw new InvalidOperationException("Network revelation requires the owning tile synchronization service");
        }
    }

    /// <summary>Remembers features without exploring or revealing occupants. Barbarian memory is skipped.</summary>
    public static bool Remember(GameState world, int observer, MapPoint point)
    {
        RequireLocal(world);
        if (observer == 0)
        {
            return false;
        }
        ref var memory = ref world.Map.Remembered(observer, point);
        var features = world.Map.TileAt(point).Features;
        bool changed = memory != features;
        memory = features;
        return changed;
    }

    public static bool Explore(GameState world, int observer, MapPoint point)
    {
        RequireLocal(world);
        ref var tile = ref world.Map.TileAt(point);
        bool changed = !tile.RevealedTo.Contains(observer);
        tile.RevealedTo = tile.RevealedTo.With(observer);
        return changed;
    }

    public static bool RevealCity(GameState world, int observer, CitySlot slot)
    {
        if (observer < 0)
        {
            return false;
        }
        ref var city = ref world.Entities.Cities.At(slot);
        byte size = unchecked((byte)city.Size);
        bool changed = !city.KnownTo.Contains(observer) || city.RememberedSize[observer] != size;
        city.KnownTo = city.KnownTo.With(observer);
        city.RememberedSize[observer] = size;
        return changed;
    }

    /// <summary>The city branch suppresses stack publication, even when city knowledge is already current.</summary>
    public static bool RevealTile(GameState world, StackOperations stacks, int observer, MapPoint point)
    {
        if (observer < 0)
        {
            return false;
        }
        bool changed = Explore(world, observer, point);
        changed |= Remember(world, observer, point);
        return CityQueries.At(world, point) is { } city
            ? RevealCity(world, observer, city) | changed
            : stacks.MarkSeen(stacks.FirstAt(point), observer) | changed;
    }

    /// <summary>Uses the tile unit-owner gate and cached origin medium, not a replacement lifetime scan.</summary>
    public static bool RevealAdjacentUnits(GameState world, StackOperations stacks, int observer, MapPoint origin)
    {
        RequireLocal(world);
        bool ocean = world.Map.TileAt(origin).Ocean, changed = false;
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, origin, offset, world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(point) && world.Map.TileAt(point).Ocean == ocean
                && world.Map.UnitOwner(point) is not null)
            {
                changed |= RevealTile(world, stacks, observer, point);
            }
        }
        return changed;
    }
}
