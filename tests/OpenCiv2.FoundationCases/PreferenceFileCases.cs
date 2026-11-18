using OpenCiv2.Base;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CallbackInput(Func<int, ReadResult> item, Func<bool> close, Func<byte?>? readByte = null) : CoreInput
    {
        protected override ReadResult ReadItem(int size) => item(size);
        protected override byte? ReadByte() => readByte is not null ? readByte() : throw new InvalidOperationException("No byte service");
        protected override bool Finish() => close();
    }
    private delegate bool WriteItem(ReadOnlySpan<byte> bytes);
    private sealed class CallbackOutput(WriteItem item, Func<bool> close) : CoreOutput
    {
        protected override bool WriteItem(ReadOnlySpan<byte> bytes) => item(bytes);
        protected override bool Finish() => close();
    }
    private sealed class CallbackStorage : IFileStorage
    {
        public Func<string, CoreInput?> ReadFile = _ => null;
        public Func<string, CoreOutput?> ReplaceFile = _ => null;
        public Func<string, bool> RemoveFile = _ => false;
        public CoreInput? Read(string path) => ReadFile(path);
        public CoreOutput? Replace(string path) => ReplaceFile(path);
        public bool Remove(string path) => RemoveFile(path);
    }
    private sealed class PreferenceFixture
    {
        public ProcessContext Process = new();
        public PreferenceDefaults Defaults = new(1000, true);
        public CallbackStorage Storage = new();
        public List<string> Trace = [];
        public List<byte> Output = [];
        public byte[] Input = Enumerable.Repeat((byte)0x88, 539).ToArray();
        public int Position, Reads, Writes, Closes, Removals;
        public bool InputOpen = true, OutputOpen = true, CloseOk = true;
        public int? ShortRead, FailWrite;
        public int Prefix;
        public Action<int>? BeforeRead, AfterWrite;
        public Action? BeforeClose;
        public PreferenceFixture()
        {
            Process.OrdinaryPreferences = StateConversion.ToLive(ClassicPreferences.DecodeOrdinary(Enumerable.Repeat((byte)0x11, 72).ToArray()));
            Process.MultiplayerPreferences = StateConversion.ToLive(ClassicPreferences.DecodeMultiplayer(Enumerable.Repeat((byte)0x22, 444).ToArray()));
            Storage.ReadFile = path =>
            {
                Equal("CIV2.DAT", path); Trace.Add("open-input");
                return InputOpen ? new CallbackInput(size =>
                {
                    int index = Reads++; Trace.Add("read:" + size); BeforeRead?.Invoke(index); int n = Math.Min(size, Input.Length - Position); bool failed = ShortRead == index;
                    if (failed) n = Math.Min(n, Prefix); var bytes = Input.AsSpan(Position, n).ToArray(); Position += n; return new(new(bytes), !failed && n == size);
                }, Close) : null;
            };
            Storage.ReplaceFile = path =>
            {
                Equal("CIV2.DAT", path); Trace.Add("open-output");
                return OutputOpen ? new CallbackOutput(bytes => { int index = Writes++; Trace.Add("write:" + bytes.Length); Output.AddRange(bytes.ToArray()); AfterWrite?.Invoke(index); return FailWrite != index; }, Close) : null;
            };
            Storage.RemoveFile = path => { Equal("CIV2.DAT", path); Trace.Add("remove"); ++Removals; return false; };
        }
        private bool Close() { Trace.Add("close"); ++Closes; BeforeClose?.Invoke(); return CloseOk; }
        public PreferenceLoadStatus Load() => PreferenceFiles.Load(Process, Defaults, Storage);
        public PreferenceSaveStatus Save() => PreferenceFiles.Save(Process, Storage);
    }
    private static void PreferenceFileLoadDefaultsAndStaging()
    {
        var missing = new PreferenceFixture { InputOpen = false }; Equal((int)PreferenceLoadStatus.Defaults, (int)missing.Load()); Equal(new OwnedList<string>(["open-input"]), new(missing.Trace)); Equal((short)2, missing.Process.OrdinaryPreferences.DefaultLevel); Equal(new OwnedList<byte>("60"u8.ToArray()), missing.Process.MultiplayerPreferences.CustomTimer);
        foreach (int item in new[] { 0, 1 }) for (int prefix = 0; prefix <= (item == 0 ? 72 : 444); ++prefix)
        {
            var f = new PreferenceFixture { ShortRead = item, Prefix = prefix }; var entry = new PreferenceSnapshot(f.Process.OrdinaryPreferences, f.Process.MultiplayerPreferences);
            f.BeforeRead = index => { Equal(entry.Ordinary, f.Process.OrdinaryPreferences); Equal(entry.Multiplayer, f.Process.MultiplayerPreferences); f.Defaults = new(999, false); };
            f.BeforeClose = () => { Equal(entry.Ordinary, f.Process.OrdinaryPreferences); Equal(entry.Multiplayer, f.Process.MultiplayerPreferences); };
            Equal((int)PreferenceLoadStatus.Defaults, (int)f.Load()); Equal(1, f.Closes); Equal(2, f.Reads); Equal(prefix + (item == 0 ? 444 : 72), f.Position);
            var ordinary = new OrdinaryPreferences(); ordinary.SetDefaults(1000, true); var multiplayer = new MultiplayerPreferences(); multiplayer.SetDefaults();
            Equal(item == 0 ? ordinary : StateConversion.ToLive(ClassicPreferences.DecodeOrdinary(Enumerable.Repeat((byte)0x88, 72).ToArray())), f.Process.OrdinaryPreferences);
            Equal(item == 1 ? multiplayer : StateConversion.ToLive(ClassicPreferences.DecodeMultiplayer(Enumerable.Repeat((byte)0x88, 444).ToArray())), f.Process.MultiplayerPreferences);
            Equal(new OwnedList<string>(["open-input", "read:72", "read:444", "close"]), new(f.Trace));
        }
        var complete = new PreferenceFixture(); Equal((int)PreferenceLoadStatus.Success, (int)complete.Load()); Equal(516, complete.Position); Equal(32, complete.Process.OrdinaryPreferences.Text.Count); Equal(6, complete.Process.MultiplayerPreferences.CustomTimer.Count); Equal(1, complete.Closes);
        foreach (bool shortInput in new[] { false, true })
        {
            var f = new PreferenceFixture(); f.Process.OrdinaryPreferences.Text = new(new byte[100]); f.Process.MultiplayerPreferences.GameName = new(new byte[100]); if (shortInput) f.Input = [];
            Equal((int)(shortInput ? PreferenceLoadStatus.Defaults : PreferenceLoadStatus.Success), (int)f.Load()); Equal(shortInput ? 0 : 32, f.Process.OrdinaryPreferences.Text.Count); Equal(shortInput ? 0 : 32, f.Process.MultiplayerPreferences.GameName.Count);
        }
    }
    private static void PreferenceFileLoadFailures()
    {
        foreach (int failure in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var f = new PreferenceFixture(); var before = new PreferenceSnapshot(f.Process.OrdinaryPreferences, f.Process.MultiplayerPreferences);
            if (failure == 0) f.CloseOk = false;
            else if (failure <= 2) { f.BeforeRead = i => { if (i == failure - 1) throw new IOException("read"); }; f.BeforeClose = () => throw new IOException("close"); }
            else if (failure == 3) f.BeforeClose = () => throw new IOException("close");
            else
            {
                f.Storage.ReadFile = _ => new CallbackInput(size => new(new(new byte[size + (failure == 4 ? 1 : -1)]), failure == 5), () => { ++f.Closes; return true; });
            }
            if (failure == 0) Equal((int)PreferenceLoadStatus.Input, (int)f.Load());
            else if (failure <= 3) { try { f.Load(); throw new Exception("Failure expected"); } catch (IOException e) { Equal(failure == 3 ? "close" : "read", e.Message); } }
            else if (failure == 4) Throws<ArgumentException>(() => f.Load()); else Throws<DecodeException>(() => f.Load());
            Equal(before.Ordinary, f.Process.OrdinaryPreferences); Equal(before.Multiplayer, f.Process.MultiplayerPreferences); Equal(1, f.Closes);
        }
    }
    private static void PreferenceFileSaveResults()
    {
        foreach (int failed in new[] { -1, 0, 1, 2, 3 })
        {
            var f = new PreferenceFixture { FailWrite = failed is 0 or 1 ? failed : null, CloseOk = failed != 2, OutputOpen = failed != 3 };
            var before = new PreferenceSnapshot(f.Process.OrdinaryPreferences, f.Process.MultiplayerPreferences);
            var ordinary = ClassicPreferences.EncodeOrdinary(StateConversion.ToStored(before.Ordinary)); var multiplayer = ClassicPreferences.EncodeMultiplayer(StateConversion.ToStored(before.Multiplayer));
            Equal((int)(failed < 0 ? PreferenceSaveStatus.Success : PreferenceSaveStatus.Output), (int)f.Save()); Equal(failed == 3 ? 0 : 1, f.Closes); Equal(failed is >= 0 and <= 2 ? 1 : 0, f.Removals); Equal(failed == 3 ? 0 : failed == 0 ? 1 : 2, f.Writes);
            BytesEqual(failed == 3 ? [] : failed == 0 ? ordinary : [.. ordinary, .. multiplayer], f.Output); Equal(before.Ordinary, f.Process.OrdinaryPreferences); Equal(before.Multiplayer, f.Process.MultiplayerPreferences);
            if (f.Removals != 0) { Equal("close", f.Trace[^2]); Equal("remove", f.Trace[^1]); }
        }
    }
    private static void PreferenceFileSaveExceptions()
    {
        foreach (int failed in new[] { 0, 1, 2 })
        {
            var f = new PreferenceFixture(); f.AfterWrite = i => { if (i == failed) throw new IOException("write"); }; f.BeforeClose = () => throw new IOException("close");
            try { f.Save(); throw new Exception("Failure expected"); } catch (IOException e) { Equal(failed == 2 ? "close" : "write", e.Message); }
            Equal(1, f.Closes); Equal(0, f.Removals); Equal(failed == 0 ? 72 : 516, f.Output.Count);
        }
        var allocation = new PreferenceFixture(); allocation.AfterWrite = _ => throw new OutOfMemoryException(); Throws<OutOfMemoryException>(() => allocation.Save()); Equal(1, allocation.Closes); Equal(0, allocation.Removals);
        foreach (int failedNotification in new[] { 2, 3, 4 })
        {
            var f = new PreferenceFixture(); int notifications = 0; var writer = new ClassicPreferenceWriter(f.Storage);
            Throws<IOException>(() => writer.Write(new(f.Process.OrdinaryPreferences, f.Process.MultiplayerPreferences), () => { if (++notifications == failedNotification) throw new IOException("notification"); }));
            Equal(1, f.Closes); Equal(0, f.Removals); Equal(failedNotification == 4 ? 516 : (failedNotification - 2) * 72, f.Output.Count);
        }
        var remove = new PreferenceFixture { FailWrite = 0 }; remove.Storage.RemoveFile = _ => throw new IOException("remove"); Throws<IOException>(() => remove.Save()); Equal(1, remove.Closes);
    }
    private static void PreferenceFileSnapshotsBeforeIo()
    {
        foreach (bool ordinary in new[] { false, true })
        {
            var f = new PreferenceFixture(); if (ordinary) f.Process.OrdinaryPreferences.Text = new(Enumerable.Repeat((byte)65, 33)); else f.Process.MultiplayerPreferences.CustomTimer = new(Enumerable.Repeat((byte)65, 7));
            int notifications = 0; Throws<ArgumentException>(() => new ClassicPreferenceWriter(f.Storage).Write(new(f.Process.OrdinaryPreferences, f.Process.MultiplayerPreferences), () => ++notifications)); Equal(0, notifications); Equal(0, f.Trace.Count);
        }
        var snapshot = new PreferenceFixture(); var values = new PreferenceSnapshot(snapshot.Process.OrdinaryPreferences, snapshot.Process.MultiplayerPreferences);
        int notified = 0; var output = new ClassicPreferenceWriter(snapshot.Storage); Equal(true, output.Write(values, () => { ++notified; })); Equal(4, notified);
        snapshot.Process.OrdinaryPreferences.Text = new([90]); Equal(32, values.Ordinary.Text.Count);
        // The exact encoded bytes are from the selected snapshot, including nonterminated fixed-width text.
        BytesEqual(ClassicPreferences.EncodeOrdinary(StateConversion.ToStored(values.Ordinary)), snapshot.Output.Take(72));
        BytesEqual(ClassicPreferences.EncodeMultiplayer(StateConversion.ToStored(values.Multiplayer)), snapshot.Output.Skip(72));
    }
    private static void PreferenceFilePublicationAndIsolation()
    {
        var f = new PreferenceFixture(); using var runtime = new SessionRuntime(f.Process, new(78)); runtime.BeginClientSession(new(10, 20)); var client = SessionClient(runtime); var random = runtime.GameplayRandom.Capture();
        runtime.World.Globals.GameOptions = 0x1234; runtime.World.Multiplayer.Saved.Simultaneous = true; Equal((int)PreferenceLoadStatus.Success, (int)f.Load());
        Equal(0x1234U, runtime.World.Globals.GameOptions); Equal(true, runtime.World.Multiplayer.Saved.Simultaneous); Equal(random, runtime.GameplayRandom.Capture());
        List<ulong> revisions = []; f.Storage.ReplaceFile = _ => { revisions.Add(client.OwnUnits().Observation.Revision); return new CallbackOutput(_ => { revisions.Add(client.OwnUnits().Observation.Revision); return true; }, () => { revisions.Add(client.OwnUnits().Observation.Revision); return false; }); };
        f.Storage.RemoveFile = _ => { revisions.Add(client.OwnUnits().Observation.Revision); return false; };
        Equal((int)PreferenceSaveStatus.Output, (int)PreferenceFiles.Save(runtime, f.Storage)); Equal(new OwnedList<ulong>([1, 2, 3, 4, 5]), new(revisions)); Equal(random, runtime.GameplayRandom.Capture());
        int opens = 0; f.Storage.ReplaceFile = _ => { ++opens; return null; }; PreferenceFiles.Save(f.Process, f.Storage); PreferenceFiles.Save(runtime, f.Storage); runtime.EndClientSession(); PreferenceFiles.Save(runtime, f.Storage); Equal(3, opens);
        var ordinary = f.Process.OrdinaryPreferences; var multiplayer = f.Process.MultiplayerPreferences; runtime.BeginClientSession(new(30, 40)); Equal(ordinary, f.Process.OrdinaryPreferences); Equal(multiplayer, f.Process.MultiplayerPreferences);
    }
}
