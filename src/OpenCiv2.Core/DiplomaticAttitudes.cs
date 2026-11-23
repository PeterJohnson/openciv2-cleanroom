using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>V04's selected evaluation pair and unclamped attitude. A new world has no selected pair.</summary>
public sealed class DiplomacyWorkspace
{
    public (int From, int To)? SelectedPair;
    public int Attitude;
    public CivilizationArray<bool> RecentPeace;
    public BorderIntrusionSummary Intrusions;
    public bool OpeningEvaluationActive, InitialEvaluationDone;
    public bool RaceThreat, OutranksTarget, TargetIsLeader, Aggressive, InitiallyAggressive, SuppressedAggression, DeclareWar;
    public int Fear, Demand, RequestMode, PeaceInclination, LastContact, NearbyUnits, CapturedCities;
    public int? WarTarget, StrongestEnemy, WeakestEnemy;
}

/// <summary>V04 §2 attitude/hostility primitives and §11 global reputation maintenance.
/// These operations do not establish contact or run an audience/AI policy.</summary>
public static class DiplomaticAttitudes
{
    public static int Get(GameState world, int from, int to) => world.Civilizations[from].Attitude[to];

    public static void Set(GameState world, int from, int to, int value)
    {
        if (world.Globals.Multiplayer == GameMode.Hotseat && world.Multiplayer.HotseatParticipants.Contains(from)
            && from != world.Multiplayer.LocalHuman)
        {
            return;
        }

        world.Civilizations[from].Attitude[to] = (byte)Arithmetic.ClampAB(value, 0, 100);
    }

    public static int Adjust(GameState world, int from, int to, int delta)
    {
        Set(world, from, to, unchecked(Get(world, from, to) + delta));
        if (world.Diplomacy.SelectedPair == (from, to))
        {
            world.Diplomacy.Attitude = unchecked(world.Diplomacy.Attitude + delta);
        }

        return Get(world, from, to);
    }

    public static int Category(int attitude) => attitude switch
    {
        < 0 => 0,
        <= 10 => 1,
        <= 25 => 2,
        <= 38 => 3,
        <= 61 => 4,
        <= 74 => 5,
        <= 89 => 6,
        <= 99 => 7,
        _ => 8
    };

    public static bool Hostile(GameState world, int from, int to)
    {
        var relation = world.Civilizations[from].Treaties[to];
        return relation.War || (!relation.Alliance && relation.Contact && !relation.Peace && Get(world, from, to) > 49);
    }

    public static void MaintainReputation(GameState world, RuleTables rules)
    {
        int period = 12 * (unchecked((byte)world.Globals.Difficulty) + 1);
        if (world.Globals.Turn % period != 0)
        {
            return;
        }

        for (int civ = 0; civ < EntityLimits.Civilizations; ++civ)
        {
            if ((world.Globals.Turn % (2 * period) == 0 || BuildingQueries.HasWonder(world, rules, civ, WonderIds.EiffelTower))
                && (world.Civilizations[civ].Reputation > 1 || BuildingQueries.HasWonder(world, rules, civ, WonderIds.EiffelTower))
                && world.Civilizations[civ].Reputation != 0)
            {
                --world.Civilizations[civ].Reputation;
                if (world.Globals.HumanPlayers.Contains(civ))
                {
                    for (int observer = 0; observer < EntityLimits.Civilizations; ++observer)
                    {
                        world.Civilizations[observer].ReputationCredit[civ] = unchecked((sbyte)Arithmetic.ClampAB(
                            world.Civilizations[observer].ReputationCredit[civ], 0, world.Civilizations[civ].Reputation));
                    }
                }
            }
        }
    }
}
