using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C01 elementary civil-war transfer. Capture policy and knowledge/unit changes belong to callers.</summary>
public static class CityTransfer
{
    public static void Transfer(GameState world, CitySlot slot, int oldOwner, int newOwner)
    {
        MapKnowledge.RequireLocal(world);
        ref var city = ref world.Entities.Cities.At(slot);
        world.Civilizations[oldOwner].CityCount = unchecked((short)(world.Civilizations[oldOwner].CityCount - 1));
        world.Civilizations[newOwner].CityCount = unchecked((short)(world.Civilizations[newOwner].CityCount + 1));
        world.Civilizations[oldOwner].TotalCitySize = unchecked((short)(world.Civilizations[oldOwner].TotalCitySize - city.Size));
        world.Civilizations[newOwner].TotalCitySize = unchecked((short)(world.Civilizations[newOwner].TotalCitySize + city.Size));
        city.Owner = new(unchecked((byte)newOwner));
        world.Map.Mutate(new TileChange.Owner(city.Position, unchecked((byte)newOwner)));
    }
}
