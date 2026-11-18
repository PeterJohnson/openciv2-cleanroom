using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Formats.Content;
using OpenCiv2.Query;
using OpenCiv2.Session;

namespace OpenCiv2.PerformanceCases;

// Same production calls on native and WASM. Input loading, reset and validation
// are outside timing; retained output lives through the allocation/GC sample.
public static class Workloads
{
    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException("Invalid performance result: " + detail);
    }
    private static Sample[] Measure<T>(Func<T> operation, Func<T, string> validate, int repetitions, Action? reset = null)
    {
        var samples = new List<Sample>();
        string? expected = null;
        for (int i = 0; i < repetitions + 3; ++i)
        {
            reset?.Invoke();
            long heapBefore = GC.GetTotalMemory(false);
            long allocated = GC.GetTotalAllocatedBytes(true);
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            TimeSpan pause = GC.GetTotalPauseDuration();
            long start = Stopwatch.GetTimestamp();
            T result = operation();
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            long bytes = GC.GetTotalAllocatedBytes(true) - allocated;
            var sample = new Sample(i < 3, elapsed, bytes, GC.CollectionCount(0) - g0,
                GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2,
                OperatingSystem.IsBrowser() ? null : (GC.GetTotalPauseDuration() - pause).TotalMilliseconds,
                GC.GetTotalMemory(false), heapBefore + bytes, "");
            string identity = validate(result);
            expected ??= identity;
            Require(identity == expected, "result changed between samples");
            samples.Add(sample with { OutputSha256 = identity });
            GC.KeepAlive(result);
        }
        return samples.ToArray();
    }
    public static string Run(string inputJson, int repetitions, Assembly host)
    {
        if (repetitions is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(repetitions));
        using var document = JsonDocument.Parse(inputJson);
        var files = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetBytesFromBase64(), StringComparer.Ordinal);
        var inputs = files.ToDictionary(p => p.Key, p => ContentIdentity.Sha256(p.Value));
        byte[] original = files["large.sav"], changed = files["changed.sav"];
        var results = new Dictionary<string, Sample[]>();
        StoredSnapshot Inspect(byte[] bytes) => StoredInspection.Inspect(bytes, FileKind.Save, OpenCiv2.Query.FileAccess.OfflineOmniscient);
        results["decode-large"] = Measure(() => ClassicFiles.DecodeStoredSave(original), save =>
        {
            Require(save.Map.Tiles.Count == 31752 && save.Units.Count == 2048 && save.Cities.Count == 256, "decode extent");
            string hash = ContentIdentity.Sha256(ClassicFiles.EncodeStoredSave(save));
            Require(hash == inputs["large.sav"], "decode/encode bytes");
            return hash;
        }, repetitions);
        results["inspect-large"] = Measure(() =>
        {
            var snapshot = Inspect(original);
            return (Snapshot: snapshot, Json: Output.SnapshotJson(snapshot));
        }, value =>
        {
            var snapshot = value.Snapshot;
            Require(snapshot.Decoded && !snapshot.HasErrors, "inspection admission");
            Require(snapshot.Records.Count(r => r.Key.Section == Section.Units) == 2048 && snapshot.Records.Count(r => r.Key.Section == Section.Tiles) == 31752, "inspection extent");
            Require(ContentIdentity.Sha256(snapshot.Records.SelectMany(r => r.Bytes).ToArray()) == inputs["large.sav"], "inspection byte closure");
            using var json = JsonDocument.Parse(value.Json);
            Require(json.RootElement.GetProperty("records").GetArrayLength() == snapshot.Records.Count, "formatted records");
            return ContentIdentity.Sha256(value.Json);
        }, repetitions);
        results["diff-large"] = Measure(() =>
        {
            var before = Inspect(original); var after = Inspect(changed); var diff = StoredInspection.Diff(before, after);
            return (Diff: diff, Json: Output.DiffJson(before, after, diff));
        }, value =>
        {
            var diff = value.Diff;
            Require(diff.Different && diff.FieldsCompared && diff.Fields.Count == 1, "diff extent");
            var field = diff.Fields[0];
            Require(field.Record == new RecordKey(Section.Units, 0) && field.Field == "token" && field.Before!.Text == "1" && field.After!.Text == "4294967295", "diff field");
            return ContentIdentity.Sha256(value.Json);
        }, repetitions);
        ContentSource Source(string path) => new(path, new(files[path]));
        MountedPack Mount(string root) => new(Source(root + "/content.json"), (scope, path) => Source(root + "/" + (scope.Length == 0 ? "" : scope + "/") + path));
        var selected = Mount("overlay-1"); var parents = new[] { Mount("overlay-0"), Mount("pack") };
        results["resolve-pack"] = Measure(() => ContentPacks.Resolve(selected, parents, "en"), pack =>
        {
            Require(pack.Manifests.Count == 3 && pack.Components.Count != 0, "pack chain/components");
            // Every selected document's complete bytes contribute, including locale trees.
            var documents = pack.Components.Select(p => p.Value).Concat(pack.Localized.SelectMany(p => p.Value.Select(v => v.Value)));
            string identity = string.Join("\n", pack.Manifests.Select(m => m.Pack.Sha256)) + "\n" + string.Join("\n", documents.Select(d => d.File.Receipt.Sha256));
            return ContentIdentity.Sha256(Encoding.UTF8.GetBytes(identity));
        }, repetitions);
        var rules = Source("RULES.TXT");
        results["convert-rules"] = Measure(() => ContentConversions.Rules(rules, TextCase.Ascii), conversion =>
        {
            using var parsed = JsonDocument.Parse(conversion.Rules.ToArray());
            Require(parsed.RootElement.GetProperty("units").GetArrayLength() == 62 && parsed.RootElement.GetProperty("advances").GetArrayLength() == 100, "converted rule extent");
            return ContentIdentity.Sha256(conversion.Rules.Concat(conversion.Compatibility).ToArray());
        }, repetitions);
        results["create-world"] = Measure(() =>
        {
            var world = new GameState(new ProcessContext()); Populate(world); return world;
        }, WorldIdentity, repetitions);
        using var runtime = new SessionRuntime(new(), new(703));
        runtime.BeginClientSession(new(17, 23)); Populate(runtime.World);
        var client = runtime.Bind(runtime.AddParticipant(new(1)), ControllerKind.Scripted);
        results["own-projection"] = Measure(() => (Units: client.OwnUnits(), Cities: client.OwnCities()), value =>
        {
            Require(value.Units.Records.Count == 2048 && value.Cities.Records.Count == 256, "projection extent");
            Require(value.Units.Records.Select((u, i) => u == new OwnUnit(new((ushort)i), new((uint)i + 1),
                (short)(i % 126 * 2 + i / 126 % 2), (short)(i / 126), new(0), 0, 0, UnitOrder.None, null)).All(b => b), "unit projection values");
            Require(value.Cities.Records.Select((c, i) => c == new OwnCity(new((ushort)i), new((uint)i + 1),
                (short)(i % 126 * 2 + i / 126 % 2), (short)(i / 126), new(Encoding.ASCII.GetBytes("City " + i)), 1, 0, 0, 0)).All(b => b), "city projection values");
            Require(value.Units.Observation == value.Cities.Observation && value.Units.Observation.Observer.Value == 1, "projection observation");
            return WorldIdentity(runtime.World);
        }, repetitions);
        using var command = new CommandWorkload();
        results["session-command"] = Measure(command.Execute, command.Validate, repetitions, command.Reset);
        var build = new[] { typeof(BuildIdentity).Assembly, typeof(IPlayerClient).Assembly, typeof(OpenCiv2.IO.ByteReader).Assembly,
            typeof(GameState).Assembly, typeof(ClassicFiles).Assembly, typeof(StoredInspection).Assembly,
            typeof(OpenCiv2.Network.Messages).Assembly, typeof(SessionRuntime).Assembly, typeof(Workloads).Assembly, host }
            .Distinct().Select(BuildIdentity.Read).ToArray();
        return JsonSerializer.Serialize(new PerformanceReport("openciv2-warm-performance/1", inputs, results, build), PerformanceJson.Default.PerformanceReport);
    }
    private static void Populate(GameState world)
    {
        world.Map.Allocate(new(252, 252)); world.Entities.Units.SetEnd(2048); world.Entities.Cities.SetEnd(256);
        world.Globals.UnitEnd = 2048; world.Globals.CityEnd = 256;
        for (int i = 0; i < 2048; ++i) world.Entities.Units.Storage[i] = new()
        {
            Token = new((uint)i + 1),
            Owner = new(1),
            X = (short)(i % 126 * 2 + i / 126 % 2),
            Y = (short)(i / 126)
        };
        for (int i = 0; i < 256; ++i) world.Entities.Cities.Storage[i] = new()
        {
            Token = new((uint)i + 1),
            Owner = new(1),
            X = (short)(i % 126 * 2 + i / 126 % 2),
            Y = (short)(i / 126),
            Size = 1,
            Name = new(Encoding.ASCII.GetBytes("City " + i))
        };
    }
    private static string WorldIdentity(GameState world)
    {
        Require(world.Map.Allocated && world.Map.Geometry.Width == 252 && world.Map.Geometry.Height == 252 && world.Map.Tiles.Length == 31752, "live map extent");
        Require(world.Map.Tiles.ToArray().All(t => t == new OpenCiv2.Core.Tile()), "live initial tiles");
        Require(world.Entities.Units.End == 2048 && world.Entities.Cities.End == 256, "live world extent");
        Require(world.Entities.Units.Scanned.ToArray().Select((u, i) => u == new Unit
        {
            Token = new((uint)i + 1),
            Owner = new(1),
            X = (short)(i % 126 * 2 + i / 126 % 2),
            Y = (short)(i / 126)
        }).All(b => b), "live units");
        Require(world.Entities.Cities.Scanned.ToArray().Select((c, i) => c == new City
        {
            Token = new((uint)i + 1),
            Owner = new(1),
            X = (short)(i % 126 * 2 + i / 126 % 2),
            Y = (short)(i / 126),
            Size = 1,
            Name = new(Encoding.ASCII.GetBytes("City " + i))
        }).All(b => b), "live cities");
        return ContentIdentity.Sha256("world-252x252-2048units-256cities-owner1"u8);
    }
    private sealed class CommandWorkload : IDisposable
    {
        private readonly SessionRuntime runtime = new(new(), new(704));
        private readonly IPlayerClient client;
        public CommandWorkload()
        {
            runtime.RegisterCommand(LocalUnitCommands.StackBottom()); runtime.BeginClientSession(new(17, 23));
            runtime.World.Map.Allocate(new(8, 6)); runtime.World.Entities.Units.SetEnd(3); runtime.World.Globals.UnitEnd = 3;
            client = runtime.Bind(runtime.AddParticipant(new(1)), ControllerKind.Scripted);
        }
        public void Reset()
        {
            for (int i = 0; i < 3; ++i) runtime.World.Entities.Units.Storage[i] = new()
            {
                Token = new((uint)i + 100),
                Owner = new(1),
                X = 2,
                Y = 2,
                Previous = new((short)(i - 1)),
                Next = new((short)(i == 2 ? -1 : i + 1))
            };
        }
        public CommandUpdate Execute()
        {
            Require(client.OwnUnits().Records.Count == 3, "command query");
            var update = client.Submit(new(LocalUnitCommands.StackBottomName, new([new("unit", new CommandValue.Unit(new(new(0), new(100))))])));
            Require(update.Status == CommandStatus.Pending && runtime.AdvanceOne(), "command admission");
            var interaction = client.Result(update.Correlation.Command)!.Interaction!;
            Require(client.Reply(interaction.Id, 1).Accepted && runtime.AdvanceOne(), "command confirmation");
            var result = client.Result(update.Correlation.Command)!;
            client.Updates(); Require(runtime.RetireCommand(update.Correlation.Command), "command retirement");
            return result;
        }
        public string Validate(CommandUpdate result)
        {
            Require(result.Status == CommandStatus.Completed && CommandDescriptor.Value(result.Result, "changed") == new CommandValue.Boolean(true), "command completion");
            Require(result.Committed.Count == 1 && result.Committed[0].Name == "unit.stack-reordered", "command effect");
            var units = runtime.World.Entities.Units.Scanned;
            Require(units[0].Previous.Value == 2 && units[0].Next.Value == -1 && units[1].Previous.Value == -1 && units[1].Next.Value == 2 && units[2].Previous.Value == 1 && units[2].Next.Value == 0 && !runtime.HasPendingCommands, "command changed stack");
            return ContentIdentity.Sha256("stack-1-2-0;changed=true;effects=1;retired"u8);
        }
        public void Dispose() => runtime.Dispose();
    }
}
public sealed record Sample(bool Warmup, double WallMs, long AllocatedBytes, int Gen0, int Gen1, int Gen2, double? GcPauseMs, long ManagedHeapBytes, long ManagedHeapPlusAllocatedBytes, string OutputSha256);
public sealed record PerformanceReport(string Format, Dictionary<string, string> Inputs, Dictionary<string, Sample[]> Results, CompiledInputs[] Build);
[JsonSerializable(typeof(PerformanceReport))]
internal partial class PerformanceJson : JsonSerializerContext;
