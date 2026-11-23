using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class OrderFixture : ITerrainWorkEffects
    {
        public DispatcherFixture Move { get; }
        public RevelationFixture Data => Move.Data;
        public UnitOrderExecution Orders { get; }
        public List<string> Effects = [];
        public CitySlot? CityToRefresh => null;
        public OrderFixture(RevelationFixture? data = null)
        {
            Move = new(data: data);
            Data.Rules.Cosmic = Data.Rules.Cosmic with { TransformationWork = 4 };
            Data.Rules.Terrain[(int)Terrain.Grassland] = Data.Rules.Terrain[(int)Terrain.Grassland] with
            { Work = new(new(TerrainResult.ImproveInPlace, 1, 4), new(TerrainResult.ImproveInPlace, 1, 4), (TerrainResult)Terrain.Plains) };
            var fort = new UnitFortification(Data.World, c => Effects.Add("home:" + c.Value), _ => Effects.Add("fortified"), _ => Effects.Add("unit"));
            Orders = new(Data.World, Data.Rules, fort, new(Data.World, Data.Process.Rules, Data.Stacks, this),
                new(Data.World, Data.Rules, Data.Stacks, new(Data.World, Data.Rules, Data.Stacks)), Move.Dispatcher);
            Data.Unit(0).Destination = new(20, 12); Data.Unit(0).Orders = UnitOrder.GoTo;
            // Existing effect fixtures check the actual move endpoints; this does not execute a child.
            using var setup = Move.Begin();
        }
        public UnitOrderExecution.Operation Begin() => Orders.Begin(new(0), Move.Inputs, "orders");
        public void Complete(UnitOrderExecution.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); Equal(false, Data.World.UnitMovementActive);
            Move.Capture.Life.Random.RequireChoicesConsumed();
        }
        public void Notice(UnitSlot unit, UnitOrder job, TerrainWorkProblem problem) => Effects.Add("notice");
        public void Redraw(MapPoint position, int radius) => Effects.Add("work:" + radius);
        public void RedrawStatus() => Effects.Add("status");
        public void UnitChanged(UnitSlot unit) => Effects.Add("unit");
        public void CityChanged(CitySlot city) => Effects.Add("city");
    }

    private static void OrdersDispatchChildren()
    {
        foreach (var order in new[] { UnitOrder.None, UnitOrder.Sleeping, UnitOrder.Fortified, UnitOrder.AiIdle })
        {
            var f = new OrderFixture(); f.Data.Unit(0).Orders = order;
            using var op = f.Begin(); f.Complete(op); Equal(0, op.Result);
            Equal(order, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(0, f.Effects.Count);
        }
        var fort = new OrderFixture(); fort.Data.Unit(0).Orders = UnitOrder.Fortifying;
        using var fortified = fort.Begin(); fort.Complete(fortified); Equal(1, fortified.Result);
        Equal(UnitOrder.Fortified, fort.Data.Unit(0).Orders); Equal((byte)0, fort.Data.Unit(0).MovesUsed);
        Sequence(new[] { "fortified", "unit" }, fort.Effects);
        foreach (var job in new[] { UnitOrder.Fortress, UnitOrder.Road, UnitOrder.Irrigate, UnitOrder.Mine,
            UnitOrder.Transform, UnitOrder.Clean, UnitOrder.Airbase })
        {
            var f = new OrderFixture(); f.Data.Unit(0).Orders = job;
            using var op = f.Begin(); f.Complete(op); Equal(1, op.Result);
            Equal(job, f.Data.Unit(0).Orders); Equal((byte)1, f.Data.Unit(0).Counter);
            Equal((byte)6, f.Data.Unit(0).MovesUsed); Sequence(new[] { "work:0" }, f.Effects);
        }
    }

    private static void OrdersGotoArrivalAndRefusal()
    {
        foreach (var order in new[] { UnitOrder.GoTo, UnitOrder.AiGoTo })
        {
            var f = new OrderFixture(); f.Data.Unit(0).Orders = order; f.Data.Unit(0).Flags.GoalReached = true;
            using var op = f.Begin(); f.Complete(op); Equal(1, op.Result); Equal(1, op.MovementResult);
            Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal((byte)3, f.Data.Unit(0).MovesUsed);
            Equal(order == UnitOrder.GoTo, f.Data.Unit(0).Flags.GoalReached);
            Equal(order == UnitOrder.GoTo ? UnitOrder.None : order, f.Data.Unit(0).Orders);
            Equal(-1, f.Data.World.Paths.Owner);
        }
        var exhausted = new OrderFixture(); exhausted.Data.Unit(0).MovesUsed = 6; exhausted.Data.Unit(0).Flags.GoalReached = true;
        using var rejected = exhausted.Begin(); exhausted.Complete(rejected); Equal(1, rejected.Result); Equal(0, rejected.MovementResult);
        Equal(1, exhausted.Data.World.Paths.Owner); Equal(true, exhausted.Data.Unit(0).Flags.GoalReached);
        Equal(new MapPoint(18, 12), exhausted.Data.Unit(0).Position);
        var negative = new OrderFixture(); negative.Data.Unit(0).Destination = negative.Data.Unit(0).Position;
        negative.Data.Rules.Units[0] = negative.Data.Rules.Units[0] with { Role = RuleRole.SeaTransport };
        negative.Data.World.Globals.Turn = 13;
        using var stopped = negative.Begin(); negative.Complete(stopped); Equal(1, stopped.Result); Equal(null, stopped.MovementResult);
        Equal(UnitOrder.None, negative.Data.Unit(0).Orders); Equal((byte)5, negative.Data.Unit(0).Counter);
        Equal((byte)0, negative.Data.Unit(0).MovesUsed); Equal(-1, negative.Data.World.Paths.Owner);
    }

    private static void OrdersAiArrivalExhaustion()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new OrderFixture(); f.Data.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.Data.World.Multiplayer.LocalHuman = 2;
            f.Data.Unit(0).AuxiliaryTimer = (byte)(variant == 0 ? 9 : 10);
            if (variant == 2) f.Data.AddUnit(2, 2, 0, new(22, 12));
            if (variant == 3)
            {
                f.Data.Unit(0).Type = new(1); f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Role = RuleRole.SeaTransport };
                f.Data.World.Map.TileAt(new(18, 12)).Terrain = f.Data.World.Map.TileAt(new(20, 12)).Terrain = Terrain.Ocean;
                f.Data.World.Globals.Turn = 13;
            }
            using var op = f.Begin(); f.Complete(op); Equal(1, op.Result); Equal(1, op.MovementResult);
            Equal(true, f.Data.Unit(0).Flags.GoalReached); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
            Equal((byte)(variant is 1 or 3 ? 6 : 3), f.Data.Unit(0).MovesUsed);
            if (variant == 3) Equal((byte)5, f.Data.Unit(0).Counter);
        }
    }

    private static void OrdersRetainedMovement()
    {
        foreach (bool abandon in new[] { false, true })
        {
            var f = new OrderFixture(); f.Data.Unit(0).AuxiliaryTimer = 47;
            using var op = f.Begin(); var pending = op.Resume();
            Equal(true, pending is MovementDecision.Finish { Decision: MovementCompletionDecision.LongMove });
            Equal(pending, op.Resume()); Equal(false, op.Completed); Equal(true, f.Data.World.UnitMovementActive);
            Equal(1, f.Data.World.Paths.Owner); Equal(false, f.Data.Unit(0).Flags.GoalReached);
            Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal((byte)3, f.Data.Unit(0).MovesUsed);
            if (abandon)
            {
                op.Dispose(); Equal(false, f.Data.World.UnitMovementActive); Equal(1, f.Data.World.Paths.Owner);
                Equal(false, f.Data.Unit(0).Flags.GoalReached); Throws<ObjectDisposedException>(() => op.Resume());
            }
            else
            {
                f.Data.Unit(0).Destination = new(22, 12); op.Resume(new(0)); f.Complete(op);
                Equal(1, op.Result); Equal(false, f.Data.Unit(0).Flags.GoalReached); Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders);
                Equal(-1, f.Data.World.Paths.Owner); Equal((byte)3, f.Data.Unit(0).MovesUsed);
            }
        }
    }

    private static void OrdersMovementFailure()
    {
        var f = new OrderFixture(); f.Move.Relocation.FailPublish = 1;
        using var op = f.Begin(); Throws<InvalidOperationException>(() => op.Resume());
        Equal(false, op.Completed); Equal(false, f.Data.World.UnitMovementActive);
        Equal(1, f.Data.World.Paths.Owner); Equal(false, f.Data.Unit(0).Flags.GoalReached);
        Equal((byte)3, f.Data.Unit(0).MovesUsed); Throws<InvalidOperationException>(() => op.Resume());
    }

    private static void OrdersPrivateSession()
    {
        foreach (int variant in new[] { 0, 1, 2 })
        {
            using var runtime = new SessionRuntime(new(), new(254));
            var f = new OrderFixture(CreationFixture(new(runtime.Process, runtime.World)));
            if (variant == 0) f.Data.Unit(0).AuxiliaryTimer = 47;
            else
            {
                f.Data.Unit(0).Type = new(1); f.Data.World.Map.TileAt(new(18, 12)).Terrain = Terrain.Ocean;
                f.Data.AddUnit(2, 1, 4, new(18, 12)); f.Data.Unit(2).Orders = UnitOrder.Sleeping; f.Data.Unit(2).Carrier = new(0);
            }
            ParticipantId actorSeat = default, observerSeat = default;
            runtime.RegisterCommand(new(new(1, "test.orders", "Authored order execution", AccessClass.Player, new([]), new([new("result")])), (_, _, _) => new(),
                _ => new UnitOrderOperation(f.Begin(), c => c == 1 ? actorSeat : observerSeat,
                    _ => throw new InvalidOperationException("No investigation"), _ => throw new InvalidOperationException("No intelligence"),
                    _ => throw new InvalidOperationException("No capture"), _ => throw new InvalidOperationException("No hut"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var observer = SessionClient(runtime, 2);
            actorSeat = actor.Identity.Participant; observerSeat = observer.Identity.Participant;
            var id = actor.Submit(new("test.orders", new([]))).Correlation.Command; runtime.AdvanceOne();
            var question = RequiredInteraction(actor, id); Equal(false, observer.Reply(question, 0).Accepted);
            if (variant == 2) Equal(true, actor.Cancel(id).Accepted);
            else Equal(true, actor.Reply(question, variant == 1 ? 1 : 0).Accepted);
            runtime.AdvanceOne();
            for (int i = 0; i < 20 && RequiredResult(actor, id).Status == CommandStatus.Pending; ++i)
            { Equal(false, runtime.ContinueCommand(id)); Equal(true, runtime.AdvanceOne()); }
            Equal(CommandStatus.Completed, RequiredResult(actor, id).Status);
            Equal(new CommandValue.Integer(1), RequiredResult(actor, id).Result.Single(v => v.Name == "result").Value);
            Equal(variant == 0, f.Data.Unit(0).Flags.GoalReached);
            Equal(variant == 2 ? 1 : -1, f.Data.World.Paths.Owner);
            if (variant == 0) Equal((byte)3, f.Data.Unit(0).MovesUsed);
            if (variant == 1) Equal(new MapPoint(20, 12), f.Data.Unit(2).Position);
            Equal(false, f.Data.World.UnitMovementActive);
            f.Move.Capture.Life.Random.RequireChoicesConsumed();
        }
    }
}
