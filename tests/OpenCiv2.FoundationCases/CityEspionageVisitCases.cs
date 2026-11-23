using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class VisitFixture : ICityEspionageVisitEffects
    {
        public CityDamageFixture Data { get; }
        public RevelationFixture Units => Data.Data;
        public GameState World => Data.World;
        public CityEspionageVisits Visits { get; }
        public List<CityEspionageVisitNotice> Notices { get; } = [];
        public int Sounds;
        public bool FailSound, FailNotice;
        public VisitFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Data = new(choices, data);
            Visits = new(World, Data.Lifecycle.Loss, Data.Lifecycle.Elimination, Data.Incidents.Incidents, this);
        }
        public CityEspionageVisits.Operation Embassy() => Visits.Embassy(Data.Mission, "visit");
        public CityEspionageVisits.Operation Investigate() => Visits.Investigate(Data.Mission, "visit");
        public void Sound() { ++Sounds; if (FailSound) throw new InvalidOperationException("Sound unavailable"); }
        public void Notice(CityEspionageVisitNotice notice) { Notices.Add(notice); if (FailNotice) throw new InvalidOperationException("Notice unavailable"); }
        public CityEspionageVisit Window(CityEspionageDecision? decision, CityEspionageVisitKind kind)
        { var request = ((CityEspionageDecision.Visit)decision!).Request; Equal(kind, request.Kind); Equal(1, request.Actor); Equal(2, request.Victim); return request; }
    }

    // V06 §4: directed embassy, human visits even on autoplay, byte movement and victim notices after costs.
    private static void EmbassySpyVisitAndNoticeGates()
    {
        foreach (bool autoplay in new[] { false, true })
            foreach (int local in new[] { 1, 2, 3 })
            {
                var f = new VisitFixture(); var w = f.World;
                w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); w.Multiplayer.LocalHuman = local; w.Multiplayer.Saved.AutoplayActive = autoplay;
                w.Civilizations[2].Treaties[1].Peace = true; f.Units.Unit(0).MovesUsed = 255;
                using var op = f.Embassy(); var decision = op.Resume(); f.Window(decision, CityEspionageVisitKind.Intelligence);
                Equal(true, w.Civilizations[1].Treaties[2].Embassy); Equal(false, w.Civilizations[2].Treaties[1].Embassy);
                Equal(true, w.Civilizations[2].Treaties[1].Peace); Equal((byte)255, f.Units.Unit(0).MovesUsed);
                Equal(decision, op.Resume()); Equal(0, f.Notices.Count); Equal(local == 3 ? 0 : 1, f.Sounds);
                Equal(null, op.Resume(-1)); Equal(true, op.Completed); Equal((byte)0, f.Units.Unit(0).MovesUsed);
                Equal(true, f.Units.Unit(0).Token.Occupied); Equal(false, f.Data.City.Flags.Investigated);
                Equal(autoplay ? 0 : 1, f.Notices.Count); Equal(0, f.Data.Lifecycle.SpyNotices.Count);
                if (!autoplay) { Equal("Alderfolk", f.Notices.Single().ActorPlural); Equal(new UnitType(47), f.Notices.Single().InitialType); }
                op.Resume(0); Equal((byte)0, f.Units.Unit(0).MovesUsed); f.Data.Lifecycle.Random.RequireChoicesConsumed();
            }
    }

    private static void EmbassyPlainDeletionAndCapturedIdentity()
    {
        var f = new VisitFixture(); f.Units.Unit(0).Type = new(46); f.Units.Unit(0).Home = new(0); f.Units.Unit(0).MovesUsed = 254;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        using var op = f.Embassy(); f.Window(op.Resume(), CityEspionageVisitKind.Intelligence);
        // The opened mission retains its Spy classification and original type; U01 deletes the live type.
        f.Units.Unit(0).Type = new(47); f.World.Civilizations[1].UnitsActive[47] = 1; f.World.UnitTotals[1] = 1;
        Equal(null, op.Resume(0)); Equal(false, f.Units.Unit(0).Token.Occupied); Equal((byte)254, f.Units.Unit(0).MovesUsed);
        Equal((byte)0, f.World.Civilizations[1].UnitsActive[47]); Equal(0, f.World.UnitTotals[1]);
        Equal(0, f.Data.Lifecycle.Redraws.Count); Sequence(new[] { "unit:10", "home:0" }, f.Data.Lifecycle.Order);
        Equal(new UnitType(46), f.Notices.Single().InitialType); f.Data.Lifecycle.Random.RequireChoicesConsumed();

        f = new(); using var capturedSpy = f.Embassy(); capturedSpy.Resume(); f.Units.Unit(0).Type = new(46); capturedSpy.Resume(0);
        Equal(true, f.Units.Unit(0).Token.Occupied); Equal((byte)1, f.Units.Unit(0).MovesUsed); f.Data.Lifecycle.Random.RequireChoicesConsumed();

        // Plain deletion of a custom diplomatic ship wakes, but does not destroy, its cargo.
        f = new(); f.Units.Unit(0).Type = new(46);
        f.Units.Rules.Units[46] = f.Units.Rules.Units[46] with { Domain = RuleDomain.Sea, Capacity = 1 };
        f.World.Map.TileAt(f.Units.Unit(0).Position).Terrain = Terrain.Ocean;
        f.Units.AddUnit(1, 1, 4, f.Units.Unit(0).Position); f.Units.Unit(1).Orders = UnitOrder.Sleeping; f.Units.Unit(1).Carrier = new(0);
        using var ship = f.Embassy(); ship.Resume(); ship.Resume(0);
        Equal(false, f.Units.Unit(0).Token.Occupied); Equal(true, f.Units.Unit(1).Token.Occupied); Equal(UnitOrder.None, f.Units.Unit(1).Orders);
        Equal(0, f.Data.Lifecycle.Redraws.Count); Equal(false, f.World.UnitDeletion.CargoActive); f.Data.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CityVisitsAiDestructionAndCargo()
    {
        foreach (bool embassy in new[] { false, true })
            foreach (bool sea in new[] { false, true })
            {
                var f = new VisitFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2;
                f.Units.Unit(0).MovesUsed = 17;
                if (sea)
                {
                    f.Units.Rules.Units[47] = f.Units.Rules.Units[47] with { Domain = RuleDomain.Sea, Capacity = 1 };
                    f.World.Map.TileAt(f.Units.Unit(0).Position).Terrain = Terrain.Ocean;
                    f.Units.AddUnit(1, 1, 4, f.Units.Unit(0).Position); f.Units.Unit(1).Orders = UnitOrder.Sleeping; f.Units.Unit(1).Carrier = new(0);
                }
                using var op = embassy ? f.Embassy() : f.Investigate(); Equal(null, op.Resume()); Equal(true, op.Completed);
                Equal(false, f.Units.Unit(0).Token.Occupied); if (sea) Equal(false, f.Units.Unit(1).Token.Occupied);
                Equal((byte)17, f.Units.Unit(0).MovesUsed); Equal(false, f.Data.City.Flags.Investigated);
                Equal(embassy, f.World.Civilizations[1].Treaties[2].Embassy); Equal(1, f.Data.Lifecycle.Redraws.Count);
                Equal(1, f.Sounds); Equal(1, f.Notices.Count); Equal(0, f.Data.Lifecycle.SpyNotices.Count);
                Equal(sea, f.Data.Lifecycle.Order.Contains("units")); Equal(false, f.World.UnitDeletion.DestroyActive || f.World.UnitDeletion.CargoActive);
                f.Data.Lifecycle.Random.RequireChoicesConsumed();
            }
    }

    private static void InvestigationLiveFlagAndEscape()
    {
        foreach (bool already in new[] { false, true })
        {
            var f = new VisitFixture(); f.Data.City.Flags.Investigated = already; f.Units.Unit(0).MovesUsed = 255;
            f.Data.City.FoodBox = 41; f.Data.City.ShieldBox = 29; var memory = f.Data.City.KnownTo;
            using var op = f.Investigate(); f.Window(op.Resume(), CityEspionageVisitKind.City); op.Resume(0);
            Equal(already ? (byte)255 : (byte)0, f.Units.Unit(0).MovesUsed); Equal(true, f.Data.City.Flags.Investigated);
            Equal(memory, f.Data.City.KnownTo); Equal((short)41, f.Data.City.FoodBox); Equal((short)29, f.Data.City.ShieldBox);
            Equal(true, f.Units.Unit(0).Token.Occupied); Equal(false, f.World.Civilizations[1].Treaties[2].Embassy);
            using var again = f.Investigate(); again.Resume(); again.Resume(0); Equal(already ? (byte)255 : (byte)0, f.Units.Unit(0).MovesUsed);
            f.Data.Lifecycle.Random.RequireChoicesConsumed();
        }
        var live = new VisitFixture(); using var current = live.Investigate(); current.Resume(); live.Data.City.Flags.Investigated = true; current.Resume(0);
        Equal((byte)0, live.Units.Unit(0).MovesUsed);
        var lost = new VisitFixture(); lost.Units.Unit(0).Type = new(46); lost.Units.Unit(0).MovesUsed = 254;
        lost.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); lost.Data.Incidents.Treaty();
        using var plain = lost.Investigate(); plain.Resume(); plain.Resume(0);
        Equal((byte)1, lost.Units.Unit(0).MovesUsed); Equal(false, lost.Units.Unit(0).Token.Occupied); Equal(true, lost.Data.City.Flags.Investigated);
        Equal(0, lost.Data.Incidents.Notices.Count); Equal(0, lost.World.Civilizations[2].Attitude[1]); lost.Data.Lifecycle.Random.RequireChoicesConsumed();

        // An originally ordinary unit can be a live Spy at escape; the child still executes its real odds/relocation.
        var changed = new VisitFixture([EscapeChoice("visit/escape", 2, 1)]); changed.Units.Unit(0).Type = new(46);
        changed.Units.AddCity(1, 1, new(4, 4));
        using var escape = changed.Investigate(); escape.Resume(); changed.Units.Unit(0).Type = new(47); escape.Resume(0);
        Equal(true, changed.Units.Unit(0).Token.Occupied); Equal(new MapPoint(4, 4), changed.Units.Unit(0).Position);
        Equal((byte)6, changed.Units.Unit(0).MovesUsed); Equal(true, changed.Units.Unit(0).Flags.Veteran); Equal(true, changed.Data.City.Flags.Investigated);
        changed.Data.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CityVisitsFallbackAndNestedLoss()
    {
        var existing = new VisitFixture(); existing.World.Civilizations[1].Treaties[2].Embassy = true;
        existing.World.Espionage.LastMission = CityMission.Embassy;
        using var fallback = existing.Embassy(); Equal(null, fallback.Resume()); Equal(true, fallback.Completed); Equal(CityMission.Investigate, fallback.Fallback);
        Equal(0, existing.Sounds); Equal(0, existing.Notices.Count); Equal((byte)0, existing.Units.Unit(0).MovesUsed);
        Equal(CityMission.Embassy, existing.World.Espionage.LastMission); existing.Data.Lifecycle.Random.RequireChoicesConsumed();
        foreach (bool embassy in new[] { false, true })
        {
            var f = new VisitFixture(); f.Units.Unit(0).Type = new(46);
            f.Units.Rules.Units[46] = f.Units.Rules.Units[46] with { Role = RuleRole.Settle };
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            f.Data.Lifecycle.Recreate = owner => Creation(f.Units, owner, f.Data.Lifecycle.Random);
            f.World.Civilizations[1].UnitsActive[46] = 1; f.World.Civilizations[1].CityCount = 0;
            f.Units.AddUnit(1, 3, 4, new(14, 8)); f.Units.Unit(1).Flags.SpyMission = true; f.Units.Unit(1).Counter = 1;
            f.World.Civilizations[1].Treaties[3].Peace = true;
            using var op = embassy ? f.Embassy() : f.Investigate(); op.Resume();
            var pending = (CityEspionageDecision.Lifecycle)op.Resume(0)!; Equal(1, ((EspionagePretext)pending.Decision).Victim.Civilization);
            Equal(false, f.Units.Unit(0).Token.Occupied || f.Units.Unit(1).Token.Occupied);
            Equal(false, f.Data.City.Flags.Investigated); Equal(0, f.Notices.Count); Equal(false, op.Completed);
            Equal(0, f.Data.Lifecycle.Checks);
            Equal(null, op.Resume(0)); Equal(true, op.Completed); Equal(!embassy, f.Data.City.Flags.Investigated);
            Equal(1, f.Notices.Count); Equal(EndReason.Defeat, f.World.PendingEndReason); Equal(false, f.World.UnitDeletion.Active);
            f.Data.Lifecycle.Random.RequireChoicesConsumed();
        }
        // The direct destruction entry uses the same real nested check and retains its redraw tail.
        var destroyed = new VisitFixture(); destroyed.Units.Rules.Units[47] = destroyed.Units.Rules.Units[47] with { Role = RuleRole.Settle };
        destroyed.World.Civilizations[1].UnitsActive[47] = 1; destroyed.World.Civilizations[1].CityCount = 0;
        destroyed.Units.AddUnit(1, 3, 4, new(14, 8)); destroyed.Units.Unit(1).Flags.SpyMission = true; destroyed.Units.Unit(1).Counter = 1;
        destroyed.World.Civilizations[1].Treaties[3].Peace = true;
        using var destruction = destroyed.Data.Lifecycle.Elimination.BeginDestroy(destroyed.Data.Lifecycle.Loss, 0, "destroy", destroyed.Data.Incidents.Incidents);
        Equal(1, ((EspionagePretext)destruction.Resume()!).Victim.Civilization); Equal(null, destruction.Result);
        Equal(null, destruction.Resume(0)); Equal(1, destruction.Result); Equal(false, destroyed.World.UnitDeletion.DestroyActive);
        Equal(2, destroyed.Data.Lifecycle.Redraws.Count); destroyed.Data.Lifecycle.Random.RequireChoicesConsumed();
    }

    private static void CityVisitFailureAuthorityAndDisposal()
    {
        var sound = new VisitFixture { FailSound = true }; using var early = sound.Embassy();
        Throws<InvalidOperationException>(() => early.Resume()); Throws<InvalidOperationException>(() => early.Resume());
        Equal(true, sound.World.Civilizations[1].Treaties[2].Embassy); Equal(true, sound.Units.Unit(0).Token.Occupied); Equal(1, sound.Sounds);
        var late = new VisitFixture { FailNotice = true }; late.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); late.Units.Unit(0).Type = new(46);
        using var loss = late.Investigate(); loss.Resume(); Throws<InvalidOperationException>(() => loss.Resume(0));
        Equal(false, late.Units.Unit(0).Token.Occupied); Equal(true, late.Data.City.Flags.Investigated); Equal(1, late.Notices.Count);
        Throws<InvalidOperationException>(() => loss.Resume(0)); late.Data.Lifecycle.Random.RequireChoicesConsumed();
        var held = new VisitFixture(); var disposed = held.Embassy(); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(0)); Equal(true, held.World.Civilizations[1].Treaties[2].Embassy);
        Equal((byte)0, held.Units.Unit(0).MovesUsed); Equal(true, held.Units.Unit(0).Token.Occupied);
        var remote = new VisitFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp; using var denied = remote.Embassy();
        Throws<InvalidOperationException>(() => denied.Resume()); Equal(false, remote.World.Civilizations[1].Treaties[2].Embassy); Equal(0, remote.Sounds);
    }

    private static void CityVisitSessionWindowPrivacyAndFailure()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(195));
            var f = new VisitFixture(data: CreationFixture(new(runtime.Process, runtime.World))); f.Units.Unit(0).Type = new(46);
            ParticipantId actorSeat = default, victimSeat = default; int windows = 0;
            runtime.RegisterCommand(new(new(1, "test.visit", "Authored visit routing", AccessClass.Player, new([]), new([new("fallback")])),
                (_, _, _) => new(), _ => new CityEspionageVisitOperation(f.Embassy(), c => c == 1 ? actorSeat : victimSeat, request =>
                {
                    ++windows; Equal(CityEspionageVisitKind.Intelligence, request.Kind);
                    // Required window adapter boundary fixture; it does not claim a P07 renderer.
                    return new("Authored intelligence visit", victimSeat, new CommandValue.City(request.City), new([0]));
                })));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2);
            actorSeat = actor.Identity.Participant; victimSeat = victim.Identity.Participant;
            var command = actor.Submit(new("test.visit", new([]))).Correlation.Command; runtime.AdvanceOne();
            var window = RequiredInteraction(actor, command); Equal(null, victim.Result(command)); Equal(false, victim.Reply(window, 0).Accepted);
            Equal(true, f.Units.Unit(0).Token.Occupied); Equal(true, f.World.Civilizations[1].Treaties[2].Embassy);
            if (cancel) Equal(true, actor.Cancel(command).Accepted); else Equal(true, actor.Reply(window, 0).Accepted);
            runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal(false, f.Units.Unit(0).Token.Occupied); Equal(1, windows); f.Data.Lifecycle.Random.RequireChoicesConsumed();
        }
        foreach (bool fail in new[] { false, true })
        {
            var f = new VisitFixture(); int windows = 0, publishes = 0;
            var context = new OperationContext(f.World, f.Units.Process, f.Data.Lifecycle.Random, new PresentationRandom(3, "visit"),
                new(default, new(1)), default, (_, _) => { }, () => ++publishes);
            using var op = new CityEspionageVisitOperation(f.Embassy(), _ => new(1), request =>
            {
                ++windows; if (fail) throw new InvalidOperationException("Required window unavailable");
                return new("Authored window", null, new CommandValue.City(request.City), new([0]));
            });
            if (fail)
            {
                Throws<InvalidOperationException>(() => op.Resume(context, new())); Throws<InvalidOperationException>(() => op.Resume(context, new()));
                Equal(true, f.World.Civilizations[1].Treaties[2].Embassy); Equal((byte)0, f.Units.Unit(0).MovesUsed);
            }
            else
            {
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Proceed)).Status);
                Equal(CommandStatus.Completed, op.Resume(context, new(OperationInputKind.Timeout)).Status); Equal((byte)1, f.Units.Unit(0).MovesUsed);
            }
            Equal(1, windows); Equal(true, publishes > 0); f.Data.Lifecycle.Random.RequireChoicesConsumed();
        }
    }
}
