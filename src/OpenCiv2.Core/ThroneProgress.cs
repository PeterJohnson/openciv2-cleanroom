using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum ThroneUpgradeKind { Piece, Overlay }
public readonly record struct ThroneSelection(ThroneUpgradeKind Kind, int Index);
public sealed record ThroneRoomReport(int Civilization, short Progress, OwnedList<byte> Pieces, OwnedList<bool> Overlays);
public sealed record ThroneProgressResult(int Civilization, bool Awarded, short Progress, ThroneUpgrade? Upgrade);

/// <summary>P09 persistent throne progress using actual V08 score. F04 owns the old-turn
/// call; presentation does not calculate score or mutate the room.</summary>
public sealed class ThroneProgress(GameState world, CivilizationScore score)
{
    public int Threshold(int stage)
    {
        if (stage < 0)
        {
            return 1;
        }

        if (stage == int.MaxValue)
        { throw new ArgumentOutOfRangeException(nameof(stage), "The reference threshold loop cannot terminate at this stage"); }
        // No admitted operation interleaves with this query. Sum modulo 2^32 before signed
        // division; a wide, unwrapped final threshold would give different results.
        int triangular = unchecked((int)((long)stage * (stage + 1L) / 2));
        int sum = unchecked((7 - (byte)world.Globals.Difficulty) * triangular);
        return sum / 2 + 1;
    }

    public ThroneProgressResult Check(short accelerationSelection)
    {
        MapKnowledge.RequireLocal(world);
        int actor = world.Multiplayer.LocalHuman;
        if (world.Globals.Status.Scenario || world.Globals.Multiplayer == GameMode.Hotseat)
        { return Unchanged(); }
        bool palace = false;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (city.Token.Occupied && unchecked((sbyte)city.Owner.Value) == actor
                && BuildingQueries.HasBuilding(world, new((ushort)i), BuildingIds.Palace))
            { palace = true; break; }
        }
        if (!palace || world.Globals.Turn <= accelerationSelection * 20 + 1)
        { return Unchanged(); }
        int points = score.Calculate(actor);
        ref var civ = ref world.Civilizations[actor];
        if (points < Threshold(civ.ThroneRoomUpgrades + 1))
        { return Unchanged(); }
        civ.ThroneRoomUpgrades = unchecked((short)(civ.ThroneRoomUpgrades + 1));
        var upgrade = civ.ThroneRoomUpgrades <= 37 && world.Globals.GameOptions.Throne
            ? BeginUpgrade(world.Multiplayer.LocalHuman) : null;
        return new(actor, true, civ.ThroneRoomUpgrades, upgrade);
        ThroneProgressResult Unchanged() => new(actor, false, world.Civilizations[actor].ThroneRoomUpgrades, null);
    }

    public ThroneRoomReport View(int actor)
    {
        MapKnowledge.RequireLocal(world);
        ref var civ = ref world.Civilizations[actor];
        byte overlays = civ.ThroneRoom[8];
        return new(actor, civ.ThroneRoomUpgrades, new(((ReadOnlySpan<byte>)civ.ThroneRoom)[..8].ToArray()),
            new(Enumerable.Range(0, 7).Select(i => (overlays & (1 << i)) != 0)));
    }
    public ThroneUpgrade? BeginUpgrade(int actor)
    {
        MapKnowledge.RequireLocal(world);
        ref var room = ref world.Civilizations[actor].ThroneRoom;
        bool available = (room[8] & 127) != 127;
        for (int piece = 0; piece < 7; ++piece)
        {
            available |= room[piece] < 4;
        }

        return available ? new(world, actor) : null;
    }
}

/// <summary>P09 semantic draft. An accepted selection commits once; cancellation/disposal
/// do not consume a selector. Original paired-overlay pixel fallback is not a semantic action.</summary>
public sealed class ThroneUpgrade : IDisposable
{
    private readonly GameState world;
    private bool disposed;
    public int Civilization { get; }
    public bool Overlays { get; }
    public bool Completed { get; private set; }
    public ThroneSelection? Accepted { get; private set; }
    public bool ActorCurrent => world.Multiplayer.LocalHuman == Civilization;
    internal ThroneUpgrade(GameState world, int actor)
    {
        this.world = world;
        Civilization = actor;
        Overlays = true;
        for (int i = 0; i < 8; ++i)
        {
            Overlays &= world.Civilizations[actor].ThroneRoom[i] >= 4;
        }
    }
    public OwnedList<ThroneSelection> Choices()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        MapKnowledge.RequireLocal(world);
        if (Completed || !ActorCurrent)
        {
            return OwnedList<ThroneSelection>.Empty;
        }

        List<ThroneSelection> choices = [];
        ref var room = ref world.Civilizations[Civilization].ThroneRoom;
        for (int i = 0; i < (Overlays ? 7 : 8); ++i)
        {
            if (Overlays ? (room[8] & (1 << i)) == 0 : room[i] < 4)
            { choices.Add(new(Overlays ? ThroneUpgradeKind.Overlay : ThroneUpgradeKind.Piece, i)); }
        }
        return new(choices);
    }
    public bool Select(ThroneSelection selection)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Choices().Contains(selection))
        {
            return false;
        }

        ref var room = ref world.Civilizations[Civilization].ThroneRoom;
        if (Overlays)
        {
            room[8] |= (byte)(1 << selection.Index);
        }
        else
        {
            ++room[selection.Index];
        }

        Accepted = selection;
        Completed = true;
        return true;
    }
    public void Cancel() { ObjectDisposedException.ThrowIf(disposed, this); Completed = true; }
    public void Dispose() { disposed = true; }
}
