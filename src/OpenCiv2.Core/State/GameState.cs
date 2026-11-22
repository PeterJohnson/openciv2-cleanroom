using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core.State;

/// <summary>F02 process ownership. Construction does not execute setup/default/load operations.</summary>
public sealed class ProcessContext
{
    public ProcessTokens Tokens { get; } = new();
    public RuleState Rules { get; } = new();
    public MultiplayerPreferences MultiplayerPreferences = new();
    public OrdinaryPreferences OrdinaryPreferences = new();
    public TextParameters TextParameters { get; private set; } = new();
    public TileCell Fallback { get; private set; } = new();
    public object PreferencesIdentity { get; private set; } = new();

    /// <summary>Install staged checkpoint contents without callbacks or allocation. Process, token and
    /// rule-provider identities remain stable. Old world/fallback/text references are invalidated.</summary>
    public void SwapContents(ProcessContext other)
    {
        ArgumentNullException.ThrowIfNull(other);
        (Tokens.NextUnit, other.Tokens.NextUnit) = (other.Tokens.NextUnit, Tokens.NextUnit);
        (Tokens.NextCity, other.Tokens.NextCity) = (other.Tokens.NextCity, Tokens.NextCity);
        Rules.SwapContents(other.Rules);
        (MultiplayerPreferences, other.MultiplayerPreferences) = (other.MultiplayerPreferences, MultiplayerPreferences);
        (OrdinaryPreferences, other.OrdinaryPreferences) = (other.OrdinaryPreferences, OrdinaryPreferences);
        (TextParameters, other.TextParameters) = (other.TextParameters, TextParameters);
        (Fallback, other.Fallback) = (other.Fallback, Fallback);
        (PreferencesIdentity, other.PreferencesIdentity) = (other.PreferencesIdentity, PreferencesIdentity);
    }
}
/// <summary>
/// An identity marker that distinguishes a world instance from replacements with otherwise equal values.
/// </summary>
public sealed class WorldLifetime;
/// <summary>Logical world and retained workspaces. Session owns replacement, authority and fresh-game reset.</summary>
public sealed class GameState
{
    public const int PowerHistorySamples = 150;
    /// <summary>
    /// Creates empty world storage bound to the process fallback tile; setup and loading remain explicit
    /// operations.
    /// </summary>
    public GameState(ProcessContext process)
    {
        ArgumentNullException.ThrowIfNull(process);
        Map = new(process.Fallback);
    }

    public WorldLifetime Lifetime { get; } = new();
    public EntityPools Entities { get; } = new();
    public UnitStackState UnitStacks { get; } = new();
    public UnitDeletionState UnitDeletion { get; } = new();
    public bool CityDeletionActive;
    public CivilizationArray<int> UnitTotals;
    public bool TurnLoopActive;
    public bool PlayerPhaseActive;
    public EndReason PendingEndReason;
    public MovementMode MovementMode;
    public GlobalValues Globals = new();
    public WorldSetup WorldSetup;
    public Civilization[] Civilizations { get; } = new Civilization[EntityLimits.Civilizations];
    public CivilizationNames[] Names { get; } = Enumerable.Range(0, EntityLimits.Civilizations).Select(_ => new CivilizationNames()).ToArray();
    public sbyte[] PendingLeaderOverrides { get; } = Enumerable.Repeat((sbyte)-1, EntityLimits.Civilizations).ToArray();
    public MultiplayerState Multiplayer = new();
    public EventState Events { get; } = new();
    public PowerHistoryArray<CivilizationArray<byte>> PowerHistory;
    public EliminationHistory Eliminations = new();
    public ScenarioParameters Scenario = new();
    public Layout Layout;
    public MapView[] Views { get; } = new MapView[EntityLimits.Civilizations];
    public CityMoodWorkspace Mood { get; } = new();
    public CityCalculationWorkspace Calculation { get; } = new();
    public GenerationPathWorkspace Paths { get; } = new();
    public MapStorage Map { get; }
}
