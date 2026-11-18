using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.Rules;

public sealed record RuleText(string Unicode, OwnedList<byte> Classic)
{
    public static RuleText Empty { get; } = new("", new([]));
    public static RuleText Space { get; } = new(" ", new([(byte)' ']));
}
public sealed record RuleName(string Id, RuleText Name)
{
    public static RuleName Empty { get; } = new("", RuleText.Empty);
}
public enum AdvanceReference : short { None = -1, Never = -2, Invalid = -3 }
public enum TerrainResult : short { NoResult = -1, ImproveInPlace = -2, Invalid = -3 }
public enum RuleDomain : byte { Land, Air, Sea }
public enum RuleRole : byte { Attack, Defend, NavalSuperiority, AirSuperiority, SeaTransport, Settle, Diplomacy, Trade }
public enum AdvanceEpoch : byte { Ancient, Renaissance, Industrial, Modern }
public enum AdvanceCategory : byte { Military, Economic, Social, Academic, Applied }
public enum CosmicParameter
{
    RoadMovementMultiplier, TriremeLossDenominator, FoodPerCitizen, FoodBoxRows, ShieldBoxRows,
    SettlerFoodLowGovernment, SettlerFoodHighGovernment, UnhappinessCitySize, UnhappinessEmpireSize,
    AqueductSize, SewerSize, TechnologyCostParadigm, TransformationWork, MonarchyFreeSupport,
    CommunismFreeSupport, FundamentalismFreeSupport, CommunismPalaceDistance, FundamentalismScienceLossPercent,
    ProductionSwitchLossPercent, ParadropRange, SpaceshipMassThrustParadigm, FundamentalismScienceCapTenths
}
public sealed record CosmicRules(OwnedList<byte> Values)
{
    public CosmicRules() : this(new OwnedList<byte>(new byte[22])) { }
    public byte this[CosmicParameter parameter] => Values[(int)parameter];
}
public sealed record AdvanceRule
{
    public RuleName Identity { get; init; } = RuleName.Empty;
    public sbyte AiValue { get; init; }
    public sbyte CivilizedAiModifier { get; init; }
    public OwnedList<AdvanceReference> Prerequisites { get; init; } = new([AdvanceReference.Never, AdvanceReference.Never]);
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
    public uint Abilities { get; init; }
}
public sealed record TerrainWork(TerrainResult Result = TerrainResult.NoResult, byte YieldBonus = 0, byte WorkTurns = 0, byte AiGovernmentThreshold = 0);
public sealed record BaseTerrainWork(TerrainWork Irrigation, TerrainWork Mining, TerrainResult Transformation = TerrainResult.NoResult)
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
public sealed record TitlePair(RuleText Male, RuleText Female)
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
    public RuleText Plural { get; init; } = RuleText.Empty;
    public RuleText Adjective { get; init; } = RuleText.Empty;
    public OwnedList<RuleText> Leaders { get; init; } = new([RuleText.Empty, RuleText.Empty]);
    public Gender PreferredGender { get; init; }
    public byte PreferredColor { get; init; } = 1;
    public byte CityStyle { get; init; }
    public OwnedList<sbyte> Personality { get; init; } = new(new sbyte[3]);
    public OwnedList<TitlePair> Titles { get; init; } = new(Enumerable.Range(0, 7).Select(_ => new TitlePair()));
}
public sealed record OrderRule
{
    public RuleName Identity { get; init; } = RuleName.Empty;
    public byte Shortcut { get; init; }
}
public sealed record RuleDefinitions
{
    public CosmicRules Cosmic { get; init; } = new();
    public OwnedList<AdvanceRule> Advances { get; init; } = new(Enumerable.Range(0, 100).Select(_ => new AdvanceRule()));
    public ushort EnabledAdvanceCount { get; init; }
    public OwnedList<ImprovementRule> Improvements { get; init; } = new(Enumerable.Range(0, 67).Select(_ => new ImprovementRule()));
    public OwnedList<AdvanceReference> WonderObsolescence { get; init; } = new(new AdvanceReference[28]);
    public OwnedList<UnitRule> Units { get; init; } = new(Enumerable.Range(0, 62).Select(_ => new UnitRule()));
    public OwnedList<TerrainRule> Terrain { get; init; } = new(Enumerable.Range(0, 33).Select(_ => new TerrainRule()));
    public OwnedList<GovernmentRule> Governments { get; init; } = new(Enumerable.Range(0, 7).Select(_ => new GovernmentRule()));
    public OwnedList<TribeRule> Tribes { get; init; } = new(Enumerable.Range(0, 21).Select(_ => new TribeRule()));
    public OwnedList<RuleName> Commodities { get; init; } = new(Enumerable.Repeat(RuleName.Empty, 16));
    public OwnedList<OrderRule> Orders { get; init; } = new(Enumerable.Range(0, 12).Select(_ => new OrderRule()));
    public OwnedList<RuleName> Difficulties { get; init; } = new(Enumerable.Repeat(RuleName.Empty, 6));
    public OwnedList<RuleName> DiplomaticAttitudes { get; init; } = new(Enumerable.Repeat(RuleName.Empty, 9));
}
public sealed record LeaderTitleOverride(Government Government, TitlePair Titles);
public sealed record RuleInput
{
    public RuleDefinitions Definitions { get; init; } = new();
    public OwnedList<int> MovementPoints { get; init; } = new(new int[62]);
    public OwnedList<OwnedList<LeaderTitleOverride>> TitleOverrides { get; init; } = new(Enumerable.Range(0, 21).Select(_ => new OwnedList<LeaderTitleOverride>([])));
    public OwnedList<AdvanceReference?> GovernmentAdvances { get; init; } = new(new AdvanceReference?[7]);
}

/// <summary>Mutable engine tables. Snapshot copies sequence storage for published observations.</summary>
public sealed class RuleTables
{
    public CosmicRules Cosmic { get; set; } = new();
    public AdvanceRule[] Advances { get; } = Enumerable.Range(0, 100).Select(_ => new AdvanceRule()).ToArray();
    public ushort EnabledAdvanceCount { get; set; }
    public ImprovementRule[] Improvements { get; } = Enumerable.Range(0, 67).Select(_ => new ImprovementRule()).ToArray();
    public AdvanceReference[] WonderObsolescence { get; } = new AdvanceReference[28];
    public UnitRule[] Units { get; } = Enumerable.Range(0, 62).Select(_ => new UnitRule()).ToArray();
    public TerrainRule[] Terrain { get; } = Enumerable.Range(0, 33).Select(_ => new TerrainRule()).ToArray();
    public GovernmentRule[] Governments { get; } = Enumerable.Range(0, 7).Select(_ => new GovernmentRule()).ToArray();
    public TribeRule[] Tribes { get; } = Enumerable.Range(0, 21).Select(_ => new TribeRule()).ToArray();
    public RuleName[] Commodities { get; } = Enumerable.Repeat(RuleName.Empty, 16).ToArray();
    public OrderRule[] Orders { get; } = Enumerable.Range(0, 12).Select(_ => new OrderRule()).ToArray();
    public RuleName[] Difficulties { get; } = Enumerable.Repeat(RuleName.Empty, 6).ToArray();
    public RuleName[] DiplomaticAttitudes { get; } = Enumerable.Repeat(RuleName.Empty, 9).ToArray();
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
