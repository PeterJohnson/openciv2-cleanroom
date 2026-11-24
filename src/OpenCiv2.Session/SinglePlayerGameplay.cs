using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;

namespace OpenCiv2.Session;

public sealed record SinglePlayerGameplayContent(NameCatalog Names, CityPreferences CityPreferences,
    StartingPositions Starts, GameplayValues Values, MapCompatibility MapCompatibility,
    IScenarioMedia Media, Func<OwnedList<string>, OwnedList<string>> PrepareScenarioText,
    Func<CityMissionText?> MissionOptions, Func<RevoltOptionText?> RevoltOptions, string FoodName,
    IReadOnlyDictionary<int, string>? ExtraCommodityNames = null,
    IReadOnlyDictionary<int, CivilizationSet>? ExtraAdvancePresence = null,
    WonderBuildRestrictions? WonderRestrictions = null);

/// <summary>Basic deletion's presentation only. Civilization checks belong to the actual
/// lifecycle graph. AI production airlift uses the same deletion service.</summary>
public sealed record SinglePlayerDeletionPresentation(Action<MapPoint> UnitRemoved,
    Action<int> RefreshHomeCity, Action<MapPoint, CitySlot?> Synchronize);

public sealed record SinglePlayerGameplayEffects(
    IResearchGovernmentEffects ResearchGovernment, IResearchGrantEffects ResearchGrant, IEmissaryEffects Emissary,
    SinglePlayerDiplomacyEffects Diplomacy, SinglePlayerLifecycleEffects Lifecycle,
    SinglePlayerCombatEffects Combat, SinglePlayerMissionEffects Missions, SinglePlayerMovementEffects Movement,
    IUnitSelectionEffects Selection, SinglePlayerDeletionPresentation Deletion,
    Action<GovernmentChangeNotice> Government, Action<LeonardoUpgradeNotice> Upgrade,
    Action<ProductionWonderNotice> Production, Action<AiAirliftNotice> Airlift, Action SuppressWonders);

/// <summary>Concrete local gameplay construction from installed state, content and semantic
/// presentation. All gameplay children are actual services. Use retained operation entries;
/// synchronous player government/rate decisions cannot be answered by this graph.
/// Rebuild after replacing the world/rules. The owner supplies a stable unique call scope.</summary>
public sealed class SinglePlayerGameplay
{
    public StackOperations Stacks { get; }
    public UnitCargo Cargo { get; }
    public UnitDeletion Deletion { get; }
    public CityEconomy Economy { get; }
    public GovernmentChanges Governments { get; }
    public WonderEffects Wonders { get; }
    public ProductionOrders Production { get; }
    public CityProductionChoice ProductionChoice { get; }
    public SinglePlayerResearch Research { get; }
    public SinglePlayerDiplomacy Diplomacy { get; }
    public SinglePlayerLifecycle Lifecycle { get; }
    public SinglePlayerCombat Combat { get; }
    public SinglePlayerMissions Missions { get; }
    public SinglePlayerMovement Movement { get; }
    public UnitSelection Selection { get; }
    public UnitFocus Focus { get; }

    public SinglePlayerGameplay(GameState world, ProcessContext process, GameplayRandom random,
        SinglePlayerGameplayContent content, SinglePlayerGameplayEffects effects, string call,
        StackBudget? budget = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        var rules = process.Rules.Definitions;
        var stackBudget = budget ?? new(5_000_000, 64);
        int sequence = 0;
        string Next(string operation) => FormattableString.Invariant($"{call}/{operation}/{sequence++}");
        Stacks = new(world, stackBudget);
        Cargo = new(world, rules, Stacks);
        SinglePlayerLifecycle? lifecycle = null;
        Deletion = new(world, rules, stackBudget, new DeletionEffects(effects.Deletion,
            (owner, killer) => (lifecycle ?? throw new InvalidOperationException("Gameplay construction is incomplete"))
                .Elimination.Check(owner, killer, Next("deletion"))));
        Economy = new(world, process.Rules, Stacks);
        Governments = new(world, process.Rules, Stacks,
            _ => throw new InvalidOperationException("Player government selection requires its retained command"),
            _ => throw new InvalidOperationException("Player rate selection requires its retained command"), effects.Government);
        Wonders = new(world, rules, effects.Upgrade, effects.Lifecycle.RedrawTile);
        Research = new(world, process.Rules, random, Stacks, Governments, Wonders,
            effects.ResearchGovernment, effects.ResearchGrant, effects.Emissary);
        ProductionChoice = new(world, process.Rules, content.CityPreferences, Economy, Stacks,
            new(world, rules, content.Values.Spaceship, content.Values.Calendar),
            new(world, rules, Stacks, Deletion, random, effects.Airlift), content.WonderRestrictions);
        Production = new(world, rules, city => ProductionChoice.Choose(city, Next("production")), effects.Production);
        var permissions = new ScenarioPermissionActions(world,
            () => (lifecycle ?? throw new InvalidOperationException("Gameplay construction is incomplete")).Events);
        Diplomacy = new(world, process.Rules, random, Stacks, Cargo, Research, Production, permissions, effects.Diplomacy);
        var presentation = new ScenarioPresentationActions(world, content.Media, content.PrepareScenarioText, effects.SuppressWonders);
        Lifecycle = lifecycle = new(world, process, random, Stacks, Cargo, Deletion, content.Starts, Governments,
            Research, Diplomacy, permissions, new ScenarioTerrainEncoding(world.Map, content.MapCompatibility), presentation, effects.Lifecycle);
        Combat = new(world, rules, random, Stacks, Cargo, Lifecycle, Research, Diplomacy, Production, Wonders, effects.Combat,
            content.WonderRestrictions);
        Missions = new(world, rules, random, Stacks, Lifecycle, Research, Diplomacy, Combat,
            content.MissionOptions, content.RevoltOptions, effects.Missions);
        Movement = new(world, process, random, Stacks, Cargo, content.Names, Economy, Lifecycle, Research,
            Diplomacy, Combat, Missions, effects.Movement, content.FoodName, content.ExtraCommodityNames, content.ExtraAdvancePresence);
        Selection = new(world, rules, Combat.State, Lifecycle.Revelation, effects.Selection);
        Focus = new(world, rules, Selection, effects.Selection);
    }

    private sealed class DeletionEffects(SinglePlayerDeletionPresentation presentation, Action<int, int> check) : IUnitDeletionEffects
    {
        public void UnitRemoved(MapPoint position) => presentation.UnitRemoved(position);
        public void RefreshHomeCity(int city) => presentation.RefreshHomeCity(city);
        public void CheckCivilization(int owner, int killer) => check(owner, killer);
        public void SynchronizeDeletion(MapPoint position, CitySlot? home) => presentation.Synchronize(position, home);
    }
}
