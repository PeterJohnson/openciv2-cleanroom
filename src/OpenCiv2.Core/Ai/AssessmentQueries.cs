using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Ai;

public static class AiGoalRoles
{
    public const int AntiSubmarine = 18, FoundCity = 21;
}
public static class AiTaskIds
{
    public const int Nuclear = 0x21, Goal = 0x31, Preserved = 0x37, Unassigned = 0x3f;
    public const byte RespondToAttack = 75;
    public const byte Recover = 100, SevereRecovery = 68, Fortify = 70;
    public const byte Idle = 48;
    public const byte HuntNonmilitary = 80, PartisanPillage = 112;
    public const byte ChooseMove = 57;
    public const byte RecruitPort = 0x33, OverseasAttack = 0x34, AnotherLandmassPort = 0x35, ExploreNearby = 0x36;
    public const byte ReinforcePort = 0x41, RaidEmptyCity = 0x49, OverseasTrade = 0x54;
    public const byte UnloadCargo = 0x55, ExploreRegion = 0x56, RepositionLanding = 0x62, UnloadTrade = 0x75;
    public const byte TradeLanding = 0x74, CityTarget = 0x63, DiplomatTarget = 0x53;
    public const byte FortressGuard = 0x46;
}

/// <summary>A01 §2 role, defense quota and sparse city/land-region contact queries.</summary>
public static class AssessmentQueries
{
    public static int Role(RuleTables rules, in Unit unit) => rules.Units[unit.Type.Value].Role == RuleRole.Settle && unit.Flags.Colonist
        ? AiGoalRoles.FoundCity : unchecked((sbyte)rules.Units[unit.Type.Value].Role);

    public static int DefenseQuota(GameState world, int owner, int region)
    {
        int cities = world.Civilizations[owner].ContinentCities[region];
        return 3 * Math.Min(cities, 5) + 2 * Math.Clamp(cities - 5, 0, 5) + Math.Clamp(cities - 10, 0, 5);
    }

    public static bool CityTouchesRegion(GameState world, CitySlot slot, int region)
    {
        var origin = world.Entities.Cities.At(slot).Position;
        if (world.Map.TileAt(origin).Region == region)
        {
            return true;
        }

        for (int d = 0; d < MapLimits.Directions; ++d)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, origin, MapNeighborhoods.Directions[d], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point) || !world.Map.TileAt(point).Ocean)
            {
                continue;
            }

            if (world.Map.Regions[region].NeighboringWater.Contains(world.Map.TileAt(point).Region))
            {
                return true;
            }

            d += 2 - d % 2;
        }
        return false;
    }
}
