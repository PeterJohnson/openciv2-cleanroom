using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F05 §2. W02, creation, balancing, acceleration and presentation remain declared test providers.
    private sealed class EntryPreferences : IPreferenceWriter
    {
        public List<PreferenceSnapshot> Snapshots = [];
        public bool Write(PreferenceSnapshot values, Action? beforeIo = null) { Snapshots.Add(values); return true; }
    }
    private sealed class EntryFixture : IDisposable
    {
        public readonly PlayerFixture Players = new();
        public SessionRuntime Runtime => Players.Runtime;
        public ClassicFileState Files = new();
        public SizeInteraction Size = new();
        public RuleInput Input;
        public WorldEntryServices Services;
        public List<string> Trace = [];
        public List<List<byte>> Saved = [];
        public Action<string>? Observe;
        public string? SelectedMap = "authored.mp";
        public byte[] MapBytes = PremadeBytes(29, 0, false);
        public bool PreferenceOpen = true, PreferenceWrite = true;
        public string Introduced = "";
        public EntryFixture()
        {
            var text = new ClassicTextInput(); text.Files["scenario/RULES.TXT"] = string.Concat(ClassicRuleSections()); using var reader = text.Reader(); Input = new RuleLoader(reader, TextCase.Ascii).Load("RULES");
            Files.Text.Home = Files.Text.Search = Files.Text.Working = "home"; Files.DefaultGameStem = "home/GAME";
            Players.Observe = name => Event(name == "rules" ? "player-rules" : name); Players.Gender = _ => 0; Players.Tribe = (_, _) => new(0); Players.Style = (_, _) => 1;
            Size.Preset = _ => { Event("size"); return new WorldSizeChoice.Preset(0); };
            var preferences = new CallbackStorage { ReplaceFile = path => { Equal("CIV2.DAT", path); Event("save"); if (!PreferenceOpen) return null; var bytes = new List<byte>(); Saved.Add(bytes); return new CallbackOutput(item => { Event("write:" + item.Length); bytes.AddRange(item.ToArray()); return PreferenceWrite; }, () => { Event("close-preferences"); return true; }); }, RemoveFile = _ => { Event("delete"); return false; } };
            var mapStorage = new CallbackStorage { ReadFile = path => { Equal("authored.mp", path); Event("open-map"); int offset = 0; return new CallbackInput(count => { int n = Math.Min(count, MapBytes.Length - offset); var bytes = MapBytes.AsSpan(offset, n).ToArray(); offset += n; return new(new(bytes), n == count); }, () => { Event("close-map"); return true; }); } };
            var premade = new PremadeWorldServices(new ClassicPremadeWorldSource(mapStorage), () => Event("map-failure"), new(() => { Event("use-seed"); return 1; }, () => throw new Exception("Unexpected hints"), () => throw new Exception("Unexpected clock")));
            Services = new(() => Event("reset"), () => { Event("rules"); return new(Input); }, Size, () => { Event("choose-map"); return SelectedMap; }, premade,
                new((parameter, _) => { Event("parameter:" + (int)parameter); return new WorldParameterChoice.Selected(2); }, new ClassicPreferenceWriter(preferences)), Players,
                () => Event("stop-sound"), keep => { Event(keep ? "retain-world" : "generate-world"); if (!keep) Runtime.World.Map.Allocate(new(Runtime.World.WorldSetup.Width, Runtime.World.WorldSetup.Height)); },
                new(slot => { Event("create:" + slot); if (slot == Runtime.World.Globals.PlayerCiv) foreach (int advance in new[] { 0, 1, 89, 95 }) Runtime.World.Civilizations[slot].Advances[advance / 8] |= (byte)(1 << (advance % 8)); return -1; }, () => Event("balance")),
                selection => { Event("accelerate:" + selection); Runtime.World.Globals.Turn = 41; }, (slot, advances) => { Equal(new CivilizationSlot(1), slot); Introduced = ByteString(advances); Event("introduce"); });
        }
        public void Event(string name) { Trace.Add(name); Observe?.Invoke(name); }
        public bool Run(GameplayRandom random, NewWorldChoice choice = NewWorldChoice.Random, string call = "entry") => ClassicWorldEntry.Enter(Runtime, Files, random, call, choice, ClassicCalendar, Services);
        public void Dispose() => Players.Dispose();
    }
    private static GameplayRandom EntryChoices(bool parameters = true, bool players = true, bool initialize = true, string call = "entry")
    {
        var choices = new List<Choice>(); if (parameters) for (int i = 0; i < 5; ++i) choices.Add(Choice.Create(new(WorldChoiceSites[i], call + "/parameters"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(i)));
        if (players) { int ordinal = 0; foreach (int code in new[] { 1, 0, 3, 3, 5 }) choices.Add(Choice.Create(new("F05.setup.opponent-slot", call + "/players/" + ordinal++), new SamplingLaw.PrimaryModulo(8), ChoiceCode.Primary(code))); }
        if (initialize) choices.Add(HistoryChoice(call + "/initialize", 49)); return new(new ControlledChoices(new(choices)), "entry-test");
    }
    private static void WorldEntryOrderAndPreferenceSaves()
    {
        foreach (bool custom in new[] { false, true })
        {
            using var f = new EntryFixture(); f.Runtime.Process.OrdinaryPreferences.Science = f.Runtime.Process.OrdinaryPreferences.Tax = 8; f.Runtime.World.Globals.Turn = 77; var random = EntryChoices(!custom);
            Equal(true, f.Run(random, custom ? NewWorldChoice.Customize : NewWorldChoice.Random)); random.RequireChoicesConsumed(); Equal(custom ? 2 : 1, f.Saved.Count); Equal(516, f.Saved[^1].Count);
            var saved = ClassicPreferences.DecodeOrdinary(f.Saved[^1].Take(72).ToArray()); Equal((short)8, saved.Science); Equal((short)8, saved.Tax); Equal((short)6, f.Runtime.Process.OrdinaryPreferences.Science); Equal((short)4, f.Runtime.Process.OrdinaryPreferences.Tax);
            Equal((short)0, f.Runtime.World.Globals.Turn); Equal((short)-4000, f.Runtime.World.Globals.Year); Equal((byte)0x2b, f.Runtime.World.Globals.CivsAtStart); Equal("advances 1, advances 95, ", f.Introduced); Equal(false, f.Runtime.Active); Equal(80, f.Runtime.World.Map.Geometry.Width);
            var expected = new List<string> { "rules", "size" }; if (custom) expected.AddRange(["parameter:0", "parameter:1", "parameter:3", "parameter:2", "parameter:4", "save", "write:72", "write:444", "close-preferences"]);
            expected.AddRange(["difficulty", "opponents", "barbarians", "player-rules", "gender", "tribe", "name", "style", "save", "write:72", "write:444", "close-preferences", "stop-sound", "generate-world"]); expected.AddRange(Enumerable.Range(0, 8).Select(i => "create:" + i)); expected.AddRange(["balance", "introduce"]); Equal(new OwnedList<string>(expected), new(f.Trace));
        }
    }
    private static void WorldEntryCancellationPrefixes()
    {
        foreach (string stage in new[] { "size", "map-choice", "map-read", "parameter", "player" })
        {
            using var f = new EntryFixture(); var choice = stage.StartsWith("map", StringComparison.Ordinal) ? NewWorldChoice.Premade : stage is "parameter" or "player" ? NewWorldChoice.Customize : NewWorldChoice.Random;
            if (stage == "size") f.Size.Preset = _ => new WorldSizeChoice.Preset(-1); if (stage == "map-choice") f.SelectedMap = null; if (stage == "map-read") f.MapBytes = [1, 2];
            if (stage == "parameter") f.Services = f.Services with { Parameters = f.Services.Parameters with { Choose = (p, _) => new WorldParameterChoice.Selected(p == WorldParameter.Climate ? -1 : 2) } };
            if (stage == "player") f.Players.Difficulty = _ => -1;
            var random = EntryChoices(false, false, false); Equal(false, f.Run(random, choice)); random.RequireChoicesConsumed(); Equal(stage == "player" ? 1 : 0, f.Saved.Count); Equal(false, f.Trace.Contains("generate-world")); Equal(false, f.Trace.Contains("introduce"));
            if (stage == "map-read") { Equal(true, f.Trace.Contains("close-map")); Equal("map-failure", f.Trace[^1]); }
            if (stage == "parameter") { SetupParametersEqual([1, 1, 0, -2, 0], f.Runtime); SetupPreferencesEqual([1, 1, 0, 0, 0], f.Runtime); }
        }
        foreach (bool openFailure in new[] { false, true }) { using var f = new EntryFixture { PreferenceOpen = !openFailure, PreferenceWrite = openFailure }; Equal(true, f.Run(EntryChoices())); Equal("introduce", f.Trace[^1]); Equal(!openFailure, f.Trace.Contains("delete")); }
    }
    private static void WorldEntryPremadeAndOwnedRules()
    {
        foreach (ushort topology in new ushort[] { 0, 7 })
        {
            using var f = new EntryFixture(); f.MapBytes = PremadeBytes(29, topology, false); f.Players.CustomRules(new()); f.Players.Advanced = defaults => { Equal(topology != 0, defaults.FlatWorld); return new(-1, defaults); };
            var random = EntryChoices(); Equal(true, f.Run(random, NewWorldChoice.Premade)); random.RequireChoicesConsumed(); Equal(true, f.Trace.Contains("retain-world")); Equal(false, f.Trace.Contains("size")); Equal(4, f.Runtime.World.Map.Geometry.Width); Equal((ushort)(topology != 0 ? 0x803f : 0x3f), f.Runtime.World.Globals.Rules); Equal((ushort)0, (ushort)(f.Runtime.Process.OrdinaryPreferences.Rules & 0x8000)); Equal((ushort)29, f.Runtime.World.Map.PatternSeed);
        }
        using var owned = new EntryFixture(); int sources = 0; owned.Observe = stage => { if (stage == "rules") ++sources; if (stage == "save") { var rules = owned.Runtime.Process.Rules; rules.Definitions.Advances[1] = rules.Definitions.Advances[1] with { AiValue = 99 }; rules.Definitions.Units[1] = rules.Definitions.Units[1] with { Attack = 77 }; } if (stage == "stop-sound") { Equal((sbyte)3, owned.Runtime.Process.Rules.Definitions.Advances[1].AiValue); Equal(false, owned.Runtime.Process.Rules.Definitions.Units[1].Attack == 77); } };
        Equal(true, owned.Run(EntryChoices())); Equal(1, sources);
    }
    private static void WorldEntryAccelerationAndChildFailures()
    {
        using (var f = new EntryFixture())
        {
            int rules = 0; f.Players.Rules = _ => ++rules == 1 ? 1 : 0; f.Players.Advanced = defaults => new(0, defaults with { AcceleratedStartup = true }); f.Players.Acceleration = (years, _) => { Equal(new AccelerationYears(4000, 3000, 2000), years); return -7; };
            f.Services = f.Services with { Accelerate = selection => { Equal(-7, selection); Equal((short)0, f.Runtime.World.Globals.Turn); Equal((byte)0x2b, f.Runtime.World.Globals.CivsAtStart); f.Runtime.World.Globals.Turn = 41; f.Runtime.World.Civilizations[1].Advances[12] |= 8; f.Event("acceleration"); } };
            Equal(true, f.Run(EntryChoices())); Equal((short)41, f.Runtime.World.Globals.Turn); Equal((short)-4000, f.Runtime.World.Globals.Year); Equal("advances 1, advances 95, advances 99, ", f.Introduced); Equal("acceleration", f.Trace[^2]);
        }
        foreach (string failure in new[] { "rules", "size", "save", "stop-sound", "generate-world", "create:4", "balance", "introduce" })
        {
            using var f = new EntryFixture(); f.Runtime.BeginClientSession(new()); var client = SessionClient(f.Runtime); var world = f.Runtime.World; f.Observe = stage => { if (stage == failure) throw new IOException(stage); };
            Throws<IOException>(() => f.Run(EntryChoices())); Equal(false, ReferenceEquals(world, f.Runtime.World)); Equal(false, f.Runtime.Active); CheckRevoked(client, AccessFailure.SessionEnded); Equal(failure, f.Trace[^1]); if (failure is "create:4" or "balance" or "introduce") Equal((short)-4000, f.Runtime.World.Globals.Year); f.Runtime.PrepareNewGame();
        }
    }
    private static void WorldEntryIsolationAndClassicScratch()
    {
        foreach (string previous in new[] { "completed", "cancelled", "failed" })
        {
            using var fresh = new EntryFixture(); using var reused = new EntryFixture();
            if (previous == "completed") reused.Run(EntryChoices());
            if (previous == "cancelled") { var preset = reused.Size.Preset; reused.Size.Preset = _ => new WorldSizeChoice.Preset(-1); reused.Run(EntryChoices(false, false, false)); reused.Size.Preset = preset; }
            if (previous == "failed") { var bytes = reused.MapBytes; reused.MapBytes = []; reused.Run(EntryChoices(false, false, false), NewWorldChoice.Premade); reused.MapBytes = bytes; }
            var old = reused.Runtime.World; old.Globals.PeaceTurns = 87; old.Globals.SavedCurrentCiv = 6; old.Civilizations[1].FutureTechs = 19; old.Civilizations[1].Goals[0] = new(8, 8, 3, 90); old.Civilizations[1].StandingGoals[0] = new(4, 4, 2, 70); old.Entities.Units.Storage[2047].Damage = 9; old.Entities.Cities.Storage[255].ShieldBox = 999; old.Multiplayer.Saved.ResumeEvent = 12; old.PendingLeaderOverrides[3] = 7;
            reused.Runtime.Process.Rules.Leaders[0].CityNameIndex = 12; reused.Runtime.Process.Rules.DoubleMovementApplied = true; reused.Runtime.Process.Fallback.Value.Region = 17;
            reused.Files.LoadedFilename = "OLD.SAV"; reused.Files.ScenarioDirectory = "old"; reused.Files.GameStem = "OLD"; reused.Files.Text.Search = reused.Files.Text.Working = "old"; reused.Files.Extension = ".NET"; reused.Files.OpenedPath = "OLD"; reused.Files.RememberedSubdirectory = "nested"; reused.Files.CursorX = 23; reused.Files.Starts.X[20] = 99; reused.Files.RecordBytes = new(old.Lifetime); reused.Files.Text.Extension = "FRE"; reused.Files.CurrentScreen = new(1, 2, 900, 700); reused.Files.EventBytes[9] = new(old.Events.Collection.Append(), new(new byte[40]));
            reused.Runtime.Process.OrdinaryPreferences = fresh.Runtime.Process.OrdinaryPreferences; reused.Runtime.Process.MultiplayerPreferences = fresh.Runtime.Process.MultiplayerPreferences; reused.Runtime.Process.Tokens.NextUnit = 500; reused.Runtime.Process.Tokens.NextCity = 90;
            var first = EntryChoices(); var second = EntryChoices(); Equal(true, fresh.Run(first)); Equal(true, reused.Run(second)); first.RequireChoicesConsumed(); second.RequireChoicesConsumed(); var a = fresh.Runtime.World; var b = reused.Runtime.World;
            Equal(a.Globals, b.Globals); Equal(new OwnedList<OpenCiv2.Core.State.Civilization>(a.Civilizations), new(b.Civilizations)); Equal(a.WorldSetup, b.WorldSetup); Equal(a.Eliminations, b.Eliminations); Equal(a.PowerHistory, b.PowerHistory); Equal(a.Multiplayer.Saved, b.Multiplayer.Saved); Equal(new OwnedList<sbyte>(a.PendingLeaderOverrides), new(b.PendingLeaderOverrides)); Equal(new OwnedList<OpenCiv2.Core.State.Unit>(a.Entities.Units.Storage.ToArray()), new(b.Entities.Units.Storage.ToArray())); Equal(new OwnedList<OpenCiv2.Core.State.City>(a.Entities.Cities.Storage.ToArray()), new(b.Entities.Cities.Storage.ToArray())); Equal(PremadeTiles(a.Map), PremadeTiles(b.Map)); Equal(fresh.Runtime.Process.Fallback.Value, reused.Runtime.Process.Fallback.Value); Equal(fresh.Runtime.Process.Rules.Definitions.Snapshot(), reused.Runtime.Process.Rules.Definitions.Snapshot()); Equal(new OwnedList<LeaderSnapshot>(fresh.Runtime.Process.Rules.Leaders.Select(l => l.Snapshot())), new(reused.Runtime.Process.Rules.Leaders.Select(l => l.Snapshot())));
            Equal("", reused.Files.LoadedFilename); Equal("", reused.Files.ScenarioDirectory); Equal("", reused.Files.OpenedPath); Equal("", reused.Files.RememberedSubdirectory); Equal(".SAV", reused.Files.Extension); Equal("home", reused.Files.Text.Search); Equal("home", reused.Files.Text.Working); Equal(fresh.Files.GameStem, reused.Files.GameStem); Equal((short)0, reused.Files.CursorX); Equal(new StartingPositions(), reused.Files.Starts); Equal(true, reused.Files.RecordBytes is null); Equal(0, reused.Files.EventBytes.Count); Equal("FRE", reused.Files.Text.Extension); Equal(new Rectangle(1, 2, 900, 700), reused.Files.CurrentScreen); Equal(500U, reused.Runtime.Process.Tokens.NextUnit); Equal(90U, reused.Runtime.Process.Tokens.NextCity); Equal(fresh.Introduced, reused.Introduced);
        }
    }
    private static void WorldEntryAdmissionAndInactivePins()
    {
        using var f = new EntryFixture(); f.Runtime.BeginClientSession(new()); var client = SessionClient(f.Runtime); var observation = client.OwnUnits().Observation; var world = f.Runtime.World; f.Files.GameStem = "existing";
        foreach (var invalid in new[] { f.Services with { ResetSource = null! }, f.Services with { LoadContent = null! }, f.Services with { Size = null }, f.Services with { Players = null! }, f.Services with { Initialize = new(_ => 0, null!) }, f.Services with { Parameters = f.Services.Parameters with { Preferences = null! } } })
        {
            var random = EntryChoices(); var before = random.Capture(); Throws<ArgumentNullException>(() => WorldEntry.Enter(f.Runtime, ref f.Files.Starts, random, "entry", NewWorldChoice.Random, ClassicCalendar, invalid)); Equal(true, ReferenceEquals(world, f.Runtime.World)); Equal(observation, client.OwnUnits().Observation); Equal("existing", f.Files.GameStem); Equal(before, random.Capture()); Equal(0, f.Trace.Count);
        }
        foreach (NewWorldChoice choice in new[] { NewWorldChoice.Customize, NewWorldChoice.Premade })
        { var invalid = choice == NewWorldChoice.Premade ? f.Services with { ChooseMap = null } : f.Services with { Parameters = f.Services.Parameters with { Choose = null! } }; Throws<ArgumentNullException>(() => WorldEntry.Enter(f.Runtime, ref f.Files.Starts, EntryChoices(), "entry", choice, ClassicCalendar, invalid)); }
        Throws<ArgumentException>(() => f.Run(EntryChoices(), call: "")); Throws<ArgumentOutOfRangeException>(() => f.Run(EntryChoices(), (NewWorldChoice)99)); Equal(true, ReferenceEquals(world, f.Runtime.World));
        int callbacks = 0; f.Observe = _ => { ++callbacks; Equal(false, f.Runtime.Active); Throws<InvalidOperationException>(f.Runtime.PrepareNewGame); Throws<InvalidOperationException>(() => f.Runtime.BeginClientSession(new())); };
        var runtimeRandom = f.Runtime.GameplayRandom; Equal(true, f.Run(runtimeRandom)); Equal(true, runtimeRandom.Draws > 0); Equal(true, callbacks > 10); f.Runtime.PrepareNewGame();
    }
    private static void WorldEntryIntroductionAndCommonSource()
    {
        using var f = new EntryFixture(); var rules = f.Runtime.Process.Rules; new RuleInstaller(f.Input, rules).Load(true); for (int i = 0; i < 13; ++i) f.Runtime.World.Civilizations[1].Advances[i] = 255;
        rules.Definitions.Advances[98] = rules.Definitions.Advances[98] with { Prerequisites = new([AdvanceReference.Never, AdvanceReference.Never]) }; rules.Definitions.Advances[97] = rules.Definitions.Advances[97] with { Prerequisites = new([AdvanceReference.Never, AdvanceReference.None]) };
        var civ = f.Runtime.World.Civilizations[1]; var globals = f.Runtime.World.Globals;
        Equal("advances 86, advances 87, advances 88, advances 90, advances 91, advances 92, advances 93, advances 94, advances 95, advances 96, advances 97, advances 99, ", ByteString(WorldEntry.StartingAdvanceList(f.Runtime.World, rules, 1))); Equal(civ, f.Runtime.World.Civilizations[1]); Equal(globals, f.Runtime.World.Globals);
        f.Runtime.World.Civilizations[1].Advances = default; f.Runtime.World.Civilizations[1].Advances[0] = 1; Equal(0, WorldEntry.StartingAdvanceList(f.Runtime.World, rules, 1).Count); f.Runtime.World.Civilizations[1].Advances[0] = 2; rules.Names.Advances[1] = rules.Text.Store("Live label"u8); Equal("Live label, ", ByteString(WorldEntry.StartingAdvanceList(f.Runtime.World, rules, 1))); Equal(0, WorldEntry.StartingAdvanceList(f.Runtime.World, rules, 0).Count); Throws<ArgumentOutOfRangeException>(() => WorldEntry.StartingAdvanceList(f.Runtime.World, rules, 8));
        var preferences = new EntryPreferences(); f.Files.GameStem = "adapter-owned"; f.Files.Starts.X[0] = 7; f.Services = f.Services with { Parameters = f.Services.Parameters with { Preferences = preferences }, ResetSource = () => { Equal(new StartingPositions(), f.Files.Starts); Equal((short)0, f.Runtime.World.Globals.Turn); f.Event("reset"); } }; var random = EntryChoices(); Equal(true, WorldEntry.Enter(f.Runtime, ref f.Files.Starts, random, "entry", NewWorldChoice.Random, ClassicCalendar, f.Services)); random.RequireChoicesConsumed(); Equal("reset", f.Trace[0]); Equal("rules", f.Trace[1]); Equal("adapter-owned", f.Files.GameStem); Equal(0, f.Saved.Count); Equal(1, preferences.Snapshots.Count); Equal((int)Difficulty.Prince, (int)preferences.Snapshots[0].Ordinary.Difficulty);
    }
}
