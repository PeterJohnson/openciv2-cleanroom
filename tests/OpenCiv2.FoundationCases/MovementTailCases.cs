using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U04 Long-move handling: independently authored signed-byte and retained-decision vectors.
    private static void LongMoveTimerAndReversal()
    {
        foreach (var (timer, last, direction, expected, prompt) in new[]
        {
            (46, 0, 1, 47, false), (47, 0, 1, 48, true), (31, 1, 5, 47, false),
            (32, 1, 5, 48, true), (127, 0, 1, 128, false), (255, 0, 1, 0, false),
            (240, 1, 5, 0, false), (32, 255, -5, 48, true), (32, 255, 251, 33, false)
        })
        {
            var f = new RevelationFixture(); f.AddUnit(0, 1, 0, new(10, 8));
            f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).AuxiliaryTimer = (byte)timer; f.Unit(0).LastDirection = (byte)last;
            using var op = new LongMove(f.World).Begin(new(0), direction, true);
            var pending = op.Resume(); Equal(prompt, pending is not null); Equal((byte)expected, f.Unit(0).AuxiliaryTimer);
            Equal((byte)last, f.Unit(0).LastDirection); Equal(!prompt, op.Completed);
            if (prompt)
            {
                Equal(pending, op.Resume()); Equal((byte)expected, f.Unit(0).AuxiliaryTimer);
                op.Resume(0); Equal((byte)0, f.Unit(0).AuxiliaryTimer); Equal(UnitOrder.GoTo, f.Unit(0).Orders);
            }
            Equal(true, op.Completed); op.Resume(1); Equal(UnitOrder.GoTo, f.Unit(0).Orders);
        }
        foreach (bool local in new[] { false, true })
            foreach (bool automated in new[] { false, true })
            {
                var f = new RevelationFixture(); f.AddUnit(0, 2, 0, new(10, 8));
                f.Unit(0).Orders = (UnitOrder)((int)UnitOrder.GoTo | 16); f.Unit(0).Flags.Automated = automated;
                f.Unit(0).AuxiliaryTimer = 20; f.Unit(0).LastDirection = 0;
                using var op = new LongMove(f.World).Begin(new(0), 1, local); Equal(null, op.Resume());
                Equal(!local ? (byte)20 : automated ? (byte)21 : (byte)0, f.Unit(0).AuxiliaryTimer);
                Equal(automated, f.Unit(0).Flags.Automated); // Local indicator is independent of owner/human membership.
            }
    }
    private static void LongMoveRetainedRepliesAndFailures()
    {
        foreach (int reply in new[] { -1, 0, 1, 9 })
        {
            var f = new RevelationFixture(); f.AddUnit(0, 1, 0, new(10, 8));
            f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Flags.Automated = true; f.Unit(0).Flags.Garrison = true;
            f.Unit(0).AuxiliaryTimer = 47; f.Unit(0).LastDirection = 1; f.Unit(0).MovesUsed = 3;
            using var op = new LongMove(f.World).Begin(new(0), 1, true); Equal(1, op.Resume()!.Actor);
            // Accepted interaction resumes against the retained slot and live order; no timer/eligibility retest.
            f.Unit(0).Orders = UnitOrder.Sleeping; f.Unit(0).AuxiliaryTimer = 255;
            op.Resume(reply); Equal(reply == 0 ? UnitOrder.Sleeping : UnitOrder.None, f.Unit(0).Orders);
            Equal(reply == 0, f.Unit(0).Flags.Automated); Equal((byte)0, f.Unit(0).AuxiliaryTimer);
            Equal(true, f.Unit(0).Flags.Garrison); Equal((byte)3, f.Unit(0).MovesUsed); Equal((byte)1, f.Unit(0).LastDirection);
        }
        var remote = new RevelationFixture(); remote.AddUnit(0, 1, 0, new(10, 8)); remote.Unit(0).AuxiliaryTimer = 47;
        remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = new LongMove(remote.World).Begin(new(0), 1, true);
        Throws<InvalidOperationException>(() => denied.Resume()); Equal((byte)47, remote.Unit(0).AuxiliaryTimer);
        remote.World.Globals.Multiplayer = GameMode.SinglePlayer; Throws<InvalidOperationException>(() => denied.Resume());
        var disposed = new LongMove(remote.World).Begin(new(0), 1, true); disposed.Dispose(); Throws<ObjectDisposedException>(() => disposed.Resume());
    }
    private static void MovementCleanupBoundAndResult()
    {
        foreach (int result in new[] { 0, 1, 999, -1 })
            foreach (bool changedBound in new[] { false, true })
            {
                var f = new RevelationFixture(); f.AddUnit(0, 1, 0, new(10, 8));
                f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Flags.Automated = true; f.Unit(0).Flags.Garrison = true;
                f.Unit(0).AuxiliaryTimer = 23; f.Unit(0).MovesUsed = 2;
                var movement = new MovementContinuation(new(0), changedBound ? 0 : 1, new(10, 8));
                Equal(changedBound ? 0 : result, MovementCleanup.Apply(f.World, f.Rules, movement, 1, result));
                Equal(!changedBound && result == 0 ? UnitOrder.None : UnitOrder.GoTo, f.Unit(0).Orders);
                Equal(!changedBound && result == 0 ? (byte)6 : (byte)2, f.Unit(0).MovesUsed);
                Equal(changedBound || result == 0, f.Unit(0).Flags.Garrison); Equal((byte)23, f.Unit(0).AuxiliaryTimer);
            }
        // The bound, not live occupancy/token count, determines whether the retained record is processed.
        var dead = new RevelationFixture(); dead.AddUnit(0, 1, 0, new(10, 8)); dead.Unit(0).Token = default;
        dead.Unit(0).Orders = UnitOrder.GoTo;
        Equal(0, MovementCleanup.Apply(dead.World, dead.Rules, new(new(0), 1, new(10, 8)), 1, 0)); Equal(UnitOrder.None, dead.Unit(0).Orders);
    }
    private static void MovementCleanupAiAndTransport()
    {
        foreach (var (timer, expected, exhausted) in new[] { (18, 19, false), (19, 0, true), (126, 0, true), (127, 128, false), (255, 0, false) })
        {
            var f = new RevelationFixture(); f.AddUnit(0, 1, 1, new(10, 8)); f.World.Globals.Turn = 19;
            f.Rules.Units[1] = f.Rules.Units[1] with { Role = RuleRole.SeaTransport }; f.Unit(0).Flags.Garrison = true;
            f.Unit(0).AuxiliaryTimer = (byte)timer; f.Unit(0).Damage = 4; f.Unit(0).MovesUsed = 1;
            // Captured AI owner drives the branch even though the current unit belongs to a human.
            Equal(0, MovementCleanup.Apply(f.World, f.Rules, new(new(0), 1, new(10, 8)), 2, 0));
            Equal(UnitOrder.Fortified, f.Unit(0).Orders); Equal((byte)3, f.Unit(0).Counter);
            Equal((byte)expected, f.Unit(0).AuxiliaryTimer); Equal(exhausted ? (byte)6 : (byte)1, f.Unit(0).MovesUsed);
        }
        var f2 = new RevelationFixture(); f2.AddUnit(0, 1, 0, new(10, 8)); f2.Unit(0).Orders = UnitOrder.GoTo;
        f2.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => MovementCleanup.Apply(f2.World, f2.Rules, new(new(0), 1, new(10, 8)), 1, 0));
        Equal(UnitOrder.GoTo, f2.Unit(0).Orders);
    }
    private static void LongMovePrivateSession()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(227)); var f = new RevelationFixture(runtime.Process, runtime.World);
            f.AddUnit(0, 1, 0, new(10, 8)); f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Flags.Automated = true;
            f.Unit(0).AuxiliaryTimer = 47; f.Unit(0).LastDirection = 1;
            ParticipantId seat = default;
            runtime.RegisterCommand(new(new(1, "test.long-move", "Authored long move", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
                _ => new LongMoveOperation(new LongMove(f.World).Begin(new(0), 1, true), _ => seat)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var observer = SessionClient(runtime, 2); seat = actor.Identity.Participant;
            var id = actor.Submit(new("test.long-move", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(actor, id); Equal(false, observer.Reply(prompt, 1).Accepted); Equal((byte)48, f.Unit(0).AuxiliaryTimer);
            Equal(true, cancel ? actor.Cancel(id).Accepted : actor.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(actor, id).Status); Equal((byte)0, f.Unit(0).AuxiliaryTimer);
            Equal(cancel, f.Unit(0).Flags.Automated); Equal(cancel ? UnitOrder.GoTo : UnitOrder.None, f.Unit(0).Orders);
        }
    }
}
