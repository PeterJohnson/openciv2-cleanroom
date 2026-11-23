using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>F04 city stage 6 after research. Test the captured owner's current human
/// control once, then run actual W05 pollution/meltdown before C08 improvement upkeep.</summary>
public sealed class CityHumanMaintenance(GameState world, CityPollution pollution, BuildingMaintenance buildings)
{
    public IReadOnlyList<Exception> Run(CitySlot city, int capturedOwner, string call)
    {
        MapKnowledge.RequireLocal(world);
        if (!world.Globals.HumanPlayers.Contains(capturedOwner))
        {
            return Array.Empty<Exception>();
        }

        List<Exception> failures = [.. pollution.Run(city, call)];
        failures.AddRange(buildings.Run(city));
        return failures.AsReadOnly();
    }
}
