using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static List<Choice> ActiveRolls(string call) => Enumerable.Range(0, 8).Select(d =>
        Choice.Create(new("A04.ground-base", call + "/tactics/plan", d), new SamplingLaw.SecondaryRange(0, 4), ChoiceCode.Secondary(0))).ToList();
    private sealed class ActiveFixture
    {
        public OrderFixture Execute { get; }
        public TacticsFixture Choose { get; }
        public RevelationFixture Data => Choose.Data;
        public GameState World => Data.World;
        public AutomatedOrders Active { get; }
        public AiUnitPasses Passes { get; }
        public List<UnitSlot?> Redraws { get; } = [];
        public bool FailRedraw;
        public ActiveFixture(List<Choice>? choices = null, RevelationFixture? data = null, List<Choice>? moveChoices = null)
        {
            Execute = new(data, moveChoices); Choose = new(choices, Execute.Data);
            Data.Stacks.Relocate(0, new(18, 12), false);
            World.Globals.TurnCiv = 1; World.Globals.ActiveUnit = new(0);
            Active = new(World, Data.Rules, Choose.Tactics, Execute.Orders);
            Passes = new(World, Data.Rules, Data.Stacks, Active, () =>
            { Redraws.Add(World.Globals.ActiveUnit); if (FailRedraw) throw new InvalidOperationException("Cursor unavailable"); });
        }
        public AutomatedOrders.Operation Begin(bool hold = false) => Active.Begin(Choose.Starts, Execute.Move.Inputs, hold, "active");
        public AiUnitPasses.Operation BeginPasses(bool hold = false) => Passes.Begin(Choose.Starts, Execute.Move.Inputs, hold, "passes");
        public void Complete(AutomatedOrders.Operation op)
        { for (int i = 0; i < 1000 && !op.Completed; ++i) Equal(null, op.Resume()); Equal(true, op.Completed); Consumed(); }
        public void Complete(AiUnitPasses.Operation op)
        { for (int i = 0; i < 1000 && !op.Completed; ++i) Equal(null, op.Resume()); Equal(true, op.Completed); Consumed(); }
        public void Consumed()
        {
            Choose.Random.RequireChoicesConsumed(); Choose.Missions.Lifecycle.Random.RequireChoicesConsumed();
            Choose.Para.Capture.Life.Random.RequireChoicesConsumed(); Execute.Move.Capture.Life.Random.RequireChoicesConsumed();
        }
    }
    private static void AutomatedOrdersGotoBypass()
    {
        foreach (bool hold in new[] { false, true })
        {
            var f = new ActiveFixture(); f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 12);
            f.Data.Unit(0).MovesUsed = 1; f.Data.Unit(0).Flags.Colonist = true;
            using var op = f.Begin(hold); f.Complete(op);
            Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal((byte)4, f.Data.Unit(0).MovesUsed);
            Equal(!hold, f.Data.Unit(0).Flags.Colonist); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        }
        // The first disjunct has no movement-left gate; an exhausted Goto still reaches U02.
        var spent = new ActiveFixture(); spent.Data.Unit(0).Orders = UnitOrder.GoTo; spent.Data.Unit(0).Destination = new(20, 12);
        spent.Data.Unit(0).MovesUsed = 6; spent.Data.Unit(0).Flags.Colonist = true;
        using var entered = spent.Begin(); spent.Complete(entered);
        Equal(1, spent.Execute.Move.Blinks); Equal(true, spent.Data.Unit(0).Flags.Colonist);
        Equal(new MapPoint(18, 12), spent.Data.Unit(0).Position);
    }
    private static void AutomatedOrdersNavalGate()
    {
        foreach (int variant in new[] { 0, 1, 2 })
        {
            var f = new ActiveFixture(); f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 12);
            f.Data.Unit(0).MovesUsed = 1; f.Data.Unit(0).Flags.Colonist = true;
            f.World.Globals.Status.Scenario = true; f.World.Scenario.Flags.HistoricalAi = true; f.World.Globals.Turn = 5;
            if (variant != 0) f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.NavalSuperiority };
            if (variant != 1) f.Data.AddUnit(1, 2, 3, new(17, 11));
            using var op = f.Begin(); f.Complete(op);
            Equal(variant != 2, f.Data.Unit(0).Flags.Colonist);
            Equal(new MapPoint(variant == 2 ? 18 : 20, 12), f.Data.Unit(0).Position);
            Equal(variant == 2 ? 0 : 1, f.Execute.Move.Blinks);
        }
    }
    private static void AutomatedOrdersActualChoiceAndWork()
    {
        var f = new ActiveFixture(ActiveRolls("active")); f.Data.Unit(0).LastDirection = 1;
        using var move = f.Begin(); f.Complete(move);
        Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal((byte)3, f.Data.Unit(0).MovesUsed);
        var work = new ActiveFixture(); work.Data.Unit(0).Orders = UnitOrder.Clean;
        work.World.Map.TileAt(work.Data.Unit(0).Position).Features |= MapFeatures.Pollution;
        using var clean = work.Begin(); work.Complete(clean); Equal((byte)6, work.Data.Unit(0).MovesUsed);
        Equal(1, work.Execute.Effects.Count);
        var off = new ActiveFixture(); off.Data.Stacks.Relocate(0, new(-2, 12), false);
        using var unhandled = off.Begin(); off.Complete(unhandled);
        Equal(UnitOrder.Sleeping, off.Data.Unit(0).Orders); Equal((byte)6, off.Data.Unit(0).MovesUsed);
    }
    private static void AutomatedOrdersDirectFounding()
    {
        var f = new ActiveFixture(); f.Data.Unit(0).Type = new(0); f.World.Globals.Turn = 1;
        f.Choose.Starts.Positions[0] = f.Data.Unit(0).Position; f.World.Map.TileAt(f.Data.Unit(0).Position).Fertility = 9;
        using var city = f.Begin(); f.Complete(city);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(1, f.World.Entities.Cities.End);
        Equal(0, f.Execute.Move.Blinks);
    }
    private static void AiPassesReverseAndAdmission()
    {
        var f = new ActiveFixture();
        for (int i = 0; i < 4; ++i)
        {
            if (i > 0) f.Data.AddUnit(i, i == 2 ? 2 : 1, 4, new(10 + i * 2, 10));
            f.Data.Unit(i).Orders = UnitOrder.Clean;
        }
        using var op = f.BeginPasses(); f.Complete(op);
        Sequence(new UnitSlot?[] { new(3), new(1), new(0), new(0), new(0), new(0) }, f.Redraws);
        Equal((byte)0, f.Data.Unit(2).MovesUsed);
        foreach (int i in new[] { 0, 1, 3 }) Equal((byte)6, f.Data.Unit(i).MovesUsed);
        Equal(3, f.Execute.Effects.Count); Equal((UnitSlot?)new UnitSlot(0), f.World.Globals.ActiveUnit);
    }
    private static void AiPassesHistoricalAirFirst()
    {
        foreach (bool restrict in new[] { false, true })
        {
            var f = new ActiveFixture(); f.World.Globals.Status.Scenario = true; f.World.Scenario.Flags.HistoricalAi = true;
            f.World.Globals.Turn = 1; f.World.Scenario.ObjectiveCiv = (short)(restrict ? 1 : 2);
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air };
            f.Data.AddUnit(1, 1, 3, new(10, 10)); f.Data.Unit(0).Orders = f.Data.Unit(1).Orders = UnitOrder.Clean;
            using var op = f.BeginPasses(); f.Complete(op);
            Sequence(restrict ? new UnitSlot?[] { new(0), new(1), new(1) } : new UnitSlot?[] { new(1), new(0), new(0), new(0) }, f.Redraws);
        }
    }
    private static void AiPassesNoMovesAndSleep()
    {
        var f = new ActiveFixture(); f.World.Globals.ActiveUnit = null;
        f.Data.Unit(0).Orders = UnitOrder.Sleeping;
        f.Data.AddUnit(1, 1, 4, new(10, 10)); f.Data.Unit(1).MovesUsed = 6;
        f.Data.AddUnit(2, 2, 4, new(12, 10));
        using var op = f.BeginPasses(); f.Complete(op);
        Equal(4, f.Redraws.Count); Equal(true, f.Redraws.All(x => x is null)); Equal(0, f.Execute.Move.Blinks);
    }
    private static void AiPassesActionLimits()
    {
        foreach (bool fullGoto in new[] { false, true })
        {
            int count = fullGoto ? 21 : 11;
            var choices = fullGoto ? [] : Enumerable.Range(0, count).SelectMany(i => ActiveRolls("passes/0/0/" + i)).ToList();
            var f = new ActiveFixture(choices); f.World.UnitMovementActive = true; // Actual U02 busy guard leaves orders untouched.
            f.Data.Unit(0).LastDirection = 1;
            if (fullGoto) { f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 12); f.Data.Unit(0).MovesUsed = 1; }
            using var op = f.BeginPasses();
            int yields = 0;
            while (!op.Completed && yields < 100) { Equal(null, op.Resume()); ++yields; }
            Equal(true, op.Completed); f.Consumed(); Equal(count + 2, yields);
            Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(new MapPoint(18, 12), f.Data.Unit(0).Position);
            Equal(true, f.World.UnitMovementActive); Equal(2, f.Redraws.Count);
        }
    }
    private static void AiPassesFoundingBoundChange()
    {
        var f = new ActiveFixture(); f.Data.Unit(0).Type = new(0); f.World.Globals.Turn = 1;
        f.Choose.Starts.Positions[0] = f.Data.Unit(0).Position; f.World.Map.TileAt(f.Data.Unit(0).Position).Fertility = 9;
        using var op = f.BeginPasses(); f.Complete(op);
        Equal(0, f.World.Entities.Units.End); Equal(1, f.World.Entities.Cities.End); Equal(1, f.Redraws.Count);
    }
    private static void AiPassesHutAppend()
    {
        var choices = new[] { HutDraw("outcome", 5, 1), HutDraw("mounted", 3, 0), HutDraw("mercenary-type", 2, 0) }
            .Select(c => c with { Key = c.Key with { Call = "passes/0/0/0/orders/0/finish/hut" } }).ToList();
        var f = new ActiveFixture(moveChoices: choices);
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Movement = 6 };
        f.Data.Rules.Units[UnitTypeIds.Chariot] = f.Data.Rules.Units[UnitTypeIds.Chariot] with { Movement = 0 };
        f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 12); f.Data.Unit(0).MovesUsed = 3;
        f.World.Map.TileAt(new(20, 12)).Owner = null;
        for (int seed = 0; seed < 256 && !f.World.Map.Hut(new(20, 12)); ++seed) f.World.Map.PatternSeed = (ushort)seed;
        Equal(true, f.World.Map.Hut(new(20, 12)));
        using var op = f.BeginPasses(); f.Complete(op);
        Equal(2, f.World.Entities.Units.End); Equal(3, f.Redraws.Count);
        Equal(new MapPoint(20, 12), f.Data.Unit(1).Position); Equal((byte)UnitTypeIds.Chariot, f.Data.Unit(1).Type.Value);
        Equal(UnitOrder.GoTo, f.Data.Unit(0).Orders); // Bound change bypasses ordinary arrival cleanup.
    }
    private static void AiPassesExtraTransport()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new ActiveFixture(); f.Data.Unit(0).Type = new(1); f.Data.Unit(0).Orders = UnitOrder.GoTo;
            f.Data.Unit(0).Destination = new(20, 12); f.Data.Unit(0).MovesUsed = 3;
            f.World.Map.TileAt(new(18, 12)).Terrain = f.World.Map.TileAt(new(20, 12)).Terrain = Terrain.Ocean;
            f.World.Map.TileAt(new(20, 12)).RevealedTo = CivilizationSet.Only(variant is 1 or 3 ? 2 : 1);
            int owner = variant == 3 ? 0 : 1; f.World.Globals.TurnCiv = (sbyte)owner; f.Data.Unit(0).Owner = new((byte)owner);
            f.Data.Stacks.RelocateInPlace(0, false);
            f.Data.AddUnit(1, owner, 3, new(18, 12)); f.Data.Unit(1).Orders = UnitOrder.Sleeping;
            f.Data.Unit(1).Carrier = new(0); f.Data.Unit(1).MovesUsed = 6;
            if (variant is 2 or 3) f.Data.AddUnit(2, 2, 4, new(21, 11));
            if (variant == 1)
            {
                // A custom sea worker completes existing cleanup without relocation clearing visibility.
                f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Role = RuleRole.Settle };
                f.Data.Unit(0).Orders = UnitOrder.Clean; f.Data.Unit(0).Counter = 3;
                f.Data.Unit(0).Visibility = CivilizationSet.Only(2);
            }
            using var op = f.BeginPasses(); f.Complete(op);
            Equal(new MapPoint(variant == 1 ? 18 : 20, 12), f.Data.Unit(0).Position);
            Equal(f.Data.Unit(0).Position, f.Data.Unit(1).Position);
            Equal(variant is 1 or 2 ? UnitOrder.None : UnitOrder.Sleeping, f.Data.Unit(1).Orders);
            Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(4, f.Redraws.Count);
        }
    }
    private static void AiPassesPrivateMovement()
    {
        using var runtime = new SessionRuntime(new(), new(732)); ParticipantId actorSeat = default;
        var f = new ActiveFixture(data: new(runtime.Process, runtime.World));
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Multiplayer.LocalHuman = 1;
        f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = new(20, 12);
        f.Data.Unit(0).MovesUsed = 3; f.Data.Unit(0).AuxiliaryTimer = 47;
        runtime.RegisterCommand(new(new(1, "test.passes", "Run unit passes", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
            _ => new AiUnitPassOperation(f.BeginPasses(), _ => actorSeat, _ => throw new InvalidOperationException(),
                _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())));
        runtime.BeginClientSession(new()); var caller = SessionClient(runtime, 2); var actor = SessionClient(runtime); actorSeat = actor.Identity.Participant;
        var command = caller.Submit(new("test.passes", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(null, RequiredResult(caller, command).Interaction); var prompt = RequiredInteraction(actor, command);
        Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal(true, f.World.UnitMovementActive);
        Equal(false, caller.Reply(prompt, 0).Accepted); Equal(true, actor.Reply(prompt, 0).Accepted);
        for (int i = 0; i < 20 && RequiredResult(caller, command).Status != CommandStatus.Completed; ++i) runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(caller, command).Status); Equal(false, f.World.UnitMovementActive);
        Equal((byte)0, f.Data.Unit(0).AuxiliaryTimer); Equal(false, actor.Reply(prompt, 0).Accepted); f.Consumed();
    }
    private static void AiPassesFailures()
    {
        var f = new ActiveFixture(); f.Data.Unit(0).Orders = UnitOrder.Clean; f.FailRedraw = true;
        using var op = f.BeginPasses(); f.Complete(op); Equal(2, op.PresentationFailures.Count);
        var missing = new ActiveFixture(); missing.Data.Unit(0).Flags.Colonist = true;
        using var failure = missing.BeginPasses(); Throws<ChoiceException>(() => failure.Resume());
        Equal(false, missing.Data.Unit(0).Flags.Colonist); Equal((UnitSlot?)new UnitSlot(0), missing.World.Globals.ActiveUnit);
        Throws<InvalidOperationException>(() => failure.Resume());
        var remote = new ActiveFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp; remote.World.Globals.ActiveUnit = null;
        using var rejected = remote.BeginPasses(); Throws<InvalidOperationException>(() => rejected.Resume()); Equal(null, remote.World.Globals.ActiveUnit);
        var absent = new ActiveFixture(); absent.World.Globals.ActiveUnit = null;
        using var noActor = absent.Begin(); Throws<InvalidOperationException>(() => noActor.Resume());
    }
    private static void AiPassesPrivateRansom()
    {
        foreach (bool pay in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(731)); ParticipantId victimSeat = default;
            var f = new ActiveFixture(data: new(runtime.Process, runtime.World));
            f.World.Globals.TurnCiv = 0; f.Data.Unit(0).Owner = new(0); f.Data.Stacks.RelocateInPlace(0, false);
            f.World.Globals.Turn = 3; f.Choose.City(f.Choose.At(0), 2); f.World.Civilizations[2].Gold = 150;
            runtime.RegisterCommand(new(new(1, "test.passes", "Run unit passes", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
                _ => new AiUnitPassOperation(f.BeginPasses(), _ => victimSeat, _ => throw new InvalidOperationException(),
                    _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())));
            runtime.BeginClientSession(new()); var caller = SessionClient(runtime); var victim = SessionClient(runtime, 2); victimSeat = victim.Identity.Participant;
            var command = caller.Submit(new("test.passes", new([]))).Correlation.Command; runtime.AdvanceOne();
            Equal(null, RequiredResult(caller, command).Interaction); var prompt = RequiredInteraction(victim, command);
            Equal(false, caller.Reply(prompt, 1).Accepted);
            if (pay) victim.Reply(prompt, 1); else victim.Cancel(command);
            for (int i = 0; i < 20 && RequiredResult(caller, command).Status != CommandStatus.Completed; ++i) runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(caller, command).Status); Equal(pay ? 50 : 30, f.World.Civilizations[2].Gold);
            if (!pay) Equal(new CivilizationSlot(0), f.World.Entities.Cities.Storage[0].Owner); // Refusal resumes actual conquest and plunder.
            Equal(!pay, f.Data.Unit(0).Token.Occupied); Equal(false, victim.Reply(prompt, 1).Accepted); f.Consumed();
        }
        var pending = new ActiveFixture(); pending.World.Globals.TurnCiv = 0; pending.Data.Unit(0).Owner = new(0);
        pending.World.Globals.Turn = 3; pending.Choose.City(pending.Choose.At(0), 2); pending.World.Civilizations[2].Gold = 150;
        using var operation = pending.BeginPasses(); Equal(true, operation.Resume() is AutomatedOrderDecision.Tactics);
        operation.Dispose(); Equal(150, pending.World.Civilizations[2].Gold); Throws<ObjectDisposedException>(() => operation.Resume());
    }
}
