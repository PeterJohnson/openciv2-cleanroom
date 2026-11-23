using OpenCiv2.Base;
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
    // U02 Foreign city: authored admission and actual U07/U01/V01/V06 compositions.
    private sealed class ForeignCityFixture : IForeignCityEffects
    {
        public ForeignFixture F { get; }
        public RevelationFixture Data => F.Data;
        public GameState World => F.World;
        public ForeignCityMovement City { get; }
        public List<OccupationDenied> Denials { get; } = [];
        public List<AttackNotice> AttackNotices { get; } = [];
        public List<MapPoint> Redraws { get; } = [];
        public bool FailNotice, FailRedraw;
        public ForeignCityFixture(RevelationFixture? data = null)
        {
            F = new(data: data);
            var life = F.E.F.Life;
            City = new(World, Data.Rules, new(World, Data.Rules, AttackNotices.Add),
                new(World, life.Random, Data.Stacks, life.Loss, life.Elimination, F.E.F.Incidents.Incidents, F.E.F),
                life.Elimination, life.Loss, F.E.F.Incidents.Incidents, this);
        }
        public void CannotOccupy(OccupationDenied notice) { Denials.Add(notice); if (FailNotice) throw new InvalidOperationException("Unavailable occupation notice"); }
        public void Redraw(MapPoint point) { Redraws.Add(point); if (FailRedraw) throw new InvalidOperationException("Unavailable origin redraw"); }
        public ForeignCityMovement.Operation Begin() => City.Begin(new(0), F.Movement, F.Step, "city");
        public void Complete(ForeignCityMovement.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); F.E.F.Life.Random.RequireChoicesConsumed();
        }
        public void Nuclear(bool intercept, bool cargo = false, bool settler = false)
        {
            F.E.F.Missile(settler); F.E.F.Ocean();
            Data.Rules.Units[20] = Data.Rules.Units[20] with { Domain = cargo ? RuleDomain.Sea : RuleDomain.Air, Range = 1, Capacity = 2 };
            F.Step = F.Step with { Domain = cargo ? RuleDomain.Sea : RuleDomain.Air, DestinationTerrain = Terrain.Ocean };
            World.Civilizations[1].AggressionCounter[2] = 13; World.Civilizations[2].AggressionCounter[1] = 17;
            BuildingQueries.SetBuilding(World, new(0), BuildingIds.SdiDefense, intercept);
            if (cargo)
            {
                Data.AddUnit(2, 1, 9, F.Step.From); Data.Unit(2).Orders = UnitOrder.Sleeping;
                new UnitCargo(World, Data.Rules, Data.Stacks).Gather(0, false, false);
                Equal(true, Data.Unit(2).Flags.Aboard);
            }
        }
        public void LastSettler()
        {
            Nuclear(true, settler: true); World.Civilizations[1].CityCount = 0;
            Data.AddUnit(2, 3, 4, new(24, 10)); Data.Unit(2).Flags.SpyMission = true; Data.Unit(2).Counter = 1;
            World.Civilizations[1].Treaties[3].Peace = true;
        }
    }

    private static void ForeignCityDomainAndSignedAttack()
    {
        foreach (RuleDomain captured in new[] { RuleDomain.Land, RuleDomain.Sea, RuleDomain.Air })
            foreach (byte range in new byte[] { 0, 1, 255 })
            {
                var f = new ForeignCityFixture(); f.F.Step = f.F.Step with { Domain = captured };
                // The dispatcher uses its captured domain and the unit's live range.
                f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Sea, Range = range };
                using var op = f.Begin(); f.Complete(op); bool ground = captured == RuleDomain.Land || captured == RuleDomain.Air && range == 0;
                Equal(ground, op.Result!.Proceed); Equal(0, op.Result.Value); Equal(ground ? 0 : 1, f.Denials.Count);
                Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(f.F.Step.From, f.Data.Unit(0).Position);
            }
        foreach (byte attack in new byte[] { 98, 99, 127, 128, 255 })
        {
            var f = new ForeignCityFixture(); f.F.Step = f.F.Step with { Domain = RuleDomain.Sea }; f.F.Movement.Options = new(true, false);
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = attack };
            using var op = f.Begin(); f.Complete(op); bool nuclear = attack is 99 or 127;
            Equal(false, op.Result!.Proceed); Equal(nuclear ? 1 : 0, op.Result.Value); Equal(nuclear ? 0 : 1, f.Denials.Count);
            Equal(true, f.Data.Unit(0).Token.Occupied); Equal(0, f.F.E.F.Notices.Count);
        }
        foreach (int suppressed in new[] { 0, 1, 2 })
        {
            var f = new ForeignCityFixture(); f.F.Step = f.F.Step with { Domain = RuleDomain.Sea };
            if (suppressed == 0) f.F.Movement.Options = new(false, true);
            if (suppressed == 1) f.F.Movement.LocalHuman = false;
            if (suppressed == 2) f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            using var op = f.Begin(); f.Complete(op); Equal(0, f.Denials.Count);
        }
    }

    private static void ForeignCityTreatiesAndPermission()
    {
        foreach (int relation in new[] { 0, 1, 2, 3 })
            foreach (bool human in new[] { false, true })
            {
                var f = new ForeignCityFixture(); ref var treaty = ref f.World.Civilizations[1].Treaties[2];
                treaty.Ceasefire = relation == 1; treaty.Peace = relation == 2; treaty.Alliance = relation == 3;
                if (!human) f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
                f.Data.Unit(0).Orders = UnitOrder.GoTo;
                using var op = f.Begin(); var pending = op.Resume();
                if (human && relation is 1 or 2)
                {
                    Equal(true, pending is ForeignCityDecision.Attack); Equal(pending, op.Resume());
                    Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(null, op.Resume(1));
                }
                else Equal(null, pending);
                Equal(true, op.Completed); Equal(relation == 0 || human && relation != 3, op.Result!.Proceed);
                Equal(!human && relation != 0 ? UnitOrder.None : UnitOrder.GoTo, f.Data.Unit(0).Orders);
                Equal(false, treaty.War); Equal(relation == 2, treaty.Peace); Equal((byte)0, f.Data.Unit(0).MovesUsed);
                Equal(human && relation == 3 ? 1 : 0, f.AttackNotices.Count);
                f.F.E.F.Life.Random.RequireChoicesConsumed();
            }
        foreach (bool local in new[] { false, true })
        {
            var f = new ForeignCityFixture(); f.World.Civilizations[1].Treaties[2].Alliance = true;
            f.F.Movement.LocalHuman = local; f.F.Movement.Options = new(!local, true);
            using var op = f.Begin(); f.Complete(op); Equal(true, op.Result!.Proceed); Equal(0, f.AttackNotices.Count);
        }
        var changed = new ForeignCityFixture(); changed.World.Entities.Cities.Storage[0].Owner = new(3);
        changed.World.Civilizations[1].Treaties[3].Peace = true;
        using var city = changed.Begin(); var request = (ForeignCityDecision.Attack)city.Resume()!;
        Equal(3, request.Warning.Victim.Civilization); city.Resume(0); Equal(false, city.Result!.Proceed);
        var senate = new ForeignCityFixture(); senate.World.Civilizations[1].Treaties[2].Peace = true;
        senate.World.Civilizations[1].Government = Government.Democracy;
        using var blocked = senate.Begin(); blocked.Resume(); blocked.Resume(1);
        Equal(false, blocked.Result!.Proceed); Equal(AttackNoticeKind.Overruled, senate.AttackNotices.Single().Kind);
    }

    private static void ForeignCityNuclearDelivery()
    {
        foreach (bool intercept in new[] { false, true })
        {
            var f = new ForeignCityFixture(); f.Nuclear(intercept);
            using var op = f.Begin(); f.Complete(op); Equal(false, op.Result!.Proceed); Equal(0, op.Result.Value);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(intercept, f.Data.Unit(1).Token.Occupied);
            Equal(intercept ? (sbyte)5 : (sbyte)3, f.World.Entities.Cities.Storage[0].Size);
            Equal(intercept ? (byte)13 : (byte)0, f.World.Civilizations[1].AggressionCounter[2]);
            Equal((byte)17, f.World.Civilizations[2].AggressionCounter[1]);
            Equal(intercept ? 1 : 0, f.Redraws.Count); Equal(new UnitSlot(0), f.F.Movement.Unit);
            Equal(null, op.Resume(0)); Equal(intercept ? (sbyte)5 : (sbyte)3, f.World.Entities.Cities.Storage[0].Size);
        }
        var cargo = new ForeignCityFixture { FailRedraw = true }; cargo.Nuclear(true, cargo: true);
        using var destroyed = cargo.Begin(); cargo.Complete(destroyed);
        Equal(false, cargo.Data.Unit(0).Token.Occupied || cargo.Data.Unit(2).Token.Occupied); Equal(true, cargo.Data.Unit(1).Token.Occupied);
        Equal(cargo.F.Step.From, cargo.Redraws.Single()); Equal(1, destroyed.PresentationFailures.Count);
    }

    private static void ForeignCityFailuresAndNestedLoss()
    {
        var f = new ForeignCityFixture(); f.LastSettler();
        using var op = f.Begin(); Equal(true, op.Resume() is ForeignCityDecision.Incident);
        Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(2).Token.Occupied); Equal(0, f.Redraws.Count);
        Equal(true, op.Resume() is ForeignCityDecision.Incident); op.Resume(0); f.Complete(op);
        Equal(EndReason.Defeat, f.World.PendingEndReason); Equal(f.F.Step.From, f.Redraws.Single());
        var notice = new ForeignCityFixture { FailNotice = true }; notice.F.Step = notice.F.Step with { Domain = RuleDomain.Sea };
        using var failed = notice.Begin(); Throws<InvalidOperationException>(() => failed.Resume()); Throws<InvalidOperationException>(() => failed.Resume());
        Equal(false, failed.Completed); Equal(true, notice.Data.Unit(0).Token.Occupied); Equal(1, notice.Denials.Count);
        var nuclear = new ForeignCityFixture(); nuclear.Nuclear(true); nuclear.F.E.F.FailNuclearNotice = true;
        using var interrupted = nuclear.Begin(); Throws<InvalidOperationException>(() => interrupted.Resume()); Equal(true, nuclear.Data.Unit(0).Token.Occupied);
        var remote = new ForeignCityFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Begin(); Throws<InvalidOperationException>(() => denied.Resume()); Equal(0, remote.Denials.Count);
        var disposed = new ForeignCityFixture(); disposed.World.Civilizations[1].Treaties[2].Peace = true;
        var stopped = disposed.Begin(); stopped.Resume(); stopped.Dispose(); Throws<ObjectDisposedException>(() => stopped.Resume(1)); Equal((byte)0, disposed.Data.Unit(0).MovesUsed);
        var missing = new ForeignCityFixture(); missing.F.Step = missing.F.Step with { City = null }; Throws<ArgumentException>(() => missing.Begin());
    }

    private static void ForeignCityPrivateSession()
    {
        foreach (bool nuclear in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(245));
            var f = new ForeignCityFixture(CreationFixture(new(runtime.Process, runtime.World)));
            if (nuclear) f.LastSettler(); else f.World.Civilizations[1].Treaties[2].Peace = true;
            ParticipantId recipient = default;
            runtime.RegisterCommand(new(new(1, "test.foreign-city", "Authored foreign-city admission", AccessClass.Player, new([]),
                new([new("result"), new("proceed", ValueKind.Boolean)])), (_, _, _) => new(),
                _ => new ForeignCityOperation(f.Begin(), _ => recipient)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
            var command = actor.Submit(new("test.foreign-city", new([]))).Correlation.Command; runtime.AdvanceOne();
            var question = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(!nuclear, f.Data.Unit(0).Token.Occupied); Equal(0, f.Redraws.Count);
            Equal(false, actor.Reply(question, 0).Accepted); Equal(true, human.Reply(question, nuclear ? 0 : 1).Accepted); runtime.AdvanceOne();
            var result = RequiredResult(actor, command); Equal(CommandStatus.Completed, result.Status);
            Equal(new CommandValue.Boolean(!nuclear), result.Result.Single(v => v.Name == "proceed").Value);
            Equal(nuclear ? 1 : 0, f.Redraws.Count); Equal(false, f.World.Civilizations[1].Treaties[2].War);
            f.F.E.F.Life.Random.RequireChoicesConsumed();
        }
    }
}
