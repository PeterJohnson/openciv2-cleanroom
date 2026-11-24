using OpenCiv2.Base.Randomness;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed record SinglePlayerMissionEffects(
    ICityEspionageVisitEffects Visits,
    ICityEspionageDamageEffects Damage,
    ITechnologyTheftEffects Theft,
    ICityIncitementEffects Incitement,
    IUnitEspionageEffects Units,
    Action<PlantedNuclearNotice> Device,
    Action StopBlinking);

/// <summary>V06 local mission composition using the same retained lifecycle, research,
/// diplomacy, capture and nuclear services as movement/combat. Required option readers
/// supply authored content; decisions and fallbacks stay in the real mission operations.</summary>
public sealed class SinglePlayerMissions
{
    public CityEspionageVisits Visits { get; }
    public CityEspionageDamage Damage { get; }
    public CityTechnologyTheft Theft { get; }
    public PlantedNuclearDevice Device { get; }
    public CityIncitement Incitement { get; }
    public CityMissions Cities { get; }
    public UnitEspionage Units { get; }

    public SinglePlayerMissions(GameState world, RuleTables rules, GameplayRandom random, StackOperations stacks,
        SinglePlayerLifecycle lifecycle, SinglePlayerResearch research, SinglePlayerDiplomacy diplomacy,
        SinglePlayerCombat combat, Func<CityMissionText?> readMissions, Func<RevoltOptionText?> readRevolt,
        SinglePlayerMissionEffects effects)
    {
        Visits = new(world, lifecycle.Loss, lifecycle.Elimination, lifecycle.Incidents, effects.Visits);
        Damage = new(world, rules, random, lifecycle.Elimination, lifecycle.Incidents, effects.Damage);
        Theft = new(world, rules, random, stacks, research.Grants, diplomacy.CapturedTechnology,
            lifecycle.Elimination, lifecycle.Incidents, effects.Theft);
        Device = new(world, random, lifecycle.Elimination, lifecycle.Incidents, combat.Nuclear, effects.Device);
        Incitement = new(world, lifecycle.Elimination, lifecycle.Incidents, combat.Capture, readRevolt, effects.Incitement);
        Cities = new(world, rules, random, new(Visits, Theft, Damage, Device, Incitement), readMissions, effects.StopBlinking);
        Units = new(world, rules, stacks, lifecycle.Elimination, lifecycle.Incidents, effects.Units);
    }
}
