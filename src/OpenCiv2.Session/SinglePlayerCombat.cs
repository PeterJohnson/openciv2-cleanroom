using OpenCiv2.Base.Randomness;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Semantic content, publication and presentation ports. Decisions remain in the
/// retained operations and are answered through the owning session continuation.</summary>
public sealed record SinglePlayerCombatEffects(
    ICityCaptureEffects Capture,
    ICaptureProductionEffects Production,
    ICapturePartisansEffects Partisans,
    IParadropEffects Paradrop,
    INuclearEffects Nuclear,
    ICombatHistoryEffects History,
    ICombatRoundEffects Rounds,
    ICombatResolutionEffects Resolution,
    ICombatAftermathEffects Aftermath,
    ICombatContinuationEffects Continuation,
    Action<CombatTreatyNotice> Treaty,
    Action<CombatPromotionNotice> Promotion,
    Action<CombatOpeningPresentation> Opening,
    Action Publish);

/// <summary>Local U06/C09 combat and capture composition over the shared lifecycle,
/// research, diplomacy and production graph. U05 paradrop and A04 response use the same
/// capture and contact services. Rebuild with the world; execute on its session lane.</summary>
public sealed class SinglePlayerCombat
{
    public CombatWorkspace State { get; } = new();
    public CombatStrength Strength { get; }
    public CombatPreparation Preparation { get; }
    public CityCapture Capture { get; }
    public Paradrop Paradrop { get; }
    public AttackResponse Response { get; }
    public NuclearEffects Nuclear { get; }
    public CombatCasualties Casualties { get; }
    public CombatResolution Resolution { get; }
    public CombatAftermath Aftermath { get; }
    public CombatContinuation Continuation { get; }
    public CombatExecution Execution { get; }

    public SinglePlayerCombat(GameState world, RuleTables rules, GameplayRandom random,
        StackOperations stacks, UnitCargo cargo, SinglePlayerLifecycle lifecycle,
        SinglePlayerResearch research, SinglePlayerDiplomacy diplomacy, ProductionOrders production,
        WonderEffects wonders, SinglePlayerCombatEffects effects, WonderBuildRestrictions? restrictions = null)
    {
        Strength = new(world, rules, stacks, State);
        Preparation = new(world, rules, stacks, State);
        var capture = new CityCaptureServices(diplomacy.War, lifecycle.CivilWar,
            new(world, random, lifecycle.Cities, lifecycle.Elimination, lifecycle.Incidents),
            new(world, rules, stacks, lifecycle.Cities, lifecycle.Elimination, lifecycle.Incidents,
                production, effects.Production, restrictions),
            new(world, rules, lifecycle.Units, random, effects.Partisans),
            diplomacy.CapturedTechnology, lifecycle.Elimination, lifecycle.Incidents, production,
            wonders, new(world, rules));
        Capture = new(world, rules, stacks, random, capture, lifecycle.Triggers, effects.Capture);
        Paradrop = new(world, rules, stacks, cargo, lifecycle.Revelation, Capture, diplomacy.Attacks, random, effects.Paradrop);
        Response = new(world, rules, stacks, Paradrop);
        Nuclear = new(world, random, stacks, lifecycle.Loss, lifecycle.Elimination, lifecycle.Incidents, effects.Nuclear);
        Casualties = new(world, stacks, State, new(world, effects.History), lifecycle.Elimination,
            lifecycle.Incidents, lifecycle.Triggers);
        Resolution = new(world, rules, State, new(new(world, rules, random, effects.Promotion), Casualties,
            cargo, lifecycle.Cities, lifecycle.Elimination, lifecycle.Incidents, production,
            new(world, rules, stacks)), effects.Resolution);
        Aftermath = new(world, State, diplomacy.War, diplomacy.Allies, effects.Publish, effects.Aftermath);
        Continuation = new(world, rules, State, stacks, lifecycle.Elimination, lifecycle.Incidents,
            Nuclear, Response, effects.Continuation);
        Execution = new(world, State, new(Preparation,
            new(world, rules, State, random, diplomacy.Attacks, diplomacy.War, research.GovernmentPolicy, effects.Treaty),
            new(world, rules, State, stacks, cargo, effects.Publish, effects.Opening),
            new(world, rules, random, State, effects.Rounds), Resolution, Aftermath, Continuation));
    }
}
