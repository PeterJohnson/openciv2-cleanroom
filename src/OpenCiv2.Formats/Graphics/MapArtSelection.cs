using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using OpenCiv2.Base;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Formats.Graphics;

public sealed record MapImageOverride(RgbaImage Pixels, ArtPoint? Marker = null, ArtPoint? Flag = null);
public sealed record MapArtReceipt(string Source, string Sha256);

/// <summary>F03/P03 map service selection. The selected classic scenario has precedence
/// over selected native art and stock. Sheet failures are reported before consulting the next candidate.</summary>
public sealed class MapArtSelection
{
    private readonly FrozenDictionary<string, MapImageOverride> images;
    private readonly FrozenDictionary<int, FactionColors> colors;

    private readonly record struct FactionColors(uint Label, uint Primary, uint Secondary);

    public OwnedList<MapArtReceipt> Sources { get; }
    public OwnedList<string> Diagnostics { get; }

    private MapArtSelection(Dictionary<string, MapImageOverride> images, Dictionary<int, FactionColors> colors,
        List<MapArtReceipt> sources, List<string> diagnostics)
    {
        this.images = images.ToFrozenDictionary(StringComparer.Ordinal);
        this.colors = colors.ToFrozenDictionary();
        Sources = new(sources);
        Diagnostics = new(diagnostics);
    }

    public MapImageOverride? Image(string key) => images.GetValueOrDefault(key);

    public uint Color(int role, uint defaultColor) =>
        colors.TryGetValue(role, out var selected) ? selected.Label : defaultColor;

    public (uint Primary, uint Secondary) ShieldColors(int role, uint defaultColor) =>
        colors.TryGetValue(role, out var selected)
            ? (selected.Primary, selected.Secondary)
            : (defaultColor, defaultColor);

