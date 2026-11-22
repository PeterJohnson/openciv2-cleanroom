using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

/// <summary>Authored terrain and rules for Alder Valley. Classic slot identities remain explicit.</summary>
internal static class LocalGameRules
{
    internal const int Owner = 1, Settler = 0, Guard = 2, Rider = 4, Palace = 1;
    internal const int Temple = -BuildingIds.Temple, Library = -BuildingIds.Library;
    internal const int Width = 24, Height = 16;
    internal const ushort Seed = 47;
    internal static readonly MapPoint StartingTile = new(8, 8);

    internal static Terrain Landform(MapPoint point) => point.X < 3 || point.X >= 22
        ? Terrain.Ocean : point.Y < 4 ? Terrain.Forest
        : point.Y > 11 ? Terrain.Hills : (point.X + point.Y) % 4 == 0 ? Terrain.Grassland : Terrain.Plains;

    internal static bool River(MapPoint point) => point.Y is >= 4 and <= 11
        && point.X == 8 + (point.Y & 1);

    internal static MapFeatures Features(MapPoint point) =>
        point.Y == 8 && point.X is >= 6 and <= 16 ? MapFeatures.Road : MapFeatures.None;

    internal static bool ProductionAvailable(GameState world, RuleTables rules, int type, CitySlot? city = null) =>
        type is Guard or Rider ? UnitAvailability.CanBuild(world, rules, Owner, city, new((byte)type))
        : type is Temple or Library && city is { } slot
            && CivilizationKnowledge.KnowsAdvance(world.Civilizations, Owner, (int)rules.Improvements[-type].Prerequisite)
            && !BuildingQueries.HasBuilding(world, slot, -type);

    internal static bool SupportedBuildings(ImprovementSet buildings) =>
        buildings.With(BuildingIds.Palace, false).With(BuildingIds.Temple, false).With(BuildingIds.Library, false) == default;

    internal static RuleInput Input()
    {
        var tables = new RuleTables
        {
            Cosmic = new()
            {
                RoadMovementMultiplier = 3,
                FoodPerCitizen = 2,
                FoodBoxRows = 6,
                ShieldBoxRows = 4,
                AqueductSize = 8,
                SewerSize = 12,
                TechnologyCostParadigm = 10,
                UnhappinessCitySize = 20,
                UnhappinessEmpireSize = 14,
                ProductionSwitchLossPercent = 50
            }
        };
        for (int i = 0; i < tables.Terrain.Length; i++)
        {
            tables.Terrain[i] = new()
            {
                MovementCost = 1,
                Food = 2,
                Shields = 1,
                Trade = 1,
                Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 1))
            };
        }

        tables.Terrain[(int)Terrain.Plains] = tables.Terrain[(int)Terrain.Plains] with { Shields = 2 };
        tables.Terrain[(int)Terrain.Hills] = tables.Terrain[(int)Terrain.Hills] with { MovementCost = 2, Shields = 2 };
        tables.Terrain[(int)Terrain.Forest] = tables.Terrain[(int)Terrain.Forest] with { MovementCost = 2, Shields = 2 };
        tables.Terrain[(int)Terrain.Ocean] = tables.Terrain[(int)Terrain.Ocean] with { Shields = 0 };
        tables.Units[Settler] = new()
        {
            Identity = new("pioneer", "Pioneer"),
            Role = RuleRole.Settle,
            Defense = 1,
            HitPoints = 10,
            CostRows = 2,
            Abilities = new() { SeeTwo = true }
        };
        tables.Units[Guard] = new()
        {
            Identity = new("watch", "Watch"),
            Role = RuleRole.Defend,
            Prerequisite = AdvanceReference.None,
            Attack = 1,
            Defense = 2,
            HitPoints = 10,
            CostRows = 1
        };
        tables.Units[Rider] = new()
        {
            Identity = new("outrider", "Outrider"),
            Role = RuleRole.Attack,
            Prerequisite = AdvanceReference.None,
            Attack = 2,
            Defense = 1,
            HitPoints = 10,
            CostRows = 2,
            Abilities = new() { SeeTwo = true }
        };
        tables.Improvements[BuildingIds.Temple] = new() { Identity = new("temple", "Temple"), CostRows = 2, Upkeep = 1, Prerequisite = (AdvanceReference)0 };
        tables.Improvements[BuildingIds.Library] = new() { Identity = new("library", "Library"), CostRows = 3, Upkeep = 1, Prerequisite = (AdvanceReference)1 };
        tables.Advances[0] = Advance("land-surveys", "Land Surveys");
        tables.Advances[1] = Advance("clay-casting", "Clay Casting");
        tables.Advances[2] = Advance("field-ledgers", "Field Ledgers", 0);
        tables.Tribes[0] = new()
        {
            Id = "alder",
            Plural = "Alderfolk",
            Adjective = "Alder",
            Leaders = new(["Cedar", "Rowan"])
        };
        var movement = new int[RuleLimits.UnitTypes];
        movement[Settler] = movement[Guard] = 1;
        movement[Rider] = 2;
        return new() { Definitions = tables.Snapshot(), MovementPoints = new(movement) };
    }

    private static AdvanceRule Advance(string id, string name, AdvanceReference prerequisite = AdvanceReference.None) =>
        new() { Identity = new(id, name), Prerequisites = new([prerequisite, AdvanceReference.None]) };
}
