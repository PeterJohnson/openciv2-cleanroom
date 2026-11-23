using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A01 §3 power/history publication and one-sided coordination using actual V04/V07/V08 queries.</summary>
public sealed class PowerRanking(GameState world, GameplayRandom random)
{
    public void Publish(string call)
    {
        MapKnowledge.RequireLocal(world);
        unchecked
        {
            var powers = new int[EntityLimits.Civilizations];
            bool Objectives() => world.Globals.Status.HasScenario && world.Scenario.Flags.ObjectiveVictory;
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                if (world.Globals.CivsAlive.Contains(c))
                {
                    if (!Objectives())
                    {
                        powers[c] = 3 * world.Civilizations[c].NumAdvances + 8 * world.Civilizations[c].TotalCitySize + (world.Civilizations[c].Gold >> 5);
                    }
                    else
                    {
                        for (int i = 0; i < world.Entities.Cities.End; ++i)
                        {
                            if (world.Entities.Cities.Storage[i].Token.Occupied && world.Entities.Cities.Storage[i].Owner.Value == c)
                            {
                                powers[c] += 2 * CityObjectives.Value(world, new((ushort)i));
                            }
                        }
                    }
                }
                bool objectives = Objectives();
                int turn = world.Globals.Turn;
                if (turn < (objectives ? 75 : 600))
                {
                    int sample = (turn >> (objectives ? 1 : 2)) % GameState.PowerHistorySamples;
                    // A01/X01 permits omission of an invalid negative history destination.
                    if (sample >= 0)
                    {
                        world.PowerHistory[sample][c] = (byte)Arithmetic.ClampAB(objectives ? powers[c] : powers[c] >> 3, 0, 255);
                    }
                }
            }
            for (int rank = 7; rank > 0; --rank)
            {
                int best = -1, selected = 0;
                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                {
                    if (powers[c] > best)
                    { best = powers[c]; selected = c; }
                }

                world.Globals.PowerRank[selected] = (byte)rank;
                powers[selected] = -1;
            }
            world.Globals.Status.AiCoordination = false;
            world.Globals.CivByRank = default;
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                if (world.Globals.CivsAlive.Contains(c) && world.Globals.PowerRank[c] < EntityLimits.Civilizations)
                {
                    world.Globals.CivByRank[world.Globals.PowerRank[c]] = (byte)c;
                }
            }

            world.Globals.BestHumanRank = world.Globals.WorstHumanRank = 0;
            for (int rank = 7; rank >= 0; --rank)
            {
                if (world.Globals.HumanPlayers.Contains(world.Globals.CivByRank[rank]))
                { world.Globals.BestHumanRank = (byte)rank; break; }
            }

            for (int rank = 0; rank < EntityLimits.Civilizations; ++rank)
            {
                if (world.Globals.HumanPlayers.Contains(world.Globals.CivByRank[rank]))
                { world.Globals.WorstHumanRank = (byte)rank; break; }
            }

            Coordinate(call);
        }
    }

    private void Coordinate(string call)
    {
        int target = world.Globals.CivByRank[7];
        bool alert = world.Globals.HumanPlayers.Contains(target) && world.Civilizations[target].CityCount > 4
            && world.Civilizations[target].UnitsActive[UnitTypeIds.NuclearMissile] == 0 && world.Globals.Turn > 200 && world.Globals.Difficulty > Difficulty.Chieftain;
        if (world.Globals.Difficulty >= Difficulty.Emperor)
        {
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                if (world.Globals.HumanPlayers.Contains(c) && world.Civilizations[c].SpaceshipFlags.Launched)
                { target = c; alert = true; }
            }
        }

        if (!alert)
        {
            return;
        }

        world.Globals.Status.AiCoordination = true;
        int wars = 0;
        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        {
            if (world.Civilizations[target].Treaties[c].War)
            {
                ++wars;
            }
        }

        for (int actor = 1; actor < EntityLimits.Civilizations; ++actor)
        {
            if (!world.Globals.CivsAlive.Contains(actor) || world.Globals.HumanPlayers.Contains(actor) || world.Civilizations[actor].CityCount <= 1)
            {
                continue;
            }

            ref var civ = ref world.Civilizations[actor];
            if (!(3 * wars + 3 < world.Civilizations[target].Reputation || civ.Treaties[target].Vendetta || SpaceshipQueries.RaceActive(world)))
            {
                continue;
            }

            if (civ.Treaties[target].Alliance)
            {
                if (civ.SpaceshipFlags.Launched && world.Globals.Difficulty > Difficulty.King)
                {
                    civ.Treaties[target].HostileIntent = true;
                }
            }
            else if (civ.Treaties[target].Ceasefire || civ.Treaties[target].Peace)
            {
                if (random.PrimaryModulo(new ChoiceKey("A01.coordination", call, actor), 32) <= (byte)world.Globals.Difficulty || civ.SpaceshipFlags.Launched)
                {
                    civ.Treaties[target].HostileIntent = true;
                }
            }
            else
            {
                DiplomaticAttitudes.Adjust(world, actor, target, 1);
                civ.Treaties[target].Contact = true;
            }
        }
    }
}
