using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static ActiveUnitCycle.Operation BeginActiveCycle(ActiveFixture f, bool hold = false)
    {
        f.World.PlayerPhaseActive = f.World.TurnLoopActive = true;
        return new ActiveUnitCycle(f.World, f.Data.Rules, f.Execute.Orders, f.Active)
            .Begin(f.Choose.Starts, f.Execute.Move.Inputs, hold, "cycle");
    }
    private static void CompleteActiveCycle(ActiveUnitCycle.Operation op)
    {
        for (int i = 0; i < 1000 && !op.Completed; i++) { Equal(null, op.Resume()); }
        Equal(true, op.Completed);
    }
    private static void ActiveCycleGatesAndHandledOrders()
    {
        for (int gate = 0; gate < 7; gate++)
        {
            var f = new ActiveFixture(); using var op = BeginActiveCycle(f);
            switch (gate)
            {
                case 0: f.World.Globals.ActiveUnit = null; break;
                case 1: f.Data.Unit(0).Token = default; break;
                case 2: f.Data.Unit(0).Orders = UnitOrder.Fortified; break;
                case 3: f.Data.Unit(0).MovesUsed = 6; break;
                case 4: f.World.PlayerPhaseActive = false; break;
                case 5: f.World.TurnLoopActive = false; break;
                case 6: f.World.Globals.HumanPlayers = default; break;
            }
            CompleteActiveCycle(op); Equal(false, op.EnteredLoop); Equal(false, op.WaitedForInput);
            Equal(null, f.World.Globals.ActiveUnit); Equal(0, f.Execute.Effects.Count); f.Consumed();
        }
        foreach (var order in new[] { UnitOrder.Fortifying, UnitOrder.Clean })
        {
            var f = new ActiveFixture(); f.Data.Unit(0).Orders = order; using var op = BeginActiveCycle(f);
            CompleteActiveCycle(op); Equal(true, op.EnteredLoop); Equal(false, op.WaitedForInput); Equal(0, op.AutomatedIterations);
            Equal((UnitSlot?)new UnitSlot(0), f.World.Globals.ActiveUnit);
            Equal(order == UnitOrder.Fortifying ? UnitOrder.Fortified : UnitOrder.Clean, f.Data.Unit(0).Orders);
            Equal((byte)(order == UnitOrder.Fortifying ? 0 : 6), f.Data.Unit(0).MovesUsed); f.Consumed();
        }
    }
    private static void ActiveCycleInputAndCapturedActor()
    {
        var f = new ActiveFixture(); f.Data.Unit(0).Orders = UnitOrder.None; f.Data.AddUnit(1, 1, 4, new(4, 4));
        using var op = BeginActiveCycle(f); var input = op.Resume(); Equal(true, input is ActiveUnitDecision.PlayerInput);
        Equal(input, op.Resume()); Equal(true, op.WaitedForInput); Equal(false, op.Completed); Equal(0, f.Execute.Effects.Count);
        // Ordered input selects another unit and assigns the cached actor an actual terrain order.
        f.World.Globals.ActiveUnit = new(1); f.Data.Unit(0).Orders = UnitOrder.Clean;
        Equal(null, op.Resume(new ActiveUnitReply.InputHandled())); Equal(null, op.Resume());
        Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal((byte)0, f.Data.Unit(1).MovesUsed);
        Equal((UnitSlot?)new UnitSlot(0), op.Actor); Equal((UnitSlot?)new UnitSlot(1), f.World.Globals.ActiveUnit);
        op.RequestStop(); CompleteActiveCycle(op); Equal(0, op.PresentationFailures.Count); f.Consumed();
        var created = new ActiveFixture(); created.Data.Unit(0).Orders = UnitOrder.None;
        using var changed = BeginActiveCycle(created); changed.Resume();
        created.Data.Create(4, 1, new(4, 4));
        changed.Resume(new ActiveUnitReply.InputHandled()); CompleteActiveCycle(changed);
        Equal(true, changed.EnteredLoop); Equal((UnitSlot?)new UnitSlot(0), created.World.Globals.ActiveUnit);
        var stop = new ActiveFixture(); stop.Data.Unit(0).Orders = UnitOrder.None; using var waiting = BeginActiveCycle(stop);
        waiting.Resume(); waiting.RequestStop(); CompleteActiveCycle(waiting); Equal(true, waiting.WaitedForInput);
    }
    private static void ActiveCycleRetainedMovement()
    {
        foreach (bool automated in new[] { false, true })
        {
            var f = new ActiveFixture(); f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 12);
            f.World.Multiplayer.LocalHuman = 1; f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
            f.Data.Unit(0).AuxiliaryTimer = 47; f.Data.Unit(0).Flags.Automated = automated; f.Data.Unit(0).MovesUsed = 1;
            using var op = BeginActiveCycle(f); var decision = op.Resume();
            Equal(automated, decision is ActiveUnitDecision.Automated); Equal(!automated, decision is ActiveUnitDecision.Movement);
            Equal(decision, op.Resume()); Equal(false, op.WaitedForInput); Equal(true, f.World.UnitMovementActive);
            Equal(new MapPoint(20, 12), f.Data.Unit(0).Position);
            op.RequestStop(); Equal(decision, op.Resume());
            ActiveUnitReply answer = automated ? new ActiveUnitReply.Automated(new(Movement: new ResearchReply(0)))
                : new ActiveUnitReply.Movement(new(0));
            Equal(null, op.Resume(answer)); CompleteActiveCycle(op);
            Equal(false, op.WaitedForInput); Equal(automated ? 1 : 0, op.AutomatedIterations);
            Equal(false, f.World.UnitMovementActive); Equal(0, op.PresentationFailures.Count); f.Consumed();
        }
    }
    private static void ActiveCycleAutomationLimit()
    {
        foreach (bool replace in new[] { false, true })
        {
            var f = new ActiveFixture(); f.Data.Stacks.Relocate(0, new(0, 0), false);
            if (replace) { f.Data.AddUnit(1, 1, 4, new(0, 0)); }
            int moving = replace ? 1 : 0;
            foreach (ref var tile in f.World.Map.Tiles) { tile.Features |= MapFeatures.Road | MapFeatures.Railroad; }
            // A non-viewing human observer avoids animation; no gameplay child is substituted.
            f.World.Multiplayer.LocalHuman = 2; f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            f.Data.Unit(0).Orders = UnitOrder.None;
            using var op = BeginActiveCycle(f); Equal(true, op.Resume() is ActiveUnitDecision.PlayerInput);
            f.World.Globals.ActiveUnit = new((ushort)moving); f.Data.Unit(0).Flags.Automated = true;
            f.Data.Unit(moving).Flags.Automated = true; f.Data.Unit(moving).Orders = UnitOrder.GoTo;
            f.Data.Unit(moving).Destination = new(38, 22); f.Data.Unit(moving).MovesUsed = 1;
            op.Resume(new ActiveUnitReply.InputHandled());
            bool interrupted = false;
            for (int n = 0; n < 3000 && op.AutomatedIterations < 21 && !op.Completed; n++)
            {
                Equal(null, op.Resume());
                if (!interrupted && op.AutomatedIterations == 10)
                {
                    interrupted = true; f.Data.Unit(0).Flags.Automated = false;
                    var previous = f.Data.Unit(0).Orders; f.Data.Unit(0).Orders = UnitOrder.None;
                    Equal(true, op.Resume() is ActiveUnitDecision.PlayerInput); Equal(10, op.AutomatedIterations);
                    f.Data.Unit(0).Orders = previous; f.Data.Unit(0).Flags.Automated = true;
                    op.Resume(new ActiveUnitReply.InputHandled());
                }
            }
            Equal(true, interrupted); Equal(21, op.AutomatedIterations); Equal((byte)6, f.Data.Unit(0).MovesUsed);
            if (replace) { Equal((byte)1, f.Data.Unit(1).MovesUsed); }
            Equal(false, f.Data.Unit(moving).Position == new MapPoint(38, 22));
            op.RequestStop(); CompleteActiveCycle(op); Equal(0, op.PresentationFailures.Count); f.Consumed();
        }
    }
    private static void ActiveCycleFailuresAndDisposal()
    {
        var f = new ActiveFixture(); f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 12); f.Data.Unit(0).AuxiliaryTimer = 47;
        f.World.Multiplayer.LocalHuman = 1; f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        using var op = BeginActiveCycle(f); Equal(true, op.Resume() is ActiveUnitDecision.Movement); op.Dispose();
        Throws<ObjectDisposedException>(() => op.Resume(new ActiveUnitReply.Movement(new(0))));
        Equal(false, f.World.UnitMovementActive); Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal(false, op.Completed);
        var fail = new ActiveFixture(); fail.Data.Unit(0).Orders = UnitOrder.GoTo; fail.Data.Unit(0).Destination = new(20, 12);
        fail.Execute.Move.Relocation.FailPublish = 1; using var broken = BeginActiveCycle(fail);
        Throws<InvalidOperationException>(() => broken.Resume()); Throws<InvalidOperationException>(() => broken.Resume());
        Equal((byte)3, fail.Data.Unit(0).MovesUsed); Equal(false, fail.World.UnitMovementActive);
        var cosmetic = new ActiveFixture(); cosmetic.Data.Unit(0).Orders = UnitOrder.GoTo; cosmetic.Data.Unit(0).Destination = new(20, 12);
        cosmetic.Execute.Move.FailBlink = true; using var reported = BeginActiveCycle(cosmetic);
        reported.Resume(); reported.RequestStop(); CompleteActiveCycle(reported); Equal(1, reported.PresentationFailures.Count);
    }
    private sealed class CycleFortify(UnitSlot unit) : IContinuation
    {
        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            new UnitFortification(context.World, _ => { }, _ => { }, _ => { }).Apply(unit);
            context.Effect("unit.fortified", new([])); return new();
        }
    }
    private static void ActiveCycleSessionInputBoundary()
    {
        using var runtime = new SessionRuntime(new(), new(256)); var f = new ActiveFixture(data: new(runtime.Process, runtime.World));
        f.Data.Unit(0).Orders = UnitOrder.None; using var cycle = BeginActiveCycle(f);
        ActiveUnitCycleHost? host = null;
        bool handled = false;
        runtime.RegisterCommand(new(new(1, "test.cycle", "Drive cycle", AccessClass.Player, new([]), ActiveUnitCycleHost.ResultSchema), (_, _, _) => new(), _ => host!.BeginSegment(handled)));
        runtime.RegisterCommand(new(new(1, "test.fortify", "Fortify", AccessClass.Player, new([]), new([])), (_, _, _) => new(), _ => new CycleFortify(new(0))));
        runtime.BeginClientSession(new()); var player = SessionClient(runtime);
        using var owned = host = new ActiveUnitCycleHost(runtime, cycle, c => new((ulong)c), _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        var first = player.Submit(new("test.cycle", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(player, first).Status); Equal(true, host.NeedsInput); Equal(false, runtime.HasPendingCommands);
        Equal(new CommandValue.Boolean(true), RequiredResult(player, first).Result.Single(v => v.Name == "input_required").Value);
        Throws<InvalidOperationException>(() => runtime.PrepareNewGame());
        var order = player.Submit(new("test.fortify", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(player, order).Status); Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders);
        handled = true; var resumed = player.Submit(new("test.cycle", new([]))).Correlation.Command;
        for (int n = 0; n < 8 && !RequiredResult(player, resumed).Status.Terminal(); n++) { runtime.AdvanceOne(); }
        Equal(CommandStatus.Completed, RequiredResult(player, resumed).Status); Equal(true, host.Completed); Equal(true, cycle.WaitedForInput);
        Throws<InvalidOperationException>(() => host.BeginSegment(true)); runtime.PrepareNewGame();
    }
    private static void ActiveCyclePrivateSessionAndPin()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(257)); var f = new ActiveFixture(data: new(runtime.Process, runtime.World));
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 12); f.Data.Unit(0).AuxiliaryTimer = 47;
            using var cycle = BeginActiveCycle(f); ParticipantId seat = default;
            f.World.Multiplayer.LocalHuman = 1; f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
            ActiveUnitCycleHost? host = null;
            runtime.RegisterCommand(new(new(1, "test.cycle", "Drive cycle", AccessClass.Player, new([]), ActiveUnitCycleHost.ResultSchema), (_, _, _) => new(), _ => host!.BeginSegment(false)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); seat = player.Identity.Participant;
            using var owned = host = new ActiveUnitCycleHost(runtime, cycle, _ => seat, _ => throw new InvalidOperationException(),
                _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
            var command = actor.Submit(new("test.cycle", new([]))).Correlation.Command; runtime.AdvanceOne(); var prompt = RequiredInteraction(player, command);
            Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(prompt, 0).Accepted);
            Throws<InvalidOperationException>(() => host.BeginSegment(false)); Throws<InvalidOperationException>(() => runtime.PrepareNewGame());
            if (cancel) { Equal(true, actor.Cancel(command).Accepted); }
            else { host.RequestStop(); Equal(true, player.Reply(prompt, 0).Accepted); }
            for (int n = 0; n < 8 && !RequiredResult(actor, command).Status.Terminal(); n++) { runtime.AdvanceOne(); }
            Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, host.Completed);
            Equal(false, cycle.WaitedForInput); Equal(false, f.World.UnitMovementActive); Equal(false, player.Reply(prompt, 0).Accepted);
            runtime.PrepareNewGame();
        }
        using var abandoned = new SessionRuntime(new(), new(258)); var data = new ActiveFixture(data: new(abandoned.Process, abandoned.World));
        using var pendingCycle = BeginActiveCycle(data);
        using var owner = new ActiveUnitCycleHost(abandoned, pendingCycle, c => new((ulong)c), _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        using var segment = owner.BeginSegment(false); segment.Dispose();
        Throws<ObjectDisposedException>(() => owner.BeginSegment(false)); abandoned.PrepareNewGame();
    }
}
