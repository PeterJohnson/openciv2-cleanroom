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
    private sealed class DeviceFixture
    {
        public NuclearFixture Nuclear { get; }
        public RevelationFixture Data => Nuclear.Data;
        public GameState World => Data.World;
        public PlantedNuclearDevice Device { get; }
        public List<PlantedNuclearNotice> Notices { get; } = [];
        public bool FailNotice;
        public DeviceFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Nuclear = new(choices, data);
            Data.AddUnit(0, 1, 47, new(4, 10)); Data.Unit(0).MovesUsed = 14;
            Data.AddCity(0, 2, new(10, 10), 9); World.Entities.Cities.Storage[0].Name = "Birch";
            World.Civilizations[1].Government = Government.Despotism;
            Device = new(World, Nuclear.Lifecycle.Random, Nuclear.Lifecycle.Elimination, Nuclear.Incidents.Incidents, Nuclear.Nuclear,
                notice => { Notices.Add(notice); if (FailNotice) throw new InvalidOperationException("Device notice failed"); });
        }
        public PlantedNuclearDevice.Operation Begin() => Device.Begin(new(0, new(0), 1, 2, Data.Unit(0).Type, Data.Unit(0).Type.Value == 47), "device");
        public void Palace() => BuildingQueries.SetBuilding(World, new(0), BuildingIds.Palace, true);
        public void Ask(PlantedNuclearDevice.Operation op)
        { var pending = (CityEspionageDecision.Question)op.Resume()!; Equal(CityEspionageQuestion.MajorIncident, pending.Kind); Equal(1, pending.Actor); Equal(pending, op.Resume()); }
    }
    private static Choice DeviceCapture(int index, int roll = 1, int odds = 3) => EscapeChoice("device/capture/" + index, odds, roll);
    private static Choice DeviceIdentity(int roll, int odds = 2) => Choice.Create(new("V06.device.identification", "device"),
        new SamplingLaw.PrimaryModulo(odds), ChoiceCode.Primary(roll));
    private static List<Choice> DeviceChoices(bool palace = false, int identity = 1) =>
        [DeviceCapture(0), DeviceCapture(1), DeviceCapture(2), .. palace ? new[] { DeviceCapture(3) } : [], DeviceIdentity(identity)];

    // V06 §7: autoplay suppresses the entire body; all other actors get the same exact-reply confirmation.
    private static void DeviceAdmissionAndConfirmation()
    {
        foreach (bool human in new[] { false, true })
        {
            var autoplay = new DeviceFixture(); autoplay.World.Globals.HumanPlayers = human ? CivilizationSet.Only(1) : default;
            autoplay.World.Multiplayer.Saved.AutoplayActive = true; using var skipped = autoplay.Begin();
            Equal(null, skipped.Resume()); Equal(true, skipped.Completed); Equal(0, autoplay.Notices.Count); Equal(0, autoplay.Nuclear.Animations);
            Equal((byte)14, autoplay.Data.Unit(0).MovesUsed); autoplay.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
            foreach (int reply in new[] { -1, 0, 2, 99 })
            {
                var f = new DeviceFixture(); f.World.Globals.HumanPlayers = human ? CivilizationSet.Only(1) : default;
                using var cancelled = f.Begin(); f.Ask(cancelled); cancelled.Resume(reply); Equal(true, cancelled.Completed);
                Equal(0, f.Notices.Count); Equal(0, f.Nuclear.Animations); Equal(true, f.Data.Unit(0).Token.Occupied);
                Equal((sbyte)9, f.World.Entities.Cities.Storage[0].Size); f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
            }
            var ai = new DeviceFixture(DeviceChoices()); ai.World.Globals.HumanPlayers = human ? CivilizationSet.Only(1) : default;
            using var accepted = ai.Begin(); ai.Ask(accepted); accepted.Resume(1); Equal(true, accepted.Completed);
            Equal(1, ai.Notices.Count); Equal((sbyte)5, ai.World.Entities.Cities.Storage[0].Size); ai.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    private static void DeviceCaptureSequenceAndRelocation()
    {
        foreach (bool palace in new[] { false, true })
        {
            for (int caught = 0; caught < (palace ? 4 : 3); ++caught)
            {
                List<Choice> choices = []; for (int i = 0; i <= caught; ++i) choices.Add(DeviceCapture(i, i == caught ? 0 : 1));
                var f = new DeviceFixture(choices); if (palace) f.Palace();
                f.World.Civilizations[1].Treaties[2].Peace = true;
                using var op = f.Begin(); f.Ask(op); op.Resume(1); Equal(true, op.Completed);
                Equal(false, f.Data.Unit(0).Token.Occupied); Equal((byte)14, f.Data.Unit(0).MovesUsed);
                Equal(0, f.Nuclear.Animations); Equal(0, f.Notices.Count); Equal((sbyte)9, f.World.Entities.Cities.Storage[0].Size);
                Equal(SpyFate.Caught, f.Nuclear.Lifecycle.SpyNotices.Single().Fate); Equal(0, f.Nuclear.Incidents.Notices.Count);
                Equal(true, f.World.Civilizations[1].Treaties[2].Peace); f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
            }
            var survivor = new DeviceFixture(DeviceChoices(palace)); if (palace) survivor.Palace();
            survivor.Data.AddCity(1, 1, new(2, 2)); BuildingQueries.SetBuilding(survivor.World, new(0), BuildingIds.SdiDefense, true);
            using var mission = survivor.Begin(); survivor.Ask(mission); mission.Resume(1); Equal(true, mission.Completed);
            Equal(new MapPoint(2, 2), survivor.Data.Unit(0).Position); Equal((byte)6, survivor.Data.Unit(0).MovesUsed);
            Equal(false, survivor.Data.Unit(0).Flags.Veteran); Equal(0, survivor.Nuclear.Lifecycle.SpyNotices.Count);
            Equal((sbyte)5, survivor.World.Entities.Cities.Storage[0].Size); Equal((sbyte)4, survivor.World.Entities.Cities.Storage[1].Size);
            Equal(1, survivor.Nuclear.Animations); survivor.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
        }
        // Direct helper custom replies do not repeat the outer menu's Spy/prerequisite gate.
        var ordinary = new DeviceFixture(); ordinary.Data.Unit(0).Type = new(4);
        using var other = ordinary.Begin(); ordinary.Ask(other); other.Resume(1); Equal(true, other.Completed);
        Equal(false, ordinary.Data.Unit(0).Token.Occupied); Equal(0, ordinary.Nuclear.Animations); ordinary.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
    }

    // Identification uses shared attitude and bilateral setters, not full war declaration or alive/contact gates.
    private static void DeviceIdentificationAndTreatyEffects()
    {
        var f = new DeviceFixture(DeviceChoices(identity: 0)); var w = f.World;
        w.Globals.CivsAlive = CivilizationSet.Only(1); w.Civilizations[1].Reputation = 5;
        for (int other = 2; other < 8; ++other)
        {
            DiplomaticRelations.Set(w, 1, other, new() { Alliance = true, Ceasefire = true });
            w.Civilizations[other].Attitude[1] = 25; w.Civilizations[1].Attitude[other] = 10;
        }
        w.Globals.Multiplayer = GameMode.Hotseat; w.Multiplayer.HotseatParticipants = CivilizationSet.Only(3);
        w.Diplomacy.SelectedPair = (3, 1); w.Diplomacy.Attitude = 40;
        using var op = f.Begin(); f.Ask(op); op.Resume(1); Equal(true, op.Completed);
        for (int other = 2; other < 8; ++other)
        {
            var forward = w.Civilizations[1].Treaties[other]; var reverse = w.Civilizations[other].Treaties[1];
            Equal(true, forward.War && reverse.War && forward.WarThisTurn && reverse.WarThisTurn);
            Equal(false, forward.Alliance || reverse.Alliance || forward.Peace || reverse.Peace || forward.Ceasefire || reverse.Ceasefire);
            Equal(false, forward.Contact || reverse.Contact || forward.Vendetta || reverse.Vendetta || forward.Incident || reverse.Incident);
            Equal((byte)(other == 3 ? 25 : 100), w.Civilizations[other].Attitude[1]); Equal((byte)10, w.Civilizations[1].Attitude[other]);
        }
        Equal(140, w.Diplomacy.Attitude); Equal((byte)5, w.Civilizations[1].Reputation);
        Equal(false, w.Civilizations[0].Treaties[1].War || w.Civilizations[1].Treaties[1].War);
        Equal(true, f.Notices.Single().Identified); Equal(0, f.Nuclear.Incidents.Notices.Count);
        Equal((byte)14, f.Data.Unit(0).MovesUsed); Equal(false, f.Data.Unit(0).Flags.Veteran);
        op.Resume(1); Equal(1, f.Notices.Count); f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
    }

    // U07's completion precedes live government/veteran/name reads, including a destroyed actor's backed flags.
    private static void DeviceLivePostExplosionReads()
    {
        foreach (bool fundamentalism in new[] { false, true })
        {
            List<Choice> choices = [DeviceCapture(0), DeviceCapture(1), DeviceCapture(2)];
            if (!fundamentalism) choices.Add(DeviceIdentity(3, 4));
            var f = new DeviceFixture(choices); f.Data.Stacks.Relocate(0, new(8, 10), false);
            f.Nuclear.OnAnimation = () =>
            {
                f.Data.Unit(0).Flags.Veteran = true;
                f.World.Civilizations[1].Government = fundamentalism ? Government.Fundamentalism : Government.Republic;
                f.World.Entities.Cities.Storage[0].Name = "Changed city"; f.World.Names[1] = new() { Adjective = "New adjective", Tribe = "New people" };
            };
            using var op = f.Begin(); f.Ask(op); op.Resume(1); Equal(true, op.Completed);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, f.Data.Unit(0).Flags.Veteran); Equal((byte)14, f.Data.Unit(0).MovesUsed);
            var notice = f.Notices.Single(); Equal(false, notice.Identified); Equal("Changed city", notice.CityName);
            Equal("New adjective", notice.ActorAdjective); Equal("New people", notice.ActorPlural);
            Equal(false, f.World.Civilizations[1].Treaties[2].War); Equal(0, f.Nuclear.Lifecycle.SpyNotices.Count);
            f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
        }
    }

    private static DeviceFixture DeviceNestedFixture(bool capture, RevelationFixture? data = null)
    {
        var f = new DeviceFixture(capture ? [DeviceCapture(0, 0)] : DeviceChoices(), data); var w = f.World;
        int owner = capture ? 1 : 3;
        w.Globals.HumanPlayers = CivilizationSet.Only(1).With(3);
        f.Data.Rules.Units[capture ? 47 : 46] = f.Data.Rules.Units[47] with { Role = RuleRole.Settle };
        int type = capture ? 47 : 46;
        w.Civilizations[owner].UnitsActive[type] = 1; w.Civilizations[owner].CityCount = 0;
        if (!capture) f.Data.AddUnit(1, owner, type, new(11, 9));
        f.Data.AddUnit(2, 4, 4, new(24, 10)); f.Data.Unit(2).Flags.SpyMission = true; f.Data.Unit(2).Counter = (byte)owner;
        w.Civilizations[owner].Treaties[4].Peace = true;
        f.Nuclear.Lifecycle.Recreate = c => Creation(f.Data, c, f.Nuclear.Lifecycle.Random);
        return f;
    }

    private static void DeviceNestedLifecycleAndFailurePrefixes()
    {
        foreach (bool capture in new[] { false, true })
        {
            var f = DeviceNestedFixture(capture); using var op = f.Begin(); f.Ask(op);
            var decision = (CityEspionageDecision.Lifecycle)op.Resume(1)!;
            Equal(capture ? 1 : 3, ((EspionagePretext)decision.Decision).Victim.Civilization); Equal(false, op.Completed);
            Equal((sbyte)9, f.World.Entities.Cities.Storage[0].Size); Equal(0, f.Notices.Count);
            Equal(0, f.Nuclear.Lifecycle.Checks); Equal(decision, op.Resume());
            op.Resume(0); Equal(true, op.Completed); Equal(capture ? 0 : 1, f.Notices.Count);
            Equal((sbyte)(capture ? 9 : 5), f.World.Entities.Cities.Storage[0].Size); f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
        }
        var failed = new DeviceFixture(DeviceChoices(identity: 0)) { FailNotice = true };
        using var fault = failed.Begin(); failed.Ask(fault); Throws<InvalidOperationException>(() => fault.Resume(1));
        Equal(false, fault.Completed); Equal((sbyte)5, failed.World.Entities.Cities.Storage[0].Size);
        Equal(true, failed.World.Civilizations[1].Treaties[7].War); Throws<InvalidOperationException>(() => fault.Resume());
        Equal(1, failed.Notices.Count); failed.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
        var disposed = new DeviceFixture(); var stop = disposed.Begin(); disposed.Ask(stop); stop.Dispose();
        Throws<ObjectDisposedException>(() => stop.Resume(1)); Equal(true, disposed.Data.Unit(0).Token.Occupied);
        var network = new DeviceFixture(); network.World.Globals.Multiplayer = GameMode.Tcp;
        using var deny = network.Begin(); Throws<InvalidOperationException>(() => deny.Resume()); Equal(0, network.Notices.Count);
    }

    private static void DeviceSessionConfirmationAndNestedPrivacy()
    {
        using var runtime = new SessionRuntime(new(), new(197));
        var f = DeviceNestedFixture(false, CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId actorSeat = default, victimSeat = default;
        runtime.RegisterCommand(new(new(1, "test.device", "Authored device dispatch", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new PlantedNuclearDeviceOperation(f.Begin(), c => c == 1 ? actorSeat : victimSeat)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 3);
        actorSeat = actor.Identity.Participant; victimSeat = victim.Identity.Participant;
        var command = actor.Submit(new("test.device", new([]))).Correlation.Command; runtime.AdvanceOne();
        var confirm = RequiredInteraction(actor, command); Equal(null, victim.Result(command)); Equal(false, victim.Reply(confirm, 1).Accepted);
        Equal(true, actor.Reply(confirm, 1).Accepted); runtime.AdvanceOne();
        var incident = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(incident, 0).Accepted); Equal(0, f.Notices.Count);
        Equal(true, victim.Reply(incident, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(1, f.Notices.Count); f.Nuclear.Lifecycle.Random.RequireChoicesConsumed();

        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var cancelled = new DeviceFixture(); var context = new OperationContext(cancelled.World, cancelled.Data.Process,
                cancelled.Nuclear.Lifecycle.Random, new PresentationRandom(4, "device"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new PlantedNuclearDeviceOperation(cancelled.Begin(), _ => new(1));
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            Equal(CommandStatus.Completed, op.Resume(context, new(input)).Status); Equal(0, cancelled.Nuclear.Animations);
            cancelled.Nuclear.Lifecycle.Random.RequireChoicesConsumed();
        }
    }
}
