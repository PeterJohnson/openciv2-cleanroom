using System.Text.Json;
using OpenCiv2.IO;
using OpenCiv2.MapRendering;

namespace OpenCiv2.App;

/// <summary>Local presentation preferences, not CIV2.DAT gameplay options.</summary>
public sealed class ShellPreferences(IByteStore store)
{
    public MapDisplay Saved { get; private set; } = new();
    public string? Warning { get; private set; }
    public async Task LoadAsync()
    {
        try
        {
            byte[]? bytes = await store.ReadAsync("shell-preferences");
            if (bytes is null)
            {
                return;
            }

            using var json = JsonDocument.Parse(bytes);
            var value = json.RootElement;
            if (value.GetProperty("version").GetInt32() != 1)
            {
                throw new InvalidDataException("Unsupported shell preferences");
            }

            var sentry = (SentryAppearance)value.GetProperty("sentry").GetInt32();
            var color = (MapColorCorrection)value.GetProperty("color").GetInt32();
            if (!Enum.IsDefined(sentry) || !Enum.IsDefined(color))
            {
                throw new InvalidDataException("Invalid display mode");
            }

            Saved = new(Grid: value.GetProperty("grid").GetBoolean(), Bare: value.GetProperty("bare").GetBoolean(),
                ReducedMotion: value.GetProperty("reducedMotion").GetBoolean(), CityRadius: value.GetProperty("cityRadius").GetBoolean(),
                Ownership: value.GetProperty("ownership").GetBoolean(), FollowActive: value.GetProperty("followActive").GetBoolean(),
                Sentry: sentry, Color: color);
            Warning = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or
            InvalidOperationException or KeyNotFoundException or FormatException)
        { Warning = "Could not load display preferences: " + error.Message; }
    }
    public async Task SaveAsync(MapDisplay value)
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteBoolean("grid", value.Grid);
            writer.WriteBoolean("bare", value.Bare);
            writer.WriteBoolean("reducedMotion", value.ReducedMotion);
            writer.WriteBoolean("cityRadius", value.CityRadius);
            writer.WriteBoolean("ownership", value.Ownership);
            writer.WriteBoolean("followActive", value.FollowActive);
            writer.WriteNumber("sentry", (int)value.Sentry);
            writer.WriteNumber("color", (int)value.Color);
            writer.WriteEndObject();
        }
        await store.WriteAsync("shell-preferences", bytes.ToArray());
        Saved = value;
        Warning = null;
    }
}
