using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed record SinglePlayerMovementEffects(
    IMovementDispatcherEffects Dispatcher,
    ILandEntryEffects Land,
    ICaravanTradeEffects Caravans,
    IUnitExpulsionEffects Expulsion,
    IForeignUnitEffects ForeignUnit,
    ITransportMovementEffects Transport,
    IForeignCityEffects ForeignCity,
    IMovementRelocationEffects Relocation,
    IMovementCompletionEffects Completion,
    ISpecialMovementEffects Special,
    IHutEffects Huts,
    Action TooManyCities);

/// <summary>U02 local movement graph over the shared retained gameplay services.
/// The admitting caller supplies meaningful viewer/entry inputs and authored names.
/// Actual movement, combat, missions, contact and rewards retain their own decisions.</summary>
public sealed class SinglePlayerMovement
{
    public CityCreation Cities { get; }
    public CaravanTrade Caravans { get; }
    public HutEntry Huts { get; }
    public UnitExpulsion Expulsion { get; }
    public LandEntry Land { get; }
    public ForeignUnitMovement ForeignUnit { get; }
    public TransportMovement Transport { get; }
    public ForeignCityMovement ForeignCity { get; }
    public MovementCharge Charge { get; }
    public MovementRelocation Relocation { get; }
    public SpecialMovementFinish Special { get; }
    public MovementCompletion Completion { get; }
    public MovementDispatcher Dispatcher { get; }

    public SinglePlayerMovement(GameState world, ProcessContext process, GameplayRandom random,
        StackOperations stacks, UnitCargo cargo, NameCatalog names, CityEconomy economy,
        SinglePlayerLifecycle lifecycle, SinglePlayerResearch research, SinglePlayerDiplomacy diplomacy,
        SinglePlayerCombat combat, SinglePlayerMissions missions, SinglePlayerMovementEffects effects,
        string foodName, IReadOnlyDictionary<int, string>? extraCommodityNames = null,
        IReadOnlyDictionary<int, CivilizationSet>? extraAdvancePresence = null)
    {
        var rules = process.Rules.Definitions;
        Cities = new(world, process, names, lifecycle.Units, effects.TooManyCities);
        Caravans = new(world, rules, random, economy, lifecycle.Elimination, lifecycle.Incidents,
            effects.Caravans, foodName, extraCommodityNames);
        Huts = new(world, rules, random, lifecycle.Units, Cities, research.Grants, effects.Huts, extraAdvancePresence);
        Expulsion = new(world, rules, stacks, cargo, random, effects.Expulsion);
        Land = new(world, rules, stacks, cargo, new(Caravans, missions.Cities, missions.Units), effects.Land);
        ForeignUnit = new(world, rules, stacks, Expulsion, combat.Execution, effects.ForeignUnit);
        Transport = new(world, rules, stacks, cargo, effects.Transport);
        ForeignCity = new(world, rules, diplomacy.Attacks, combat.Nuclear, lifecycle.Elimination,
            lifecycle.Loss, lifecycle.Incidents, effects.ForeignCity);
        Charge = new(world, rules, stacks, random);
        Relocation = new(world, rules, stacks, cargo, effects.Relocation);
        Special = new(world, rules, stacks, random, lifecycle.Elimination, lifecycle.Loss, lifecycle.Incidents, effects.Special);
        Completion = new(world, rules, stacks, new(combat.Capture, lifecycle.Revelation, Special, Huts, Transport, new(world)), effects.Completion);
        Dispatcher = new(world, rules, stacks, new(Land, ForeignUnit, Transport, ForeignCity, Charge, Relocation, Completion), effects.Dispatcher);
    }
}
