using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F05 §§2–4, F01 cyclic arithmetic, F03 preference persistence. Specification-derived
    // expectations; these cases port our cleanroom regression domain, not an original execution.
    private sealed class SizeInteraction : IWorldSizeInteraction
    {
        public Func<WorldSize, WorldSizeChoice> Preset = _ => new WorldSizeChoice.Preset(1);
        public Func<string, string, CustomSizeResponse> Custom = (_, _) => throw new InvalidOperationException("Unexpected custom size");
        public WorldSizeChoice ChoosePreset(WorldSize rememberedPreset) => Preset(rememberedPreset);
        public CustomSizeResponse ChooseCustom(string widthDefault, string heightDefault) => Custom(widthDefault, heightDefault);
    }
    private sealed class WorldSetupFixture : IDisposable
    {
        public readonly PreferenceFixture Files = new();
        public readonly SessionRuntime Runtime;
        public WorldSetupFixture()
        {
            Runtime = new(Files.Process, new(95)); Runtime.BeginClientSession(new(10, 20));
            Files.Process.OrdinaryPreferences = new() { MapSize = WorldSize.Large };
            Files.Process.MultiplayerPreferences = new();
            Files.Process.OrdinaryPreferences.WorldParameters[3] = -1; Files.Process.OrdinaryPreferences.WorldParameters[4] = 1;
            Runtime.World.WorldSetup.Width = 31; Runtime.World.WorldSetup.Height = 37; Runtime.World.Globals.GameFlags = 0xffff;
            for (int i = 0; i < 5; ++i) Runtime.World.WorldSetup.Parameters[i] = 11 + i;
        }
        public WorldParameterServices Services(Func<WorldParameter, short, WorldParameterChoice>? choose = null) => new(choose ?? ((_, _) => new WorldParameterChoice.Selected(1)), new ClassicPreferenceWriter(Files.Storage));
        public void Dispose() => Runtime.Dispose();
    }
    private static readonly WorldParameter[] CustomWorldOrder = [WorldParameter.LandMass, WorldParameter.LandForm, WorldParameter.Climate, WorldParameter.Temperature, WorldParameter.Age];
    private static readonly string[] WorldChoiceSites = ["F05.world.land-mass", "F05.world.land-form", "F05.world.temperature", "F05.world.climate", "F05.world.age"];
    private static GameplayRandom WorldChoices(int[] codes, int? limit = null)
    {
        var choices = codes.Select((code, i) => Choice.Create(new(WorldChoiceSites[i], "world/one"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(code))).Take(limit ?? codes.Length);
        return new(new ControlledChoices(new(choices)), "setup");
    }
    private static void SetupParametersEqual(int[] expected, SessionRuntime runtime)
    { for (int i = 0; i < 5; ++i) Equal(expected[i], runtime.World.WorldSetup.Parameters[i]); }
    private static void SetupPreferencesEqual(short[] expected, SessionRuntime runtime)
    { for (int i = 0; i < 5; ++i) Equal(expected[i], runtime.Process.OrdinaryPreferences.WorldParameters[i]); }

    private static void WorldSizePresets()
    {
        foreach (var (row, width, height, flags) in new (int, short, short, ushort)[] { (0, 80, 50, 8), (1, 100, 80, 0), (2, 150, 120, 4), (7, 100, 80, 0), (65536, 100, 80, 0) })
        {
            using var f = new WorldSetupFixture(); var random = f.Runtime.GameplayRandom.Capture();
            var ui = new SizeInteraction { Preset = remembered => { Equal(2, (int)remembered); Equal((ushort)0, f.Runtime.World.Globals.GameFlags); return new WorldSizeChoice.Preset(row); } };
            Equal(true, WorldSetupOperations.SelectSize(f.Runtime, ui)); Equal(width, f.Runtime.World.WorldSetup.Width); Equal(height, f.Runtime.World.WorldSetup.Height); Equal(flags, f.Runtime.World.Globals.GameFlags);
            Equal((int)unchecked((short)row), (int)f.Runtime.Process.OrdinaryPreferences.MapSize); SetupParametersEqual([11, 12, 13, 14, 15], f.Runtime); Equal(0, f.Files.Trace.Count); Equal(random, f.Runtime.GameplayRandom.Capture());
        }
    }
    private static void WorldSizeDecimalAndBoundaries()
    {
        foreach (var (width, height, w, h, flags) in new (string, string, short, short, ushort)[] {
            ("65586","80",100,80,0), ("-65486","80",100,80,0), ("20","50",40,50,8), ("250","40",500,40,4), ("40","250",80,250,4),
            ("60","50",120,50,0), ("100","60",200,60,4), ("59","101",118,101,0), ("  +50suffix","\t80",100,80,0), ("4294967346","80",100,80,0) })
        {
            using var f = new WorldSetupFixture(); var ui = new SizeInteraction { Preset = _ => new WorldSizeChoice.Custom(), Custom = (a, b) => { Equal("50", a); Equal("80", b); return new(CustomSizeStatus.Accepted, width, height); } };
            Equal(true, WorldSetupOperations.SelectSize(f.Runtime, ui)); Equal(w, f.Runtime.World.WorldSetup.Width); Equal(h, f.Runtime.World.WorldSetup.Height); Equal(flags, f.Runtime.World.Globals.GameFlags); Equal(2, (int)f.Runtime.Process.OrdinaryPreferences.MapSize);
        }
        foreach (var (width, height) in new[] { ("19", "80"), ("50", "19"), ("251", "20"), ("20", "251"), ("20", "49"), ("101", "100"), ("65535", "80"), ("0x32", "80"), ("0b110010", "80"), ("", "80"), ("word", "80") })
        {
            using var f = new WorldSetupFixture(); int presets = 0, customs = 0;
            var ui = new SizeInteraction { Preset = _ => { ++presets; return new WorldSizeChoice.Custom(); }, Custom = (a, b) => { Equal("50", a); Equal("80", b); return ++customs == 1 ? new(CustomSizeStatus.Accepted, width, height) : new(CustomSizeStatus.Accepted, "50", "80"); } };
            Equal(true, WorldSetupOperations.SelectSize(f.Runtime, ui)); Equal(1, presets); Equal(2, customs); Equal((short)100, f.Runtime.World.WorldSetup.Width); Equal((short)80, f.Runtime.World.WorldSetup.Height);
        }
    }
    private static void WorldSizeCancellationAndFailures()
    {
        using var f = new WorldSetupFixture(); int presets = 0, customs = 0;
        var ui = new SizeInteraction { Preset = _ => ++presets == 1 ? new WorldSizeChoice.Custom() : new WorldSizeChoice.Preset(-1), Custom = (_, _) => ++customs == 1 ? new(CustomSizeStatus.Accepted, "19", "80") : new(CustomSizeStatus.Cancelled, "", "") };
        Equal(false, WorldSetupOperations.SelectSize(f.Runtime, ui)); Equal(2, presets); Equal(2, customs); Equal((short)38, f.Runtime.World.WorldSetup.Width); Equal((ushort)8, f.Runtime.World.Globals.GameFlags);
        Equal(false, WorldSetupOperations.SelectSize(f.Runtime, ui)); Equal((short)76, f.Runtime.World.WorldSetup.Width); Equal((ushort)0, f.Runtime.World.Globals.GameFlags);
        f.Runtime.World.WorldSetup.Width = 20000; f.Runtime.World.WorldSetup.Height = 2;
        ui.Preset = _ => new WorldSizeChoice.Custom(); ui.Custom = (_, _) => new(CustomSizeStatus.Unavailable, "", "");
        Equal(false, WorldSetupOperations.SelectSize(f.Runtime, ui)); Equal((short)-25536, f.Runtime.World.WorldSetup.Width); Equal((ushort)4, f.Runtime.World.Globals.GameFlags);
        ui.Custom = (_, _) => throw new IOException("dialog"); Throws<IOException>(() => WorldSetupOperations.SelectSize(f.Runtime, ui)); Equal((short)-25536, f.Runtime.World.WorldSetup.Width); Equal((ushort)0, f.Runtime.World.Globals.GameFlags);
        ui.Custom = (_, _) => new((CustomSizeStatus)99, "50", "80"); Throws<ArgumentException>(() => WorldSetupOperations.SelectSize(f.Runtime, ui));
        f.Runtime.PrepareNewGame(); // All world pins were released on both exceptional paths.
    }
    private static void WorldParametersRandomOrderAndLaw()
    {
        for (int mass = 0; mass < 3; ++mass) for (int form = 0; form < 3; ++form)
        {
            using var f = new WorldSetupFixture(); var before = f.Runtime.Process.OrdinaryPreferences; var random = WorldChoices([mass, form, 32767, 32766, 32765]);
            WorldSetupOperations.RandomizeParameters(f.Runtime, random, "world/one"); random.RequireChoicesConsumed(); SetupParametersEqual([mass - 1, form == 0 ? mass - 1 : 0, 0, -1, 1], f.Runtime);
            Equal(before, f.Runtime.Process.OrdinaryPreferences); Equal(0, f.Files.Trace.Count); Equal(new OwnedList<string>(WorldChoiceSites), new(random.Capture().Trace!.Events.Select(e => e.Key.Site)));
        }
        using var custom = new WorldSetupFixture(); var remembered = custom.Runtime.Process.OrdinaryPreferences; var draws = WorldChoices([2, 3, 4, 5, 6]); var visits = new List<WorldParameter>();
        Equal(true, WorldSetupOperations.CustomizeParameters(custom.Runtime, draws, "world/one", custom.Services((p, _) => { visits.Add(p); return new WorldParameterChoice.Random(); })));
        draws.RequireChoicesConsumed(); Equal(true, CustomWorldOrder.SequenceEqual(visits)); SetupParametersEqual([1, 1, 0, 1, -1], custom.Runtime); Equal(remembered, custom.Runtime.Process.OrdinaryPreferences);
        Equal(new OwnedList<string>([WorldChoiceSites[0], WorldChoiceSites[1], WorldChoiceSites[3], WorldChoiceSites[2], WorldChoiceSites[4]]), new(draws.Capture().Trace!.Events.Select(e => e.Key.Site)));
        Equal(516, custom.Files.Output.Count); Equal(remembered, StateConversion.ToLive(ClassicPreferences.DecodeOrdinary(custom.Files.Output.Take(72).ToArray())));
    }
    private static void WorldParametersOrdinaryPersistence()
    {
        using var f = new WorldSetupFixture(); var random = WorldChoices([]); var before = random.Capture(); int count = 0; int[] rows = [2, 0, 2, 0, 1]; short[] defaults = [0, 0, -1, 0, 1];
        var services = f.Services((p, remembered) => { Equal((int)CustomWorldOrder[count], (int)p); Equal(defaults[count], remembered); return new WorldParameterChoice.Selected(rows[count++]); });
        Equal(true, WorldSetupOperations.CustomizeParameters(f.Runtime, random, "world/one", services)); Equal(5, count); Equal(before, random.Capture()); SetupParametersEqual([1, -1, -1, 1, 0], f.Runtime); SetupPreferencesEqual([1, -1, -1, 1, 0], f.Runtime);
        Equal(new OwnedList<string>(["open-output", "write:72", "write:444", "close"]), new(f.Files.Trace)); Equal(f.Runtime.Process.OrdinaryPreferences, StateConversion.ToLive(ClassicPreferences.DecodeOrdinary(f.Files.Output.Take(72).ToArray())));
        Equal(new OwnedList<byte>([1, 0, 255, 255, 255, 255, 1, 0, 0, 0]), new(f.Files.Output.Skip(0x1a).Take(10)));
    }
    private static void WorldParametersCancellationPrefixes()
    {
        for (int stop = 0; stop < 5; ++stop) foreach (int cancel in new[] { -1, -7, int.MinValue })
        {
            using var f = new WorldSetupFixture(); int count = 0; var random = WorldChoices([]);
            Equal(false, WorldSetupOperations.SelectParameters(f.Runtime, random, "world/one", true, true, f.Services((p, _) => { Equal((int)CustomWorldOrder[count], (int)p); return new WorldParameterChoice.Selected(count++ == stop ? cancel : 2); })));
            Equal(stop + 1, count); int[] expected = [11, 12, 13, 14, 15]; short[] preferences = [0, 0, 0, -1, 1];
            for (int i = 0; i < stop; ++i) { expected[(int)CustomWorldOrder[i]] = 1; preferences[(int)CustomWorldOrder[i]] = 1; }
            expected[(int)CustomWorldOrder[stop]] = unchecked(cancel - 1); SetupParametersEqual(expected, f.Runtime); SetupPreferencesEqual(preferences, f.Runtime); Equal(0, f.Files.Trace.Count); random.RequireChoicesConsumed();
        }
    }
    private static void WorldParametersFailurePrefixes()
    {
        int[][] expected = [[11, 12, 13, 14, 15], [1, 0, 13, 14, 15], [1, 1, 13, 14, 15], [1, 1, 0, 14, 15], [1, 1, 0, 1, 15]];
        for (int stop = 0; stop < 5; ++stop)
        {
            using var f = new WorldSetupFixture(); var random = WorldChoices([2, 3, 4, 5, 6], stop);
            Throws<ChoiceException>(() => WorldSetupOperations.RandomizeParameters(f.Runtime, random, "world/one")); SetupParametersEqual(expected[stop], f.Runtime); Equal(stop, random.Capture().Trace!.Events.Count);
        }
        foreach (bool unsupported in new[] { false, true })
        {
            using var f = new WorldSetupFixture(); var random = WorldChoices([]);
            var services = f.Services((p, _) => p != WorldParameter.Climate ? new WorldParameterChoice.Selected(2) : unsupported ? new WorldParameterChoice.Selected(3) : throw new IOException("modal"));
            if (unsupported) Throws<ArgumentException>(() => WorldSetupOperations.CustomizeParameters(f.Runtime, random, "world/one", services)); else Throws<IOException>(() => WorldSetupOperations.CustomizeParameters(f.Runtime, random, "world/one", services));
            SetupParametersEqual([1, 1, 13, 14, 15], f.Runtime); SetupPreferencesEqual([1, 1, 0, -1, 1], f.Runtime); Equal(0, f.Files.Trace.Count); random.RequireChoicesConsumed();
            f.Files.Storage.ReplaceFile = _ => throw new IOException("open"); Throws<IOException>(() => WorldSetupOperations.CustomizeParameters(f.Runtime, random, "world/one", f.Services()));
            SetupParametersEqual([0, 0, 0, 0, 0], f.Runtime); SetupPreferencesEqual([0, 0, 0, 0, 0], f.Runtime);
        }
    }
    private static void WorldParametersPersistenceFailuresAndLiveChoices()
    {
        for (int failure = -1; failure < 3; ++failure)
        {
            using var f = new WorldSetupFixture(); f.Files.OutputOpen = failure != -1; f.Files.FailWrite = failure; f.Files.CloseOk = failure != 2;
            Equal(true, WorldSetupOperations.CustomizeParameters(f.Runtime, WorldChoices([]), "world/one", f.Services())); SetupPreferencesEqual([0, 0, 0, 0, 0], f.Runtime);
            Equal(failure == -1 ? 0 : 1, f.Files.Closes); Equal(failure == -1 ? 0 : 1, f.Files.Removals);
        }
        using var live = new WorldSetupFixture(); var choices = new[] { 1, 3 }.Select(i => Choice.Create(new(WorldChoiceSites[i], "world/one"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(i == 1 ? 3 : 2)));
        var random = new GameplayRandom(new ControlledChoices(new(choices)), "setup");
        Equal(true, WorldSetupOperations.CustomizeParameters(live.Runtime, random, "world/one", live.Services((p, remembered) =>
        {
            if (p == WorldParameter.LandForm) { Equal(-1, live.Runtime.World.WorldSetup.Parameters[0]); live.Runtime.World.WorldSetup.Parameters[0] = 1; live.Runtime.Process.OrdinaryPreferences.WorldParameters[3] = -9; return new WorldParameterChoice.Random(); }
            if (p == WorldParameter.Climate) { Equal((short)-9, remembered); return new WorldParameterChoice.Random(); }
            return new WorldParameterChoice.Selected(0);
        })));
        random.RequireChoicesConsumed(); SetupParametersEqual([1, 1, -1, 1, -1], live.Runtime); SetupPreferencesEqual([-1, 0, -1, -9, -1], live.Runtime);
    }
    private static void WorldSetupPublicationAndPins()
    {
        using var f = new WorldSetupFixture(); var runtime = f.Runtime; runtime.World.Entities.Units.SetEnd(1); runtime.World.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(1), X = 7 }; var client = SessionClient(runtime); ulong revision = client.OwnUnits().Observation.Revision; short next = 7;
        void Boundary()
        {
            var observed = client.OwnUnits(); Equal(true, observed.Observation.Revision > revision); revision = observed.Observation.Revision; Equal(next, observed.Records[0].X);
            runtime.World.Entities.Units.Storage[0].X = ++next; Throws<InvalidOperationException>(runtime.PrepareNewGame);
        }
        var ui = new SizeInteraction { Preset = _ => { Boundary(); return new WorldSizeChoice.Preset(0); } }; Equal(true, WorldSetupOperations.SelectSize(runtime, ui)); Equal(next, client.OwnUnits().Records[0].X);
        var open = f.Files.Storage.ReplaceFile; f.Files.Storage.ReplaceFile = path => { Boundary(); return open(path); }; f.Files.AfterWrite = _ => Boundary(); f.Files.BeforeClose = Boundary;
        Equal(true, WorldSetupOperations.CustomizeParameters(runtime, WorldChoices([]), "world/one", f.Services((_, _) => { Boundary(); return new WorldParameterChoice.Selected(1); })));
        Equal(next, client.OwnUnits().Records[0].X);
        Throws<IOException>(() => WorldSetupOperations.CustomizeParameters(runtime, WorldChoices([]), "world/one", f.Services((_, _) => { Boundary(); throw new IOException("dialog"); })));
        Equal(next, client.OwnUnits().Records[0].X); Equal(true, client.OwnUnits().Observation.Revision > revision); runtime.PrepareNewGame();
    }
    private static void WorldSetupAdmissionAndInactiveUse()
    {
        using var runtime = new SessionRuntime(new(), new(98)); var random = WorldChoices([0, 0, 0, 0, 0]); var before = runtime.World.WorldSetup; var preferences = runtime.Process.OrdinaryPreferences;
        Throws<ArgumentException>(() => WorldSetupOperations.RandomizeParameters(runtime, random, "")); Throws<ArgumentException>(() => WorldSetupOperations.CustomizeParameters(runtime, random, "", null!));
        foreach (var services in new WorldParameterServices[] { new(null!, new ClassicPreferenceWriter(new CallbackStorage())), new((_, _) => new WorldParameterChoice.Random(), null!) })
            Throws<ArgumentNullException>(() => WorldSetupOperations.CustomizeParameters(runtime, random, "world/one", services));
        Equal(before, runtime.World.WorldSetup); Equal(preferences, runtime.Process.OrdinaryPreferences); Equal(0, random.Capture().Trace!.Events.Count);
        foreach (bool custom in new[] { false, true }) Equal(false, WorldSetupOperations.SelectParameters(runtime, random, "", custom, false)); Equal(before, runtime.World.WorldSetup);
        Equal(true, WorldSetupOperations.SelectSize(runtime, new SizeInteraction())); Equal(true, WorldSetupOperations.SelectParameters(runtime, random, "world/one", false, true)); random.RequireChoicesConsumed(); Equal(false, runtime.Active);
    }
    private static void SetupCalendarRetention()
    {
        using var runtime = new SessionRuntime(new(), new(96)); runtime.World.Globals = CalendarGlobals(2); runtime.World.Globals.Turn = 77; runtime.World.Globals.Year = 1200; runtime.World.Globals.FirstLaunchTurn = 0;
        runtime.World.Scenario.StartYear = 123; runtime.World.Scenario.YearIncrement = 7; var scenario = runtime.World.Scenario; var preferences = runtime.Process.OrdinaryPreferences; var globals = runtime.World.Globals; globals.FirstLaunchTurn = -1;
        Equal(new AccelerationYears(4000, 3000, 2000), SetupCalendar.PrepareAccelerationYears(runtime, ClassicCalendar)); Equal(globals, runtime.World.Globals); Equal(scenario, runtime.World.Scenario); Equal(preferences, runtime.Process.OrdinaryPreferences); Equal(false, runtime.Active);
        runtime.World.Globals.GameFlags = 4; Equal(new AccelerationYears(4000, 3600, 3200), SetupCalendar.PrepareAccelerationYears(runtime, ClassicCalendar));
        runtime.World.Globals.GameFlags = 8; Equal(new AccelerationYears(4000, 3000, 2000), SetupCalendar.PrepareAccelerationYears(runtime, ClassicCalendar));
        runtime.BeginClientSession(new(17, 29)); var gameplay = runtime.GameplayRandom.Capture(); var presentation = runtime.PresentationRandom.Capture(); var client = SessionClient(runtime); ulong revision = client.OwnUnits().Observation.Revision;
        SetupCalendar.PrepareAccelerationYears(runtime, ClassicCalendar); Equal(revision + 1, client.OwnUnits().Observation.Revision); Equal(gameplay, runtime.GameplayRandom.Capture()); Equal(presentation, runtime.PresentationRandom.Capture());
    }
    private static void SetupCalendarScenarioAndMagnitude()
    {
        using var runtime = new SessionRuntime(new(), new(97)); runtime.World.Globals = CalendarGlobals(0, 0x80); runtime.World.Scenario.StartYear = -100; runtime.World.Scenario.YearIncrement = -12;
        Equal(new AccelerationYears(101, 151, 391), SetupCalendar.PrepareAccelerationYears(runtime, ClassicCalendar)); runtime.World.Scenario.StartYear = 1; runtime.World.Scenario.YearIncrement = 1;
        Equal(new AccelerationYears(0, 21, 41), SetupCalendar.PrepareAccelerationYears(runtime, ClassicCalendar));
        var calendar = CustomCalendar(Enumerable.Range(0, 6).Select(i => new CalendarPeriod(int.MinValue + i, 1, 1)).ToArray()); runtime.World.Globals.GameFlags = 0;
        Equal(new AccelerationYears(2147483648, 2147483628, 2147483608), SetupCalendar.PrepareAccelerationYears(runtime, calendar));
    }
}
