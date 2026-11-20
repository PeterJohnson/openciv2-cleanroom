using OpenCiv2.MapRendering;

namespace OpenCiv2.App;

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
