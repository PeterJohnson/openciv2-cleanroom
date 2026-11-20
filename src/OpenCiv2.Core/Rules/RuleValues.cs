using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Rules;

/// <summary>Logical display conventions for empty rule names.</summary>
public static class RuleText
{
    public const string Nil = "<nil>", Empty = "", Space = " ";
    public static string Display(string value) => value.Length == 0 ? Nil : value;
}
public sealed record RuleName(string Id, string Name)
{
    public static RuleName Empty { get; } = new("", RuleText.Empty);
}
public enum AdvanceReference : short { None = -1, Never = -2, Invalid = -3 }
public enum TerrainResult : short { NoResult = -1, ImproveInPlace = -2, Invalid = -3 }
public enum RuleDomain : byte { Land, Air, Sea }
public enum RuleRole : byte { Attack, Defend, NavalSuperiority, AirSuperiority, SeaTransport, Settle, Diplomacy, Trade }
public enum AdvanceEpoch : byte { Ancient, Renaissance, Industrial, Modern }
public enum AdvanceCategory : byte { Military, Economic, Social, Academic, Applied }
/// <summary>F03 §4 named gameplay parameters, independent of source field ordering.</summary>
public sealed record CosmicRules
{
    public byte RoadMovementMultiplier { get; init; }
    public byte TriremeLossDenominator { get; init; }
    public byte FoodPerCitizen { get; init; }
    public byte FoodBoxRows { get; init; }
    public byte ShieldBoxRows { get; init; }
    public byte SettlerFoodLowGovernment { get; init; }
    public byte SettlerFoodHighGovernment { get; init; }
    public byte UnhappinessCitySize { get; init; }
    public byte UnhappinessEmpireSize { get; init; }
    public byte AqueductSize { get; init; }
    public byte SewerSize { get; init; }
    public byte TechnologyCostParadigm { get; init; }
    public byte TransformationWork { get; init; }
    public byte MonarchyFreeSupport { get; init; }
    public byte CommunismFreeSupport { get; init; }
    public byte FundamentalismFreeSupport { get; init; }
    public byte CommunismPalaceDistance { get; init; }
    public byte FundamentalismScienceLossPercent { get; init; }
    public byte ProductionSwitchLossPercent { get; init; }
    public byte ParadropRange { get; init; }
    public byte SpaceshipMassThrustParadigm { get; init; }
    public byte FundamentalismScienceCapTenths { get; init; }
}
public sealed record AdvanceRule
{
    public RuleName Identity { get; init; } = RuleName.Empty;
    public sbyte AiValue { get; init; }
    public sbyte CivilizedAiModifier { get; init; }
    public OwnedList<AdvanceReference> Prerequisites { get; init; } = new(
        Enumerable.Repeat(AdvanceReference.Never, RuleLimits.AdvancePrerequisites));
    public AdvanceEpoch Epoch { get; init; }
    public AdvanceCategory Category { get; init; }
    public bool Enabled { get; init; }
}
public sealed record ImprovementRule
{
    public RuleName Identity { get; init; } = RuleName.Empty;
    public byte CostRows { get; init; }
    public byte Upkeep { get; init; }
    public AdvanceReference Prerequisite { get; init; } = AdvanceReference.Never;
}
public sealed record UnitRule
{
    public RuleName Identity { get; init; } = RuleName.Empty;
    public AdvanceReference ObsoleteBy { get; init; } = AdvanceReference.Never;
    public AdvanceReference Prerequisite { get; init; } = AdvanceReference.Never;
    public RuleDomain Domain { get; init; }
    public RuleRole Role { get; init; }
    public byte Movement { get; init; }
    public byte Range { get; init; }
    public byte Attack { get; init; }
    public byte Defense { get; init; }
    public byte HitPoints { get; init; }
    public byte Firepower { get; init; }
    public byte CostRows { get; init; }
    public byte Capacity { get; init; }
    public UnitAbilities Abilities { get; init; }
}
public sealed record TerrainWork(
    TerrainResult Result = TerrainResult.NoResult,
    byte YieldBonus = 0,
    byte WorkTurns = 0,
    byte AiGovernmentThreshold = 0);
