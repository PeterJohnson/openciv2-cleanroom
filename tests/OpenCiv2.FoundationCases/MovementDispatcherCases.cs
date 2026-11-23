using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class DispatcherFixture : IMovementDispatcherEffects
    {
        public ForeignFixture Foreign { get; }
        public RevelationFixture Data => Foreign.Data;
        public GameState World => Data.World;
        public CityCaptureFixture Capture => Foreign.E.F.Drop.Capture;
        public LandFixture Land { get; }
        public CompletionFixture Finish { get; }
        public RelocationFixture Relocation { get; }
        public ForeignCityFixture City { get; }
        public MovementDispatcher Dispatcher { get; }
        public MovementDispatchInputs Inputs = new(new(false), new(false, false), 77, 77, "retained city", "Diplomat", "");
        public int Blinks, OffMap;
        public bool FailBlink, FailOffMap;
        public DispatcherFixture(List<Choice>? choices = null, RevelationFixture? data = null, bool retainGrants = false)
        {
            Foreign = new(choices, data, retainGrants); Land = new(city: new(capture: Capture)); Finish = new(capture: Capture);
            Relocation = new(Data); City = new(foreign: Foreign);
            var transport = new TransportMovement(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), Finish);
            Dispatcher = new(World, Data.Rules, Data.Stacks, new(Land.Entry, Foreign.Foreign, transport, City.City,
                new(World, Data.Rules, Data.Stacks, Capture.Life.Random),
                new(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), Relocation), Finish.Completion), this);
            Data.Stacks.Relocate(0, new(18, 12), false);
            Data.Rules.Terrain[(int)Terrain.Grassland] = Data.Rules.Terrain[(int)Terrain.Grassland] with { MovementCost = 1 };
            Data.Unit(0).MovesUsed = 0; World.Globals.ActiveUnit = new(0);
        }
        public MovementDispatcher.Operation Begin(int direction = 1, MovementOptions? options = null, UnitSlot? unit = null)
        {
            var from = Data.Unit(unit?.Value ?? 0).Position;
            var to = direction == 1 ? new MapPoint(from.X + 2, from.Y) : from;
            Relocation.Step = Relocation.Step with { From = from, To = to, Direction = direction };
            Land.Step = Land.Step with { From = from, To = to, Direction = direction };
            Foreign.Step = Foreign.Step with { From = from, To = to, Direction = direction };
            return Dispatcher.Begin(unit ?? new(0), direction, options ?? new(true, true), Inputs, "move");
        }
        public void Complete(MovementDispatcher.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); Equal(false, World.UnitMovementActive); Capture.Life.Random.RequireChoicesConsumed();
        }
        public void StopBlinking() { ++Blinks; if (FailBlink) throw new InvalidOperationException("Blink unavailable"); }
        public void NotOnMap(int recipient, UnitTarget unit) { ++OffMap; if (FailOffMap) throw new InvalidOperationException("Required map notice unavailable"); }
    }
    private static void MovementDispatcherOrdinaryAndPreview()
    {
        foreach (bool execute in new[] { false, true })
        {
            var f = new DispatcherFixture(); f.Data.Unit(0).Flags.Garrison = true;
            f.World.Map.TileAt(new(18, 12)).Owner = 7;
            using var op = f.Begin(options: new(true, execute)); f.Complete(op);
            Equal(1, op.Result); Equal(new MapPoint(execute ? 20 : 18, 12), f.Data.Unit(0).Position);
            Equal((byte)(execute ? 3 : 0), f.Data.Unit(0).MovesUsed); Equal(true, f.Data.Unit(0).Flags.Moved);
            Equal(false, f.Data.Unit(0).Flags.Garrison); Equal(true, f.World.Globals.TransientFlags.AttitudeDirty);
            Equal((byte?)1, f.World.Map.TileAt(new(18, 12)).Owner); Equal(execute ? 2 : 0, f.Relocation.Publications.Count);
            Equal(execute ? 1 : 0, f.Finish.OldPositions.Count); Equal(1, f.Blinks);
        }
        var failures = new DispatcherFixture { FailBlink = true }; failures.Relocation.FailCosmetic = true;
        using var moved = failures.Begin(); failures.Complete(moved); Equal(1, moved.Result); Equal(3, moved.PresentationFailures.Count);
        var wrap = new DispatcherFixture(); wrap.World.Globals.Rules.FlatWorld = false; wrap.Data.Stacks.Relocate(0, new(38, 12), false);
        using var wrapped = wrap.Begin(); wrap.Relocation.Step = wrap.Relocation.Step with { To = new(0, 12) };
        wrap.Complete(wrapped); Equal(new MapPoint(0, 12), wrap.Data.Unit(0).Position);
    }
    private static void MovementDispatcherGuardAndFailure()
    {
        var f = new DispatcherFixture(); f.Data.Unit(0).Flags.Automated = true; f.Data.Unit(0).AuxiliaryTimer = 47;
        using var op = f.Begin(); Equal(true, op.Resume() is MovementDecision.Finish { Decision: MovementCompletionDecision.LongMove });
        Equal(true, f.World.UnitMovementActive); Equal(new MapPoint(20, 12), f.Data.Unit(0).Position);
        using var nested = f.Begin(); f.CompleteBusy(nested);
        Equal(true, f.World.UnitMovementActive); Equal(1, f.Blinks);
        op.Dispose(); Equal(false, f.World.UnitMovementActive); Equal((byte)3, f.Data.Unit(0).MovesUsed);
        Throws<ObjectDisposedException>(() => op.Resume());
        foreach (int variant in new[] { 0, 1, 2 })
        {
            f = new(); if (variant == 1) f.Data.Stacks.Relocate(0, new(-2, 2), false);
            if (variant == 2) f.Data.Stacks.Relocate(0, new(2, -2), false);
            using var invalid = f.Dispatcher.Begin(variant == 0 ? null : new UnitSlot(0), 1, new(true, true), f.Inputs, "invalid");
            f.Complete(invalid); Equal(0, invalid.Result); Equal(0, f.Blinks); Equal(false, f.Data.Unit(0).Flags.Moved);
        }
        f = new() { FailOffMap = true }; f.Data.Stacks.Relocate(0, new(38, 12), false); f.Data.Unit(0).Flags.Automated = true;
        using var failed = f.Begin(); Throws<InvalidOperationException>(() => failed.Resume());
        Equal(false, f.World.UnitMovementActive); Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(1, f.OffMap);
        Throws<InvalidOperationException>(() => failed.Resume());
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp; using var remote = f.Begin();
        Throws<InvalidOperationException>(() => remote.Resume()); Equal(0, f.Blinks); Equal(false, f.World.UnitMovementActive);
    }
    private static void CompleteBusy(this DispatcherFixture fixture, MovementDispatcher.Operation op)
    { Equal(null, op.Resume()); Equal(0, op.Result); Equal(true, op.Completed); }
    private static void MovementDispatcherNegativeAndCleanup()
    {
        var f = new DispatcherFixture();
        using var negative = f.Begin(-1); f.Complete(negative); Equal(1, negative.Result);
        Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal((byte)255, f.Data.Unit(0).LastDirection);
        Equal(false, f.Data.Unit(0).Flags.Moved); Equal(0, f.Relocation.Publications.Count); Equal(1, f.Finish.OldPositions.Count);
        f = new(); f.World.Multiplayer.LocalHuman = 2;
        using var unpublished = f.Begin(-1); f.Complete(unpublished); Equal(1, unpublished.Result); Equal(0, f.Finish.Pauses.Count);
        Equal((byte)6, f.Data.Unit(0).MovesUsed); Equal(1, f.Finish.OldPositions.Count); Equal(false, f.World.UnitMovementActive);
        f = new(); f.Data.Stacks.Relocate(0, new(38, 12), false); f.Data.Unit(0).Flags.Automated = true;
        using var off = f.Begin(); f.Complete(off); Equal(0, off.Result); Equal(1, f.OffMap); Equal((byte)6, f.Data.Unit(0).MovesUsed);
        f = new(); f.World.Globals.HumanPlayers = default; f.Data.Unit(0).Flags.Garrison = true; f.Data.Unit(0).MovesUsed = 6;
        using var exhausted = f.Begin(); f.Complete(exhausted); Equal(0, exhausted.Result);
        Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders); Equal((byte)1, f.Data.Unit(0).AuxiliaryTimer);
    }
    // U02 supported absent-owner repair, joined to actual charge, relocation and ordinary arrival.
    private static void MovementDispatcherUnownedLanding()
    {
        var f = new DispatcherFixture(); var w = f.World;
        w.Multiplayer.LocalHuman = 2; w.Globals.HumanPlayers = CivilizationSet.Only(2);
        w.Map.TileAt(new(18, 12)).Terrain = Terrain.Ocean;
        w.Map.TileAt(new(20, 12)).Owner = null;
        int nearby = w.Entities.Cities.End;
        f.Data.AddCity(nearby, 2, new(18, 10));
        f.Data.Unit(0).Visibility = CivilizationSet.Only(2);
        int region = w.Map.TileAt(new(20, 12)).Region;
        w.Civilizations[1].ContinentMilitary[region] = 0;
        using var operation = f.Begin(); f.Complete(operation);
        Equal(1, operation.Result); Equal(new MapPoint(20, 12), f.Data.Unit(0).Position);
        Equal((byte)3, f.Data.Unit(0).MovesUsed); Equal((ushort)2, w.Civilizations[1].ContinentMilitary[region]);
        Equal(1, f.Relocation.Notices.Count); Equal(2, f.Relocation.Notices[0].Recipient);
        Equal((ushort)nearby, f.Relocation.Notices[0].City.Slot.Value); Equal(2, f.Relocation.Publications.Count);
        Equal(false, w.UnitMovementActive);
    }
    private static void MovementDispatcherLandfallAndBoarding()
    {
        foreach (int answer in new[] { 0, 1 })
        {
            var f = new DispatcherFixture(); f.Data.Unit(0).Type = new(1);
            f.World.Map.TileAt(new(18, 12)).Terrain = Terrain.Ocean;
            f.Data.AddUnit(2, 1, 4, new(18, 12)); f.Data.Unit(2).Orders = UnitOrder.Sleeping; f.Data.Unit(2).Carrier = new(0);
            using var op = f.Begin(); var pending = op.Resume(); Equal(true, pending is MovementDecision.Landfall);
            Equal(pending, op.Resume()); Equal(true, f.World.UnitMovementActive);
            op.Resume(new(answer)); f.Complete(op); Equal(answer, op.Result);
            Equal(new MapPoint(answer == 1 ? 20 : 18, 12), f.Data.Unit(2).Position); Equal(new MapPoint(18, 12), f.Data.Unit(0).Position);
            Equal(answer == 1 ? 2 : 1, f.Blinks); Equal(answer == 1 ? 3 : 0, op.Movement!.UnitBound);
        }
        foreach (byte capacity in new byte[] { 0, 1, 255 })
        {
            var f = new DispatcherFixture(); f.World.Map.TileAt(new(20, 12)).Terrain = Terrain.Ocean;
            f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = capacity };
            f.Data.AddUnit(2, 1, 1, new(20, 12));
            using var op = f.Begin(); f.Complete(op); Equal(capacity == 1 ? 1 : 0, op.Result);
            Equal(new MapPoint(capacity == 1 ? 20 : 18, 12), f.Data.Unit(0).Position);
            if (capacity == 1) { Equal(UnitOrder.Sleeping, f.Data.Unit(0).Orders); Equal((UnitSlot?)new UnitSlot(2), f.World.Globals.ActiveUnit); Equal((byte)6, f.Data.Unit(0).MovesUsed); }
        }
    }
    private static void MovementDispatcherMissionsAndCapture()
    {
        var f = new DispatcherFixture(); f.Data.Stacks.Relocate(0, new(10, 8), false); f.Data.Unit(0).Type = new(47);
        f.Data.Stacks.Relocate(1, new(30, 20), false);
        using var mission = f.Begin(); Equal(true, mission.Resume() is MovementDecision.Land { Decision: LandEntryDecision.City { Decision: CityMissionDecision.Menu } });
        mission.Resume(new(-1)); f.Complete(mission); Equal(0, mission.Result); Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(true, f.Data.Unit(0).Flags.Moved);
        f = new(); f.Data.Stacks.Relocate(0, new(10, 8), false); f.Data.Unit(0).Type = new(47); f.Inputs = f.Inputs with { EntrySelection = null };
        using var unresolved = f.Begin(); Throws<InvalidOperationException>(() => unresolved.Resume());
        Equal(true, f.Data.Unit(0).Flags.Moved); Equal(false, f.World.UnitMovementActive); Equal(0, f.Land.City.Reads);
        var parity = CompletionCaptureParity() with { Key = CompletionCaptureParity().Key with { Call = "move/0/finish/capture" } };
        f = new([parity]); f.Data.Stacks.Relocate(0, new(10, 8), false); f.Data.Stacks.Relocate(1, new(30, 20), false); f.Data.Unit(0).Damage = 3;
        using var capture = f.Begin(); Equal(true, capture.Resume() is MovementDecision.Finish { Decision: MovementCompletionDecision.Capture { Decision: CityCaptureDecision.Window } });
        Equal(new CivilizationSlot(1), f.Capture.City.Owner); Equal((byte)0, f.Data.Unit(0).Damage); Equal(true, f.World.UnitMovementActive);
        capture.Resume(new(0)); f.Complete(capture); Equal(1, capture.Result); Equal(new MapPoint(12, 8), f.Data.Unit(0).Position);
    }
    private static void MovementDispatcherCombatAndForeignCity()
    {
        var rolls = ForeignWin().Select(c => c with { Key = c.Key with { Call = c.Key.Call.Replace("foreign/", "move/0/foreign-unit/") } }).ToList();
        var f = new DispatcherFixture(rolls); f.Data.Stacks.Relocate(0, new(10, 8), false); f.Inputs = f.Inputs with { RoundBudget = 1 };
        using var combat = f.Begin(); f.Complete(combat); Equal(true, combat.Movement!.AttackDone); Equal((byte)3, f.Data.Unit(0).MovesUsed);
        Equal(0, f.Relocation.Publications.Count); Equal(1, f.Finish.OldPositions.Count); Equal((byte)1, f.Data.Unit(0).LastDirection);
        f = new(); f.Data.Stacks.Relocate(0, new(10, 8), false); f.Data.Stacks.Relocate(1, new(30, 20), false); f.Data.Unit(0).Type = new(2);
        f.Data.Rules.Units[2] = f.Data.Rules.Units[2] with { Range = 1 };
        using var city = f.Begin(); f.Complete(city); Equal(0, city.Result); Equal(1, f.City.Denials.Count); Equal((byte)0, f.Data.Unit(0).MovesUsed);
    }
    private static void MovementSpareHoldsAggregate()
    {
        var f = new RevelationFixture(); var point = new MapPoint(10, 8);
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 3 };
        f.AddUnit(0, 1, 1, point); f.AddUnit(1, 1, 0, point); f.AddUnit(2, 1, 2, point);
        Equal(2, f.Stacks.SpareTransportCapacity(0, f.Rules));
        f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 255 }; Equal(-2, f.Stacks.SpareTransportCapacity(0, f.Rules));
        f = new(); f.Rules.Units[1] = f.Rules.Units[1] with { Capacity = 127 };
        f.AddUnit(0, 1, 0, point); for (int i = 1; i <= 18; ++i) f.AddUnit(i, 1, 1, point);
        Equal(2159, f.Stacks.SpareTransportCapacity(0, f.Rules)); Equal(0, f.Stacks.SpareTransportCapacity(-1, f.Rules));
    }
    private static void MovementDispatcherPrivateSession()
    {
        foreach (int variant in new[] { 0, 1, 2 })
        {
            using var runtime = new SessionRuntime(new(), new(253));
            var parity = CompletionCaptureParity() with { Key = CompletionCaptureParity().Key with { Call = "move/0/finish/capture" } };
            var f = new DispatcherFixture(variant == 0 ? [parity] : [], CreationFixture(new(runtime.Process, runtime.World)));
            if (variant == 0)
            {
                f.Data.Stacks.Relocate(0, new(10, 8), false); f.Data.Stacks.Relocate(1, new(30, 20), false);
                f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).AuxiliaryTimer = 47; f.Data.Unit(0).LastDirection = 0;
            }
            if (variant == 1)
            {
                f.Data.Unit(0).Type = new(1); f.World.Map.TileAt(new(18, 12)).Terrain = Terrain.Ocean;
                f.Data.AddUnit(2, 1, 4, new(18, 12)); f.Data.Unit(2).Orders = UnitOrder.Sleeping; f.Data.Unit(2).Carrier = new(0);
            }
            if (variant == 2) { f.Data.Stacks.Relocate(0, new(10, 8), false); f.Data.Unit(0).MovesUsed = 5; }
            ParticipantId actorSeat = default, observerSeat = default; int windows = 0;
            runtime.RegisterCommand(new(new(1, "test.move", "Authored movement dispatch", AccessClass.Player, new([]), new([new("result")])), (_, _, _) => new(),
                _ => new MovementOperation(f.Begin(), c => c == 1 ? actorSeat : observerSeat,
                    _ => throw new InvalidOperationException("No investigation"), _ => throw new InvalidOperationException("No intelligence"),
                    visit => { ++windows; return new("Captured city", observerSeat, new CommandValue.City(visit.City), new([0])); },
                    _ => throw new InvalidOperationException("No hut city"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var observer = SessionClient(runtime, 2);
            actorSeat = actor.Identity.Participant; observerSeat = observer.Identity.Participant;
            var id = actor.Submit(new("test.move", new([]))).Correlation.Command; runtime.AdvanceOne();
            var question = RequiredInteraction(actor, id); Equal(false, observer.Reply(question, 0).Accepted); Equal(true, f.World.UnitMovementActive);
            Equal(true, actor.Reply(question, variant == 0 ? 0 : 1).Accepted); runtime.AdvanceOne();
            if (variant == 0)
            {
                question = RequiredInteraction(actor, id); Equal(1, windows); Equal((byte)0, f.Data.Unit(0).LastDirection);
                Equal(false, observer.Reply(question, 1).Accepted); Equal(true, actor.Reply(question, 1).Accepted); runtime.AdvanceOne();
                Equal(UnitOrder.None, f.Data.Unit(0).Orders);
            }
            for (int i = 0; i < 20 && RequiredResult(actor, id).Status == CommandStatus.Pending; ++i)
            { Equal(false, runtime.ContinueCommand(id)); Equal(true, runtime.AdvanceOne()); }
            Equal(CommandStatus.Completed, RequiredResult(actor, id).Status); Equal(false, f.World.UnitMovementActive);
            Equal(new CommandValue.Integer(variant == 2 ? 0 : 1), RequiredResult(actor, id).Result.Single(v => v.Name == "result").Value);
            if (variant == 1) Equal(new MapPoint(20, 12), f.Data.Unit(2).Position);
            if (variant == 2) Equal((byte)5, f.Data.Unit(0).MovesUsed);
            f.Capture.Life.Random.RequireChoicesConsumed();
        }
    }
}
