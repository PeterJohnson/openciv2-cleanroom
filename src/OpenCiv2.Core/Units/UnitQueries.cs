using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U01 spatial queries over live slots, independent of player visibility or command admission.</summary>
public static class UnitQueries
{
    /// <summary>U01 nearest-unit query: unrestricted negative owner, low-byte owner match, later ties.
    /// Position, terrain, orders and bounds do not filter candidates. No match retains distance 9999.</summary>
    public static (UnitSlot? Slot, int Distance) Nearest(
        GameState world, MapPoint origin, int owner = -1, UnitSlot? excluded = null)
    {
        UnitSlot? selected = null;
        int distance = 9999;
        var pool = world.Entities.Units;
        for (int i = 0; i < pool.End; ++i)
        {
            ref readonly var unit = ref pool.Storage[i];
            if (!unit.Token.Occupied || excluded?.Value == i
                || owner >= 0 && unchecked((byte)owner) != unchecked((sbyte)unit.Owner.Value))
            {
                continue;
            }

            int candidate = world.Map.Geometry.WeightedDistance(origin, unit.Position, world.Globals.Rules.FlatWorld);
            if (candidate <= distance)
            {
                distance = candidate;
                selected = new((ushort)i);
            }
        }

        return (selected, distance);
    }
}
