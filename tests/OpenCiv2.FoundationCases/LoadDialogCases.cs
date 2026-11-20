using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F06 §11 with real U01 repair/deletion. V01/S02 execution and presentation remain supplied children.
    private static void LoadedUnitsScanAndRepair()
    {
        var f = new DeletionFixture(); f.World.Globals.Multiplayer = GameMode.Tcp; var reports = new List<StackDiagnostic>();
        f.Unit(0).Previous = new UnitSlot(3000); f.Unit(0).Next = new UnitSlot(3001); f.Live(1); f.Unit(1).Position = f.Unit(1).Position with { X = -2 }; f.Unit(1).Next = new UnitSlot(2);
        f.Live(2); f.Unit(2).Previous = new UnitSlot(1); f.Unit(2).Orders = UnitOrder.Sleeping; f.Unit(2).Carrier = new UnitSlot(1);
        f.Unit(3).Previous = new UnitSlot(99); f.Unit(3).Next = new UnitSlot(100); f.Live(4); f.Unit(4).Position = new(100, 100); f.Live(5); f.Unit(5).Position = f.Unit(5).Position with { X = -20 }; f.Bound(5); var outside = f.Unit(5);
        ClassicLoadedUnits.Cleanup(f.World, f.Process.Rules.Definitions, f.Budget, f, reports);
        Equal(true, f.World.Multiplayer.NetworkHost); Equal(1, reports.Count); Equal(0U, f.Unit(1).Token.Value); Equal((short)-200, f.Unit(1).Position.X); Equal((short)-1, (f.Unit(2).Previous?.Value ?? -1)); Equal((sbyte)UnitOrder.None, (sbyte)f.Unit(2).Orders);
        foreach (int slot in new[] { 0, 3 }) { Equal((short)-1, (f.Unit(slot).Previous?.Value ?? -1)); Equal((short)-1, (f.Unit(slot).Next?.Value ?? -1)); }
        Equal(5U, f.Unit(4).Token.Value); Equal(outside, f.Unit(5)); Equal(5, f.World.Entities.Units.End); Equal(9, f.World.UnitTotals[1]); Equal((byte)9, f.Civ.UnitsActive[3]); f.Trace("");
        foreach (bool x in new[] { false, true }) { f = new(); f.Live(0); if (x) f.Unit(0).Position = f.Unit(0).Position with { X = -1 }; else f.Unit(0).Position = f.Unit(0).Position with { Y = -1 }; ClassicLoadedUnits.Cleanup(f.World, f.Process.Rules.Definitions, f.Budget, f); Equal(0U, f.Unit(0).Token.Value); }
        f = new(); f.Live(0); f.Unit(0).Previous = f.Unit(0).Next = new UnitSlot(0); reports.Clear(); ClassicLoadedUnits.Cleanup(f.World, f.Process.Rules.Definitions, f.Budget, f, reports);
        Equal(new StackDiagnostic(StackProblem.Infinite, 0), reports.Single()); Equal(1U, f.Unit(0).Token.Value); Equal((short)-1, (f.Unit(0).Next?.Value ?? -1)); Equal((short)-1, (f.Unit(0).Previous?.Value ?? -1)); Equal(10, f.World.UnitTotals[1]);
    }
    private static void LoadedUnitsLiveTailAndFailure()
    {
        var f = new DeletionFixture(); f.Live(0); f.Unit(0).Position = f.Unit(0).Position with { X = -2 }; f.Role(5); f.Civ.UnitsActive[3] = 1;
        f.OnCheck = () => { Equal(0, f.World.Entities.Units.End); Equal(true, f.World.UnitDeletion.Active); f.Unit(1).Previous = new UnitSlot(67); f.Unit(1).Next = new UnitSlot(68); f.Live(2); f.Unit(2).Position = f.Unit(2).Position with { Y = -2 }; f.Unit(2).Type = new(4); f.Civ.UnitsActive[4] = 1; };
        ClassicLoadedUnits.Cleanup(f.World, f.Process.Rules.Definitions, f.Budget, f); f.Trace("check:1,-1"); Equal((short)-1, (f.Unit(1).Next?.Value ?? -1)); Equal(0U, f.Unit(2).Token.Value); Equal(2, f.World.Entities.Units.End); Equal((short)2, f.World.Globals.UnitEnd); Equal(8, f.World.UnitTotals[1]);
        f = new(); f.Live(0); f.Budget.Steps = 0; Throws<StackResourceStop>(() => ClassicLoadedUnits.Cleanup(f.World, f.Process.Rules.Definitions, f.Budget, f)); Equal(true, f.World.Multiplayer.NetworkHost); Equal(1U, f.Unit(0).Token.Value);
        f = new(); f.Live(0); f.Unit(0).Position = f.Unit(0).Position with { X = -1 }; f.Role(5); f.Civ.UnitsActive[3] = 1; f.OnCheck = () => throw new IOException("V01"); Throws<IOException>(() => ClassicLoadedUnits.Cleanup(f.World, f.Process.Rules.Definitions, f.Budget, f)); Equal(0U, f.Unit(0).Token.Value); Equal(true, f.World.UnitDeletion.Active);
    }
    private static StoredSave DialogSave(bool scenario = false)
    {
        var saved = ClassicFiles.DecodeStoredSave(SaveVector());
        return saved with
        {
            Globals = saved.Globals with { UnitEnd = 0, CityEnd = 0, PlayerCiv = 1, SavedCurrentCiv = 1, TurnCiv = 1, CivsAlive = 3, HumanPlayers = 2, GameFlags = scenario ? (ushort)0x40 : (ushort)0, GameOptions = 0x8000 },
            Civilizations = new(saved.Civilizations.Select(c => c with { CityCount = 0 })),
            Units = new([]),
            Cities = new([]),
            Scenario = scenario ? new() : null,
            Events = null,
            Trailing = new([])
        };
    }
    private sealed class LoadDialogFixture : IDisposable, IUnitDeletionEffects
    {
        public GameFileFixture File = new(); public ClassicLoadDialogState Dialog = new() { ChosenPath = "prior", LoadedFileKind = 19 };
        public LoadDialogServices Services; public StackBudget Budget = new(100_000, 64); public OwnedList<string> Entries = new([]); public int Enumerations;
        public OwnedList<EventInput>? Parsed = new([new(new() { Turn = 42 }, new(Enumerable.Range(0, Event.StringCount).Select(i => i == 4 ? "Authored" : null)))]);
        public Action? OnCheck; public RuleInput Rules = new(); public bool ResourceExists = true;
        public LoadDialogFixture()
        {
            File.Bytes = ClassicFiles.EncodeStoredSave(DialogSave()); File.Runtime.Process.OrdinaryPreferences.GameOptions = StateConversion.DecodeGameOptions(0x10018);
            File.Services = File.Services with { Exists = (directory, name) => { File.Trace.Add("probe:" + directory + "\\" + name); return name.StartsWith("EVENTS.") && ResourceExists; }, LoadContent = () => { File.Trace.Add("content"); return new(Rules); } };
            Services = new(File.Services, TextCase.Ascii.UppercasePath, i => File.Trace.Add("window:" + i), (directory, pattern) => { ++Enumerations; File.Trace.Add("enumerate:" + directory + "\\" + pattern); return Entries; },
                () => { File.Trace.Add("parse"); return Parsed; }, () => File.Trace.Add("scenario"), () => File.Trace.Add("music"), () => File.Trace.Add("title"),
                enabled => File.Trace.Add(enabled ? "sound:on" : "sound:off"), status => { Equal(19, Dialog.LoadedFileKind); File.Trace.Add("notice:" + (int)status); }, this);
        }
        public CoreLoadStatus? Load(string? path = "C:\\home\\world.sav", bool title = true) => ClassicLoadDialog.Load(File.Runtime, File.Files, Dialog, path, title, Services, Budget, "dialog/one");
        public void SetChoice(int? code)
        {
            var choices = new List<Choice>(); if (code.HasValue) choices.Add(Choice.Create(new("F06.scenario-alternative", "dialog/one"), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(code.Value)));
            File.Runtime.GameplayRandom.Restore(new GameplayRandom(new ControlledChoices(new(choices)), "session").Capture(), 1000);
        }
        public void UnitRemoved(MapPoint p) => throw new Exception("Unexpected deletion notification");
        public void RefreshHomeCity(int city) => throw new Exception("Unexpected city notification");
        public void SynchronizeDeletion(MapPoint p, CitySlot? home) => throw new Exception("Unexpected deletion synchronization");
        public void CheckCivilization(int owner, int killer) { Equal(1, owner); Equal(-1, killer); File.Trace.Add("destroyed"); OnCheck?.Invoke(); }
        public void Dispose() => File.Dispose();
        public string Tail() => string.Join(';', File.Trace.Skip(File.Trace.IndexOf("flush") + 1));
    }
    private static void LoadDialogCancellationAndAdmission()
    {
        using var cancelled = new LoadDialogFixture(); var random = cancelled.File.Runtime.GameplayRandom.Capture(); cancelled.File.Files.Text.Home = "new-home";
        cancelled.Services = cancelled.Services with { Input = cancelled.Services.Input with { Storage = null!, Flush = null! }, CloseMapWindow = null!, SynchronizeMusic = null!, Deletion = null! };
        Equal(true, cancelled.Load(null) is null); Equal("cd:new-home", string.Join(';', cancelled.File.Trace)); Equal("prior", cancelled.Dialog.ChosenPath); Equal(19, cancelled.Dialog.LoadedFileKind); Equal(random, cancelled.File.Runtime.GameplayRandom.Capture());
        using var missing = new LoadDialogFixture(); missing.Services = missing.Services with { SynchronizeMusic = null! }; Throws<ArgumentNullException>(() => missing.Load()); Equal(0, missing.File.Trace.Count);
        foreach (string path in new[] { "none", "C:\\bad\0" }) { using var f = new LoadDialogFixture(); Throws<ArgumentException>(() => f.Load(path)); Equal(0, f.File.Trace.Count); f.File.Runtime.PrepareNewGame(); }
        using var pending = new LoadDialogFixture(); pending.File.Runtime.EndClientSession(); pending.File.Runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); pending.File.Runtime.BeginClientSession(new()); SessionClient(pending.File.Runtime).Submit(CommandRequestFor()); Throws<InvalidOperationException>(() => pending.Load()); Equal(0, pending.File.Trace.Count);
    }
    private static void LoadDialogCoreStatuses()
    {
        foreach (var status in new[] { CoreLoadStatus.Input, CoreLoadStatus.Unsupported, CoreLoadStatus.Data, CoreLoadStatus.Newer })
        {
            using var f = new LoadDialogFixture(); if (status == CoreLoadStatus.Input) f.File.OpenOk = false; else if (status == CoreLoadStatus.Data) f.File.Bytes = f.File.Bytes[..12]; else f.File.Bytes[10] = status == CoreLoadStatus.Newer ? (byte)0x2d : (byte)0x24;
            Equal((int)status, (int)f.Load()!.Value); Equal(string.Join(';', Enumerable.Range(1, 7).Select(i => "window:" + i)), string.Join(';', f.File.Trace.Take(7))); Equal("read-open:C:\\home\\world.sav", f.File.Trace[7]);
            Equal("notice:" + (int)status, f.File.Trace[^1]); Equal(0, f.Dialog.LoadedFileKind); Equal("prior", f.Dialog.ChosenPath); Equal(false, f.File.Runtime.World.Multiplayer.NetworkHost); Equal(false, f.File.Trace.Contains("music")); Equal(status == CoreLoadStatus.Input ? 0 : 1, f.File.Closes);
        }
    }
    private static void LoadDialogAlternativeChoices()
    {
        int[] codes = [0, 8191, 8192, 16383, 16384, 24575, 24576, 32767]; string[] names = ["chosen.bin", "chosen.bin", "z.ALT", "z.ALT", "a.ALT", "a.ALT", "middle.ALT", "middle.ALT"];
        for (int i = 0; i < codes.Length; ++i)
        {
            using var f = new LoadDialogFixture(); f.Entries = new(["z.ALT", "a.ALT", "middle.ALT"]); f.File.OpenOk = false; f.SetChoice(codes[i]); var presentation = f.File.Runtime.PresentationRandom.Capture();
            Equal((int)CoreLoadStatus.Input, (int)f.Load("C:\\home\\campaign.sCn.extra\\chosen.bin")!.Value); Equal("C:\\home\\campaign.sCn.extra\\" + names[i], f.File.Files.OpenedPath); Equal(1, f.Enumerations); f.File.Runtime.GameplayRandom.RequireChoicesConsumed(); Equal(presentation, f.File.Runtime.PresentationRandom.Capture());
        }
        foreach (bool scenario in new[] { false, true }) { using var f = new LoadDialogFixture(); f.File.OpenOk = false; f.SetChoice(null); var before = f.File.Runtime.GameplayRandom.Capture(); f.Load(scenario ? "C:\\home\\choice.scn" : "C:\\home\\choice.sav"); Equal(scenario ? 1 : 0, f.Enumerations); Equal(before, f.File.Runtime.GameplayRandom.Capture()); }
        using var accepted = new LoadDialogFixture(); var saved = DialogSave(); accepted.File.Bytes = ClassicFiles.EncodeStoredSave(saved with { Globals = saved.Globals with { Multiplayer = 3 }, Multiplayer = new() }); accepted.File.Runtime.World.Globals.Multiplayer = GameMode.Tcp;
        accepted.Entries = new(["last.ALT", "first.ALT"]); accepted.SetChoice(32767); Equal((int)CoreLoadStatus.Success, (int)accepted.Load("C:\\home\\start.scn")!.Value); Equal("C:\\home\\first.ALT", accepted.Dialog.ChosenPath); Equal(1, accepted.Enumerations); accepted.File.Runtime.GameplayRandom.RequireChoicesConsumed();
    }
    private static void LoadDialogAlternativeBounds()
    {
        foreach (string name in new[] { "", ".", "..", "C:escape", "a/b", "a\\b", "bad\0name" })
        {
            using var f = new LoadDialogFixture(); f.Entries = new([name]); f.SetChoice(32767); Throws<ArgumentException>(() => f.Load("C:\\home\\choice.scn")); Equal(0, f.File.Opens); f.File.Runtime.GameplayRandom.RequireChoicesConsumed(); Equal(19, f.Dialog.LoadedFileKind);
        }
        using var oversized = new LoadDialogFixture(); oversized.Entries = new(Enumerable.Repeat("entry.ALT", 65536)); oversized.SetChoice(null); var before = oversized.File.Runtime.GameplayRandom.Capture(); Throws<ArgumentOutOfRangeException>(() => oversized.Load("C:\\home\\choice.scn")); Equal(before, oversized.File.Runtime.GameplayRandom.Capture()); Equal(0, oversized.File.Opens);
        using var maximum = new LoadDialogFixture(); maximum.Entries = new(Enumerable.Repeat("entry.ALT", 65535)); maximum.SetChoice(32767); maximum.File.OpenOk = false; maximum.Load("C:\\home\\choice.scn"); Equal("C:\\home\\entry.ALT", maximum.File.Files.OpenedPath); maximum.File.Runtime.GameplayRandom.RequireChoicesConsumed();
        using var unselected = new LoadDialogFixture(); unselected.Entries = new(["..\\bad"]); unselected.SetChoice(0); unselected.File.OpenOk = false; unselected.Load("C:\\home\\choice.scn"); Equal("C:\\home\\choice.scn", unselected.File.Files.OpenedPath);
        using var missing = new LoadDialogFixture(); missing.Entries = new(["valid.ALT"]); missing.SetChoice(null); ChoiceFails(ChoiceFailure.Missing, () => missing.Load("C:\\home\\choice.scn"), new("F06.scenario-alternative", "dialog/one")); Equal(0, missing.File.Opens);
    }
    private static void LoadDialogEventsAndOptions()
    {
        using var ordinary = new LoadDialogFixture(); var stale = ordinary.File.Runtime.World.Events.Collection.Append(); ordinary.File.Runtime.World.Events.Loaded = true;
        Equal((int)CoreLoadStatus.Success, (int)ordinary.Load()!.Value); Equal("flush;sound:on;music;title;cd:C:\\home", ordinary.Tail()); Equal(StateConversion.DecodeGameOptions((uint)0x10018), ordinary.File.Runtime.World.Globals.GameOptions); Equal(false, ordinary.File.Runtime.World.Events.Collection.Contains(stale)); Equal(0, ordinary.File.Runtime.World.Events.Collection.Count); Equal("C:\\home\\world.sav", ordinary.Dialog.ChosenPath); Equal(19, ordinary.Dialog.LoadedFileKind);
        foreach (int branch in new[] { 0, 1, 2, 3 })
        {
            using var f = new LoadDialogFixture(); f.File.Bytes = ClassicFiles.EncodeStoredSave(DialogSave(true)); f.File.Files.Text.Extension = "GER"; f.ResourceExists = branch != 1; if (branch == 2) f.Parsed = null; if (branch == 3) f.Parsed = new([]);
            Equal((int)CoreLoadStatus.Success, (int)f.Load()!.Value); Equal(branch is 0 or 3, f.File.Runtime.World.Events.Loaded); Equal(branch == 0 ? 1 : 0, f.File.Runtime.World.Events.Collection.Count); Equal(branch is 0 or 3, f.File.Trace.Contains("scenario"));
            Equal(true, f.File.Trace.Contains("probe:C:\\home\\EVENTS.GER")); if (branch == 0) { Equal(42, f.File.Runtime.World.Events.Collection.At(0).Values.Turn); Equal("Authored", f.File.Runtime.World.Events.Collection.At(0).Strings[4].Value); }
        }
        foreach (bool scenario in new[] { false, true })
        {
            using var f = new LoadDialogFixture(); f.File.Bytes = ClassicFiles.EncodeStoredSave(DialogSave(scenario) with { Events = new([new(new() { Actions = 0x80000021, WhoCiv = 6, WhomCiv = 2 }, new(new OwnedList<byte>?[35]))]) });
            f.Services = f.Services with { LoadEvents = () => throw new Exception("Persisted event replaced"), PresentTitle = null }; f.Load(title: false); Equal("flush;sound:on;scenario;music;cd:C:\\home", f.Tail()); Equal(StateConversion.DecodeEventActions(0x80000021U), f.File.Runtime.World.Events.Collection.At(0).Values.Actions);
        }
    }
    private static void LoadDialogLiveChildren()
    {
        foreach (bool enable in new[] { false, true })
        {
            using var f = new LoadDialogFixture(); f.File.Bytes = ClassicFiles.EncodeStoredSave(DialogSave(true)); f.File.Runtime.Process.OrdinaryPreferences.GameOptions = StateConversion.DecodeGameOptions(enable ? 0U : 8U);
            f.Services = f.Services with
            {
                ConfigureSound = sound => { Equal(false, sound); f.File.Runtime.World.Globals.GameOptions = StateConversion.DecodeGameOptions(enable ? 8U : 0U); },
                ScenarioLoaded = () => { Equal(true, enable); f.File.Trace.Add("scenario"); f.File.Runtime.World.Globals.GameOptions = StateConversion.DecodeGameOptions(0); f.File.Runtime.World.Events.Loaded = false; },
                SynchronizeMusic = () => Equal(StateConversion.DecodeGameOptions(0U), f.File.Runtime.World.Globals.GameOptions),
                PresentTitle = () => f.File.Files.Text.Home = "changed-home"
            };
            f.Load(); Equal(enable, f.File.Trace.Contains("scenario")); Equal("cd:changed-home", f.File.Trace[^1]); Equal(1, f.File.Runtime.World.Events.Collection.Count);
        }
        using var flush = new LoadDialogFixture(); flush.Services = flush.Services with { Input = flush.Services.Input with { Flush = () => { flush.File.Trace.Add("flush"); flush.File.Runtime.World.Globals.Status.Scenario = true; flush.File.Runtime.Process.OrdinaryPreferences.GameOptions = StateConversion.DecodeGameOptions(8); flush.File.Files.Text.Extension = "FRE"; } } };
        flush.Load(); Equal(true, flush.File.Trace.Contains("probe:C:\\home\\EVENTS.FRE")); Equal(StateConversion.DecodeGameOptions(8U), flush.File.Runtime.World.Globals.GameOptions); Equal("flush;probe:C:\\home\\EVENTS.FRE;parse;sound:off;scenario;music;title;cd:C:\\home", flush.Tail());
    }
    private static void LoadDialogCleanupAndPublication()
    {
        using var f = new LoadDialogFixture(); var saved = DialogSave(); f.File.Bytes = ClassicFiles.EncodeStoredSave(saved with { Globals = saved.Globals with { UnitEnd = 3 }, Units = new([new() { Previous = 99, Next = 99 }, new() { Token = new(91), Owner = new(1), Type = new(2), X = -1 }, new() { Token = new(92), Owner = new(1), Type = new(3), Orders = UnitOrder.Sleeping, DestinationX = 1 }]) });
        var rules = f.Rules.Definitions.Units.ToArray(); rules[2] = rules[2] with { Role = RuleRole.Settle }; f.Rules = f.Rules with { Definitions = f.Rules.Definitions with { Units = new(rules) } };
        f.File.Runtime.World.UnitTotals[1] = 2; var client = SessionClient(f.File.Runtime); var identity = client.Identity;
        f.Services = f.Services with { PresentTitle = () => { Equal(false, f.File.Runtime.World.Multiplayer.NetworkHost); Equal(2, client.OwnUnits().Records.Count); Equal("prior", f.Dialog.ChosenPath); f.File.Trace.Add("title"); } };
        f.OnCheck = () => { Equal(1, client.OwnUnits().Records.Count); Equal(new UnitSlot(2), client.OwnUnits().Records[0].Slot); Equal((sbyte)UnitOrder.None, (sbyte)client.OwnUnits().Records[0].Orders); Equal("C:\\home", f.File.Files.Text.Working); Equal("prior", f.Dialog.ChosenPath); Throws<InvalidOperationException>(() => f.File.Runtime.PrepareNewGame()); };
        Equal((int)CoreLoadStatus.Success, (int)f.Load()!.Value); Equal("flush;sound:on;music;title;cd:C:\\home;destroyed", f.Tail()); Equal(identity, client.Identity); Equal(0U, f.File.Runtime.World.Entities.Units.Storage[1].Token.Value); Equal((short)-1, (f.File.Runtime.World.Entities.Units.Storage[0].Next?.Value ?? -1)); Equal(1, f.File.Runtime.World.UnitTotals[1]);
    }
    private static void LoadDialogFailurePrefixes()
    {
        using var notice = new LoadDialogFixture(); notice.File.OpenOk = false; notice.Services = notice.Services with { LoadNotice = _ => throw new IOException("notice") }; Throws<IOException>(() => notice.Load()); Equal(19, notice.Dialog.LoadedFileKind); Equal("prior", notice.Dialog.ChosenPath);
        using var cleanup = new LoadDialogFixture(); var saved = DialogSave(); cleanup.File.Bytes = ClassicFiles.EncodeStoredSave(saved with { Globals = saved.Globals with { UnitEnd = 1 }, Units = new([new() { Token = new(19), Owner = new(1) }]) }); cleanup.Budget.Steps = 0;
        Throws<StackResourceStop>(() => cleanup.Load()); Equal("flush;sound:on;music;title;cd:C:\\home", cleanup.Tail()); Equal(true, cleanup.File.Runtime.World.Multiplayer.NetworkHost); Equal(100U, cleanup.File.Runtime.World.Entities.Units.Storage[0].Token.Value); Equal("prior", cleanup.Dialog.ChosenPath); Equal(19, cleanup.Dialog.LoadedFileKind);
        for (int stage = 0; stage < 5; ++stage)
        {
            using var f = new LoadDialogFixture(); f.File.Bytes = ClassicFiles.EncodeStoredSave(DialogSave(true));
            f.Services = stage switch { 0 => f.Services with { CloseMapWindow = _ => throw new IOException("window") }, 1 => f.Services with { LoadEvents = () => throw new IOException("parse") }, 2 => f.Services with { ConfigureSound = _ => throw new IOException("sound") }, 3 => f.Services with { PresentTitle = () => throw new IOException("title") }, _ => f.Services with { LoadEvents = () => new([new(new(), new([]))]) } };
            if (stage == 4) Throws<ArgumentException>(() => f.Load()); else Throws<IOException>(() => f.Load()); Equal(stage == 0 ? 0 : 1, f.File.Opens); Equal("prior", f.Dialog.ChosenPath); Equal(false, f.File.Runtime.World.Multiplayer.NetworkHost); if (stage == 4) Equal(false, f.File.Runtime.World.Events.Loaded); f.File.Runtime.PrepareNewGame();
        }
    }
}
