using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.IO.Native;
using OpenCiv2.Platform.Native;
using OpenCiv2.Session;

// Authored F03/F06 interoperability driver. Explicit fixture edit; no post-load admission or gameplay.
static int Integer(string value) => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result) && !value.StartsWith('+')
    ? result : throw new ArgumentException("Fixture edit requires signed decimal integers");
static string Native(string path) => path.Replace('\\', Path.DirectorySeparatorChar);
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Identity(Utf8JsonWriter w, string name, string path)
{
    var bytes = NativeFiles.ReadFile(path); w.WriteStartObject(name); w.WriteString("path", ClassicStorage.ClassicPath(path)); w.WriteNumber("bytes", bytes.Length); w.WriteString("sha256", Hash(bytes)); w.WriteEndObject();
}
static void World(Utf8JsonWriter w, string name, GameState world, int[] gold)
{
    w.WriteStartObject(name); w.WriteNumber("turn", world.Globals.Turn); w.WriteNumber("year", world.Globals.Year); w.WriteNumber("mode", (int)world.Globals.Multiplayer);
    w.WriteNumber("playerCiv", world.Globals.PlayerCiv); w.WriteNumber("localHuman", world.Multiplayer.LocalHuman); w.WriteNumber("gameFlags", world.Globals.GameFlags);
    w.WriteNumber("unitSlots", world.Globals.UnitEnd); w.WriteNumber("citySlots", world.Globals.CityEnd); w.WriteStartArray("gold"); foreach (int value in gold) w.WriteNumberValue(value); w.WriteEndArray(); w.WriteEndObject();
}
try
{
    if (args.Length is not (3 or 6)) throw new ArgumentException("Usage: OpenCiv2.LiveSave HOME INPUT.SAV NEW_OUTPUT.SAV [--gold CIV VALUE]");
    (int Civ, int Value)? edit = null;
    if (args.Length == 6)
    {
        if (args[3] != "--gold") throw new ArgumentException("Only the declared --gold fixture edit is supported");
        edit = (Integer(args[4]), Integer(args[5])); if (edit.Value.Civ is < 0 or > 7) throw new ArgumentException("Fixture civilization must be 0..7");
    }
    string home = Path.GetFullPath(args[0]), input = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
    if (Path.GetExtension(output) is not (".sav" or ".SAV")) throw new ArgumentException("Output must have an explicit .sav or .SAV extension");
    if (File.Exists(output) || Directory.Exists(output)) throw new ArgumentException("Output already exists; this driver requires a new destination");
    byte[] source = NativeFiles.ReadFile(input); var stored = ClassicFiles.DecodeStoredSave(source);
    if (stored.Globals.Multiplayer != 0) throw new ArgumentException("This driver admits only mode-zero input");
    var disk = new ClassicStorage(home); var text = new ClassicTextFiles(disk); var files = new ClassicFileState { CurrentScreen = new(0, 0, 1280, 800), DefaultGameStem = ClassicStorage.ClassicPath(Path.Combine(home, "GAME")) };
    files.Text.Working = files.Text.Home = files.Text.Search = ClassicStorage.ClassicPath(home);
    var process = new ProcessContext(); process.Tokens.NextUnit = 100; process.Tokens.NextCity = 200;
    using var runtime = new SessionRuntime(process, new(75)); runtime.BeginClientSession(new(10, 20));
    var sources = new SortedDictionary<string, string>(StringComparer.Ordinal); int sprites = 0, flushes = 0;
    var textFiles = new TextFiles((directory, name) =>
    {
        var opened = text.Open(directory, name);
        if (opened.Stream is not null)
        {
            try
            {
                string hash = Hash(NativeFiles.ReadFile(Native(opened.Path)));
                if (sources.TryGetValue(opened.Path, out string? previous) && hash != previous) throw new IOException("Text input changed during the operation");
                sources[opened.Path] = hash;
            }
            catch { try { opened.Stream.Close(); } catch { } throw; }
        }
        return opened;
    });
    using var rules = new ClassicRuleContext(files, new(), textFiles, disk.Exists, TextCase.Ascii);
    var loading = new CoreLoadServices(disk, () => ++flushes, disk.ChangeDirectory, disk.Exists, ClassicStorage.PathEqual, rules.Load, () => ++sprites);
    var status = ClassicGameFiles.Load(runtime, files, ClassicStorage.ClassicPath(input), loading, "live-driver/load");
    if (status != CoreLoadStatus.Success) throw new IOException("Core load failed with status " + (int)status);
    int[] loadedGold = runtime.World.Civilizations.Select(c => c.Gold).ToArray();
    if (edit is { } change) runtime.World.Civilizations[change.Civ].Gold = change.Value;
    var gameplay = runtime.GameplayRandom.Capture(); var presentation = runtime.PresentationRandom.Capture();
    NativeFiles.WriteNewFile(output, []);
    if (ClassicGameFiles.Save(runtime, files, ClassicStorage.ClassicPath(output), false, disk) != CoreSaveStatus.Success) throw new IOException("Core save failed; partial output retained");
    if (!gameplay.Equals(runtime.GameplayRandom.Capture()) || !presentation.Equals(runtime.PresentationRandom.Capture())) throw new IOException("Core save changed a random context");
    if (!source.SequenceEqual(NativeFiles.ReadFile(input))) throw new IOException("Input changed during the operation");
    foreach (var (path, hash) in sources) if (Hash(NativeFiles.ReadFile(Native(path))) != hash) throw new IOException("Text input changed during the operation");
    using var stream = StandardOutput.Open(); using var w = new Utf8JsonWriter(stream);
    w.WriteStartObject(); w.WriteString("format", "openciv2-live-save-driver/1"); w.WriteString("scope", "F03 wrapper and F06 core load/save; no post-load admission or gameplay");
    Identity(w, "input", input); World(w, "loaded", runtime.World, loadedGold);
    if (edit is { } fixture) { w.WriteStartObject("fixtureEdit"); w.WriteString("field", "gold"); w.WriteNumber("civilization", fixture.Civ); w.WriteNumber("before", loadedGold[fixture.Civ]); w.WriteNumber("after", fixture.Value); w.WriteEndObject(); }
    w.WriteStartArray("textSources"); foreach (var (path, hash) in sources) { w.WriteStartObject(); w.WriteString("path", path); w.WriteString("sha256", hash); w.WriteEndObject(); }
    w.WriteEndArray();
    World(w, "saved", runtime.World, runtime.World.Civilizations.Select(c => c.Gold).ToArray()); Identity(w, "output", output);
    w.WriteBoolean("outputWritten", true); w.WriteBoolean("sourceUnchanged", true); w.WriteBoolean("saveRandomContextsUnchanged", true); w.WriteNumber("loadGameplayDraws", gameplay.Position.Draws);
    w.WriteNumber("headlessSpriteCallbacks", sprites); w.WriteNumber("flushCallbacks", flushes); w.WriteString("screen", "0,0,1280,800"); w.WriteString("textSearch", "uncached native files; ASCII comparison"); w.WriteEndObject(); w.Flush(); return 0;
}
catch (OutOfMemoryException) { throw; }
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