    public static MapArtSelection Load(Func<string, Stream>? native = null, Func<string, Stream?>? scenario = null,
        Func<string, Stream?>? installation = null)
    {
        var images = new Dictionary<string, MapImageOverride>(StringComparer.Ordinal);
        var colors = new Dictionary<int, FactionColors>();
        var sources = new List<MapArtReceipt>();
        var diagnostics = new List<string>();
        var catalog = MapArtCatalog.Stock;
        if (native is not null)
        {
            Native(native, catalog, images, sources);
        }

        if (Resolve("TERRAIN1") is { } ground)
        {
            for (int i = 0; i < catalog.Terrain.Count; i++)
            {
                int y = 1 + 33 * i;
                var terrain = catalog.Terrain[i];
                Add(ground, terrain.Body, 1, y);
                Add(ground, terrain.FirstResource, 131, y);
                Add(ground, terrain.SecondResource, 196, y);
                for (int q = 0; q < terrain.Edge.Count; q++)
                {
                    images[terrain.Edge[q]] = new(
                        ground.BlendQuadrant(i == 10 ? 196 : 1, i == 10 ? 447 : y, catalog.EdgeOffsets[q]));
                }
            }
            Add(ground, catalog.Fog, 131, 447);
            for (int q = 0; q < catalog.FogEdges.Count; q++)
            {
                images[catalog.FogEdges[q]] = new(ground.BlendQuadrant(131, 447, catalog.EdgeOffsets[q]));
            }

            for (int i = 0; i < catalog.Roads.Count; i++)
            {
                Add(ground, catalog.Roads[i], 1 + 65 * i, 364);
                Add(ground, catalog.Rails[i], 1 + 65 * i, 397);
            }
            string[] improvements = [catalog.Improvements["irrigation"], catalog.Improvements["farmland"],
                catalog.Improvements["mine"], catalog.Improvements["pollution"], catalog.GrasslandShield,
                catalog.Improvements["village"]];
            for (int i = 0; i < improvements.Length; i++)
            {
                Add(ground, improvements[i], 456, 100 + 33 * i);
            }
        }
        if (Resolve("TERRAIN2") is { } overlays)
        {
            for (int i = 0; i < catalog.Rivers.Count; i++)
            {
                int x = 1 + 65 * (i % 8), y = 33 * (i / 8);
                Add(overlays, catalog.Rivers[i], x, 67 + y);
                Add(overlays, catalog.Relief[3][i], x, 133 + y);
                Add(overlays, catalog.Relief[4][i], x, 199 + y);
                Add(overlays, catalog.Relief[5][i], x, 265 + y);
            }
            for (int i = 0; i < 8; i++)
            {
                int x = 1 + 66 * i;
                Add(overlays, catalog.Coasts[0].Variants[i], x, 429);
                Add(overlays, catalog.Coasts[1].Variants[i], x + 33, 463);
                Add(overlays, catalog.Coasts[2].Variants[i], x, 446);
                Add(overlays, catalog.Coasts[3].Variants[i], x, 463);
            }
            for (int q = 0; q < 4; q++)
            {
                Add(overlays, catalog.Mouths[q], 1 + 65 * q, 331);
            }
        }
        if (Resolve("UNITS") is { } units)
        {
            for (int i = 0; i < catalog.Units.Count; i++)
            {
                var body = catalog.Units[i].Body;
                int x = 1 + 65 * (i % 9), y = 1 + 49 * (i / 9);
                Add(units, body, x, y, units.Anchor(x, y, 64, 48, 4, default));
            }
            Add(units, catalog.Shield, 586, 1);
        }
        if (Resolve("CITIES") is { } cities)
        {
            for (int style = 0; style < 6; style++)
            {
                int y = 39 + 49 * style;
                for (int size = 0; size < 4; size++)
                {
                    for (int wall = 0; wall <= 1; wall++)
                    {
                        int x = (wall == 0 ? 1 : 334) + 65 * size;
                        string key = catalog.Cities[8 * style + 2 * size + wall].Body;
                        Add(cities, key, x, y, cities.Anchor(x, y, 64, 48, 4, new(-1, -15)),
                            cities.Anchor(x, y, 64, 48, 3, default));
                    }
                }
            }
            Add(cities, catalog.Improvements["fortified"], 143, 423);
            Add(cities, catalog.Improvements["fortress"], 208, 423);
            Add(cities, catalog.Improvements["airbase"], 273, 423);
            for (int i = 0; i < catalog.Factions.Count; i++)
            {
                int x = 1 + 15 * i;
                var faction = catalog.Factions[i];
                var role = new FactionColors(cities.Rgb(x, 423), cities.Rgb(x + 3, 429), cities.Rgb(x + 6, 429));
                colors[i] = role;
                Add(cities, faction.BrightFlag, x, 425);
                Add(cities, faction.DarkFlag, x, 448);
                images[faction.OccupiedAirbase] = new(cities.Crop(338, 423, 64, 48,
                    primary: role.Primary, secondary: role.Secondary));
            }
        }
        return new(images, colors, sources, diagnostics);

        void Add(IndexedMapSheet sheet, string key, int x, int y, ArtPoint? marker = null, ArtPoint? flag = null)
        {
            var logical = catalog.Images[key].Logical;
            images[key] = new(sheet.Crop(x, y, logical.X, logical.Y), marker, flag);
        }

        IndexedMapSheet? Resolve(string sheet)
        {
            foreach (var (open, name, prefix) in new (Func<string, Stream?>?, string, string)[]
            {
                (scenario, sheet + ".BMP", "scenario"), (scenario, sheet + ".GIF", "scenario"),
                (installation, sheet + ".GIF", "installation")
            })
            {
                if (open is null)
                {
                    continue;
                }

                using var stream = open(name);
                if (stream is null)
                {
                    continue;
                }

                string source = $"{prefix}/{name}";
                try
                {
                    var bytes = Read(stream, IndexedMapSheet.MaxBytes);
                    // File selection preserves the admitted format, regardless of bytes under a different suffix.
                    if (name.EndsWith(".BMP", StringComparison.Ordinal) != bytes.AsSpan().StartsWith("BM"u8))
                    {
                        throw new InvalidDataException("Scenario image does not match the selected sheet format");
                    }

                    var decoded = IndexedMapSheet.Decode(bytes);
                    sources.Add(new(source, Hash(bytes)));
                    return decoded;
                }
                catch (InvalidDataException error)
                {
                    diagnostics.Add($"{source}: {error.Message}");
                }
            }
            return null;
        }
    }

