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
    // Authored U02 Foreign unit / Expulsion vectors, joined to actual U01 and U06 services.
    private sealed class ForeignFixture : IUnitExpulsionEffects, IForeignUnitEffects
    {
        public ExecutionFixture E { get; }
        public RevelationFixture Data => E.F.Data;
        public GameState World => Data.World;
        public UnitExpulsion Expulsion { get; }
        public ForeignUnitMovement Foreign { get; }
        public MovementContinuation Movement { get; }
        public MovementStep Step { get; set; }
        public List<ExpulsionNotice> ExpulsionNotices { get; } = [];
        public List<ForeignUnitNotice> Notices { get; } = [];
        public List<MapPoint> Redraws { get; } = [];
        public List<MapPoint> Lifted { get; } = [];
        public bool FailEntry, FailNotice, FailRedraw;
        public ForeignFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            E = new(choices, data);
            Movement = new(new(0), World.Entities.Units.End, Data.Unit(0).Position) { Options = new(true, true), LocalHuman = true };
            Step = new(1, RuleDomain.Land, Data.Unit(0).Position, Data.Unit(1).Position, Terrain.Grassland, 1, new(1), 2, new(0));
            Expulsion = new(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), E.F.Life.Random, this);
            Foreign = new(World, Data.Rules, Data.Stacks, Expulsion, E.Combat, this);
        }
        public void Diplomat()
        {
            Data.Rules.Units[4] = Data.Rules.Units[4] with { Role = RuleRole.Diplomacy };
            World.Civilizations[1].Treaties[2].Peace = true;
            World.Entities.Cities.Storage[0].Owner = new(1);
            World.Map.TileAt(Step.To).Owner = 1;
            World.Entities.Cities.Storage[1].Name = "Return city";
            Data.Unit(1).Visibility = CivilizationSet.Only(1).With(3);
        }
        public UnitExpulsion.Operation Expel(int? incoming = null) => Expulsion.Begin(new(0), new(1), Movement, Step, 2, "retained city", "Diplomat", incoming, "expel");
        public ForeignUnitMovement.Operation Begin(bool ocean = false, int? incoming = null) =>
            Foreign.Begin(new(0), new(1), Movement, Step, ocean, "retained city", "Diplomat", incoming, "", "foreign", roundBudget: 1);
        public void Complete(ForeignUnitMovement.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); E.F.Life.Random.RequireChoicesConsumed();
        }
        public void Notice(ExpulsionNotice notice) { ExpulsionNotices.Add(notice); if (FailNotice) throw new InvalidOperationException("Unavailable notice"); }
        public void Notice(ForeignUnitNotice notice) { Notices.Add(notice); if (FailNotice) throw new InvalidOperationException("Unavailable notice"); }
        public void Enter(ExpulsionEntry entry)
        {
            Equal(Step.From, entry.Origin); Equal(Step.To, entry.Destination);
            for (int i = 0; i < World.Entities.Units.End; ++i) Lifted.Add(Data.Unit(i).Position);
            if (FailEntry) throw new InvalidOperationException("Unavailable animation");
        }
        public void Redraw(MapPoint point) { Redraws.Add(point); if (FailRedraw) throw new InvalidOperationException("Unavailable redraw"); }
    }
    private static Choice ExpulsionRoll(int code = 3) => Choice.Create(new("U02.expulsion-attitude", "expel"), new SamplingLaw.PrimaryModulo(4), ChoiceCode.Primary(code));
    private static Choice ForeignRoll(string role, int strength, int code) =>
        Choice.Create(new("U06.round." + role, "foreign/combat/rounds", 0), new SamplingLaw.PrimaryModulo(strength), ChoiceCode.Primary(code));
    private static Choice ForeignPromotion(int strength = 84) => Choice.Create(new("U06.promotion", "foreign/combat/resolution/promotion"), new SamplingLaw.PrimaryModulo(strength), ChoiceCode.Primary(0));
    private static List<Choice> ForeignWin() => [ForeignRoll("attack", 80, 79), ForeignRoll("defense", 4, 0), ForeignPromotion()];

    private static void ExpulsionAdmissionAndChoices()
    {
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            var f = new ForeignFixture(); f.Diplomat();
            if (mode == 0) f.World.Civilizations[1].Treaties[2].Peace = false;
            if (mode == 1) f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Attack };
            if (mode == 2) f.Data.AddUnit(2, 2, 4, f.Step.To);
            if (mode == 3) f.World.Entities.Cities.Storage[0].Owner = new(2);
            using var op = f.Expel(); Equal(null, op.Resume()); Equal(true, op.Completed); Equal(false, op.Stopped);
            Equal(mode == 3 ? 1 : 0, f.ExpulsionNotices.Count); Equal(0, f.Lifted.Count);
            if (mode == 3) Equal(ExpulsionNoticeKind.CannotExpel, f.ExpulsionNotices[0].Kind);
        }
        foreach (int answer in new[] { 0, 1, 2, -1 })
        {
            var f = new ForeignFixture(); f.Diplomat(); using var op = f.Expel(99);
            var pending = op.Resume(0); Equal(true, pending is not null); Equal(pending, op.Resume()); Equal(99, op.Choice);
            Equal(null, op.Resume(answer)); Equal(true, op.Completed); Equal(answer, f.Movement.Answer);
            Equal(answer == 0 || answer == 2, op.Stopped); Equal(answer == 0, op.Expelled);
            Equal(answer == 0 ? f.World.Entities.Cities.Storage[1].Position : f.Step.To, f.Data.Unit(1).Position);
            Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(null, op.Resume(0));
            f.E.F.Life.Random.RequireChoicesConsumed();
        }
        var tied = new ForeignFixture(); tied.Diplomat(); tied.Data.AddCity(2, 2, tied.Step.To);
        using var tie = tied.Expel(); Equal(null, tie.Resume()); Equal(ExpulsionNoticeKind.CannotExpel, tied.ExpulsionNotices.Single().Kind);
        var empty = new ForeignFixture(); empty.Diplomat(); empty.World.Entities.Cities.SetEnd(0);
        using var missing = empty.Expel(); Equal(null, missing.Resume()); Equal(ExpulsionNoticeKind.CannotExpel, empty.ExpulsionNotices.Single().Kind);
    }

    private static void ExpulsionDestinationAndLiveRole()
    {
        foreach (bool palace in new[] { false, true })
        {
            var f = new ForeignFixture(); f.Diplomat(); f.Data.AddCity(2, 2, new(30, 18), 4);
            f.World.Entities.Cities.Storage[1].Size = 4;
            BuildingQueries.SetBuilding(f.World, new(2), BuildingIds.Palace, palace);
            using var op = f.Expel(); op.Resume(); op.Resume(0);
            Equal(f.World.Entities.Cities.Storage[palace ? 2 : 1].Position, f.Data.Unit(1).Position);
            Equal(default(CivilizationSet), f.Data.Unit(1).Visibility); Equal(f.Step.From, f.Data.Unit(0).Position);
            Equal(ExpulsionNoticeKind.VictimReturned, f.ExpulsionNotices.Single().Kind);
        }
        var negative = new ForeignFixture(); negative.Diplomat(); negative.World.Entities.Cities.Storage[1].Size = -1;
        using var noCity = negative.Expel(); noCity.Resume(); noCity.Resume(0);
        Equal(negative.Step.To, negative.Data.Unit(1).Position); Equal(true, negative.Data.Unit(1).Visibility.Contains(3));
        Equal("retained city", negative.ExpulsionNotices.Single().CityName); Equal(true, noCity.Expelled);
        foreach (bool captured in new[] { false, true })
        {
            var f = new ForeignFixture(); f.Diplomat(); f.Data.AddCity(2, 2, new(14, 8), 50);
            // Retained player decision separates the initial and later live role reads.
            if (!captured) f.Step = f.Step with { City = null };
            using var op = f.Expel(); Equal(true, op.Resume() is not null);
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Role = RuleRole.Attack };
            if (captured) { op.Resume(0); Equal(f.World.Entities.Cities.Storage[1].Position, f.Data.Unit(1).Position); }
            else { Throws<InvalidOperationException>(() => op.Resume(0)); Equal(f.Step.To, f.Data.Unit(1).Position); Throws<InvalidOperationException>(() => op.Resume()); }
            Equal(f.Step.From, f.Data.Unit(0).Position);
        }
    }

    private static void ExpulsionAutoplayAndNotifications()
    {
        var missing = new ForeignFixture(); missing.Diplomat(); missing.World.Multiplayer.Saved.AutoplayActive = true;
        using var unknown = missing.Expel(); Throws<InvalidOperationException>(() => unknown.Resume());
        Equal(0, missing.Lifted.Count); Equal(false, unknown.Completed);
        foreach (int answer in new[] { 0, 1, 2 })
        {
            var f = new ForeignFixture(answer == 0 ? [ExpulsionRoll()] : []); f.Diplomat(); f.World.Multiplayer.Saved.AutoplayActive = true;
            using var op = f.Expel(answer); Equal(null, op.Resume()); Equal(answer == 0, op.Expelled);
            Equal(answer != 1, op.Stopped); Equal(0, f.ExpulsionNotices.Count);
            if (answer == 0) Equal((byte)3, f.World.Civilizations[2].Attitude[1]);
            f.E.F.Life.Random.RequireChoicesConsumed();
        }
        foreach (int mode in new[] { 0, 1, 2 })
        {
            var f = new ForeignFixture(mode == 1 ? [ExpulsionRoll()] : []); f.Diplomat();
            f.World.Globals.HumanPlayers = mode == 2 ? default : CivilizationSet.Only(1);
            f.World.Civilizations[1].Treaties[2].Alliance = mode == 0;
            using var op = f.Expel(); var prompt = op.Resume(); if (prompt is not null) op.Resume(0);
            Equal(true, op.Expelled); Equal(mode == 2 ? 0 : 1, f.ExpulsionNotices.Count);
            if (mode < 2) Equal(mode == 0 ? ExpulsionNoticeKind.AlliedReturn : ExpulsionNoticeKind.Expelled, f.ExpulsionNotices[0].Kind);
            Equal(mode == 2 ? 0 : 2, f.Lifted.Count); f.E.F.Life.Random.RequireChoicesConsumed();
        }
    }

    private static void ExpulsionRaceAndFailurePrefix()
    {
        foreach (bool hostile in new[] { false, true })
        {
            var f = new ForeignFixture(); f.Diplomat(); f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
            f.World.Globals.Difficulty = Difficulty.Prince; f.World.Globals.WonderCity[WonderIds.Apollo] = 0;
            f.World.Civilizations[1].SpaceshipFlags.Launched = true;
            ref var reverse = ref f.World.Civilizations[2].Treaties[1]; reverse.Peace = reverse.Ceasefire = reverse.Alliance = true; reverse.HostileIntent = hostile;
            using var op = f.Expel(); op.Resume(); op.Resume(0);
            Equal(!hostile, reverse.HostileIntent); Equal(!hostile, reverse.Peace); Equal(!hostile, reverse.Ceasefire);
            Equal(hostile, reverse.BrokenTreaty); Equal(true, reverse.Alliance); Equal(true, f.World.Civilizations[1].Treaties[2].Peace);
            f.E.F.Life.Random.RequireChoicesConsumed();
        }
        var failed = new ForeignFixture { FailNotice = true }; failed.Diplomat();
        using var notice = failed.Expel(); notice.Resume(); Throws<InvalidOperationException>(() => notice.Resume(0));
        Equal(failed.World.Entities.Cities.Storage[1].Position, failed.Data.Unit(1).Position); Equal(false, notice.Completed); Throws<InvalidOperationException>(() => notice.Resume());
        var random = new ForeignFixture(); random.Diplomat(); random.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        using var draw = random.Expel(); draw.Resume(); Throws<ChoiceException>(() => draw.Resume(0));
        Equal(ExpulsionNoticeKind.Expelled, random.ExpulsionNotices.Single().Kind); Equal(random.World.Entities.Cities.Storage[1].Position, random.Data.Unit(1).Position);
        var remote = new ForeignFixture(); remote.Diplomat(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Expel(); Throws<InvalidOperationException>(() => denied.Resume()); Equal(0, remote.Lifted.Count);
        var disposed = new ForeignFixture(); disposed.Diplomat(); var stopped = disposed.Expel(); stopped.Resume(); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume(0)); Equal(disposed.Step.To, disposed.Data.Unit(1).Position);
    }

    private static void ExpulsionCargoAndCosmeticFailures()
    {
        var f = new ForeignFixture { FailEntry = true, FailRedraw = true }; f.Diplomat();
        f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Sea, Capacity = 2 };
        f.Data.AddUnit(2, 1, 9, f.Step.From); f.Data.Unit(2).Orders = UnitOrder.Sleeping;
        using var op = f.Expel(); op.Resume(); op.Resume(0);
        Equal(true, op.Expelled); Equal(2, op.PresentationFailures.Count);
        Equal(UnitStaging.Lifted(1), f.Lifted[0]); Equal(UnitStaging.Lifted(1), f.Lifted[2]);
        Equal(f.Step.From, f.Data.Unit(0).Position); Equal(f.Step.From, f.Data.Unit(2).Position);
        Equal(new UnitSlot(0), f.Data.Unit(2).Carrier); Equal(2, f.Data.Stacks.Count(0));
        Equal(false, f.World.UnitStacks.CargoActive || f.World.UnitStacks.LiftActive);
    }

    private static void ForeignUnitAdmissionOrder()
    {
        var amphib = new ForeignFixture(); amphib.Data.Rules.Units[0] = amphib.Data.Rules.Units[0] with { Attack = 0 };
        using var ocean = amphib.Begin(true); amphib.Complete(ocean); Equal(ForeignUnitNoticeKind.RequiresAmphibious, amphib.Notices.Single().Kind);
        Equal(false, ocean.Result!.Finish); Equal(0, amphib.E.Calls.Count);
        var submarine = new ForeignFixture(); submarine.Data.Rules.Units[0] = submarine.Data.Rules.Units[0] with { Abilities = new() { Submarine = true } };
        using var ground = submarine.Begin(); submarine.Complete(ground); Equal(0, submarine.Notices.Count);
        foreach (bool prompt in new[] { false, true })
        {
            var f = new ForeignFixture { FailRedraw = true }; f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Abilities = new() { Submarine = true } };
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Movement.Options = new(prompt, true);
            using var op = f.Begin(); f.Complete(op); Equal(prompt, f.Data.Unit(1).Visibility.Contains(1));
            Equal(prompt ? UnitOrder.None : UnitOrder.GoTo, f.Data.Unit(0).Orders); Equal(prompt ? 1 : 0, op.PresentationFailures.Count);
            Equal(0, f.E.Calls.Count);
        }
        var restored = new ForeignFixture(); restored.Data.AddUnit(2, 1, 9, restored.Step.From);
        restored.Movement.Unit = new(2); restored.Data.Unit(0).Orders = restored.Data.Unit(2).Orders = UnitOrder.GoTo;
        restored.Data.Rules.Units[4] = restored.Data.Rules.Units[4] with { Abilities = new() { Submarine = true } };
        using var discovery = restored.Begin(); restored.Complete(discovery);
        Equal(new UnitSlot(0), restored.Movement.Unit); Equal(UnitOrder.None, restored.Data.Unit(0).Orders); Equal(UnitOrder.GoTo, restored.Data.Unit(2).Orders);
        var diplomat = new ForeignFixture(); diplomat.Step = diplomat.Step with { Owner = 3 }; diplomat.World.Globals.HumanPlayers = CivilizationSet.Only(3);
        diplomat.Data.Rules.Units[0] = diplomat.Data.Rules.Units[0] with { Role = RuleRole.Diplomacy, Attack = 0 }; diplomat.Data.Unit(0).Orders = UnitOrder.GoTo;
        using var bit = diplomat.Begin(); diplomat.Complete(bit); Equal(8, bit.Result!.Value); Equal(UnitOrder.None, diplomat.Data.Unit(0).Orders);
        foreach (bool automated in new[] { false, true })
        {
            var f = new ForeignFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = 0 }; f.Data.Unit(0).Flags.Automated = automated;
            using var op = f.Begin(); f.Complete(op); Equal(automated ? (byte)6 : (byte)0, f.Data.Unit(0).MovesUsed);
            Equal(ForeignUnitNoticeKind.NoncombatUnit, f.Notices.Single().Kind);
        }
    }

    private static void ForeignUnitAircraftAndFatigue()
    {
        foreach (byte range in new byte[] { 1, 2, 128, 255 })
        {
            var f = new ForeignFixture(); f.Step = f.Step with { City = null }; f.World.Map.TileAt(f.Step.To).Features = default;
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air, Range = range };
            Equal(range == 2 ? 1 : 0, f.Data.Stacks.CountLongRangeAircraft(1, f.Data.Rules));
            f.Data.Unit(0).MovesUsed = 5; f.Data.Unit(0).Flags.Automated = true;
            using var op = f.Begin(); var pending = op.Resume();
            if (range == 2) { Equal(null, pending); Equal(ForeignUnitNoticeKind.RequiresFighter, f.Notices.Single().Kind); Equal((byte)6, f.Data.Unit(0).MovesUsed); }
            else { Equal(true, pending is ForeignUnitDecision.Fatigue); op.Resume(new(1)); Equal(0, f.Notices.Count); }
        }
        foreach (int exception in new[] { 0, 1, 2, 3 })
        {
            var f = new ForeignFixture(); f.Data.Unit(0).MovesUsed = 5;
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air, Range = 2 };
            if (exception != 0) f.Step = f.Step with { City = null };
            if (exception == 1) f.World.Map.TileAt(f.Step.To).Features = MapFeatures.CitySite | MapFeatures.Fortification;
            if (exception == 2) f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Abilities = new() { AttackAir = true } };
            if (exception == 3)
            {
                f.Data.Rules.Units[9] = f.Data.Rules.Units[9] with { Abilities = new() { CarryAircraft = true } };
                f.Data.AddUnit(2, 2, 9, f.Step.To); Equal(1, f.Data.Stacks.CountAircraftCarriers(1, f.Data.Rules));
            }
            using var op = f.Begin(); var fatigue = (ForeignUnitDecision.Fatigue)op.Resume()!;
            Equal(1, fatigue.Choice.MovementLeft); Equal(3, fatigue.Choice.RoadMultiplier); Equal(fatigue, op.Resume());
            op.Resume(new(1)); Equal(true, op.Completed); Equal(0, f.E.Calls.Count); Equal((byte)5, f.Data.Unit(0).MovesUsed);
        }
        var ai = new ForeignFixture(); ai.World.Globals.HumanPlayers = CivilizationSet.Only(2); ai.Movement.LocalHuman = false; ai.Data.Unit(0).MovesUsed = 5;
        using var denied = ai.Begin(); ai.Complete(denied); Equal(0, ai.E.Calls.Count); Equal(false, denied.Result!.Finish);
        var human = new ForeignFixture(); human.World.Civilizations[1].Treaties[2].Peace = true;
        human.Movement.Options = new(false, true); human.Data.Unit(0).MovesUsed = 5;
        using var permitted = human.Begin(); Equal(true, permitted.Resume() is ForeignUnitDecision.Combat); permitted.Resume(new(0));
        Equal(true, permitted.Result!.Finish); Equal((byte)8, human.Data.Unit(0).MovesUsed);
        var threshold = new ForeignFixture([ForeignPromotion(57)]); threshold.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        threshold.Movement.LocalHuman = false; threshold.Data.Unit(0).MovesUsed = 4;
        threshold.Data.Rules.Units[0] = threshold.Data.Rules.Units[0] with { Firepower = 0 };
        threshold.Data.Rules.Units[4] = threshold.Data.Rules.Units[4] with { Firepower = 0 };
        using var admitted = threshold.Begin(); threshold.Complete(admitted);
        Equal(1, threshold.E.State.KillCount); Equal(false, threshold.Data.Unit(0).Token.Occupied);
        Equal(1, threshold.World.Casualties[1].Count);
    }

    private static void ForeignUnitCombatJoin()
    {
        foreach (bool execute in new[] { false, true })
        {
            var f = new ForeignFixture(ForeignWin()); f.Movement.Options = new(false, execute); f.Data.Unit(0).Flags.Garrison = true;
            using var op = f.Begin(); f.Complete(op);
            Equal(execute, op.Result!.Finish); Equal(execute, f.Movement.AttackDone); Equal(0, op.Result.Value);
            Equal(execute ? f.Step.From : f.Step.To, op.Result.Destination); Equal(f.Step.From, f.Data.Unit(0).Position);
            Equal(execute ? (byte)3 : (byte)0, f.Data.Unit(0).MovesUsed); Equal(false, f.Data.Unit(0).Flags.Garrison);
            Equal(false, f.Data.Unit(1).Token.Occupied); Equal(1, f.World.Casualties[2].Count);
            Equal(null, op.Resume(new(0))); Equal(1, f.World.Casualties[2].Count);
        }
        foreach (bool execute in new[] { false, true })
        {
            var f = new ForeignFixture([ForeignPromotion()]); f.Movement.Options = new(false, execute);
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Firepower = 0 }; f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Firepower = 0 };
            using var op = f.Begin(); f.Complete(op); Equal(false, op.Result!.Finish); Equal(execute ? 0 : 1, op.Result.Value);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(1, f.World.Casualties[1].Count);
        }
        var cancel = new ForeignFixture(); cancel.World.Civilizations[1].Treaties[2].Peace = true;
        using var blocked = cancel.Begin(); Equal(true, blocked.Resume() is ForeignUnitDecision.Combat); Equal((byte)3, cancel.Data.Unit(0).MovesUsed);
        blocked.Resume(new(0)); Equal(true, blocked.Result!.Finish); Equal(cancel.Step.From, blocked.Result.Destination);
        Equal(true, cancel.Data.Unit(1).Token.Occupied); Equal(false, cancel.E.Calls.Contains("opening"));
    }

    private static void ForeignUnitPrivateSession()
    {
        foreach (bool expel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(241));
            var f = new ForeignFixture(data: CreationFixture(new(runtime.Process, runtime.World)));
            if (expel) f.Diplomat(); else { f.Data.Unit(0).MovesUsed = 5; f.World.Civilizations[1].Treaties[2].Peace = true; }
            ParticipantId recipient = default;
            runtime.RegisterCommand(new(new(1, "test.foreign", "Authored foreign-unit movement", AccessClass.Player, new([]),
                new([new("result"), new("finish", ValueKind.Boolean), new("x"), new("y")])), (_, _, _) => new(),
                _ => new ForeignUnitOperation(f.Begin(), _ => recipient, _ => throw new InvalidOperationException("Unexpected report"), _ => throw new InvalidOperationException("Unexpected city"))));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
            var command = actor.Submit(new("test.foreign", new([]))).Correlation.Command; runtime.AdvanceOne();
            var question = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, actor.Reply(question, 0).Accepted); Equal(true, human.Reply(question, 0).Accepted); runtime.AdvanceOne();
            if (!expel)
            {
                question = RequiredInteraction(human, command); Equal((byte)8, f.Data.Unit(0).MovesUsed);
                Equal(true, human.Reply(question, 0).Accepted); runtime.AdvanceOne();
            }
            var result = RequiredResult(actor, command); Equal(CommandStatus.Completed, result.Status);
            Equal(new CommandValue.Boolean(!expel), result.Result.Single(v => v.Name == "finish").Value);
            Equal(expel ? (byte)0 : (byte)8, f.Data.Unit(0).MovesUsed); Equal(true, f.Data.Unit(1).Token.Occupied);
            f.E.F.Life.Random.RequireChoicesConsumed();
        }
    }
}