public sealed record BaseTerrainWork(
    TerrainWork Irrigation,
    TerrainWork Mining,
    TerrainResult Transformation = TerrainResult.NoResult)
{
    public BaseTerrainWork() : this(new(), new()) { }
}
public sealed record TerrainRule
{
    public RuleName Identity { get; init; } = RuleName.Empty;
    public byte MovementCost { get; init; }
    public byte DefenseFactor { get; init; }
    public byte Food { get; init; }
    public byte Shields { get; init; }
    public byte Trade { get; init; }
    public BaseTerrainWork? Work { get; init; }
}
public sealed record TitlePair(string Male, string Female)
{
    public TitlePair() : this(RuleText.Empty, RuleText.Empty) { }
}
public sealed record GovernmentRule
{
    public RuleName Identity { get; init; } = RuleName.Empty;
    public TitlePair Titles { get; init; } = new();
}
public sealed record TribeRule
{
    public string Id { get; init; } = "";
    public string Plural { get; init; } = RuleText.Empty;
    public string Adjective { get; init; } = RuleText.Empty;
    public OwnedList<string> Leaders { get; init; } = new([RuleText.Empty, RuleText.Empty]);
    public Gender PreferredGender { get; init; }
    public byte PreferredColor { get; init; } = 1;
    public byte CityStyle { get; init; }
    public OwnedList<sbyte> Personality { get; init; } = new(new sbyte[3]);
    public OwnedList<TitlePair> Titles { get; init; } = new(Enumerable.Range(0, RuleLimits.Governments).Select(_ => new TitlePair()));
}
public sealed record OrderRule
{
    public RuleName Identity { get; init; } = RuleName.Empty;
    public string Shortcut { get; init; } = "";
}
public sealed record RuleDefinitions
{
    public CosmicRules Cosmic { get; init; } = new();
    public OwnedList<AdvanceRule> Advances { get; init; } = new(
        Enumerable.Range(0, RuleLimits.Advances).Select(_ => new AdvanceRule()));
    public ushort EnabledAdvanceCount { get; init; }
    public OwnedList<ImprovementRule> Improvements { get; init; } = new(
        Enumerable.Range(0, RuleLimits.Improvements).Select(_ => new ImprovementRule()));
    public OwnedList<AdvanceReference> WonderObsolescence { get; init; } = new(new AdvanceReference[RuleLimits.Wonders]);
    public OwnedList<UnitRule> Units { get; init; } = new(Enumerable.Range(0, RuleLimits.UnitTypes).Select(_ => new UnitRule()));
    public OwnedList<TerrainRule> Terrain { get; init; } = new(Enumerable.Range(0, RuleLimits.Terrains).Select(_ => new TerrainRule()));
    public OwnedList<GovernmentRule> Governments { get; init; } = new(
        Enumerable.Range(0, RuleLimits.Governments).Select(_ => new GovernmentRule()));
    public OwnedList<TribeRule> Tribes { get; init; } = new(Enumerable.Range(0, RuleLimits.Tribes).Select(_ => new TribeRule()));
    public OwnedList<RuleName> Commodities { get; init; } = new(Enumerable.Repeat(RuleName.Empty, RuleLimits.Commodities));
    public OwnedList<OrderRule> Orders { get; init; } = new(Enumerable.Range(0, RuleLimits.Orders).Select(_ => new OrderRule()));
    public OwnedList<RuleName> Difficulties { get; init; } = new(Enumerable.Repeat(RuleName.Empty, RuleLimits.Difficulties));
    public OwnedList<RuleName> DiplomaticAttitudes { get; init; } = new(Enumerable.Repeat(RuleName.Empty, RuleLimits.DiplomaticAttitudes));
}
public sealed record LeaderTitleOverride(Government Government, TitlePair Titles);
public sealed record RuleInput
{
    public RuleDefinitions Definitions { get; init; } = new();
    public OwnedList<int> MovementPoints { get; init; } = new(new int[RuleLimits.UnitTypes]);
    public OwnedList<OwnedList<LeaderTitleOverride>> TitleOverrides { get; init; } = new(
        Enumerable.Range(0, RuleLimits.Tribes).Select(_ => new OwnedList<LeaderTitleOverride>([])));
    public OwnedList<AdvanceReference?> GovernmentAdvances { get; init; } = new(new AdvanceReference?[RuleLimits.Governments]);
}

/// <summary>Mutable engine tables. Snapshot copies sequence storage for published observations.</summary>
public sealed class RuleTables
{
    public CosmicRules Cosmic { get; set; } = new();
    public AdvanceRule[] Advances { get; } = Enumerable.Range(0, RuleLimits.Advances).Select(_ => new AdvanceRule()).ToArray();
    public ushort EnabledAdvanceCount { get; set; }
    public ImprovementRule[] Improvements { get; } = Enumerable.Range(0, RuleLimits.Improvements).Select(_ => new ImprovementRule())
        .ToArray();
    public AdvanceReference[] WonderObsolescence { get; } = new AdvanceReference[RuleLimits.Wonders];
    public UnitRule[] Units { get; } = Enumerable.Range(0, RuleLimits.UnitTypes).Select(_ => new UnitRule()).ToArray();
    public TerrainRule[] Terrain { get; } = Enumerable.Range(0, RuleLimits.Terrains).Select(_ => new TerrainRule()).ToArray();
    public GovernmentRule[] Governments { get; } = Enumerable.Range(0, RuleLimits.Governments).Select(_ => new GovernmentRule()).ToArray();
    public TribeRule[] Tribes { get; } = Enumerable.Range(0, RuleLimits.Tribes).Select(_ => new TribeRule()).ToArray();
    public RuleName[] Commodities { get; } = Enumerable.Repeat(RuleName.Empty, RuleLimits.Commodities).ToArray();
    public OrderRule[] Orders { get; } = Enumerable.Range(0, RuleLimits.Orders).Select(_ => new OrderRule()).ToArray();
    public RuleName[] Difficulties { get; } = Enumerable.Repeat(RuleName.Empty, RuleLimits.Difficulties).ToArray();
    public RuleName[] DiplomaticAttitudes { get; } = Enumerable.Repeat(RuleName.Empty, RuleLimits.DiplomaticAttitudes).ToArray();

    public RuleDefinitions Snapshot() => new()
    {
        Cosmic = Cosmic,
        Advances = new(Advances),
        EnabledAdvanceCount = EnabledAdvanceCount,
        Improvements = new(Improvements),
        WonderObsolescence = new(WonderObsolescence),
        Units = new(Units),
        Terrain = new(Terrain),
        Governments = new(Governments),
        Tribes = new(Tribes),
        Commodities = new(Commodities),
        Orders = new(Orders),
        Difficulties = new(Difficulties),
        DiplomaticAttitudes = new(DiplomaticAttitudes)
    };
}