    private static void Native(
        Func<string, Stream> open,
        MapArtCatalog catalog,
        Dictionary<string, MapImageOverride> images,
        List<MapArtReceipt> sources)
    {
        byte[] metadata;
        using (var stream = open("map-art.json"))
        {
            metadata = Read(stream, 512 * 1024);
        }

        using var json = JsonDocument.Parse(metadata);
        var root = json.RootElement;
        RequireFields(root, ["format", "images"]);
        if (Text(root, "format") != "openciv2-map-art/1")
        {
            throw new InvalidDataException("Unknown native map art format");
        }

        var imageTable = root.GetProperty("images");
        if (imageTable.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Native map art images must be a named object");
        }

        var bindings = new HashSet<string>(StringComparer.Ordinal);
        long pixels = 0;
        foreach (var image in imageTable.EnumerateObject())
        {
            if (!catalog.Images.TryGetValue(image.Name, out var definition) || !bindings.Add(image.Name))
            {
                throw new InvalidDataException($"Unknown or duplicate map art binding '{image.Name}'");
            }

            var item = image.Value;
            RequireFields(item, ["path", "sha256"], "marker", "flag");
            string path = Text(item, "path"), expected = Text(item, "sha256");
            ContentPaths.ValidateRelative(path);
            using var stream = open(path);
            byte[] bytes = Read(stream, 16 * 1024 * 1024);
            string hash = Hash(bytes);
            if (hash != expected)
            {
                throw new InvalidDataException($"Native map art hash mismatch for '{path}'");
            }

            var (width, height) = AtlasPng.Dimensions(bytes);
            if (width % definition.Logical.X != 0 || height % definition.Logical.Y != 0 ||
                width / definition.Logical.X != height / definition.Logical.Y)
            {
                throw new InvalidDataException($"Map art '{image.Name}' has incorrect logical registration");
            }

            pixels += (long)width * height;
            if (pixels > 24_000_000)
            {
                throw new InvalidDataException("Selected native map theme exceeds its decoded pixel budget");
            }

            images.Add(image.Name, new(AtlasPng.Decode(bytes), Anchor(item, "marker"), Anchor(item, "flag")));
            sources.Add(new(path, hash));
        }
        sources.Add(new("map-art.json", Hash(metadata)));
    }

    private static string Text(JsonElement item, string name)
    {
        var value = item.GetProperty(name);
        return value.ValueKind == JsonValueKind.String ? value.GetString()! :
            throw new InvalidDataException($"Native map art field '{name}' must be a string");
    }

    private static void RequireFields(JsonElement node, string[] required, params string[] optional)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Native map art entry must be an object");
        }

        var all = new HashSet<string>(required.Concat(optional), StringComparer.Ordinal);
        foreach (var property in node.EnumerateObject())
        {
            if (!all.Remove(property.Name))
            {
                throw new InvalidDataException($"Unknown or duplicate map art field '{property.Name}'");
            }
        }

        foreach (string name in required)
        {
            if (all.Contains(name))
            {
                throw new InvalidDataException($"Missing native map art field '{name}'");
            }
        }
    }

    private static ArtPoint? Anchor(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var anchor))
        {
            return null;
        }

        if (anchor.ValueKind != JsonValueKind.Array || anchor.GetArrayLength() != 2 ||
            anchor[0].ValueKind != JsonValueKind.Number || !anchor[0].TryGetInt32(out int x) ||
            anchor[1].ValueKind != JsonValueKind.Number || !anchor[1].TryGetInt32(out int y))
        {
            throw new InvalidDataException("Map art anchor requires two integer coordinates");
        }

        if (x is < -16 or > 64 || y is < -16 or > 48)
        {
            throw new InvalidDataException("Map art anchor is outside the registered box");
        }

        return new(x, y);
    }

    private static byte[] Read(Stream stream, int limit)
    {
        using var buffer = new MemoryStream();
        byte[] block = new byte[64 * 1024];
        for (int count; (count = stream.Read(block, 0, block.Length)) != 0;)
        {
            if (count > limit - buffer.Length)
            {
                throw new InvalidDataException("Map art exceeds its input byte budget");
            }

            buffer.Write(block, 0, count);
        }
        return buffer.ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
