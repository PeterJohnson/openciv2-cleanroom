using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02 §4 retained logical state. No implicit census or diplomacy repair.</summary>
public record struct Civilization
{
    public const int CurrentGoalCapacity = 48, StandingGoalCapacity = 16;
    public CivilizationStatus Flags;
    public int Gold;
    public short Tribe, ResearchProgress, Researching, CapitalX, LastCityTurn;
    public byte NumAdvances, FutureTechs, ResearchCostAdvances, ScienceRate, TaxRate, DiploRandom;
    public Government Government;
    public Fixed4<byte> WondersByGroup;
    public short SubversionCount;
    public byte Reputation;
    public sbyte DiploRequestCount;
    public CivilizationArray<Relationship> Treaties;
    public CivilizationArray<byte> Attitude, AggressionCounter;
    public CivilizationArray<sbyte> ReputationCredit;
    public AdvanceSet Advances;
    public short MilitaryCount, CityCount, ShipCount, TotalCitySize;
    public ushort MilitaryStrength;
    public short TotalAttack, PopulationMilestone;
    public AdvanceArray<byte> AdvanceSource;
    public UnitTypeArray<byte> UnitsActive, UnitsLost, UnitsInProduction;
    public RegionArray<ushort> ContinentMilitary, ContinentAttack;
    public RegionArray<byte> ContinentCities, ContinentCitySize, ContinentTradeUnits, ContinentPolicy;
    public RegionArray<RegionalPresence> ContinentFlags;
    public short BestShieldSurplus;
    public GovernmentArray<short> GovernmentScore;
    public CivilizationArray<short> LastContactTurn;
    public byte LeaderPortrait;
    public Fixed10<byte> ThroneRoom;
    public short ThroneRoomUpgrades;
    public SpaceshipStatus SpaceshipFlags;
    public short SpaceshipArrivalYear, SpaceshipLaunchYear, SpaceshipPopulation;
    public Fixed6<short> SpaceshipParts;
    public CurrentGoalArray<CivilizationGoal> Goals;
    public StandingGoalArray<CivilizationGoal> StandingGoals;
}

/// <summary>F02 §4/F06 §3 logical fields. Scan selectors here do not install a running session.</summary>
public record struct GlobalValues
{
    public GameRules Rules;
    public GameOptions GameOptions;
    public TurnStatus TransientFlags;
    public MessageSuppression MessageOptions;
    public TutorialProgress ShownEventFlags;
    public GameStatus Status;
    public short Turn, Year, FirstLaunchTurn;
    public UnitSlot? ActiveUnit, MovingUnit;
    public GameMode Multiplayer;
    public sbyte PlayerCiv, SavedCurrentCiv, TurnCiv, ViewCiv;
    public byte RevealMap, BarbarianActivity;
    public Difficulty Difficulty;
    public CivilizationSet CivsAlive, HumanPlayers, CivsAtStart;
    public byte NumEnemies;
    public sbyte WarmingCounter, GlobalWarmings;
    public short WarmingPollution, PollutedTiles, PeaceTurns, UnitEnd, CityEnd, NumAdvances;
    public AdvanceArray<byte> FirstDiscoverer;
    public AdvanceArray<CivilizationSet> AdvanceKnownBy;
    // -1 not built, -2 destroyed, otherwise a city slot; construction is not a new-game initializer.
    public WonderArray<short> WonderCity;
    public short NextHistoryTurn;
    public byte BestHumanRank, WorstHumanRank;
    public CivilizationArray<byte> PowerRank, CivByRank;
}

public static class CivilizationCensus
{
    /// <summary>F06 §6 stage 17: rebuild only these two counters in civilization/slot order, modulo 256.</summary>
    public static void RebuildLoadedUnits(Span<Civilization> civilizations, EntityPools pools)
    {
        if (civilizations.Length != EntityLimits.Civilizations)
        {
            throw new ArgumentException("Loaded census requires eight civilizations", nameof(civilizations));
        }

        ArgumentNullException.ThrowIfNull(pools);
        for (int civ = 0; civ < EntityLimits.Civilizations; ++civ)
        {
            ref var current = ref civilizations[civ];
            current.UnitsActive = default;
            current.UnitsInProduction = default;
            foreach (ref readonly var unit in pools.Units.Scanned)
            {
                if (!unit.Token.Occupied || unit.Owner.Value != civ)
                {
                    continue;
                }

                int type = unit.Type.Value;
                if (type >= RuleLimits.UnitTypes)
                {
                    throw new ArgumentOutOfRangeException(nameof(pools), "Loaded unit census has no type backing");
                }

                current.UnitsActive[type] = unchecked((byte)(current.UnitsActive[type] + 1));
            }
            foreach (ref readonly var city in pools.Cities.Scanned)
            {
                if (!city.Token.Occupied || city.Owner.Value != civ || city.Production < 0)
                {
                    continue;
                }

                int type = city.Production;
                if (type >= RuleLimits.UnitTypes)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(pools),
                        "Loaded production census has no type backing");
                }

                current.UnitsInProduction[type] = unchecked((byte)(current.UnitsInProduction[type] + 1));
            }
        }
    }
}
