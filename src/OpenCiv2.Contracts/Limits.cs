namespace OpenCiv2.Contracts;

/// <summary>Live entity capacities, including the barbarian civilization and retained city slot.</summary>
public static class EntityLimits
{
    public const int Civilizations = 8;
    public const int PlayerCivilizations = Civilizations - 1;
    public const int Units = 2048;
    public const int NonHumanUnits = 2040;
    public const int Cities = 256;
    public const int CreatableCities = Cities - 1;
}

/// <summary>Collection sizes in the supported rules profile, shared by logical tables and content adapters.</summary>
public static class RuleLimits
{
    public const int Advances = 100;
    public const int AdvancePrerequisites = 2;
    public const int BuildingTypes = 39;
    public const int Wonders = 28;
    public const int Improvements = BuildingTypes + Wonders;
    public const int UnitTypes = 62;
    public const int BaseTerrains = 11;
    public const int TerrainVariants = 3;
    public const int Terrains = BaseTerrains * TerrainVariants;
    public const int Governments = 7;
    public const int Tribes = 21;
    public const int Commodities = 16;
    public const int Orders = 12;
    public const int Difficulties = 6;
    public const int DiplomaticAttitudes = 9;
    public const int LeaderGenders = 2;
}

/// <summary>Per-city collection capacities; these are distinct from rule-table sizes.</summary>
public static class CityLimits
{
    public const int TradeRoutes = 3;
    public const int Specialists = 16;
    public const int WorkableTiles = 21;
    public const int ImprovementSlots = 40;
}

/// <summary>Logical spatial collection sizes, independent of serialized field widths.</summary>
public static class MapLimits
{
    public const int Regions = 64;
    public const int Directions = 8;
}
