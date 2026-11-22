using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

/// <summary>C08 ordinary building storage and wonder-effect queries. These operations do not run
/// acquisition, maintenance, production or effect propagation.</summary>
public static class BuildingQueries
{
    public const int Unbuilt = -1, Destroyed = -2;

    /// <summary>Only ordinary facilities have presence bits; invalid identities do not read the city.</summary>
    public static bool HasBuilding(GameState world, CitySlot city, int building) =>
        building is >= BuildingIds.First and <= BuildingIds.Last
        && world.Entities.Cities.At(city).Improvements.Contains(building);

    /// <summary>Changes only stored ordinary-building presence, including for an unoccupied backed city.</summary>
    public static void SetBuilding(GameState world, CitySlot city, int building, bool present)
    {
        if (building is >= BuildingIds.First and <= BuildingIds.Last)
        {
            ref var record = ref world.Entities.Cities.At(city);
            record.Improvements = record.Improvements.With(building, present);
        }
    }

    /// <summary>Any normal civilization's knowledge expires a wonder; dead and nonhuman seats still count.</summary>
    public static bool Expired(GameState world, RuleTables rules, int wonder)
    {
        int advance = (int)rules.WonderObsolescence[wonder];
        if (advance < 0)
        {
            return false;
        }

        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        {
            if (CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, advance))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the recorded location, including Destroyed, unless expired. Does not check city life.</summary>
    public static int ActiveCity(GameState world, RuleTables rules, int wonder) =>
        Expired(world, rules, wonder) ? Unbuilt : world.Globals.WonderCity[wonder];

    /// <summary>Reads a nonnegative active location's signed owner, without Eiffel scenario suppression.</summary>
    public static int? ActiveOwner(GameState world, RuleTables rules, int wonder)
    {
        int city = ActiveCity(world, rules, wonder);
        return city < 0 ? null : unchecked((sbyte)world.Entities.Cities.At(new((ushort)city)).Owner.Value);
    }

    /// <summary>Owner-wide effect query; the scenario objectives flag suppresses Eiffel only at this boundary.</summary>
    public static bool HasWonder(GameState world, RuleTables rules, int civilization, int wonder)
    {
        if (wonder == WonderIds.EiffelTower && world.Globals.Status.HasScenario && world.Scenario.Flags.ObjectiveVictory)
        {
            return false;
        }

        return ActiveOwner(world, rules, wonder) == unchecked((sbyte)civilization);
    }
}
