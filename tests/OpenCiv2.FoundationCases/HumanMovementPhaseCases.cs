using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F04 §7: actual activation/revelation, order execution and human-phase wait/exit rules.
    private sealed class HumanPhaseFixture : IHumanMovementPhaseEffects
    {
        public ActiveFixture F { get; }
        public GameState World => F.World;
        public SelectionEffects Selection { get; } = new();
        public List<string> Calls { get; } = [];
        public string? Fail;
        public HumanMovementView View = new(new(18, 12), null);
        public HumanPhaseFixture(RevelationFixture? data = null)
        {
            F = new(data: data);
            World.Multiplayer.LocalHuman = 1; World.Globals.HumanPlayers = CivilizationSet.Only(1);
            World.TurnLoopActive = true; World.MovementMode = MovementMode.MoveUnits;
            F.Data.Unit(0).Orders = UnitOrder.None;
        }
        public HumanMovementPhase.Operation Begin(UnitRevelation? revelation = null)
        {
            var select = new UnitSelection(World, F.Data.Rules, new(), revelation ?? new(World, F.Data.Rules, F.Data.Stacks, F.Data), Selection);
            return new HumanMovementPhase(World, F.Data.Rules, select, new(World, F.Data.Rules, select, Selection),
                new(World, F.Data.Rules, F.Execute.Orders, F.Active), this).Begin(F.Choose.Starts, F.Execute.Move.Inputs, false, "human");
        }
        public HumanMovementDecision? Advance(HumanMovementPhase.Operation op, HumanMovementReply? reply = null)
        {
            var result = op.Resume(View, reply);
            for (int i = 0; i < 100 && !op.Completed && result is null; i++) { result = op.Resume(View); }
            Equal(true, op.Completed || result is not null); return result;
        }
        public void Fortify() => new UnitFortification(World, _ => { }, _ => { }, _ => { }).Apply(new(0));
        private void Call(string value) { Calls.Add(value); if (Fail == value) { throw new IOException(value); } }
        public void FocusInput() => Call("focus");
        public void EndOfTurn(bool moveUnits) => Call(moveUnits ? "end-move" : "end-view");
        public void CloseCity(CitySlot city) => Call("close:" + city.Value);
        public void FlushInput() => Call("flush");
    }
    private static HumanMovementReply ActiveInputHandled() => new HumanMovementReply.Active(new ActiveUnitReply.InputHandled());
    private static void HumanPhaseEntryAndAutomaticEnd()
    {
        var f = new HumanPhaseFixture(); f.F.Data.AddUnit(1, 1, 4, new(2, 2));
        f.F.Data.Unit(1).Orders = UnitOrder.Fortified; f.World.Globals.ActiveUnit = new(1);
        f.World.Globals.TransientFlags.EndOfTurn = f.World.Globals.TransientFlags.CityWindowOpened = true;
        int turn = f.World.Globals.Turn, year = f.World.Globals.Year;
        using var phase = f.Begin(); Equal(true, f.Advance(phase) is HumanMovementDecision.Active { Value: ActiveUnitDecision.PlayerInput });
        Equal((UnitSlot?)new UnitSlot(0), f.World.Globals.ActiveUnit); Equal(true, f.World.PlayerPhaseActive);
        Equal(false, f.World.Globals.TransientFlags.EndOfTurn); Equal(false, f.World.Globals.TransientFlags.CityWindowOpened);
        Equal(true, f.World.Map.TileAt(new(18, 14)).RevealedTo.Contains(1));
        f.Fortify(); Equal(null, f.Advance(phase, ActiveInputHandled()));
        Equal(true, phase.Completed); Equal(true, phase.WaitedForInput); Equal(true, phase.AutomaticallyEnded);
        Equal(false, f.World.PlayerPhaseActive); Equal(true, f.World.TurnLoopActive); Equal(null, f.World.Globals.ActiveUnit);
        Equal(turn, (int)f.World.Globals.Turn); Equal(year, (int)f.World.Globals.Year);
        Sequence(["focus", "end-move", "flush"], f.Calls); Equal(0, phase.PresentationFailures.Count); f.F.Consumed();
    }
    private static void HumanPhaseIdleAndAlwaysWait()
    {
        foreach (bool view in new[] { false, true })
        {
            var f = new HumanPhaseFixture(); f.F.Data.Unit(0).Orders = UnitOrder.Fortifying;
            if (view) { f.World.MovementMode = MovementMode.View; }
            using var phase = f.Begin(); Equal(true, f.Advance(phase) is HumanMovementDecision.IdleInput);
            Equal(view ? UnitOrder.Fortifying : UnitOrder.Fortified, f.F.Data.Unit(0).Orders);
            Equal(false, phase.WaitedForInput); Equal(false, phase.AutomaticallyEnded);
            Equal(true, f.Advance(phase, new HumanMovementReply.InputHandled()) is HumanMovementDecision.IdleInput);
            Equal(false, phase.WaitedForInput); phase.RequestStop(); Equal(null, f.Advance(phase));
            Equal(false, phase.AutomaticallyEnded); Equal(false, f.World.PlayerPhaseActive); f.F.Consumed();
        }
        var wait = new HumanPhaseFixture(); wait.World.Globals.GameOptions.Wait = true;
        using var waiting = wait.Begin(); wait.Advance(waiting); wait.Fortify();
        Equal(true, wait.Advance(waiting, ActiveInputHandled()) is HumanMovementDecision.IdleInput);
        Equal(true, waiting.WaitedForInput); Equal(false, waiting.Completed);
        wait.World.Globals.GameOptions.Wait = false;
        Equal(null, wait.Advance(waiting, new HumanMovementReply.InputHandled())); Equal(true, waiting.AutomaticallyEnded);
    }
    private static void HumanPhaseCityWindowWait()
    {
        foreach (bool selected in new[] { false, true })
        {
            var f = new HumanPhaseFixture(); f.F.Data.AddCity(0, 1, new(4, 4));
            using var phase = f.Begin(); f.Advance(phase); f.Fortify();
            f.World.Globals.TransientFlags.CityWindowOpened = true;
            f.View = f.View with { SelectedCity = selected ? new CitySlot(0) : null };
            var next = f.Advance(phase, ActiveInputHandled());
            Equal(!selected, phase.Completed); Equal(!selected, phase.WaitedForInput); Equal(!selected, phase.AutomaticallyEnded);
            if (selected)
            {
                Equal(true, next is HumanMovementDecision.IdleInput); Equal(false, f.World.Globals.TransientFlags.CityWindowOpened);
                phase.RequestStop(); Equal(null, f.Advance(phase)); Equal(false, f.Calls.Contains("close:0"));
            }
        }
    }
    private static void HumanPhaseBoundChangeReselects()
    {
        var f = new HumanPhaseFixture(); using var phase = f.Begin(); f.Advance(phase);
        f.F.Data.Unit(0).Flags.Waiting = true;
        var created = f.F.Data.Create(4, 1, new(20, 12)); Equal((UnitSlot?)new UnitSlot(1), created);
        f.F.Data.Unit(1).AuxiliaryTimer = 19;
        Equal(true, f.Advance(phase, ActiveInputHandled()) is HumanMovementDecision.Active { Value: ActiveUnitDecision.PlayerInput });
        Equal((UnitSlot?)new UnitSlot(1), f.World.Globals.ActiveUnit); Equal((byte)0, f.F.Data.Unit(1).AuxiliaryTimer);
        Equal(true, f.F.Data.Unit(0).Flags.Waiting); Equal(true, phase.WaitedForInput);
        phase.RequestStop(); Equal(null, f.Advance(phase)); Equal(false, phase.AutomaticallyEnded);
        Equal(0, phase.PresentationFailures.Count); f.F.Consumed();
    }
    private static void HumanPhaseExitPaths()
    {
        foreach (bool idle in new[] { false, true })
        {
            for (int gate = 0; gate < 4; gate++)
            {
                var f = new HumanPhaseFixture(); f.F.Data.AddCity(0, 2, new(4, 4));
                f.View = f.View with { SelectedCity = new CitySlot(0) };
                if (idle) { f.F.Data.Unit(0).Orders = UnitOrder.Fortified; }
                using var phase = f.Begin(); f.Advance(phase);
                switch (gate)
                {
                    case 0: f.World.TurnLoopActive = false; break;
                    case 1: f.World.PlayerPhaseActive = false; break;
                    case 2: f.World.Globals.HumanPlayers = default; break;
                    case 3: phase.RequestStop(); break;
                }
                Equal(null, f.Advance(phase, idle ? new HumanMovementReply.InputHandled() : ActiveInputHandled()));
                Equal(true, phase.Completed); Equal(false, phase.AutomaticallyEnded);
                Equal(gate == 0, f.World.PlayerPhaseActive);
                Equal(gate != 0, f.Calls.Contains("close:0")); Equal(gate != 0, f.Calls.Contains("flush"));
                Equal(0, phase.PresentationFailures.Count);
            }
        }
    }
    private static void HumanPhaseRetainsMovementAndFailures()
    {
        foreach (bool abandon in new[] { false, true })
        {
            var f = new HumanPhaseFixture(); using var phase = f.Begin(); f.Advance(phase);
            f.F.Data.Unit(0).Orders = UnitOrder.GoTo; f.F.Data.Unit(0).Destination = new(20, 12);
            f.F.Data.Unit(0).AuxiliaryTimer = 47; f.F.Data.Unit(0).MovesUsed = 1;
            var pending = f.Advance(phase, ActiveInputHandled());
            Equal(true, pending is HumanMovementDecision.Active { Value: ActiveUnitDecision.Movement });
            Equal(new MapPoint(20, 12), f.F.Data.Unit(0).Position); Equal(true, f.World.UnitMovementActive);
            phase.RequestStop(); Equal(pending, phase.Resume(f.View)); Equal(false, phase.Completed);
            if (abandon)
            {
                phase.Dispose(); Throws<ObjectDisposedException>(() => phase.Resume(f.View));
                Equal(false, phase.Completed); Equal(false, f.Calls.Contains("flush"));
            }
            else
            {
                Equal(null, f.Advance(phase, new HumanMovementReply.Active(new ActiveUnitReply.Movement(new(0)))));
                Equal(true, phase.WaitedForInput); Equal(false, phase.AutomaticallyEnded); Equal(true, f.Calls.Contains("flush"));
            }
            Equal(false, f.World.UnitMovementActive); Equal(0, phase.PresentationFailures.Count); f.F.Consumed();
        }
        var fail = new HumanPhaseFixture(); using var failed = fail.Begin(); fail.Advance(failed);
        fail.F.Data.Unit(0).Orders = UnitOrder.GoTo; fail.F.Data.Unit(0).Destination = new(20, 12);
        fail.F.Execute.Move.Relocation.FailPublish = 1;
        Throws<InvalidOperationException>(() => fail.Advance(failed, ActiveInputHandled()));
        Throws<InvalidOperationException>(() => failed.Resume(fail.View)); Equal(false, fail.World.UnitMovementActive);
        Equal((byte)3, fail.F.Data.Unit(0).MovesUsed); Equal(false, failed.Completed);
        var cosmetic = new HumanPhaseFixture { Fail = "flush" }; using var reported = cosmetic.Begin();
        cosmetic.Advance(reported); reported.RequestStop(); cosmetic.Advance(reported);
        Equal(true, reported.Completed); Equal(false, cosmetic.World.PlayerPhaseActive); Equal(1, reported.PresentationFailures.Count);
    }
    private static void HumanPhaseSessionCommands()
    {
        using var runtime = new SessionRuntime(new(), new(259)); var f = new HumanPhaseFixture(new(runtime.Process, runtime.World));
        HumanMovementPhaseHost? host = null; bool handled = false;
        runtime.RegisterCommand(new(new(1, "test.phase", "Drive phase", AccessClass.Player, new([]), HumanMovementPhaseHost.ResultSchema),
            (_, _, _) => new(), _ => host!.BeginSegment(handled)));
        runtime.RegisterCommand(new(new(1, "test.fortify", "Fortify", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CycleFortify(new(0))));
        runtime.BeginClientSession(new()); var player = SessionClient(runtime);
        using var phase = f.Begin(); using var owned = host = new(runtime, phase, () => f.View, c => new((ulong)c),
            _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        var first = player.Submit(new("test.phase", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(player, first).Status); Equal(true, host.NeedsInput); Equal(false, runtime.HasPendingCommands);
        Throws<InvalidOperationException>(() => runtime.PrepareNewGame());
        var order = player.Submit(new("test.fortify", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(player, order).Status); handled = true;
        var next = player.Submit(new("test.phase", new([]))).Correlation.Command;
        for (int n = 0; n < 20 && !RequiredResult(player, next).Status.Terminal(); n++) { runtime.AdvanceOne(); }
        Equal(CommandStatus.Completed, RequiredResult(player, next).Status); Equal(true, host.Completed);
        Equal(new CommandValue.Boolean(true), RequiredResult(player, next).Result.Single(v => v.Name == "automatically_ended").Value);
        Throws<InvalidOperationException>(() => host.BeginSegment(true)); runtime.PrepareNewGame();
    }
    private static void HumanPhasePrivateContactAndPin()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(260)); var f = new HumanPhaseFixture(new(runtime.Process, runtime.World));
            var contact = new ContactFixture(data: f.F.Data); ContactNeighbors(contact);
            f.World.Globals.TurnCiv = 1; f.World.Multiplayer.LocalHuman = 1; f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
            f.F.Data.Unit(0).Orders = UnitOrder.Fortified; f.View = new(new(20, 12), null);
            HumanMovementPhaseHost? host = null; ParticipantId recipient = default;
            runtime.RegisterCommand(new(new(1, "test.phase", "Drive phase", AccessClass.Player, new([]), HumanMovementPhaseHost.ResultSchema),
                (_, _, _) => new(), _ => host!.BeginSegment(false)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
            using var phase = f.Begin(ContactRevelation(contact)); using var owned = host = new(runtime, phase, () => f.View, _ => recipient,
                _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
                _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
            var command = actor.Submit(new("test.phase", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(player, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal((UnitSlot?)new UnitSlot(10), f.World.Globals.ActiveUnit); Equal(false, f.F.Data.Unit(12).Visibility.Contains(1));
            Equal(false, actor.Reply(prompt, 1).Accepted); Throws<InvalidOperationException>(() => host.BeginSegment(false));
            Throws<InvalidOperationException>(() => runtime.PrepareNewGame());
            if (cancel) { Equal(true, actor.Cancel(command).Accepted); }
            else { host.RequestStop(); Equal(true, player.Reply(prompt, 1).Accepted); }
            for (int n = 0; n < 20 && !RequiredResult(actor, command).Status.Terminal(); n++) { runtime.AdvanceOne(); }
            Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, phase.Completed);
            Equal(true, f.F.Data.Unit(12).Visibility.Contains(1)); Equal(false, phase.WaitedForInput);
            Equal(false, player.Reply(prompt, 1).Accepted); Equal(0, phase.PresentationFailures.Count);
            contact.F.T.T.Random.RequireChoicesConsumed(); runtime.PrepareNewGame();
        }
        using var abandoned = new SessionRuntime(new(), new(261)); var fixture = new HumanPhaseFixture(new(abandoned.Process, abandoned.World));
        using var operation = fixture.Begin(); using var owner = new HumanMovementPhaseHost(abandoned, operation, () => fixture.View,
            c => new((ulong)c), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        using var segment = owner.BeginSegment(false); segment.Dispose();
        Throws<ObjectDisposedException>(() => owner.BeginSegment(false)); abandoned.PrepareNewGame();
    }
}
