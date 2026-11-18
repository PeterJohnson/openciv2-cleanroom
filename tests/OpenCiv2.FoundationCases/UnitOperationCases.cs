using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class UnitChannel : IUnitRequestChannel
    {
        public int Sends, Polls, Cancellations; public UnitRequest? Request; public Queue<UnitReply> Replies = []; public List<bool> Waiting = [];
        public Action? OnSend, OnPoll, OnCancel;
        public void Send(UnitRequest request) { ++Sends; Request = request; OnSend?.Invoke(); }
        public UnitReply Poll(bool waiting) { ++Polls; Waiting.Add(waiting); OnPoll?.Invoke(); return Replies.TryDequeue(out var result) ? result : new(); }
        public void Cancel() { ++Cancellations; OnCancel?.Invoke(); }
    }
    private sealed class UnitOperationFixture : IDisposable, IUnitDeletionEffects
    {
        public SessionRuntime Runtime = new(new(), new(199)); public UnitChannel Channel = new(); public UnitOperationServices Services;
        public uint Tick = 100; public int Timeouts, Synchronizations, Removed, Refreshed, Checks;
        public UnitOperationFixture()
        {
            Runtime.BeginClientSession(new(10, 20)); var world = Runtime.World; world.Map.Allocate(new(8, 6)); world.Globals.Multiplayer = GameMode.Tcp; world.TurnLoopActive = true;
            world.Entities.Units.SetEnd(2); world.Globals.UnitEnd = 2;
            for (int i = 0; i < 2; ++i) { ref var unit = ref Unit(i); unit.Token = new((uint)i + 1); unit.X = unit.Y = 2; unit.Owner = new(1); }
            Unit(0).Next = new(1); Unit(1).Previous = new(0);
            Services = new(Placement: () => ++Synchronizations, Deletion: this, Ticks: () => Tick, TimedOut: () => { ++Timeouts; Equal(true, world.TurnLoopActive); });
        }
        public ref Unit Unit(int slot) => ref Runtime.World.Entities.Units.Storage[slot];
        public UnitOperation Operation(UnitOperationKind kind = UnitOperationKind.Relocate, bool notify = true, int slot = 0) => new(Runtime, Request(kind, slot), notify, new(10000, 64), Services, Channel);
        public void UnitRemoved(Position position) => ++Removed;
        public void RefreshHomeCity(int city) => ++Refreshed;
        public void CheckCivilization(int owner, int killer) => ++Checks;
        public void SynchronizeDeletion(Position position, CitySlot? home) => ++Synchronizations;
        public void Dispose() => Runtime.Dispose();
    }
    private static UnitRequest Request(UnitOperationKind kind, int slot = 0) => new(kind, new([slot, 4, 2, 77, 88, 99]));
    private static void UnitOperationRemotePrimitives()
    {
        foreach (var kind in new[] { UnitOperationKind.Detach, UnitOperationKind.Attach, UnitOperationKind.Relocate, UnitOperationKind.Bottom, UnitOperationKind.Erase })
        {
            using var f = new UnitOperationFixture(); var before = f.Unit(0); var gameplay = f.Runtime.GameplayRandom.Capture(); var presentation = f.Runtime.PresentationRandom.Capture(); using var operation = f.Operation(kind, false);
            Equal(true, operation.Poll() is null); Equal(1, f.Channel.Sends); Equal(0, f.Channel.Polls); Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame());
            Equal(new OwnedList<int>(kind is UnitOperationKind.Attach or UnitOperationKind.Relocate ? [0, 4, 2, 0, 0, 0] : [0, 0, 0, 0, 0, 0]), f.Channel.Request!.Arguments);
            Equal(true, operation.Poll() is null); f.Channel.Replies.Enqueue(new(71)); var result = operation.Poll()!.Value; Equal(new UnitOperationResult(71), result); Equal(before, f.Unit(0));
            Equal(result, operation.Poll()!.Value); operation.Cancel(); operation.Dispose(); Equal(1, f.Channel.Cancellations); Equal(1, f.Channel.Sends); Equal(0, f.Synchronizations);
            Equal(gameplay, f.Runtime.GameplayRandom.Capture()); Equal(presentation, f.Runtime.PresentationRandom.Capture()); f.Runtime.PrepareNewGame();
        }
        using var sync = new UnitOperationFixture(); int clocks = 0; sync.Services = sync.Services with { Ticks = () => { ++clocks; return sync.Tick; } }; using var sent = sync.Operation(UnitOperationKind.Detach);
        sync.Channel.OnSend = () => sync.Runtime.World.UnitStacks.DetachReply = 12; sent.Poll(); Equal(true, sent.Result is null); sync.Channel.Replies.Enqueue(new(null, false)); sent.Poll(); sync.Channel.Replies.Enqueue(new(13, true)); Equal(13, sent.Poll()!.Value.Reply); Equal(false, sync.Channel.Waiting.Any(x => x)); Equal(1, clocks);
    }
    private static void UnitOperationTimeoutDrainAndMarkers()
    {
        using var f = new UnitOperationFixture(); f.Tick = 0xfffffff0; f.Runtime.World.UnitDeletion.Active = true; using var operation = f.Operation(UnitOperationKind.Erase);
        operation.Poll(); Equal(false, f.Runtime.World.UnitDeletion.Active); Equal(-2, f.Runtime.World.UnitDeletion.Reply);
        f.Tick = unchecked(f.Tick + 3599); Equal(true, operation.Poll() is null); Equal(0, f.Timeouts); ++f.Tick;
        f.Channel.Replies.Enqueue(new(null, false)); Equal(true, operation.Poll() is null); Equal(1, f.Timeouts); Equal(false, f.Runtime.World.TurnLoopActive);
        f.Channel.OnPoll = () => f.Runtime.World.UnitDeletion.Active = true; f.Channel.Replies.Enqueue(new(9, true)); Equal(new UnitOperationResult(9, true), operation.Poll()!.Value); Equal(false, f.Channel.Waiting[^1]); Equal(true, f.Runtime.World.UnitDeletion.Active); Equal(1, f.Timeouts);
        using var negative = new UnitOperationFixture(); using var waiting = negative.Operation(); waiting.Poll(); negative.Tick = unchecked(negative.Tick + 0x80000000); waiting.Poll(); Equal(0, negative.Timeouts);
        using var noReply = new UnitOperationFixture(); using var expired = noReply.Operation(); expired.Poll(); noReply.Tick += 3600; Equal(new UnitOperationResult(-2, true), expired.Poll()!.Value);
    }
    private static void UnitOperationCancellationAndFailures()
    {
        for (int stage = 0; stage < 6; ++stage)
        {
            using var f = new UnitOperationFixture();
            if (stage == 1) { f.Channel.OnSend = () => throw new IOException("send"); f.Channel.OnCancel = () => throw new IOException("cleanup"); }
            if (stage == 2) f.Services = f.Services with { Ticks = () => throw new IOException("clock") };
            if (stage == 3) f.Channel.OnPoll = () => throw new IOException("poll");
            if (stage == 4) f.Services = f.Services with { TimedOut = () => throw new IOException("timeout") };
            using var operation = f.Operation();
            if (stage is 1 or 2) Throws<IOException>(() => operation.Poll());
            else { operation.Poll(); if (stage is 3 or 4) { f.Tick += 3600; Throws<IOException>(() => operation.Poll()); } else if (stage == 0) operation.Cancel(); else operation.Dispose(); }
            Equal(true, operation.Poll()!.Value.Cancelled); operation.Dispose(); Equal(1, f.Channel.Cancellations); Equal((short)2, f.Unit(0).X); if (stage == 4) Equal(true, f.Runtime.World.TurnLoopActive); f.Runtime.PrepareNewGame();
        }
        using var dispose = new UnitOperationFixture(); using var unstarted = dispose.Operation(); unstarted.Dispose(); Equal(0, dispose.Channel.Sends); Equal(1, dispose.Channel.Cancellations); Equal(new UnitOperationResult(-2, false, true), unstarted.Poll()!.Value);
    }
    private static void UnitOperationCallbackCancellation()
    {
        for (int stage = 0; stage < 4; ++stage)
        {
            using var f = new UnitOperationFixture(); UnitOperation? operation = null;
            void CancelInside() { Throws<InvalidOperationException>(() => operation!.Poll()); operation!.Cancel(); Equal(0, f.Channel.Cancellations); Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame()); }
            if (stage == 0) f.Channel.OnSend = CancelInside;
            if (stage == 1) f.Services = f.Services with { Ticks = () => { CancelInside(); return f.Tick; } };
            if (stage == 2) f.Channel.OnPoll = CancelInside;
            if (stage == 3) f.Services = f.Services with { TimedOut = CancelInside };
            using (operation = f.Operation()) { var result = operation.Poll(); if (result is null) { if (stage == 3) f.Tick += 3600; result = operation.Poll(); } Equal(true, result!.Value.Cancelled); Equal(1, f.Channel.Cancellations); }
            f.Runtime.PrepareNewGame();
        }
    }
    private static void UnitOperationLocalMechanicsAndGuards()
    {
        foreach (var kind in new[] { UnitOperationKind.Detach, UnitOperationKind.Attach, UnitOperationKind.Relocate, UnitOperationKind.Bottom, UnitOperationKind.Erase })
        {
            using var f = new UnitOperationFixture(); f.Runtime.World.Multiplayer.NetworkHost = true; f.Unit(0).Home = new(0); var client = SessionClient(f.Runtime); using var operation = f.Operation(kind);
            Equal(new UnitOperationResult(), operation.Poll()!.Value); Equal(0, f.Channel.Sends); Equal(1, f.Synchronizations); Equal(1, f.Channel.Cancellations);
            if (kind == UnitOperationKind.Erase) { Equal(0U, f.Unit(0).Token.Value); Equal(1, f.Removed); Equal(1, f.Refreshed); Equal(1, client.OwnUnits().Records.Count); }
            else if (kind is UnitOperationKind.Relocate or UnitOperationKind.Attach) Equal((short)4, f.Unit(0).X);
            else if (kind == UnitOperationKind.Detach) Equal((short)-200, f.Unit(0).X);
            else { Equal((short)1, f.Unit(0).Previous.Value); Equal((short)-1, f.Unit(0).Next.Value); }
        }
        foreach (bool remote in new[] { false, true })
        {
            using var f = new UnitOperationFixture(); f.Runtime.World.Multiplayer.NetworkHost = !remote; using var erased = f.Operation(UnitOperationKind.Erase, slot: -1); Equal(new UnitOperationResult(), erased.Poll()!.Value); Equal(0, f.Channel.Sends);
            var tailChannel = new UnitChannel(); using var tail = new UnitOperation(f.Runtime, Request(UnitOperationKind.Bottom, 1), false, new(0, 0), f.Services, tailChannel); Equal(new UnitOperationResult(), tail.Poll()!.Value); Equal(0, tailChannel.Sends);
        }
    }
    private static void UnitOperationAdmissionAndRawArguments()
    {
        foreach (var kind in new[] { UnitOperationKind.Detach, UnitOperationKind.Attach, UnitOperationKind.Relocate })
        {
            using var f = new UnitOperationFixture(); using var operation = new UnitOperation(f.Runtime, new(kind, new([-7, 70000, -80000, 1, 2, 3])), false, new(0, 0), f.Services, f.Channel);
            operation.Poll(); Equal(-7, f.Channel.Request!.Arguments[0]); if (kind != UnitOperationKind.Detach) { Equal(70000, f.Channel.Request.Arguments[1]); Equal(-80000, f.Channel.Request.Arguments[2]); }
        }
        for (int missing = 0; missing < 5; ++missing)
        {
            using var f = new UnitOperationFixture(); f.Services = missing switch { 0 => f.Services with { Ticks = null }, 1 => f.Services with { TimedOut = null }, 2 => f.Services with { Placement = null }, 3 => f.Services with { Deletion = null }, _ => f.Services };
            if (missing == 3) f.Runtime.World.Multiplayer.NetworkHost = true;
            Throws<ArgumentException>(() => new UnitOperation(f.Runtime, Request(missing == 2 ? UnitOperationKind.RelocateStack : missing == 3 ? UnitOperationKind.Erase : UnitOperationKind.Detach), true, new(100, 10), f.Services, missing == 4 ? null : f.Channel));
            Equal(0, f.Channel.Sends); Equal(missing == 4 ? 0 : 1, f.Channel.Cancellations); f.Runtime.PrepareNewGame();
        }
        using var unknown = new UnitOperationFixture(); Throws<ArgumentException>(() => new UnitOperation(unknown.Runtime, Request((UnitOperationKind)99), false, new(1, 1), unknown.Services, unknown.Channel)); Equal(1, unknown.Channel.Cancellations);
        using var pending = new UnitOperationFixture(); pending.Runtime.EndClientSession(); pending.Runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); pending.Runtime.BeginClientSession(new()); SessionClient(pending.Runtime).Submit(CommandRequestFor()); Throws<InvalidOperationException>(() => pending.Operation()); Equal(1, pending.Channel.Cancellations);
    }
    private static void UnitOperationStackSuccessorsAndAuthority()
    {
        foreach (bool becomeHost in new[] { false, true })
        {
            using var f = new UnitOperationFixture(); using var operation = f.Operation(UnitOperationKind.RelocateStack); Equal(true, operation.Poll() is null); Equal((int)UnitOperationKind.Relocate, (int)f.Channel.Request!.Operation);
            f.Channel.OnPoll = () => { int slot = f.Channel.Request!.Arguments[0]; f.Unit(slot).X = 4; f.Unit(slot).Previous = f.Unit(slot).Next = new(-1); f.Unit(1).Previous = new(-1); f.Runtime.World.Multiplayer.NetworkHost = becomeHost; };
            f.Channel.Replies.Enqueue(new(0)); Equal(true, operation.Poll() is null); Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame());
            if (becomeHost) Equal(true, operation.Poll().HasValue);
            else { Equal(true, operation.Poll() is null); Equal(1, f.Channel.Request!.Arguments[0]); f.Channel.Replies.Enqueue(new(0)); Equal(true, operation.Poll().HasValue); }
            Equal(becomeHost ? 1 : 2, f.Channel.Sends); Equal((short)4, f.Unit(0).X); Equal((short)4, f.Unit(1).X); Equal(1, f.Synchronizations); Equal(1, f.Channel.Cancellations); f.Runtime.PrepareNewGame();
        }
        using var local = new UnitOperationFixture(); local.Runtime.World.Multiplayer.NetworkHost = true; var budget = new StackBudget(10000, 64); using var moving = new UnitOperation(local.Runtime, Request(UnitOperationKind.RelocateStack), true, budget, local.Services, local.Channel);
        Equal(true, moving.Poll() is null); Equal((short)4, local.Unit(0).X); Equal((short)2, local.Unit(1).X); Equal(new UnitOperationResult(), moving.Poll()!.Value); Equal(10000UL, budget.Steps); Equal(1, local.Synchronizations);
    }
    private static void UnitOperationStackTimeoutAndPartialFailure()
    {
        using var f = new UnitOperationFixture(); using var operation = f.Operation(UnitOperationKind.RelocateStack); operation.Poll(); f.Tick += 3600;
        f.Channel.OnPoll = () => { f.Unit(0).Next = new(-1); f.Unit(1).Previous = new(-1); }; Equal(true, operation.Poll() is null); Equal(1, f.Timeouts); Equal(true, operation.Poll() is null); Equal(1, f.Channel.Request!.Arguments[0]); f.Channel.Replies.Enqueue(new(3)); Equal(new UnitOperationResult(3, true), operation.Poll()!.Value); Equal(1, f.Synchronizations);
        using var fail = new UnitOperationFixture(); fail.Runtime.World.Multiplayer.NetworkHost = true; fail.Services = fail.Services with { Placement = () => throw new IOException("sync") }; using var partial = fail.Operation(UnitOperationKind.RelocateStack); partial.Poll(); Throws<IOException>(() => partial.Poll()); Equal((short)4, fail.Unit(0).X); Equal((short)4, fail.Unit(1).X); Equal(true, partial.Result!.Value.Cancelled); Equal(1, fail.Channel.Cancellations); fail.Runtime.PrepareNewGame();
    }
    private sealed class CommandUnit(UnitOperationServices services, IUnitRequestChannel channel) : IContinuation
    {
        private UnitOperation? child;
        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            child ??= new(context, Request(UnitOperationKind.Relocate), false, new(10000, 64), services, channel);
            if (input.Kind == OperationInputKind.Cancel) { child.Cancel(); return new(CommandStatus.Cancelled); }
            return child.Poll().HasValue ? CommandDone(0) : new(CommandStatus.Pending);
        }
        public void Dispose() { if (child is null) channel.Cancel(); else child.Dispose(); }
    }
    private static void UnitOperationCommandComposition()
    {
        for (int route = 0; route < 3; ++route)
        {
            using var f = new UnitOperationFixture(); f.Runtime.EndClientSession(); f.Runtime.RegisterCommand(CommandDefinitionFor(_ => new CommandUnit(f.Services, f.Channel))); f.Runtime.BeginClientSession(new(10, 20)); f.Runtime.World.Multiplayer.NetworkHost = route == 0;
            var client = SessionClient(f.Runtime); var id = client.Submit(CommandRequestFor()).Correlation.Command; Equal(true, f.Runtime.AdvanceOne());
            if (route != 0)
            {
                Equal((int)CommandStatus.Pending, (int)client.Result(id)!.Status); Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame());
                if (route == 2) f.Runtime.CancelCommand(id); else { f.Channel.Replies.Enqueue(new(0)); Equal(true, f.Runtime.ContinueCommand(id)); }
                Equal(true, f.Runtime.AdvanceOne());
            }
            Equal((int)(route == 2 ? CommandStatus.Cancelled : CommandStatus.Completed), (int)client.Result(id)!.Status); Equal(route == 0 ? 0 : 1, f.Channel.Sends); Equal(1, f.Channel.Cancellations); if (route == 0) Equal((short)4, f.Unit(0).X); f.Runtime.PrepareNewGame();
        }
    }
}
