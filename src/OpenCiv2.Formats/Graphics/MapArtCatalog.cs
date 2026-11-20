using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using OpenCiv2.Base;

namespace OpenCiv2.Formats.Graphics;

public readonly record struct ArtPoint(int X, int Y);
public sealed record MapImage(ArtPoint Logical, ArtPoint Physical, string Sha256);
public sealed record TerrainArt(string Body, string FirstResource, string SecondResource, OwnedList<string> Edge);
public sealed record CoastArt(ArtPoint Offset, OwnedList<string> Variants);
public sealed record UnitArt(string Body, ArtPoint Shield);
public sealed record CityArt(string Body, ArtPoint Population);
public sealed record FactionArt(uint Rgb, string BrightFlag, string DarkFlag, string OccupiedAirbase);

/// <summary>P03/P02 stock semantic bindings. Only the compiled catalog selects art from the pinned release.</summary>
public sealed class MapArtCatalog
{
    public static MapArtCatalog Stock { get; } = ReadStock();
    public FrozenDictionary<string, MapImage> Images { get; }
    public OwnedList<TerrainArt> Terrain { get; }
    public OwnedList<CoastArt> Coasts { get; }
    public FrozenDictionary<int, OwnedList<string>> Relief { get; }
    public OwnedList<string> Rivers { get; }
    public OwnedList<string> Mouths { get; }
    public OwnedList<string> Roads { get; }
    public OwnedList<string> Rails { get; }
    public OwnedList<string> FogEdges { get; }
    public string Fog { get; }
    public string GrasslandShield { get; }
    public OwnedList<ArtPoint> EdgeOffsets { get; }
    public FrozenDictionary<string, string> Improvements { get; }
    public OwnedList<UnitArt> Units { get; }
    public UnitArt Leader { get; }
    public string Shield { get; }
    public OwnedList<CityArt> Cities { get; }
    public string Disorder { get; }
    public ArtPoint FlagAnchor { get; }
    public OwnedList<FactionArt> Factions { get; }
    public FrozenDictionary<string, string> Overlays { get; }
    public OwnedList<string> Explosion { get; }
    public OwnedList<string> Nuclear { get; }
    public FrozenDictionary<string, uint> DisplayColors { get; }

    private MapArtCatalog(JsonElement root)
    {
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException("Unsupported stock map art catalog");
        }

        var registration = root.GetProperty("registration");
        var diagonals = Strings(registration.GetProperty("diagonalOrder"));
        var directions = Strings(registration.GetProperty("routeOrder"));
        Images = root.GetProperty("images").EnumerateObject().ToFrozenDictionary(
            p => p.Name,
            p =>
            new MapImage(
                Point(p.Value.GetProperty("logicalSize")),
                Point(p.Value.GetProperty("physicalSize")),
                Read(p.Value, "pngSha256")),
            StringComparer.Ordinal);
        Terrain = new(root.GetProperty("terrain").EnumerateArray().Select(t =>
        {
            var resources = t.GetProperty("specialResources");
            return new TerrainArt(Read(t, "base"), Read(resources[0], "atlasKey"), Read(resources[1], "atlasKey"),
                ForDirections(t.GetProperty("edgeBlends"), diagonals));
        }));
        GrasslandShield = Read(root, "grasslandShield");
        Coasts = new(root.GetProperty("coasts").EnumerateArray().Select(row =>
            new CoastArt(Point(row.GetProperty("offset")), Bindings(row.GetProperty("variants")))));
        var relief = root.GetProperty("connectedTerrain");
        Relief = new Dictionary<int, OwnedList<string>>
        {
            [3] = Bindings(relief.GetProperty("forest")),
            [4] = Bindings(relief.GetProperty("hills")),
            [5] = Bindings(relief.GetProperty("mountains"))
        }.ToFrozenDictionary();
        var rivers = root.GetProperty("rivers");
        Rivers = Bindings(rivers.GetProperty("connections"));
        Mouths = ForDirections(rivers.GetProperty("mouths"), diagonals);
        var routes = root.GetProperty("routes");
        Roads = Routes(routes.GetProperty("road"), directions);
        Rails = Routes(routes.GetProperty("railroad"), directions);
        var fog = root.GetProperty("fog");
        Fog = Read(fog, "unknown");
        FogEdges = ForDirections(fog.GetProperty("edges"), diagonals);
        var offsets = registration.GetProperty("edgeOffsets");
        EdgeOffsets = new(diagonals.Select(d => Point(offsets.GetProperty(d))));
        Improvements = Lookup(root.GetProperty("improvements"));

