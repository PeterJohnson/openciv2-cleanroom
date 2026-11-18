using RandN.Rngs;

namespace OpenCiv2.Base.Randomness;

/// <summary>Logical replay position; never the layout of a package object.</summary>
public readonly record struct Pcg32Position(ulong Seed, ulong Stream, ulong Draws);

/// <summary>Owned raw source for the versioned PCG-XSH-RR 64/32 policy.</summary>
public sealed class Pcg32Source
{
    private readonly Pcg32 engine;
    private readonly ulong seed;
    private readonly ulong stream;
    private ulong draws;

    public Pcg32Source(ulong seed, ulong stream)
    {
        this.seed = seed;
        this.stream = stream & 0x7FFFFFFFFFFFFFFFUL;
        engine = Pcg32.Create(seed, this.stream);
    }

    public uint NextUInt32()
    {
        if (draws == ulong.MaxValue)
            throw new OverflowException("Random source draw counter exhausted.");
        uint value = engine.NextUInt32();
        ++draws;
        return value;
    }

    public Pcg32Position Capture() => new(seed, stream, draws);

    /// <summary>Reconstruct a position within an explicit host work budget.</summary>
    public static Pcg32Source Restore(Pcg32Position position, ulong replayLimit)
    {
        if (position.Stream > 0x7FFFFFFFFFFFFFFFUL || position.Draws > replayLimit)
            throw new ArgumentException("Invalid stream or checkpoint replay budget exceeded.", nameof(position));
        var restored = new Pcg32Source(position.Seed, position.Stream);
        for (ulong i = 0; i < position.Draws; ++i)
            restored.NextUInt32();
        return restored;
    }
}
