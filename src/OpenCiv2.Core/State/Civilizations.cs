using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>F02 §4 retained logical state. No implicit census or diplomacy repair.</summary>
public record struct Civilization
{
    public ushort Flags;
    public int Gold;
    public short Tribe, ResearchProgress, Researching, CapitalX, LastCityTurn;
    public byte NumAdvances, FutureTechs, ResearchCostAdvances, ScienceRate, TaxRate, DiploRandom;
    public Government Government;
    public Fixed4<byte> WondersByGroup;
    public short SubversionCount;
    public byte Reputation;
    public sbyte DiploRequestCount;
    public Fixed8<uint> Treaties;
    public Fixed8<byte> Attitude, AggressionCounter;
    public Fixed8<sbyte> ReputationCredit;
    public Fixed13<byte> Advances;
    public short MilitaryCount, CityCount, ShipCount, TotalCitySize;
    public ushort MilitaryStrength;
    public short TotalAttack, PopulationMilestone;
    public Fixed100<byte> AdvanceSource;
    public Fixed62<byte> UnitsActive, UnitsLost, UnitsInProduction;
    public Fixed64<ushort> ContinentMilitary, ContinentAttack;
    public Fixed64<byte> ContinentCities, ContinentCitySize, ContinentTradeUnits, ContinentFlags, ContinentPolicy;
    public short BestShieldSurplus;
    public Fixed7<short> GovernmentScore;
    public Fixed8<short> LastContactTurn;
    public byte LeaderPortrait;
    public Fixed10<byte> ThroneRoom;
    public short ThroneRoomUpgrades;
    public byte SpaceshipFlags;
    public short SpaceshipArrivalYear, SpaceshipLaunchYear, SpaceshipPopulation;
    public Fixed6<short> SpaceshipParts;
    public Fixed48<CivilizationGoal> Goals;
    public Fixed16<CivilizationGoal> StandingGoals;
}

/// <summary>F02 §4/F06 §3 logical fields. Scan selectors here do not install a running session.</summary>
public record struct GlobalValues
{
    public GlobalValues()
    {
        ActiveUnit = new();
        MovingUnit = new();
    }

    public ushort Rules;
    public uint GameOptions;
    public ushort TransientFlags, GameFlags, MessageOptions, ShownEventFlags;
    public short Turn, Year, FirstLaunchTurn;
    public UnitLink ActiveUnit, MovingUnit;
    public GameMode Multiplayer;
    public sbyte PlayerCiv, SavedCurrentCiv, TurnCiv, ViewCiv;
    public byte RevealMap, BarbarianActivity;
    public Difficulty Difficulty;
    public byte CivsAlive, HumanPlayers, CivsAtStart, NumEnemies;
    public sbyte WarmingCounter, GlobalWarmings;
    public short WarmingPollution, PollutedTiles, PeaceTurns, UnitEnd, CityEnd, NumAdvances;
    public Fixed100<byte> FirstDiscoverer, AdvanceKnownBy;
    // -1 not built, -2 destroyed, otherwise a city slot; construction is not a new-game initializer.
    public Fixed28<short> WonderCity;
    public short NextHistoryTurn;
    public byte BestHumanRank, WorstHumanRank;
    public Fixed8<byte> PowerRank, CivByRank;
}

public static class CivilizationCensus
{
    /// <summary>F06 §6 stage 17: rebuild only these two counters in civilization/slot order, modulo 256.</summary>
    public static void RebuildLoadedUnits(Span<Civilization> civilizations, EntityPools pools)
    {
        if (civilizations.Length != 8)
        {
            throw new ArgumentException("Loaded census requires eight civilizations", nameof(civilizations));
        }

        ArgumentNullException.ThrowIfNull(pools);
        for (int civ = 0; civ < 8; ++civ)
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
                if (type >= 62)
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
                if (type >= 62)
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
