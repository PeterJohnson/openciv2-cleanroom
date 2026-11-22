using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public readonly record struct CivilizationDestroyedNotice(int Civilization, int Killer,
    string Adjective, string KillerName, byte Portrait);

public interface ICivilizationEliminationEffects
{
    void Destroyed(CivilizationDestroyedNotice notice);
    void SpaceshipLost(SpaceshipLossNotice notice);
    void RedrawMaps();
}

/// <summary>V01 §6 local last-city check, using real U01 deletion, V06 escape, V07 loss and V01 recreation.
/// Forced retirement and civil war are separate operations. Network request orchestration belongs to the session.</summary>
public sealed class CivilizationElimination(GameState world, UnitDeletion deletion, SpyEscape escape,
    Func<int, CivilizationCreation> recreate, ICivilizationEliminationEffects effects)
{
    public int Check(int civilization, int killer, string call)
    {
        if (civilization == 0)
        {
            return 0;
        }

        MapKnowledge.RequireLocal(world);
        ArgumentOutOfRangeException.ThrowIfNegative(civilization);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(civilization, EntityLimits.Civilizations);
        ArgumentException.ThrowIfNullOrEmpty(call);
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (city.Token.Occupied && unchecked((sbyte)city.Owner.Value) == civilization)
            {
                return 0;
            }
        }
        if (killer == -1)
        {
            SpaceshipLifecycle.Reset(world, civilization);
        }
        else
        {
            RecordDestruction(civilization, killer);
        }
        int bound = world.Entities.Units.End;
        for (int i = bound - 1; i >= 0; --i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied)
            {
                continue;
            }

            if (unchecked((sbyte)unit.Owner.Value) == civilization)
            {
                deletion.Erase(i, true);
            }
            else if (unit.Flags.SpyMission && unchecked((sbyte)unit.Counter) == civilization)
            {
                unit.Flags.SpyMission = false;
                world.Civilizations[civilization].Treaties[unchecked((sbyte)unit.Counter)].SpyChat = false;
                escape.Try(i, 0, unchecked((sbyte)unit.Counter), call + "/spy/" + i);
            }
        }
        if (world.Globals.HumanPlayers.Contains(civilization))
        {
            if (civilization == world.Multiplayer.LocalHuman)
            {
                world.PendingEndReason = EndReason.Defeat;
            }

            var passwords = world.Multiplayer.Saved.Passwords.ToArray();
            passwords[civilization] = string.Empty;
            world.Multiplayer.Saved.Passwords = new(passwords);
            if (world.Globals.HumanPlayers.With(civilization, false).Count == 0)
            {
                return 0;
            }
        }
        var formerOwner = CivilizationSet.Only(civilization);
        for (int x = 0; x < world.Map.Geometry.Width; ++x)
        {
            for (int y = 0; y < world.Map.Geometry.Height; ++y)
            {
                world.Map.Mutate(new TileChange.Revelation(new(x, y), formerOwner, false));
            }
        }
        if (world.Globals.Multiplayer == GameMode.Hotseat)
        {
            world.Globals.HumanPlayers = world.Globals.HumanPlayers.With(civilization, false);
        }
        var creation = recreate(civilization);
        while (creation.Advance(256) is null)
        { }
        if (world.Globals.Multiplayer != GameMode.Hotseat || world.Globals.CivsAlive.Contains(world.Multiplayer.LocalHuman))
        {
            effects.RedrawMaps();
        }
        return 1;
    }

    private void RecordDestruction(int civilization, int killer)
    {
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            effects.Destroyed(new(civilization, killer, world.Names[civilization].Adjective,
                world.Names[killer].Tribe, world.Civilizations[civilization].LeaderPortrait));
        }
        ref var history = ref world.Eliminations;
        if (history.Count < EliminationHistory.Capacity)
        {
            int index = history.Count++;
            history.Turns[index] = world.Globals.Turn;
            history.Killers[index] = unchecked((byte)killer);
            history.Portraits[index] = world.Civilizations[civilization].LeaderPortrait;
            var names = history.Names.ToArray();
            names[index] = world.Names[civilization].Tribe;
            history.Names = new(names);
        }
        SpaceshipLifecycle.Lost(world, civilization, effects.SpaceshipLost);
    }
}
