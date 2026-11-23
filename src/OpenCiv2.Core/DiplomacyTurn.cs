using OpenCiv2.Base.Randomness;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>Required local V04/A08 children. No default negotiation or AI policy is supplied.</summary>
public interface IDiplomacyTurnEffects
{
    void ExchangeAdvances(int from, int to);
    void Meet(int from, int to);
    void WarEnded(int from, int to);
    void ReviewRelation(int from, int human);
    void MilitaryAid(int owner);
    void SeekWarAlly(int from, int human);
}

/// <summary>V04 §11 local continuation after the completed V02 government prefix.
/// Actual AI policy/audience children belong to their owners; human maintenance needs none.</summary>
public sealed class DiplomacyTurn(GameState world, RuleTables rules, GameplayRandom random, IDiplomacyTurnEffects effects)
{
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);

    public void Run(int owner, string call)
    {
        MapKnowledge.RequireLocal(world);
        if (owner == 0)
        {
            return;
        }
        world.Civilizations[owner].DiploRandom = (byte)random.PrimaryModulo(new ChoiceKey("V04.turn-random", call), 100);
        if (random.PrimaryModulo(new ChoiceKey("V04.turn-senate", call), 3) == 0)
        {
            world.Civilizations[owner].Flags.SenateActive = !world.Civilizations[owner].Flags.SenateActive;
        }
        if (world.Globals.Turn % 3 == 0 && world.Civilizations[owner].DiploRequestCount != 0)
        {
            world.Civilizations[owner].DiploRequestCount = unchecked((sbyte)(world.Civilizations[owner].DiploRequestCount - 1));
        }
        for (int other = 1; other < 8; ++other)
        {
            ref var relation = ref world.Civilizations[owner].Treaties[other];
            world.Diplomacy.RecentPeace[other] = relation.ChangedThisTurn;
            relation.ChangedThisTurn = relation.WarThisTurn = relation.MetThisTurn = false;
            if (!world.Globals.CivsAlive.Contains(other) || other == owner || Human(owner))
            {
                continue;
            }
            if (relation.HostileIntent)
            {
                int n = Math.Clamp(3 - world.Civilizations[other].Reputation / 4, 1, 3);
                int roll = n == 1 ? 0 : random.PrimaryModulo(new ChoiceKey("V04.turn-hostile", $"{call}/{other}"), n);
                if (roll == 0)
                {
                    if (!relation.Alliance)
                    {
                        relation.Ceasefire = relation.Peace = relation.HostileIntent = false;
                        relation.BrokenTreaty = relation.Aggressor = relation.ExtendedAggressor = true;
                        world.Civilizations[other].LastContactTurn[owner] = unchecked((short)Math.Max(
                            world.Civilizations[other].LastContactTurn[owner], world.Globals.Turn - 8));
                    }
                    else
                    {
                        DiplomaticAttitudes.Adjust(world, owner, other, 100);
                        world.Civilizations[other].LastContactTurn[owner] = -1;
                        relation.HostileIntent = false;
                    }
                }
            }
            if (Human(other))
            {
                continue;
            }
            relation.Tribute = false;
            if (world.Globals.Turn % 32 == 0)
            {
                if (!relation.ExtendedAggressor)
                {
                    relation.Aggressor = false;
                }
                relation.ExtendedAggressor = false;
            }
            if (world.Globals.Turn % 16 == 0)
            {
                if (!relation.RecentContact)
                {
                    StaleContact(owner, other);
                }
                relation.RecentContact = false;
            }
            else if (world.Globals.Turn % 8 == 0 && relation.Alliance && !relation.RecentContact)
            {
                effects.ExchangeAdvances(owner, other);
            }
        }
        if (!Human(owner))
        {
            for (int other = 1; other < 8; ++other)
            {
                if (Human(other) && !world.Multiplayer.Saved.AutoplayActive && world.Civilizations[owner].Treaties[other].Contact)
                {
                    if (other == world.Multiplayer.LocalHuman)
                    {
                        effects.ReviewRelation(owner, other);
                    }
                }
            }
            effects.MilitaryAid(owner);
            for (int other = 1; other < 8; ++other)
            {
                if (Human(other) && other == world.Multiplayer.LocalHuman)
                {
                    effects.SeekWarAlly(owner, other);
                }
            }
        }
    }

    private void StaleContact(int owner, int other)
    {
        if (!world.Civilizations[owner].Treaties[other].Alliance)
        {
            if (!world.Civilizations[owner].Treaties[other].Peace || !world.Globals.Status.AiCoordination)
            {
                var relation = world.Civilizations[owner].Treaties[other];
                if (relation.Contact && relation.War && !relation.Aggressor
                    && !world.Civilizations[other].Treaties[owner].Aggressor
                    && !Human(owner) && !Human(other) && Observed(owner, other)
                    && !world.Multiplayer.Saved.AutoplayActive)
                {
                    effects.WarEnded(owner, other);
                }
            }
            else
            {
                effects.Meet(owner, other);
            }
        }
        else
        {
            effects.ExchangeAdvances(owner, other);
        }
        var live = world.Civilizations[owner].Treaties[other];
        if (live.Contact && !live.Aggressor && !world.Civilizations[other].Treaties[owner].Aggressor
            && (!Human(owner) || !Human(other)))
        {
            DiplomaticRelations.Clear(world, owner, other, new() { Contact = true });
        }
    }

    private bool Observed(int owner, int other)
    {
        int observer = world.Multiplayer.LocalHuman;
        bool Wonder(int wonder) => BuildingQueries.HasWonder(world, rules, observer, wonder);
        return world.Civilizations[observer].Treaties[owner].Embassy
            || Wonder(WonderIds.UnitedNations) || Wonder(WonderIds.MarcoPolo)
            || world.Civilizations[observer].Treaties[other].Embassy
            || Wonder(WonderIds.UnitedNations) || Wonder(WonderIds.MarcoPolo) || world.Globals.RevealMap != 0;
    }
}
