using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>F04 §6 steps 1 and 8. Owns pre-upkeep presentation counts and the sum comparison,
/// including loss of human control or loss of the ship during intervening gameplay.</summary>
public sealed class SpaceshipUpkeep
{
    private readonly GameState world;
    private readonly int owner, before;
    private bool? requested;
    public OwnedList<short> BeforeParts { get; }
    public SpaceshipUpkeep(GameState world, int owner)
    {
        this.world = world;
        this.owner = owner;
        BeforeParts = Capture(world, owner);
        if (world.Globals.HumanPlayers.Contains(owner) && world.Civilizations[owner].SpaceshipFlags.Exists)
        { before = BeforeParts.Sum(p => (int)p); }
    }
    public bool NeedsPresentation()
    {
        if (requested is { } prior)
        { return prior; }
        int difference = before;
        if (world.Globals.HumanPlayers.Contains(owner) && world.Civilizations[owner].SpaceshipFlags.Exists)
        { difference -= Capture(world, owner).Sum(p => (int)p); }
        requested = difference != 0;
        return requested.Value;
    }
    public static OwnedList<short> Capture(GameState world, int owner)
    {
        if (owner < 0 || owner >= EntityLimits.Civilizations)
        { throw new ArgumentOutOfRangeException(nameof(owner)); }
        var parts = new short[6];
        for (int p = 0; p < parts.Length; ++p)
        { parts[p] = world.Civilizations[owner].SpaceshipParts[p]; }
        return new(parts);
    }
}
