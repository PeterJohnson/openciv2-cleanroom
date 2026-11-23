using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U04 apply-fortification and U01's AI home adjustment. Refresh callbacks own
/// presentation; this operation performs no extra support, population or production pass.</summary>
public sealed class UnitFortification(GameState world, Action<CitySlot> refreshHome,
    Action<MapPoint> redraw, Action<UnitSlot> unitChanged)
{
    public void Apply(UnitSlot slot)
    {
        MapKnowledge.RequireLocal(world);
        ref var unit = ref world.Entities.Units.At(slot);
        unit.Orders = UnitOrder.Fortified;
        if (!world.Globals.HumanPlayers.Contains(unit.Owner.Value) && unit.Home is { Value: < 128 })
        {
            CitySlot? selected = CityQueries.At(world, unit.Position);
            if (selected is null)
            {
                var tile = world.Map.TileAt(unit.Position);
                if (tile.Features.HasFlag(MapFeatures.Fortification) && !tile.Features.HasFlag(MapFeatures.CitySite))
                {
                    selected = CityQueries.Nearest(world, unit.Position, unit.Owner.Value).Slot;
                }
            }
            if (selected is { } city && world.Entities.Cities.At(city).Size > 2)
            {
                var oldHome = unit.Home;
                unit.Home = city;
                if (oldHome is { } old)
                {
                    refreshHome(old);
                }

                refreshHome(city);
            }
        }
        redraw(unit.Position);
        unitChanged(slot);
    }
}