        var unit = root.GetProperty("units");
        Units = new(unit.GetProperty("variants").EnumerateArray().Select(Unit));
        Leader = new(Read(unit.GetProperty("barbarianLeader"), "atlasKey"), Units[46].Shield);
        Shield = Read(unit, "shieldMask");
        var city = root.GetProperty("cities");
        Cities = new(city.GetProperty("variants").EnumerateArray().Select(c =>
            new CityArt(Read(c, "atlasKey"), Point(c.GetProperty("populationAnchor")))));
        FlagAnchor = Point(city.GetProperty("flagAnchor"));
        Disorder = Read(city, "disorderMask");
        Factions = new(root.GetProperty("factions").EnumerateArray().Select(f =>
            new FactionArt(
                Rgb(Read(f, "rgb")),
                Read(f, "brightFlag"),
                Read(f, "darkFlag"),
                Read(f, "occupiedAirbase"))));
        Overlays = Lookup(root.GetProperty("overlays"));
        var effects = root.GetProperty("effects");
        Explosion = Strings(effects.GetProperty("explosion"));
        Nuclear = Strings(effects.GetProperty("nuclear"));
        DisplayColors = root.GetProperty("displayColors").EnumerateObject()
            .ToFrozenDictionary(
                p => p.Name,
                p => Rgb(p.Value.GetString()!),
                StringComparer.Ordinal);
    }

    /// <summary>Check the full stock semantic catalog before admitting its theme. No image paths are opened.</summary>
    public void Verify(AssetAtlas atlas)
    {
        foreach (var (key, expected) in Images)
        {
            var sprite = atlas.Sprite(key);
            if (new ArtPoint(sprite.LogicalWidth, sprite.LogicalHeight) != expected.Logical ||
                new ArtPoint(sprite.PixelWidth, sprite.PixelHeight) != expected.Physical)
            {
                throw new InvalidDataException($"Stock map binding '{key}' has unexpected dimensions");
            }

            using var png = atlas.OpenPng(key);
            if (Convert.ToHexStringLower(SHA256.HashData(png)) != expected.Sha256)
            {
                throw new InvalidDataException($"Stock map binding '{key}' has unexpected pixels");
            }
        }
    }

    private static MapArtCatalog ReadStock()
    {
        using var stream = typeof(MapArtCatalog).Assembly.GetManifestResourceStream(
            "OpenCiv2.Graphics.map-asset-bindings.json")!;
        using var catalog = JsonDocument.Parse(stream);
        return new(catalog.RootElement);
    }

    private static string Read(JsonElement row, string name) => row.GetProperty(name).GetString()!;

    private static ArtPoint Point(JsonElement row) => new(row[0].GetInt32(), row[1].GetInt32());

    private static uint Rgb(string value) => Convert.ToUInt32(value[1..], 16);

    private static UnitArt Unit(JsonElement row) => new(Read(row, "atlasKey"), Point(row.GetProperty("shieldAnchor")));

    private static OwnedList<string> Strings(JsonElement row) => new(row.EnumerateArray().Select(p => p.GetString()!));

    private static OwnedList<string> Bindings(JsonElement row) =>
        new(row.EnumerateArray().Select(p => Read(p, "atlasKey")));

    private static OwnedList<string> ForDirections(JsonElement row, OwnedList<string> directions) =>
        new(directions.Select(d => Read(row, d)));

    private static OwnedList<string> Routes(JsonElement row, OwnedList<string> directions) =>
        new(directions.Select(d => Read(row.GetProperty("directions"), d)).Prepend(Read(row, "isolated")));

    private static FrozenDictionary<string, string> Lookup(JsonElement row) =>
        row.EnumerateObject().ToFrozenDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
}
