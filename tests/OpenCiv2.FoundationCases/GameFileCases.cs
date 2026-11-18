using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class GameFileFixture : IDisposable
    {
        public SessionRuntime Runtime = InstallationRuntime();
        public ClassicFileState Files = new() { CurrentScreen = new(10, 20, 650, 500), DefaultGameStem = "C:\\home\\GAME", RememberedSubdirectory = "retained", CursorX = -2, CursorY = 7 };
        public CallbackStorage Storage = new();
        public CoreLoadServices Services;
        public List<string> Trace = [];
        public byte[] Bytes = SaveVector();
        public List<byte> Output = [];
        public int Position, Closes, Writes, Opens;
        public int WriteLimit = int.MaxValue;
        public bool OpenOk = true, CloseOk = true, WriteOk = true;
        public Action? Observe;
        public Action? BeforeClose;
        public GameFileFixture()
        {
            Files.Text.Working = "caller"; Files.Text.Home = Files.Text.Search = "C:\\home";
            Storage.ReadFile = path =>
            {
                ++Opens; Trace.Add("read-open:" + path); Observe?.Invoke();
                return OpenOk ? new CallbackInput(size => { Trace.Add("item:" + size); Observe?.Invoke(); int n = Math.Min(size, Bytes.Length - Position); var result = Bytes.AsSpan(Position, n).ToArray(); Position += n; return new(new(result), n == size); }, Close,
                    () => { Observe?.Invoke(); return Position < Bytes.Length ? Bytes[Position++] : null; }) : null;
            };
            Storage.ReplaceFile = path =>
            {
                ++Opens; Trace.Add("write-open:" + path); Observe?.Invoke();
                return OpenOk ? new CallbackOutput(bytes => { ++Writes; Observe?.Invoke(); int n = Math.Min(WriteLimit, bytes.Length); Output.AddRange(bytes[..n].ToArray()); return WriteOk && n == bytes.Length; }, Close) : null;
            };
            Storage.RemoveFile = _ => throw new Exception("Game replacement policy belongs to storage");
            Services = new(Storage, () => { Trace.Add("flush"); Observe?.Invoke(); }, path => { Trace.Add("cd:" + path); Observe?.Invoke(); },
                (directory, name) => { Trace.Add("probe:" + directory + "\\" + name); return true; }, (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
                () => { Trace.Add("content"); Observe?.Invoke(); return new(new()); });
        }
        private bool Close() { ++Closes; Trace.Add("close"); BeforeClose?.Invoke(); return CloseOk; }
        public CoreLoadStatus Load(string path = "C:\\home\\Scenario\\Example") => ClassicGameFiles.Load(Runtime, Files, path, Services, "load/one");
        public CoreSaveStatus Save(bool scenario = false, string path = "C:\\saves\\Name") => ClassicGameFiles.Save(Runtime, Files, path, scenario, Storage);
        public void ReadySave()
        {
            InstallInput(Runtime, InstallationInput()); Runtime.World.Globals.GameFlags = 0x40; Runtime.World.Globals.NumEnemies = 99; Runtime.World.Civilizations[1].UnitsActive[2] = 55;
            Runtime.World.Multiplayer.LocalHuman = 2; Runtime.World.Multiplayer.NetworkTurnCiv = 5; Runtime.World.Multiplayer.HotseatParticipants = 6; Runtime.World.Multiplayer.Saved.ResumeCiv = 6;
            foreach (var leader in Runtime.Process.Rules.Leaders) leader.LeaderName = leader.Names[0];
        }
        public void Dispose() => Runtime.Dispose();
    }
    private static byte[] LegacyGameFile(byte version)
    {
        var current = SaveVector(); var data = new List<byte>(current.AsSpan(0, 12).ToArray()); data[10] = version;
        void Part(int offset, int size) => data.AddRange(current.AsSpan(offset, size).ToArray());
        if (version < 0x28) { Part(12, 0x36); Part(12 + 0x36, 0x5d); Part(12 + 0x9a, 0x5d); Part(12 + 0xfe, 0x4c); } else Part(12, 330);
        Part(12 + 330, 8 * 242);
        for (int i = 0; i < 8; ++i)
        {
            int offset = 12 + 330 + 8 * 242 + i * 1428;
            if (version >= 0x28) Part(offset, 1428);
            else { foreach (var (start, size) in new[] { (0, 0x58), (0x58, 0xc), (0x66, 0xe), (0x74, 0x5d), (0xd8, 0x36), (0x116, 0x36), (0x154, 0x36) }) Part(offset + start, size); data.Add(0xa5); Part(offset + 0x192, 0x402); }
        }
        Part(MapOffset, EntitiesOffset - MapOffset); Part(EntitiesOffset, 26); Part(EntitiesOffset + 32, 26); Part(EntitiesOffset + 64, 84); Part(UsageOffset, RequiredEnd - UsageOffset); return data.ToArray();
    }
    private static void GameFileLoadVersions()
    {
        foreach (byte version in new byte[] { 0x26, 0x27, 0x28, 0x2c })
        {
            using var f = new GameFileFixture(); if (version != 0x2c) f.Bytes = LegacyGameFile(version); var client = SessionClient(f.Runtime, 0); var identity = client.Identity; var presentation = f.Runtime.PresentationRandom.Capture();
            Equal((int)CoreLoadStatus.Success, (int)f.Load()); Equal(1, f.Closes); Equal(identity, client.Identity); Equal(presentation, f.Runtime.PresentationRandom.Capture()); Equal(255UL, f.Runtime.GameplayRandom.Capture().Position.Draws);
            Equal(short.MaxValue, f.Runtime.World.Globals.Turn); Equal(new UnitToken(100), f.Runtime.World.Entities.Units.Storage[0].Token); Equal(version < 0x29 ? 101U : 0U, f.Runtime.World.Entities.Units.Storage[1].Token.Value); Equal(new CityToken(200), f.Runtime.World.Entities.Cities.Storage[0].Token); Equal(version < 0x29 ? 2 : 1, client.OwnUnits().Records.Count);
            Equal((byte)0xfe, f.Runtime.World.PowerHistory[149][7]); Equal((short)-1, f.Files.CursorX); Equal((short)2, f.Files.CursorY); Equal((short)-1, f.Files.Starts.X[0]); Equal("C:\\home\\Scenario\\Example.SAV", f.Files.OpenedPath); Equal("C:\\home\\Scenario\\GAME", f.Files.GameStem); Equal("Scenario", f.Files.RememberedSubdirectory); Equal("Example", f.Files.LoadedFilename); Equal("C:\\home", f.Files.Text.Working);
            Equal(true, f.Trace.IndexOf("close") < f.Trace.IndexOf("content")); Equal("flush", f.Trace[^1]); Equal(1UL, client.OwnUnits().Observation.Revision);
        }
        using var chunks = new GameFileFixture(); Array.Resize(ref chunks.Bytes, 12 + 2 * 65536);
        Equal((int)CoreLoadStatus.Success, (int)chunks.Load()); Equal(chunks.Bytes.Length, chunks.Position); Equal(3, chunks.Trace.Count(s => s == "item:65536")); Equal(1, chunks.Closes);
    }
    private static void GameFileLoadRejectedInputs()
    {
        foreach (int length in new[] { 0, 8, 11, 30, 340, 3000, 14000, RequiredEnd - 1 })
        {
            using var f = new GameFileFixture(); f.Bytes = f.Bytes[..length]; f.Runtime.World.Globals.Turn = 123; var random = f.Runtime.GameplayRandom.Capture(); var rules = f.Runtime.Process.Rules.Definitions.Snapshot();
            Equal((int)(length < 12 ? CoreLoadStatus.Input : CoreLoadStatus.Data), (int)f.Load()); Equal(1, f.Closes); Equal((short)123, f.Runtime.World.Globals.Turn); Equal(100U, f.Runtime.Process.Tokens.NextUnit); Equal(random, f.Runtime.GameplayRandom.Capture()); Equal(rules, f.Runtime.Process.Rules.Definitions.Snapshot()); Equal(false, f.Trace.Contains("content"));
        }
        foreach (byte version in new byte[] { 0x25, 0x29, 0x2d })
        {
            using var f = new GameFileFixture(); f.Bytes[10] = version; Equal((int)(version > 0x2c ? CoreLoadStatus.Newer : CoreLoadStatus.Unsupported), (int)f.Load()); Equal(1, f.Closes); Equal(false, f.Trace.Any(s => s.StartsWith("item:"))); Equal(false, f.Trace.Contains("content"));
        }
        using var badHeader = new GameFileFixture(); badHeader.Bytes[0] = 0; Equal((int)CoreLoadStatus.Input, (int)badHeader.Load()); Equal(1, badHeader.Closes);
        using var missing = new GameFileFixture { OpenOk = false }; Equal((int)CoreLoadStatus.Input, (int)missing.Load()); Equal(0, missing.Closes);
        using var failedClose = new GameFileFixture { CloseOk = false }; Equal((int)CoreLoadStatus.Input, (int)failedClose.Load()); Equal(false, failedClose.Trace.Contains("content")); Equal((short)0, failedClose.Runtime.World.Globals.Turn); Equal(0UL, failedClose.Runtime.GameplayRandom.Capture().Position.Draws);
    }
    private static void GameFileLoadGuardsAndPaths()
    {
        using var f = new GameFileFixture(); Throws<ArgumentNullException>(() => new ClassicLoadOperation(f.Runtime, f.Files, f.Services with { LoadContent = null! })); Equal(0, f.Opens);
        f.Observe = () => Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame());
        using (var operation = new ClassicLoadOperation(f.Runtime, f.Files, f.Services))
        {
            Throws<ArgumentException>(() => operation.Load("unqualified", "load/one")); Throws<ArgumentException>(() => operation.Load("C:\\bad\0", "load/one")); Equal(0, f.Opens);
            Equal((int)CoreLoadStatus.Success, (int)operation.Load("c:\\HOME\\Folder.ext\\Example", "load/one")); Equal("c:\\HOME\\Folder.ext\\Example", f.Files.OpenedPath); Equal("Folder.ext", f.Files.RememberedSubdirectory);
            Throws<InvalidOperationException>(() => operation.Load("C:\\x", "load/one")); Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame());
        }
        f.Runtime.PrepareNewGame();
        using var outside = new GameFileFixture(); outside.Services = outside.Services with { Exists = (_, _) => false }; Equal((int)CoreLoadStatus.Success, (int)outside.Load("D:\\世界\\Example")); Equal(outside.Files.DefaultGameStem, outside.Files.GameStem); Equal("retained", outside.Files.RememberedSubdirectory);
        using var pending = new GameFileFixture(); pending.Runtime.EndClientSession(); pending.Runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); pending.Runtime.BeginClientSession(new()); SessionClient(pending.Runtime).Submit(CommandRequestFor()); Throws<InvalidOperationException>(() => pending.Load()); Equal(0, pending.Opens);
        using var owned = new GameFileFixture(); var ruleInput = new RulesInput().Load().Input;
        owned.Services = owned.Services with
        {
            LoadContent = () => new(ruleInput, new(1, new([new("Authored"u8.ToArray())]), new(new OwnedList<byte>?[3]))),
            ContentInstalled = () => { Equal(1, owned.Closes); Equal((short)0, owned.Runtime.World.Globals.Turn); Equal("Authored", ByteString(owned.Runtime.Process.Rules.Labels.Label(0, owned.Runtime.Process.Rules.Text))); Equal((byte)6, owned.Runtime.Process.Rules.Definitions.Units[0].Movement); }
        };
        Equal((int)CoreLoadStatus.Success, (int)owned.Load()); Equal((byte)6, owned.Runtime.Process.Rules.Definitions.Units[0].Movement);
    }
    private static void GameFileLoadFailuresAndPublication()
    {
        using var f = new GameFileFixture(); var client = SessionClient(f.Runtime); f.BeforeClose = () => throw new IOException("close"); Throws<IOException>(() => f.Load()); Equal(1, f.Closes); Equal(false, f.Trace.Contains("content")); Equal(1UL, client.OwnUnits().Observation.Revision);
        using var inconsistent = new GameFileFixture(); int position = 0; inconsistent.Storage.ReadFile = _ => new CallbackInput(_ => new(new([1]), true), () => { ++inconsistent.Closes; return true; }, () => inconsistent.Bytes[position++]); Throws<ArgumentException>(() => inconsistent.Load()); Equal(1, inconsistent.Closes); Equal(false, inconsistent.Trace.Contains("content"));
        using var readFailure = new GameFileFixture(); position = 0; readFailure.Storage.ReadFile = _ => new CallbackInput(_ => throw new IOException("read"), () => { ++readFailure.Closes; throw new IOException("close"); }, () => readFailure.Bytes[position++]);
        try { readFailure.Load(); throw new Exception("Failure expected"); } catch (IOException e) { Equal("read", e.Message); }
        Equal(1, readFailure.Closes);
        using var install = new GameFileFixture(); install.Runtime.GameplayRandom.Restore(new GameplayRandom(PasswordChoices("load/one", 100), "session").Capture(), 1000);
        ChoiceFails(ChoiceFailure.Missing, () => install.Load(), PasswordKey("load/one", 100)); Equal(1, install.Closes); Equal(short.MaxValue, install.Runtime.World.Globals.Turn); Equal(new UnitToken(100), install.Runtime.World.Entities.Units.Storage[0].Token); Equal(true, install.Files.RecordBytes is null); Equal(false, install.Trace.Contains("flush"));
        using var contentFailure = new GameFileFixture(); contentFailure.Services = contentFailure.Services with { ContentInstalled = () => throw new IOException("content notification") }; Throws<IOException>(() => contentFailure.Load()); Equal((short)0, contentFailure.Runtime.World.Globals.Turn); Equal(0UL, contentFailure.Runtime.GameplayRandom.Capture().Position.Draws); Equal(1, contentFailure.Closes);
        using var exhausted = new GameFileFixture(); position = 0; exhausted.Storage.ReadFile = _ => new CallbackInput(_ => throw new OutOfMemoryException(), () => { ++exhausted.Closes; return true; }, () => exhausted.Bytes[position++]); Throws<OutOfMemoryException>(() => exhausted.Load()); Equal(1, exhausted.Closes); Equal(false, exhausted.Trace.Contains("content"));
    }
    private static void GameFileSaveCompleteImage()
    {
        using var f = new GameFileFixture(); f.ReadySave(); var client = SessionClient(f.Runtime); var identity = client.Identity; var gameplay = f.Runtime.GameplayRandom.Capture(); var presentation = f.Runtime.PresentationRandom.Capture(); var layout = f.Runtime.World.Layout;
        f.Observe = () => { Equal((byte)0, f.Runtime.World.Globals.HumanPlayers); Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame()); };
        Equal((int)CoreSaveStatus.Success, (int)f.Save()); Equal(1, f.Writes); Equal(1, f.Closes); Equal("C:\\saves\\Name.SAV", f.Files.OpenedPath); var stored = ClassicFiles.DecodeStoredSave(f.Output.ToArray()); BytesEqual(f.Output, ClassicFiles.EncodeStoredSave(stored));
        Equal((sbyte)2, stored.Globals.SavedCurrentCiv); Equal((byte)99, stored.Globals.NumEnemies); Equal((ushort)0, stored.Globals.GameFlags); Equal((byte)55, stored.Civilizations[1].UnitsActive[2]); Equal(new UnitToken(100), stored.Units[0].Token); Equal(new CityToken(200), stored.Cities[0].Token); Equal((short)-2, stored.CursorX); Equal((short)7, stored.CursorY); Equal((short)10, stored.Layout.Rectangles[0].Left); Equal((short)650, stored.Layout.Rectangles[0].Right);
        Equal(layout, f.Runtime.World.Layout); Equal(gameplay, f.Runtime.GameplayRandom.Capture()); Equal(presentation, f.Runtime.PresentationRandom.Capture()); Equal(identity, client.Identity); Equal(101U, f.Runtime.Process.Tokens.NextUnit); Equal(201U, f.Runtime.Process.Tokens.NextCity); Equal(1UL, client.OwnUnits().Observation.Revision);
    }
    private static void GameFileSaveScenarioAndEvents()
    {
        foreach (bool export in new[] { false, true })
        {
            using var f = new GameFileFixture(); f.ReadySave(); var world = f.Runtime.World; world.Globals.GameFlags = 0xc0;
            for (int i = 0; i < 100; ++i) { world.Globals.FirstDiscoverer[i] = 7; world.Globals.AdvanceKnownBy[i] = 0xaa; }
            foreach (ref var civ in world.Civilizations.AsSpan()) civ.Flags = 0xffff; world.Civilizations[1].Advances[0] = 3; world.Civilizations[2].Advances[0] = 2; world.Civilizations[0].Advances[0] = 4; world.Civilizations[1].Advances[11] = 2;
            world.Events.Loaded = true; var entry = world.Events.Collection.Append(); entry.Values.Actions = 0x80000001; entry.SetString(0, new("Unit"u8.ToArray())); world.Scenario.TechnologyParadigm = -11;
            f.Runtime.Process.Rules.Leaders[0].Plural = f.Runtime.Process.Rules.Leaders[0].Plural with { Custom = true }; world.Names[1].Tribe = new([88]);
            Equal((int)CoreSaveStatus.Success, (int)f.Save(export)); var stored = ClassicFiles.DecodeStoredSave(f.Output.ToArray()); Equal(export ? ".SCN" : ".SAV", f.Files.Extension); Equal(export ? (ushort)0xc0 : (ushort)0x80, stored.Globals.GameFlags); Equal(export, stored.Events is null); Equal(true, world.Events.Loaded); Equal((short)-11, stored.Scenario!.TechnologyParadigm); Equal((byte)88, stored.Names[1].Tribe[0]); Equal((byte)'L', stored.Names[1].Leader[0]);
            if (export)
            {
                Equal((byte)2, stored.Globals.AdvanceKnownBy[0]); Equal((byte)6, stored.Globals.AdvanceKnownBy[1]); Equal((byte)1, stored.Globals.FirstDiscoverer[0]); Equal((byte)8, stored.Globals.FirstDiscoverer[1]);
                for (int i = 2; i < 100; ++i) { Equal((byte)0, stored.Globals.AdvanceKnownBy[i]); Equal((byte)7, stored.Globals.FirstDiscoverer[i]); }
                Equal((ushort)0xffff, stored.Civilizations[0].Flags); for (int civ = 1; civ < 8; ++civ) Equal((ushort)0xff96, stored.Civilizations[civ].Flags);
            }
            else { Equal((byte)0xaa, stored.Globals.AdvanceKnownBy[0]); Equal((ushort)0xffff, stored.Civilizations[1].Flags); Equal(0x80000001U, stored.Events![0].Record.Actions); }
        }
    }
    private static void GameFileSaveFailures()
    {
        using var full = new GameFileFixture(); full.ReadySave(); full.Save();
        foreach (int prefix in new[] { 0, 1, 9, 10, 12, full.Output.Count / 2, full.Output.Count - 1 })
        {
            using var f = new GameFileFixture { WriteLimit = prefix }; f.ReadySave(); Equal((int)CoreSaveStatus.Output, (int)f.Save()); BytesEqual(full.Output.Take(prefix), f.Output); Equal(1, f.Closes);
        }
        foreach (int failure in new[] { 0, 1, 2 })
        {
            using var f = new GameFileFixture { OpenOk = failure != 0, WriteOk = failure != 1, CloseOk = failure != 2 }; f.ReadySave(); Equal((int)CoreSaveStatus.Output, (int)f.Save()); Equal(failure == 0 ? 0 : 1, f.Closes);
        }
        foreach (bool duringWrite in new[] { false, true })
        {
            using var f = new GameFileFixture(); f.ReadySave(); f.BeforeClose = () => throw new IOException("close"); if (duringWrite) f.Observe = () => { if (f.Opens > 0 && f.Writes > 0) throw new IOException("write"); };
            try { f.Save(); throw new Exception("Failure expected"); } catch (IOException e) { Equal(duringWrite ? "write" : "close", e.Message); }
            Equal(1, f.Closes);
        }
    }
    private static void GameFileSaveAdmissionAndHotseat()
    {
        using var invalid = new GameFileFixture(); invalid.ReadySave(); invalid.Runtime.World.Globals.UnitEnd = -1; Throws<ArgumentException>(() => invalid.Save()); Equal(0, invalid.Opens);
        invalid.Runtime.World.Globals.UnitEnd = 2; Throws<ArgumentException>(() => invalid.Save(path: "bad\0")); Equal(0, invalid.Opens);
        invalid.Runtime.EndClientSession(); invalid.Runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); invalid.Runtime.BeginClientSession(new()); SessionClient(invalid.Runtime).Submit(CommandRequestFor()); Throws<InvalidOperationException>(() => invalid.Save()); Equal(0, invalid.Opens);
        using var badEncoding = new GameFileFixture(); badEncoding.ReadySave(); badEncoding.Runtime.World.Map.CoarseGrid(0)[0] = null; Throws<InvalidOperationException>(() => badEncoding.Save(true)); Equal(0, badEncoding.Opens); Equal(true, (badEncoding.Runtime.World.Globals.GameFlags & 0x40) != 0); Equal((byte)'L', badEncoding.Runtime.World.Names[1].Leader[0]);
        foreach (bool fail in new[] { false, true })
        {
            using var f = new GameFileFixture { CloseOk = !fail }; f.ReadySave(); f.Runtime.World.Globals.Multiplayer = GameMode.Hotseat; f.Runtime.World.Globals.HumanPlayers = 2;
            f.Observe = () => { Equal((byte)2, f.Runtime.World.Globals.HumanPlayers); Equal((sbyte)1, f.Runtime.World.Globals.PlayerCiv); Equal((byte)6, f.Runtime.World.Multiplayer.Saved.ResumeCiv); };
            Equal((int)(fail ? CoreSaveStatus.Output : CoreSaveStatus.Success), (int)f.Save(path: "C:\\dir.ext\\NoExtension")); var stored = ClassicFiles.DecodeStoredSave(f.Output.ToArray()); Equal((byte)6, stored.Globals.HumanPlayers); Equal((byte)5, stored.Multiplayer!.ResumeCiv); Equal("C:\\dir.ext\\NoExtension", f.Files.OpenedPath); Equal(".HOT", f.Files.Extension);
        }
        foreach (GameMode mode in Enum.GetValues<GameMode>()) Equal(mode == GameMode.SinglePlayer ? ".SAV" : mode == GameMode.Hotseat ? ".HOT" : mode == GameMode.Email ? ".EML" : ".NET", ClassicGameFiles.SelectExtension(false, mode, "prior")); Equal("prior", ClassicGameFiles.SelectExtension(false, (GameMode)99, "prior")); Equal(".SCN", ClassicGameFiles.SelectExtension(true, (GameMode)99, "prior"));
    }
    private static void GameFileLiveRoundtripAndOpaqueLifetimes()
    {
        using var f = new GameFileFixture(); var original = ClassicFiles.DecodeStoredSave(f.Bytes); var civs = original.Civilizations.ToArray(); civs[1] = civs[1] with { Retained65 = 91 }; var units = original.Units.ToArray(); units[0] = units[0] with { RetainedWord = 0xabcd }; var cities = original.Cities.ToArray(); cities[0] = cities[0] with { RetainedByte = 79 };
        var strings = new OwnedList<byte>?[35]; strings[0] = new("Unit"u8.ToArray()); original = original with { Globals = original.Globals with { Retained34 = 812 }, Civilizations = new(civs), Units = new(units), Cities = new(cities), Events = new([new(new() { UnitName = 1, Retained = new(Enumerable.Repeat((byte)0xa5, 40)) }, new(strings))]) }; f.Bytes = ClassicFiles.EncodeStoredSave(original);
        Equal((int)CoreLoadStatus.Success, (int)f.Load()); f.Runtime.World.Globals.Turn = 77; f.Runtime.World.Entities.Cities.Storage[0].FoodBox = 1234;
        Equal((int)CoreSaveStatus.Success, (int)f.Save()); var stored = ClassicFiles.DecodeStoredSave(f.Output.ToArray()); Equal((short)812, stored.Globals.Retained34); Equal((byte)91, stored.Civilizations[1].Retained65); Equal((ushort)0xabcd, stored.Units[0].RetainedWord); Equal((byte)79, stored.Cities[0].RetainedByte); Equal(new OwnedList<byte>(Enumerable.Repeat((byte)0xa5, 40)), stored.Events![0].Record.Retained);
        using var next = new GameFileFixture { Bytes = f.Output.ToArray() }; Equal((int)CoreLoadStatus.Success, (int)next.Load()); Equal((short)77, next.Runtime.World.Globals.Turn); Equal((short)1234, next.Runtime.World.Entities.Cities.Storage[0].FoodBox);
        f.Output.Clear(); f.Runtime.World.Entities.Units.Storage[0].Token = new(9000); f.Runtime.World.Entities.Cities.Storage[0].Token = new(9001); f.Runtime.World.Events.Collection.Reset(); f.Runtime.World.Events.Collection.Append(); f.Save(); stored = ClassicFiles.DecodeStoredSave(f.Output.ToArray()); Equal((ushort)0, stored.Units[0].RetainedWord); Equal((byte)0, stored.Cities[0].RetainedByte); Equal(true, stored.Events![0].Record.Retained.All(b => b == 0));
        var old = f.Files.RecordBytes!; f.Runtime.PrepareNewGame(); Equal(true, old.For(f.Runtime.World.Lifetime) is null);
    }
}
