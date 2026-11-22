using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

/// <summary>C06 semantic scenario inputs. Their original-file bit binding remains an S01/F06 gap.</summary>
public readonly record struct WonderBuildRestrictions(bool NoNewWonders, bool AllowManhattan);

/// <summary>C06 wonder and nonunit eligibility. A missing city skips every city-specific gate;
/// availability never substitutes active effects for stored facility presence.</summary>
public static class BuildingAvailability
{
    public static bool Wonder(GameState world, RuleTables rules, int owner, int wonder, WonderBuildRestrictions? scenario = null)
    {
        if (world.Globals.Status.HasScenario && world.Globals.RevealMap == 0)
        {
            var restrictions = scenario ?? throw new InvalidOperationException("Scenario wonder eligibility requires explicit C06 restrictions; the classic flag binding is unspecified.");
            if (restrictions.NoNewWonders && !(restrictions.AllowManhattan && wonder == WonderIds.Manhattan))
            {
                return false;
            }
        }
        return world.Globals.WonderCity[wonder] == BuildingQueries.Unbuilt
            && CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, (int)rules.Improvements[39 + wonder].Prerequisite);
    }

    public static bool Improvement(GameState world, RuleTables rules, int owner, CitySlot? city, int item,
        WonderBuildRestrictions? scenario = null)
    {
        if (item > BuildingIds.Capitalization)
        {
            return Wonder(world, rules, owner, item - 39, scenario);
        }

        bool Knows(int building) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, (int)rules.Improvements[building].Prerequisite);
        if (!Knows(item))
        {
            return false;
        }

        if (city is not { } slot)
        {
            return true;
        }

        bool Has(int building) => BuildingQueries.HasBuilding(world, slot, building);
        bool Effect(int wonder) => BuildingQueries.HasWonder(world, rules, owner, wonder);
        if (Has(item))
        {
            return false;
        }

        var flags = world.Entities.Cities.At(slot).Flags;
        if (item is BuildingIds.Harbor or BuildingIds.OffshorePlatform && !flags.Coastal)
        {
            return false;
        }

        if (item is BuildingIds.PortFacility or BuildingIds.CoastalFortress && (!flags.Coastal || !flags.OceanAccess))
        {
            return false;
        }

        switch (item)
        {
            case BuildingIds.Granary when Effect(WonderIds.Pyramids):
            case BuildingIds.Cathedral when Effect(WonderIds.Michelangelo):
            case BuildingIds.Courthouse when Has(BuildingIds.Palace):
            case BuildingIds.Bank when !Has(BuildingIds.Marketplace) || !Knows(BuildingIds.Marketplace):
            case BuildingIds.StockExchange when !Has(BuildingIds.Bank) || !Knows(BuildingIds.Bank):
            case BuildingIds.University when !Has(BuildingIds.Library) || !Knows(BuildingIds.Library):
            case BuildingIds.ResearchLab when Effect(WonderIds.Seti) || !Has(BuildingIds.University) || !Knows(BuildingIds.University):
            case BuildingIds.SewerSystem when !Has(BuildingIds.Aqueduct):
            case BuildingIds.PoliceStation when Effect(WonderIds.WomensSuffrage):
            case BuildingIds.ManufacturingPlant when !Has(BuildingIds.Factory) || !Knows(BuildingIds.Factory):
            case BuildingIds.HydroPlant when !flags.HydroPossible:
                return false;
        }
        if (item is BuildingIds.PowerPlant or BuildingIds.HydroPlant or BuildingIds.NuclearPlant or BuildingIds.SolarPlant)
        {
            if (!Has(BuildingIds.Factory) && !Has(BuildingIds.ManufacturingPlant))
            {
                return false;
            }

            if (item != BuildingIds.SolarPlant && (Effect(WonderIds.HooverDam) || Has(BuildingIds.HydroPlant)
                || item != BuildingIds.HydroPlant && Has(BuildingIds.NuclearPlant) || Has(BuildingIds.SolarPlant)))
            {
                return false;
            }
        }
        if (item is >= BuildingIds.Structural and <= BuildingIds.Module)
        {
            return !world.Globals.Rules.Bloodlust && world.Globals.WonderCity[WonderIds.Apollo] != BuildingQueries.Unbuilt
                && (world.Globals.Year > 0 || world.Globals.Status.HasScenario) && !world.Civilizations[owner].SpaceshipFlags.Launched;
        }

        return true;
    }
}
