using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F03 §8 / N03 §11; authored bounded channels, not original-peer transport evidence.
    private sealed class VoteChannel : IOptionVoteChannel
    {
        public int Begins, Polls, Cancellations, Disposals;
        public OptionProposal? Proposal;
        public OptionVoteProgress Progress;
        public Action? OnBegin, OnPoll;
        public void Begin(OptionProposal proposal) { ++Begins; Proposal = proposal; OnBegin?.Invoke(); }
        public OptionVoteProgress Poll() { ++Polls; OnPoll?.Invoke(); return Progress; }
        public void Cancel() { ++Cancellations; }
        public void Dispose() { ++Disposals; }
    }
    private sealed class VoteBroadcaster : IOptionBroadcaster
    {
        public Action RejectedAction = () => { }, AcceptedAction = () => { }, UnitsAction = () => { };
        public Action<bool, bool, bool> OptionsAction = (_, _, _) => { };
        public void Rejected() => RejectedAction();
        public void Accepted() => AcceptedAction();
        public void Units() => UnitsAction();
        public void Options(bool production, bool movement, bool simultaneous) => OptionsAction(production, movement, simultaneous);
    }
    private sealed class VoteFixture : IDisposable
    {
        public readonly ConfigurationFixture Config = new();
        public SessionRuntime Runtime => Config.Runtime;
        public readonly VoteChannel Channel = new();
        public readonly VoteBroadcaster Broadcaster = new();
        public MultiplayerOptionServices Services;
        public readonly List<(bool, bool, bool)> Sent = [];
        public uint Tick = 100;
        public VoteFixture()
        {
            Runtime.World.Globals.Multiplayer = GameMode.Tcp; Runtime.World.Multiplayer.NetworkHost = true;
            Runtime.World.Multiplayer.PlayerCount = 3; Runtime.World.Multiplayer.Saved.Simultaneous = true;
            Runtime.Process.MultiplayerPreferences.DoubleProduction = Runtime.Process.MultiplayerPreferences.DoubleMovement = true;
            Runtime.Process.Rules.Definitions.Units[0] = new() { Movement = 64 };
            Runtime.Process.Rules.Definitions.Units[1] = new() { Movement = 3, Domain = RuleDomain.Air };
            Runtime.Process.Rules.Definitions.Units[61] = new() { Movement = 127 };
            Broadcaster.RejectedAction = () => Config.Files.Trace.Add("rejected"); Broadcaster.AcceptedAction = () => Config.Files.Trace.Add("accepted");
            Broadcaster.UnitsAction = () => Config.Files.Trace.Add("units"); Broadcaster.OptionsAction = (p, m, s) => { Config.Files.Trace.Add("options"); Sent.Add((p, m, s)); };
            Services = new(Config.Files.Storage, () => Tick, () => "Local player", () => " / ",
                on => on ? "Production on" : "Production off", on => on ? "Movement on" : "Movement off", Broadcaster,
                name => Config.Files.Trace.Add(name), value => { Equal(1, value); Config.Files.Trace.Add("redraw"); });
        }
        public MultiplayerOptions Operation(MultiplayerOptionChoices? choices = null, int result = 0) => new(Runtime, result, choices ?? new(true, false, true, true), Services, Channel);
        public void Dispose() => Config.Dispose();
    }
    private static void VoteResult(MultiplayerOptionResult expected, MultiplayerOptionResult? actual) => Equal((int)expected, actual.HasValue ? (int)actual.Value : -1);
    private static void MultiplayerOptionAdmission()
    {
        foreach (int cancelled in new[] { -1, 1, 256 })
        {
            using var f = new VoteFixture(); var preferences = f.Runtime.Process.MultiplayerPreferences;
            using var op = new MultiplayerOptions(f.Runtime, cancelled, null!, null!, f.Channel); VoteResult(MultiplayerOptionResult.Cancelled, op.Poll());
            Equal(preferences, f.Runtime.Process.MultiplayerPreferences); Equal(0, f.Config.Files.Trace.Count); Equal(0, f.Channel.Begins); Equal(1, f.Channel.Cancellations); Equal(1, f.Channel.Disposals);
        }
        using var missing = new VoteFixture(); Throws<ArgumentException>(() => new MultiplayerOptions(missing.Runtime, 0, new(true, false, true, true), missing.Services));
        missing.Services = missing.Services with { Clock = null! }; Throws<ArgumentNullException>(() => missing.Operation()); Equal(0, missing.Config.Files.Trace.Count); Equal(1, missing.Channel.Cancellations); Equal(1, missing.Channel.Disposals); missing.Runtime.PrepareNewGame();
    }
    private static void MultiplayerOptionYieldAndApply()
    {
        using var f = new VoteFixture(); f.Channel.Progress = new(2, 0); var random = f.Runtime.GameplayRandom.Capture();
        // Saving precedes new proposal preferences: distinguish old remembered values from new choices.
        f.Runtime.Process.MultiplayerPreferences.DoubleProduction = f.Runtime.Process.MultiplayerPreferences.DoubleMovement = false;
        var client = SessionClient(f.Runtime); ulong revision = 0;
        void Published() { ulong now = client.OwnUnits().Observation.Revision; Equal(true, now > revision); revision = now; }
        f.Channel.OnBegin = Published; f.Channel.OnPoll = Published;
        using var op = f.Operation(); Equal(false, op.Poll().HasValue); Equal(1, f.Channel.Begins); Equal(0, f.Channel.Polls);
        Equal(new OptionProposal("Local player", "Production on / Movement on", true, true, 2), op.Proposal!);
        Equal(false, op.Poll().HasValue); Equal(false, f.Runtime.World.Multiplayer.Saved.DoubleMovement); Equal((byte)64, f.Runtime.Process.Rules.Definitions.Units[0].Movement);
        var saved = ClassicPreferences.DecodeMultiplayer(f.Config.Files.Output.Skip(72).ToArray());
        Equal(true, StateConversion.ToLive(saved).ClearChatNewGame); Equal(false, StateConversion.ToLive(saved).DoubleMovement);
        Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame());
        f.Channel.Progress = new(0, 0); VoteResult(MultiplayerOptionResult.Applied, op.Poll()); VoteResult(MultiplayerOptionResult.Applied, op.Poll());
        Equal(true, f.Runtime.World.Multiplayer.Saved.DoubleMovement); Equal((byte)128, f.Runtime.Process.Rules.Definitions.Units[0].Movement); Equal((byte)3, f.Runtime.Process.Rules.Definitions.Units[1].Movement); Equal((byte)254, f.Runtime.Process.Rules.Definitions.Units[61].Movement);
        Equal(new OwnedList<(bool, bool, bool)>([(true, true, true)]), new(f.Sent));
        Equal(new OwnedList<string>(["redraw", "units", "options", "accepted", "PMCHANGEYES"]), new(f.Config.Files.Trace.TakeLast(5)));
        Equal(2, f.Config.Files.Writes); Equal(1, f.Channel.Cancellations); Equal(1, f.Channel.Disposals); Equal(random, f.Runtime.GameplayRandom.Capture()); f.Runtime.PrepareNewGame();
    }
    private static void MultiplayerOptionLocalGates()
    {
        using (var f = new VoteFixture())
        {
            f.Runtime.World.Multiplayer.NetworkHost = false; using var op = f.Operation(); VoteResult(MultiplayerOptionResult.ClientOnly, op.Poll());
            Equal(true, f.Runtime.Process.MultiplayerPreferences.DoubleMovement); Equal(false, f.Runtime.World.Multiplayer.Saved.DoubleMovement); Equal(2, f.Config.Files.Writes); Equal(0, f.Channel.Begins);
        }
        using (var f = new VoteFixture())
        {
            using var op = f.Operation(new(true, true, false, false)); VoteResult(MultiplayerOptionResult.Unchanged, op.Poll()); Equal(false, f.Runtime.Process.MultiplayerPreferences.DoubleMovement); Equal(0, f.Channel.Begins); Equal((byte)64, f.Runtime.Process.Rules.Definitions.Units[0].Movement);
        }
        foreach (GameMode mode in new[] { GameMode.SinglePlayer, GameMode.Hotseat, GameMode.Email, GameMode.Tcp })
        {
            using var f = new VoteFixture(); f.Runtime.World.Globals.Multiplayer = mode; f.Runtime.World.Multiplayer.PlayerCount = 1;
            using var op = new MultiplayerOptions(f.Runtime, 0, new(true, false, true, true), f.Services);
            VoteResult(MultiplayerOptionResult.Applied, op.Poll()); Equal(mode != GameMode.SinglePlayer, f.Runtime.Process.Rules.DoubleMovementApplied); Equal(0, f.Sent.Count);
        }
        using var failedSave = new VoteFixture(); failedSave.Config.Files.FailWrite = 0; using var failedSaveOp = failedSave.Operation(); Equal(false, failedSaveOp.Poll().HasValue); VoteResult(MultiplayerOptionResult.Applied, failedSaveOp.Poll()); Equal(1, failedSave.Config.Files.Removals); Equal(1, failedSave.Config.Files.Writes);
    }
    private static void MultiplayerOptionRejectionAndDeadlines()
    {
        foreach (var progress in new[] { new OptionVoteProgress(0, -1), new(1, 0), new(-1, 0) })
        {
            using var f = new VoteFixture(); using var op = f.Operation(); Equal(false, op.Poll().HasValue); f.Channel.Progress = progress; f.Tick += 2401;
            f.Runtime.World.Multiplayer.Saved.DoubleProduction = false; f.Runtime.World.Multiplayer.Saved.DoubleMovement = true;
            VoteResult(MultiplayerOptionResult.Rejected, op.Poll()); Equal(false, f.Runtime.Process.MultiplayerPreferences.DoubleProduction); Equal(true, f.Runtime.Process.MultiplayerPreferences.DoubleMovement);
            Equal(false, f.Runtime.Process.Rules.DoubleMovementApplied); Equal("PMCHANGENO", f.Config.Files.Trace[^1]); Equal(0, f.Sent.Count);
        }
        foreach (int status in new[] { -2, 0, 9 })
        {
            using var f = new VoteFixture(); f.Channel.Progress = new(0, status); using var op = f.Operation(); Equal(false, op.Poll().HasValue); VoteResult(MultiplayerOptionResult.Applied, op.Poll());
        }
        foreach (var time in new (uint Start, uint Now, bool Wait)[] { (10, 2410, true), (10, 2411, false), (0xfffffff0, 0x10, true), (0, 0x80000000, true) })
        {
            using var f = new VoteFixture(); f.Tick = time.Start; f.Channel.Progress = new(-2, -2); using var op = f.Operation(); Equal(false, op.Poll().HasValue); f.Tick = time.Now;
            var result = op.Poll(); Equal(!time.Wait, result.HasValue); if (result.HasValue) VoteResult(MultiplayerOptionResult.Rejected, result); else Equal(time.Start, f.Runtime.World.Multiplayer.Vote.PopupTick);
        }
    }
    private static void MultiplayerOptionHandoffAndLiveEffects()
    {
        foreach (bool handoff in new[] { false, true })
        {
            using var f = new VoteFixture(); int clocks = 0; f.Services = f.Services with { Clock = () => (uint)(100 * ++clocks) };
            f.Channel.OnPoll = () => f.Runtime.World.Multiplayer.Handoff = handoff;
            using var op = f.Operation(); Equal(false, op.Poll().HasValue); VoteResult(MultiplayerOptionResult.Applied, op.Poll()); Equal(handoff ? 2 : 3, clocks); Equal(handoff ? 200U : 300U, f.Runtime.World.Multiplayer.Vote.PopupTick);
        }
        using (var f = new VoteFixture())
        {
            f.Services = f.Services with { LocalPlayerName = () => "Local\0ignored", ProductionLabel = enabled => { Equal(true, enabled); f.Runtime.Process.MultiplayerPreferences.DoubleMovement = false; return "Production on"; }, Separator = () => { f.Runtime.World.Multiplayer.PlayerCount = int.MinValue; return " / "; } };
            using var op = f.Operation(); Equal(false, op.Poll().HasValue); VoteResult(MultiplayerOptionResult.Applied, op.Poll());
            Equal(new OptionProposal("Local", "Production on / Movement off", true, false, int.MaxValue), op.Proposal!); Equal(0, f.Sent.Count);
        }
        using (var f = new VoteFixture())
        {
            f.Runtime.World.Globals.Multiplayer = GameMode.Hotseat; f.Runtime.World.Multiplayer.PlayerCount = 1;
            f.Services = f.Services with { RedrawStatus = _ => { f.Runtime.World.Globals.Multiplayer = GameMode.Tcp; f.Runtime.World.Multiplayer.PlayerCount = 2; } };
            f.Broadcaster.UnitsAction = () => { f.Runtime.World.Globals.Multiplayer = GameMode.SinglePlayer; f.Runtime.World.Multiplayer.Saved.DoubleMovement = false; };
            using var op = f.Operation(); VoteResult(MultiplayerOptionResult.Applied, op.Poll()); Equal((true, false, true), f.Sent[0]); Equal("PMCHANGEYES", f.Config.Files.Trace[^1]);
        }
        using (var f = new VoteFixture())
        {
            f.Services = f.Services with { RedrawStatus = _ => f.Runtime.World.Multiplayer.PlayerCount = 1 }; f.Broadcaster.UnitsAction = () => throw new IOException("No publication");
            using var op = f.Operation(); Equal(false, op.Poll().HasValue); VoteResult(MultiplayerOptionResult.Applied, op.Poll()); Equal(0, f.Sent.Count);
        }
    }
    private static void MultiplayerOptionCancellation()
    {
        foreach (int stage in new[] { 0, 1, 2, 3, 4 })
        {
            using var f = new VoteFixture(); using var op = f.Operation();
            if (stage == 0) op.Cancel();
            else if (stage == 1) { f.Config.Files.AfterWrite = _ => op.Cancel(); VoteResult(MultiplayerOptionResult.Cancelled, op.Poll()); }
            else if (stage == 2)
            {
                f.Channel.OnBegin = () => { Throws<InvalidOperationException>(() => op.Poll()); op.Cancel(); Equal(0, f.Channel.Cancellations); Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame()); };
                VoteResult(MultiplayerOptionResult.Rejected, op.Poll());
            }
            else { Equal(false, op.Poll().HasValue); if (stage == 3) op.Cancel(); else op.Dispose(); }
            VoteResult(stage < 2 ? MultiplayerOptionResult.Cancelled : MultiplayerOptionResult.Rejected, op.Poll());
            Equal(1, f.Channel.Cancellations); Equal(1, f.Channel.Disposals); Equal(stage < 2, f.Runtime.Process.MultiplayerPreferences.DoubleMovement); f.Runtime.PrepareNewGame();
        }
    }
    private static void MultiplayerOptionChildFailures()
    {
        for (int stage = 0; stage < 8; ++stage)
        {
            using var f = new VoteFixture(); void Fail() => throw new IOException("authored child");
            if (stage == 0) f.Channel.OnBegin = Fail;
            if (stage == 1) f.Channel.OnPoll = Fail;
            if (stage == 2) f.Broadcaster.UnitsAction = Fail;
            if (stage == 3) f.Services = f.Services with { Notice = _ => Fail() };
            if (stage == 4) f.Services = f.Services with { Clock = () => throw new IOException("authored child") };
            if (stage == 5) f.Services = f.Services with { MovementLabel = _ => throw new IOException("authored child") };
            if (stage == 6) f.Config.Files.AfterWrite = _ => Fail();
            if (stage == 7) { f.Channel.Progress = new(0, -1); f.Broadcaster.RejectedAction = Fail; }
            using var op = f.Operation(); if (stage is 1 or 2 or 3 or 7) Equal(false, op.Poll().HasValue);
            Throws<IOException>(() => op.Poll()); VoteResult(MultiplayerOptionResult.Failed, op.Poll());
            Equal(stage is 2 or 3, f.Runtime.World.Multiplayer.Saved.DoubleMovement); Equal(1, f.Channel.Cancellations); Equal(1, f.Channel.Disposals); f.Runtime.PrepareNewGame();
        }
        using var retained = new VoteFixture(); int calls = 0; retained.Broadcaster.UnitsAction = () => ++calls;
        foreach (bool enable in new[] { true, false })
        {
            var channel = new VoteChannel { Progress = new(1, 0) }; using var op = new MultiplayerOptions(retained.Runtime, 0, new(true, false, enable, enable), retained.Services, channel); Equal(false, op.Poll().HasValue); Equal(false, op.Poll().HasValue);
            channel.Progress = new(0, 0); VoteResult(MultiplayerOptionResult.Applied, op.Poll()); Equal(1, channel.Cancellations); Equal(1, channel.Disposals);
        }
        Equal(2, calls);
    }
}
