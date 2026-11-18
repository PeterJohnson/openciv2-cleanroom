using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F03 §8 and F06 CIV2.DAT: authored choices, exact output and supplied child failures.
    private sealed class ConfigurationMusic : IMusicControl
    {
        public Action StopAction = () => { }, ReinitializeAction = () => { }, SynchronizeAction = () => { };
        public void Stop() => StopAction();
        public void Reinitialize() => ReinitializeAction();
        public void Synchronize() => SynchronizeAction();
    }
    private sealed class ConfigurationFixture : IDisposable
    {
        public readonly PreferenceFixture Files = new();
        public readonly SessionRuntime Runtime;
        public readonly ConfigurationMusic Music = new();
        public GameOptionServices Game => new(Files.Storage, Music);
        public GraphicsOptionServices Graphics => new(Files.Storage, () => true, () => { });
        public ConfigurationFixture()
        {
            Files.Process.OrdinaryPreferences.SetDefaults(1000, true); Files.Process.MultiplayerPreferences.SetDefaults();
            Files.Process.OrdinaryPreferences.GameOptions = 0xdeadbeef; Files.Process.OrdinaryPreferences.DisabledMessages = 0x9876;
            Runtime = new(Files.Process, new(79)); Runtime.BeginClientSession(new(10, 20));
            Music.StopAction = () => Files.Trace.Add("stop"); Music.ReinitializeAction = () => Files.Trace.Add("reinitialize"); Music.SynchronizeAction = () => Files.Trace.Add("synchronize");
        }
        public void Dispose() => Runtime.Dispose();
    }
    private static GameOptionChoices GameChoices(Func<int, bool> at) => new(at(0), at(1), at(2), at(3), at(4), at(5), at(6), at(7), at(8), at(9), at(10));
    private static GraphicsOptionChoices GraphicsChoices(Func<int, bool> at) => new(at(0), at(1), at(2), at(3), at(4), at(5));
    private static MessageOptionChoices MessageChoices(Func<int, bool> at) => new(at(0), at(1), at(2), at(3), at(4), at(5), at(6), at(7), at(8), at(9), at(10));
    private static void ConfigurationCancellationAndAdmission()
    {
        using var f = new ConfigurationFixture(); f.Runtime.World.Globals.GameOptions = uint.MaxValue; f.Runtime.World.Globals.MessageOptions = ushort.MaxValue;
        var globals = f.Runtime.World.Globals; var preferences = f.Files.Process.OrdinaryPreferences; var random = f.Runtime.GameplayRandom.Capture();
        Equal(false, Configuration.Game(f.Runtime, null, null!)); Equal(false, Configuration.Graphics(f.Runtime, null, null!)); Equal(false, Configuration.Messages(f.Runtime, null, null!));
        Throws<ArgumentNullException>(() => Configuration.Game(f.Runtime, GameChoices(_ => true), new(f.Files.Storage, null!)));
        Throws<ArgumentNullException>(() => Configuration.Game(f.Runtime, GameChoices(_ => true), new(null!, f.Music)));
        Throws<ArgumentNullException>(() => Configuration.Graphics(f.Runtime, GraphicsChoices(_ => true), new(f.Files.Storage, null!, () => { })));
        Throws<ArgumentNullException>(() => Configuration.Graphics(f.Runtime, GraphicsChoices(_ => true), new(f.Files.Storage, () => true, null!)));
        Throws<ArgumentNullException>(() => Configuration.Messages(f.Runtime, MessageChoices(_ => true), null!));
        Equal(globals, f.Runtime.World.Globals); Equal(preferences, f.Files.Process.OrdinaryPreferences); Equal(random, f.Runtime.GameplayRandom.Capture()); Equal(0, f.Files.Trace.Count);
    }
    private static void ConfigurationGameBits()
    {
        uint[] bits = [0x10, 8, 0x4000, 0x2000, 0x1000, 0x800, 0x400, 0x200, 0x100, 0x80, 0x40];
        for (int selected = 0; selected < bits.Length; ++selected)
        {
            using var f = new ConfigurationFixture(); f.Runtime.World.Globals.GameOptions = uint.MaxValue;
            var random = f.Runtime.GameplayRandom.Capture(); Equal(true, Configuration.Game(f.Runtime, GameChoices(i => i == selected), f.Game));
            uint expected = 0xffff8027 | bits[selected]; Equal(expected, f.Runtime.World.Globals.GameOptions); Equal(expected, f.Files.Process.OrdinaryPreferences.GameOptions);
            var stored = ClassicPreferences.DecodeOrdinary(f.Files.Output.Take(72).ToArray()); Equal(expected, stored.GameOptions); Equal((ushort)0x9876, stored.DisabledMessages);
            Equal(516, f.Files.Output.Count); Equal(1, f.Files.Closes); Equal(random, f.Runtime.GameplayRandom.Capture());
        }
    }
    private static void ConfigurationMusicTransitions()
    {
        foreach (bool before in new[] { false, true }) foreach (bool after in new[] { false, true })
        {
            using var f = new ConfigurationFixture(); f.Runtime.World.Globals.GameOptions = before ? 8U : 0U;
            var client = SessionClient(f.Runtime); List<ulong> revisions = [];
            void Audio(string name) { Equal(after ? 0x18U : 0x10U, f.Runtime.World.Globals.GameOptions); Equal(0xdeadbeefU, f.Files.Process.OrdinaryPreferences.GameOptions); revisions.Add(client.OwnUnits().Observation.Revision); f.Files.Trace.Add(name); }
            f.Music.StopAction = () => Audio("stop"); f.Music.ReinitializeAction = () => Audio("reinitialize"); f.Music.SynchronizeAction = () => Audio("synchronize");
            Equal(true, Configuration.Game(f.Runtime, GameChoices(i => i != 1 || after), f.Game));
            List<string> expected = []; if (!after) expected.Add("stop"); else { if (!before) expected.Add("reinitialize"); expected.Add("synchronize"); }
            expected.AddRange(["open-output", "write:72", "write:444", "close"]); Equal(new OwnedList<string>(expected), new(f.Files.Trace));
            Equal(after ? 0x7fd8U : 0x7fd0U, f.Runtime.World.Globals.GameOptions); Equal(new OwnedList<ulong>(Enumerable.Range(1, revisions.Count).Select(i => (ulong)i)), new(revisions));
        }
        using var repeated = new ConfigurationFixture(); int calls = 0; repeated.Music.StopAction = () => ++calls;
        Configuration.Game(repeated.Runtime, GameChoices(_ => false), repeated.Game); Configuration.Game(repeated.Runtime, GameChoices(_ => false), repeated.Game); Equal(2, calls);
    }
    private static void ConfigurationFailurePrefixes()
    {
        foreach (int failure in new[] { 0, 1, 2, 3, 4, 5 })
        {
            using var f = new ConfigurationFixture(); var client = SessionClient(f.Runtime);
            if (failure == 0) f.Files.OutputOpen = false;
            if (failure is 1 or 2) f.Files.FailWrite = failure - 1;
            if (failure == 3) f.Files.CloseOk = false;
            if (failure == 4) f.Music.ReinitializeAction = () => throw new IOException("music");
            if (failure == 5) f.Files.AfterWrite = _ => throw new IOException("write");
            if (failure < 4) Equal(true, Configuration.Game(f.Runtime, GameChoices(_ => true), f.Game));
            else Throws<IOException>(() => Configuration.Game(f.Runtime, GameChoices(_ => true), f.Game));
            Equal(failure == 4 ? 0x18U : 0x7fd8U, f.Runtime.World.Globals.GameOptions);
            Equal(failure == 4 ? 0xdeadbeefU : 0x7fd8U, f.Files.Process.OrdinaryPreferences.GameOptions);
            Equal(failure is 0 or 4 ? 0 : 1, f.Files.Closes); Equal(failure is >= 1 and <= 3 ? 1 : 0, f.Files.Removals);
            Equal(true, client.OwnUnits().Observation.Revision > 0); f.Runtime.PrepareNewGame();
        }
    }
    private static void ConfigurationGraphicsBitsAndWarning()
    {
        uint[] bits = [0x20000, 0x40000, 0x200000, 0x80000, 0x100000, 0x10000];
        for (int selected = 0; selected < bits.Length; ++selected)
        {
            using var f = new ConfigurationFixture(); f.Runtime.World.Globals.GameOptions = uint.MaxValue;
            Equal(true, Configuration.Graphics(f.Runtime, GraphicsChoices(i => i == selected), f.Graphics with { HeraldsAvailable = () => throw new Exception("Already enabled") }));
            uint expected = 0xffc0ffff | bits[selected]; Equal(expected, f.Runtime.World.Globals.GameOptions); Equal(expected, ClassicPreferences.DecodeOrdinary(f.Files.Output.Take(72).ToArray()).GameOptions);
        }
        foreach (bool fails in new[] { false, true })
        {
            using var f = new ConfigurationFixture(); f.Runtime.World.Globals.GameOptions = 0x1f0010;
            var client = SessionClient(f.Runtime); int calls = 0;
            var services = f.Graphics with
            {
                HeraldsAvailable = () => { Equal(0x60010U, f.Runtime.World.Globals.GameOptions); Equal(1UL, client.OwnUnits().Observation.Revision); ++calls; return false; },
                LowMemoryNotice = () => { Equal(0x60010U, f.Runtime.World.Globals.GameOptions); Equal(2UL, client.OwnUnits().Observation.Revision); ++calls; if (fails) throw new IOException("warning"); f.Runtime.World.Globals.GameOptions |= 0x80000020; }
            };
            if (fails) Throws<IOException>(() => Configuration.Graphics(f.Runtime, GraphicsChoices(_ => true), services)); else Equal(true, Configuration.Graphics(f.Runtime, GraphicsChoices(_ => true), services));
            Equal(2, calls); Equal(fails ? 0x60010U : 0x803f0030U, f.Runtime.World.Globals.GameOptions); Equal(fails ? 0xdeadbeefU : 0x803f0030U, f.Files.Process.OrdinaryPreferences.GameOptions); Equal(fails ? 0 : 516, f.Files.Output.Count);
        }
    }
    private static void ConfigurationMessageReplacement()
    {
        for (int disabled = -1; disabled < 12; ++disabled)
        {
            using var f = new ConfigurationFixture(); f.Runtime.World.Globals.MessageOptions = ushort.MaxValue; f.Files.OutputOpen = disabled != 11;
            Configuration.Messages(f.Runtime, MessageChoices(i => disabled != 11 && i != disabled), f.Files.Storage);
            ushort expected = disabled == 11 ? (ushort)0x7ff : disabled < 0 ? (ushort)0 : (ushort)(1 << disabled);
            Equal(expected, f.Runtime.World.Globals.MessageOptions); Equal(expected, f.Files.Process.OrdinaryPreferences.DisabledMessages); Equal(0xdeadbeefU, f.Files.Process.OrdinaryPreferences.GameOptions);
            if (disabled != 11) Equal(expected, ClassicPreferences.DecodeOrdinary(f.Files.Output.Take(72).ToArray()).DisabledMessages);
        }
    }
    private static void ConfigurationWorldPinAndPublication()
    {
        foreach (bool active in new[] { false, true })
        {
            using var f = new ConfigurationFixture(); var client = SessionClient(f.Runtime); if (!active) f.Runtime.EndClientSession();
            int callbacks = 0; ulong revision = 0; var world = f.Runtime.World;
            void Check()
            {
                ++callbacks; Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame()); Throws<InvalidOperationException>(() => f.Runtime.BeginClientSession(new(1, 2)));
                Equal(true, ReferenceEquals(world, f.Runtime.World));
                if (active) { ulong next = client.OwnUnits().Observation.Revision; Equal(true, next > revision); revision = next; }
            }
            f.Music.StopAction = Check; f.Files.AfterWrite = _ => Check(); f.Files.BeforeClose = Check;
            Configuration.Game(f.Runtime, GameChoices(_ => false), f.Game);
            Configuration.Graphics(f.Runtime, GraphicsChoices(_ => true), f.Graphics with { HeraldsAvailable = () => { Check(); return false; }, LowMemoryNotice = Check });
            Configuration.Messages(f.Runtime, MessageChoices(_ => true), f.Files.Storage);
            Equal(12, callbacks); f.Runtime.PrepareNewGame();
        }
    }
    private static void DoubleMovementByteLaws()
    {
        var rules = new RuleState(); int redraws = 0;
        for (int code = 0; code < 256; ++code)
        {
            rules.DoubleMovementApplied = false;
            for (int i = 0; i < 62; ++i) rules.Definitions.Units[i] = new() { Movement = (byte)code, Attack = 71, Domain = i switch { 1 => RuleDomain.Air, 2 => RuleDomain.Sea, 3 => (RuleDomain)255, _ => RuleDomain.Land } };
            void Check(int argument) { Equal(1, argument); ++redraws; }
            DoubleMovement.Apply(GameMode.Hotseat, true, rules, Check); int doubled = code * 2 % 256;
            Equal(true, rules.DoubleMovementApplied); Equal((byte)doubled, rules.Definitions.Units[0].Movement); Equal((byte)doubled, rules.Definitions.Units[61].Movement);
            DoubleMovement.Apply(GameMode.Tcp, false, rules, Check); int signed = doubled < 128 ? doubled : doubled - 256; byte halved = (byte)((signed / 2 + 256) % 256);
            Equal(false, rules.DoubleMovementApplied); Equal(halved, rules.Definitions.Units[0].Movement); Equal(halved, rules.Definitions.Units[61].Movement);
            for (int i = 1; i <= 3; ++i) Equal((byte)code, rules.Definitions.Units[i].Movement);
            Equal((byte)71, rules.Definitions.Units[61].Attack);
        }
        Equal(512, redraws);
        byte[] input = [0, 1, 127, 128, 129, 253, 254, 255], expected = [0, 0, 63, 192, 193, 255, 255, 0];
        rules.DoubleMovementApplied = true; for (int i = 0; i < input.Length; ++i) rules.Definitions.Units[i] = new() { Movement = input[i] };
        DoubleMovement.Apply(GameMode.Email, false, rules, _ => { }); for (int i = 0; i < input.Length; ++i) Equal(expected[i], rules.Definitions.Units[i].Movement);
    }
    private static void DoubleMovementGatesAndFailure()
    {
        var rules = new RuleState(); rules.Definitions.Units[0] = new() { Movement = 200 };
        foreach (bool applied in new[] { false, true })
        {
            rules.DoubleMovementApplied = applied;
            DoubleMovement.Apply(GameMode.SinglePlayer, !applied, rules, null); DoubleMovement.Apply(GameMode.Tcp, applied, rules, null);
            Equal(applied, rules.DoubleMovementApplied); Equal((byte)200, rules.Definitions.Units[0].Movement);
        }
        rules.DoubleMovementApplied = false; rules.Definitions.Units[0] = new() { Movement = 64 }; rules.Definitions.Units[61] = new() { Movement = 127 };
        Throws<InvalidOperationException>(() => DoubleMovement.Apply(GameMode.Tcp, true, rules, null)); Equal(true, rules.DoubleMovementApplied); Equal((byte)128, rules.Definitions.Units[0].Movement); Equal((byte)254, rules.Definitions.Units[61].Movement);
        Throws<IOException>(() => DoubleMovement.Apply(GameMode.Tcp, false, rules, argument => { Equal(1, argument); Equal(false, rules.DoubleMovementApplied); Equal((byte)192, rules.Definitions.Units[0].Movement); Equal((byte)255, rules.Definitions.Units[61].Movement); throw new IOException("redraw"); }));
        Equal(false, rules.DoubleMovementApplied); Equal((byte)255, rules.Definitions.Units[61].Movement);
    }
}
