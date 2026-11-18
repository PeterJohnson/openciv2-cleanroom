using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

/// <summary>F06 standalone-map adapter. Bounded byte transfer, close-once ownership and no live mutation.</summary>
public sealed class ClassicPremadeWorldSource : IPremadeWorldSource
{
    private readonly IFileStorage storage;
    public ClassicPremadeWorldSource(IFileStorage storage) { ArgumentNullException.ThrowIfNull(storage); this.storage = storage; }
    public PremadeWorldInput? Load(string path)
    {
        using var input = storage.Read(path); if (input is null) return null;
        var bytes = new List<byte>();
        for (; ; )
        {
            var part = input.Item(65536);
            if (part.Prefix.Count > 65536 || part.Complete && part.Prefix.Count != 65536) throw new ArgumentException("Inconsistent map transfer");
            if ((long)bytes.Count + part.Prefix.Count > 256 * 1024 * 1024) throw new ArgumentException("Map exceeds admission limit");
            bytes.AddRange(part.Prefix); if (!part.Complete) break;
        }
        bool closed = input.Close(); if (!closed) return null;
        RawMapFile decoded;
        try { decoded = ClassicFiles.DecodeRawMap(bytes.ToArray()); }
        catch (DecodeException) { return null; }
        var starts = new StartingPositions(); for (int i = 0; i < 21; ++i) { starts.X[i] = decoded.StartX[i]; starts.Y[i] = decoded.StartY[i]; }
        return new(RestorationInput.Map(decoded), starts);
    }
}
