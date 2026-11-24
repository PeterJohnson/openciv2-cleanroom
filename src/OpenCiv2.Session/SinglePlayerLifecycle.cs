using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed record SinglePlayerLifecycleEffects(
    IUnitCreationEffects Creation,
    IUnitRevelationEffects Revelation,
    IUnitLossEffects Loss,
    ISpyEscapeEffects Escape,
    ICivilizationEliminationEffects Elimination,
    ICivilWarEffects CivilWar,
    Action<EspionageIncidentNotice> Incident,
    Action<CityTarget> CityRemoved,
    Action<MapPoint> RedrawTile,
    Action RedrawMaps);

/// <summary>Local lifecycle and scenario composition over the shared research/diplomacy graph.
/// Supply the same basic deletion service used by production. Bind the scenario permission
/// factory to Events after construction and before executing any operation. Creation keys
/// come from each owning operation; no hidden allocation counter or replacement RNG is used.</summary>
public sealed class SinglePlayerLifecycle
{
    private readonly GameState world;
    private readonly ProcessContext process;
    private readonly GameplayRandom random;
    private readonly StartingPositions hints;
    private readonly CivilizationCreationServices creation;
    public UnitRevelation Revelation { get; }
    public UnitCreation Units { get; }
    public UnitLoss Loss { get; }
    public SpyEscape Escape { get; }
    public EspionageIncidents Incidents { get; }
    public CivilizationElimination Elimination { get; }
    public CityRemoval Cities { get; }
    public CivilizationRemoval Civilizations { get; }
    public CivilWar CivilWar { get; }
    public ScenarioTerrain Terrain { get; }
    public ScenarioDispatcher Events { get; }
    public ScenarioTriggers Triggers { get; }

    public SinglePlayerLifecycle(GameState world, ProcessContext process, GameplayRandom random,
        StackOperations stacks, UnitCargo cargo, UnitDeletion deletion, StartingPositions hints,
        GovernmentChanges governments, SinglePlayerResearch research, SinglePlayerDiplomacy diplomacy,
        ScenarioPermissionActions permissions, IScenarioTerrainEncoding terrain,
        ScenarioPresentationActions presentation, SinglePlayerLifecycleEffects effects)
    {
        this.world = world;
        this.process = process;
        this.random = random;
        this.hints = hints;
        var rules = process.Rules.Definitions;
        Revelation = new(world, rules, stacks, diplomacy.Contact, effects.Revelation);
        Units = new(world, process, stacks, Revelation, effects.Creation);
        Loss = new(world, rules, stacks, cargo, deletion, effects.Loss);
        Escape = new(world, rules, stacks, Loss, random, effects.Escape);
        Incidents = new(world, random, diplomacy.War, research.Stance, governments, effects.Incident);
        creation = new(Units, deletion, research.Grants,
            (owner, type) => UnitAvailability.CanBuild(world, rules, owner, null, type));
        Elimination = new(world, deletion, Escape, Create, effects.Elimination);
        Cities = new(world, rules, stacks, Loss, effects.CityRemoved);
        Civilizations = new(world, rules, stacks, Elimination, Incidents, Create, effects.RedrawMaps);
        CivilWar = new(world, stacks, Create, permissions, effects.CivilWar);
        Terrain = new(world, stacks, Cities, Elimination, Incidents, Create, terrain,
            effects.RedrawTile, (_, _) => effects.RedrawMaps());
        Events = new(world, new(world, rules, Units), Terrain, new(world, diplomacy.War, research.Grants),
            new(world, Civilizations), presentation);
        Triggers = new(world, random, () => Events);
    }

    public CivilizationCreation Create(int owner, string call) =>
        new(world, process, owner, hints, random, call, creation);
}
