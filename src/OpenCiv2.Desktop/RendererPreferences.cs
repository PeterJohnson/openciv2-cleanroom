using System.Text.Json;
using OpenCiv2.App;
using OpenCiv2.MapRendering;

internal static class RendererPreferences
{
    internal static MapRendererPreferences Load(string? path, string? renderer, string? cache)
    {
        path = Path.GetFullPath(path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCiv2", "renderer.json"));
        MapRendererOptions saved = new();
        string? warning = null;
        try
        {
            using var input = File.OpenRead(path);
            saved = MapRendererOptions.Read(input);
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            warning = "Could not load renderer preferences; using defaults. " + error.Message;
            System.Console.Error.WriteLine(warning);
        }
        int mib = cache is null ? saved.GpuCacheMiB : int.TryParse(cache, out int parsed) ? parsed :
            throw new ArgumentException("GPU cache must be an integer from 128 to 1024 MiB");
        var startup = new MapRendererOptions(renderer is null ? saved.Mode : MapRendererOptions.ParseMode(renderer), mib);
        return new(startup, saved, Save, warning);

        void Save(MapRendererOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) options.Write(output);
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
