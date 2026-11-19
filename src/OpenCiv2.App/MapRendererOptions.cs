using System.Text.Json;

namespace OpenCiv2.App;

public enum MapRendererMode { Cpu, Gpu }

/// <summary>Immutable startup configuration. GPU is opt-in until the platform gates pass.</summary>
public sealed record MapRendererOptions
{
    public MapRendererMode Mode { get; }
    public int GpuCacheMiB { get; }
    public MapRendererOptions(MapRendererMode mode = MapRendererMode.Cpu, int gpuCacheMiB = 512)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (gpuCacheMiB is < 128 or > 1024) throw new ArgumentOutOfRangeException(nameof(gpuCacheMiB), "GPU cache must be 128–1024 MiB");
        Mode = mode;
        GpuCacheMiB = gpuCacheMiB;
    }

    public static MapRendererMode ParseMode(string value) => value switch
    {
        "cpu" => MapRendererMode.Cpu,
        "gpu" => MapRendererMode.Gpu,
        _ => throw new ArgumentException("Map renderer must be cpu or gpu")
    };

    public static MapRendererOptions Read(Stream input)
    {
        using var document = JsonDocument.Parse(input);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != "openciv2-map-renderer/1" ||
            !root.TryGetProperty("renderer", out var mode) || mode.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("gpuCacheMiB", out var cache) || cache.ValueKind != JsonValueKind.Number || !cache.TryGetInt32(out int mib))
            throw new InvalidDataException("Invalid map renderer preferences");
        return new(ParseMode(mode.GetString()!), mib);
    }

    public void Write(Stream output)
    {
        using var writer = new Utf8JsonWriter(output, new() { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("format", "openciv2-map-renderer/1");
        writer.WriteString("renderer", Mode == MapRendererMode.Gpu ? "gpu" : "cpu");
        writer.WriteNumber("gpuCacheMiB", GpuCacheMiB);
        writer.WriteEndObject();
    }
}

/// <summary>The host owns persistence; changing this preference never mutates a running renderer.</summary>
public sealed class MapRendererPreferences(MapRendererOptions startup, MapRendererOptions saved,
    Action<MapRendererOptions>? save = null, string? warning = null)
{
    public MapRendererOptions Startup { get; } = startup;
    public MapRendererOptions Saved { get; private set; } = saved;
    public string? Warning { get; } = warning;
    public bool CanSave => save is not null;
    public void Save(MapRendererMode mode)
    {
        var next = new MapRendererOptions(mode, Startup.GpuCacheMiB);
        if (save is null) throw new InvalidOperationException("Renderer preferences are unavailable in this host");
        save(next);
        Saved = next;
    }
}
