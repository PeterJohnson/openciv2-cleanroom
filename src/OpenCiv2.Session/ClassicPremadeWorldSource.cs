using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

/// <summary>F06 standalone-map adapter. Bounded byte transfer, close-once ownership and no live mutation.</summary>
public sealed class ClassicPremadeWorldSource : IPremadeWorldSource
{
    private readonly IFileStorage storage;
    public MapCompatibility? MapBytes { get; private set; }

    internal void PrepareNewWorld() => MapBytes = null;

    private sealed record ImportedMap(MapInput Map, StartingPositions Starts,
        OwnedList<StoredTile> Tiles, ClassicPremadeWorldSource Source) : PremadeWorldInput(Map, Starts)
    {
        public override void Install(MapStorage destination)
        {
            base.Install(destination);
            // F05 §3 clears radius metadata but retains every terrain bit when starting from a raw map.
            Source.MapBytes = new(destination, Tiles.Select(tile => tile with { RadiusOther = 0 }));
        }
    }

    public ClassicPremadeWorldSource(IFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        this.storage = storage;
    }

    public PremadeWorldInput? Load(string path)
    {
        using var input = storage.Read(path);
        if (input is null)
        {
            return null;
        }

        var bytes = new List<byte>();
        for (; ; )
        {
            var part = input.Item(65536);
            if (part.Prefix.Count > 65536 || part.Complete && part.Prefix.Count != 65536)
            {
                throw new ArgumentException("Inconsistent map transfer");
            }

            if ((long)bytes.Count + part.Prefix.Count > 256 * 1024 * 1024)
            {
                throw new ArgumentException("Map exceeds admission limit");
            }

            bytes.AddRange(part.Prefix);
            if (!part.Complete)
            {
                break;
            }
        }
        bool closed = input.Close();
        if (!closed)
        {
            return null;
        }

        RawMapFile decoded;
        try
        {
            decoded = ClassicFiles.DecodeRawMap(bytes.ToArray());
        }
        catch (DecodeException)
        {
            return null;
        }
        var starts = new StartingPositions();
        for (int i = 0; i < RuleLimits.Tribes; ++i)
        {
            starts.Positions[i] = starts.Positions[i] with { X = decoded.StartX[i] };
            starts.Positions[i] = starts.Positions[i] with { Y = decoded.StartY[i] };
        }
        return new ImportedMap(RestorationInput.Map(decoded), starts, decoded.Tiles, this);
    }
}
