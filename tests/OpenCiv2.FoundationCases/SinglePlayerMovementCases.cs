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
    // U02 dispatch, U05 transport, V06 missions, U06 combat and W03/W06 retained arrival.
    private sealed class SinglePlayerMovementFixture
    {
        public SinglePlayerMissionFixture Missions { get; }
        public SinglePlayerCombatFixture Combat => Missions.Combat;
        public SinglePlayerLifecycleFixture Life => Missions.Life;
        public DispatcherFixture F { get; }
        public RevelationFixture Data => Missions.Data;
        public GameState World => Data.World;
        public SinglePlayerMovement Services { get; }
        public SinglePlayerMovementFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Missions = new(choices, data); F = new(data: Data);
            World.Civilizations[1].Treaties[2] = World.Civilizations[2].Treaties[1] = default;
            World.Civilizations[1].Government = World.Civilizations[2].Government = Government.Despotism;
            World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); World.Multiplayer.LocalHuman = 1;
            World.Globals.Turn = 41; World.Globals.RevealMap = 0; World.Events.Loaded = false;
            Life.Life.Deletion.Erase(1, false);
            Services = new(World, Data.Process, Life.Random, Data.Stacks, new(World, Data.Rules, Data.Stacks),
                NamingCatalog(["First", "Second"]), new(World, Data.Process.Rules, Data.Stacks), Life.Services,
                Life.Diplomacy.Grant.Services, Life.Diplomacy.Services, Combat.Services, Missions.Services,
                new(F, F.Land, F.Land, F.Foreign, F.Foreign, F.Finish, F.City, F.Relocation, F.Finish, F.Finish, F.Finish,
                    () => throw new InvalidOperationException("Unexpected city limit")), "Food");
        }
        public MovementDispatcher.Operation Begin(int direction = 1, MovementOptions? options = null)
        {
            var from = Data.Unit(0).Position;
            var to = direction == 1 ? new MapPoint(from.X + 2, from.Y) : from;
            F.Relocation.Step = F.Relocation.Step with { From = from, To = to, Direction = direction };
            F.Land.Step = F.Land.Step with { From = from, To = to, Direction = direction };
            return Services.Dispatcher.Begin(new(0), direction, options ?? new(true, true), F.Inputs, "move");
        }
        public void Complete(MovementDispatcher.Operation operation)
        {
            for (int i = 0; i < 100 && !operation.Completed; ++i) Equal(null, operation.Resume());
            Equal(true, operation.Completed); Equal(false, World.UnitMovementActive); Consumed();
        }
        public void Consumed() { Combat.Consumed(); F.Capture.Life.Random.RequireChoicesConsumed(); }
        public MovementOperation Session(Func<int, ParticipantId>? recipient = null) => new(Begin(), recipient ?? (c => new((ulong)c)),
            visit => new("Visit", new(1), new CommandValue.City(visit.City), new([0])),
            _ => throw new InvalidOperationException("No intelligence"),
            visit => new("Capture", new(1), new CommandValue.City(visit.City), new([0])),
            visit => new("Hut", new(1), new CommandValue.City(visit.City), new([0])));
        public void AtBirch(bool spy = false)
        {
            Data.Stacks.Relocate(0, new(10, 8), false);
            if (spy)
            { Data.Unit(0).Type = new(47); Data.Rules.Units[47] = Data.Rules.Units[47] with { Role = RuleRole.Diplomacy }; }
        }
    }
    private static List<Choice> MoveChoices(IEnumerable<Choice> choices, string prefix, string replacement) =>
        choices.Select(c => c with { Key = c.Key with { Call = replacement + c.Key.Call[prefix.Length..] } }).ToList();

    private static void SinglePlayerMovementOrdinaryAndTransport()
    {
        foreach (bool execute in new[] { false, true })
        {
            var f = new SinglePlayerMovementFixture(); using var operation = f.Begin(options: new(true, execute));
            f.Complete(operation); Equal(1, operation.Result); Equal(new MapPoint(execute ? 20 : 18, 12), f.Data.Unit(0).Position);
            Equal((byte)(execute ? 3 : 0), f.Data.Unit(0).MovesUsed); Equal(execute ? 2 : 0, f.F.Relocation.Publications.Count);
        }
        foreach (int answer in new[] { 0, 1 })
        {
            var f = new SinglePlayerMovementFixture(); f.Data.Unit(0).Type = new(1);
            f.World.Map.TileAt(new(18, 12)).Terrain = Terrain.Ocean;
            f.Data.AddUnit(2, 1, 4, new(18, 12)); f.Data.Unit(2).Orders = UnitOrder.Sleeping; f.Data.Unit(2).Carrier = new(0);
            using var operation = f.Begin(); var pending = operation.Resume(); Equal(true, pending is MovementDecision.Landfall);
            Equal(pending, operation.Resume()); operation.Resume(new(answer)); f.Complete(operation); Equal(answer, operation.Result);
            Equal(new MapPoint(18, 12), f.Data.Unit(0).Position); Equal(new MapPoint(answer == 1 ? 20 : 18, 12), f.Data.Unit(2).Position);
        }
        var board = new SinglePlayerMovementFixture(); board.World.Map.TileAt(new(20, 12)).Terrain = Terrain.Ocean;
        board.Data.Rules.Units[1] = board.Data.Rules.Units[1] with { Capacity = 1 };
        board.Data.AddUnit(2, 1, 1, new(20, 12)); using var boarded = board.Begin(); board.Complete(boarded);
        Equal(new MapPoint(20, 12), board.Data.Unit(0).Position); Equal(UnitOrder.Sleeping, board.Data.Unit(0).Orders);
        Equal((UnitSlot?)new UnitSlot(2), board.Data.Unit(0).Carrier);
    }

    private static void SinglePlayerMovementPrivateMissionFallback()
    {
        using var runtime = new SessionRuntime(new(), new(276));
        var f = new SinglePlayerMovementFixture(data: new(runtime.Process, runtime.World)); f.AtBirch(spy: true);
        ParticipantId human = default;
        runtime.RegisterCommand(new(new(1, "test.composed-move", "Composed movement", AccessClass.Player, new([]),
            new([new("result", ValueKind.Integer)])), (_, _, _) => new(), _ => f.Session(_ => human)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var recipient = SessionClient(runtime, 1); human = recipient.Identity.Participant;
        var command = actor.Submit(new("test.composed-move", new([]))).Correlation.Command; runtime.AdvanceOne();
        var menu = RequiredInteraction(recipient, command); Equal(false, actor.Reply(menu, 0).Accepted);
        f.World.Civilizations[1].Treaties[2].Embassy = true;
        Equal(true, recipient.Reply(menu, (int)CityMission.Embassy).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Pending, RequiredResult(actor, command).Status); Equal(true, f.World.UnitMovementActive);
        f.Data.Unit(0).Type = new(46); runtime.AdvanceOne();
        var visit = RequiredInteraction(recipient, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(visit, 0).Accepted); Equal(true, recipient.Reply(visit, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, recipient.Reply(visit, 0).Accepted);
        Equal(true, f.F.Capture.City.Flags.Investigated); Equal((byte)1, f.Data.Unit(0).MovesUsed);
        Equal(new MapPoint(10, 8), f.Data.Unit(0).Position); Equal(false, f.World.UnitMovementActive); f.Consumed();
    }

    private static void SinglePlayerMovementCombatEvent()
    {
        var f = new SinglePlayerMovementFixture(MoveChoices(ExecutionWin(), "combat", "move/0/foreign-unit/combat"));
        f.AtBirch(); f.Data.AddUnit(1, 2, 4, new(12, 8)); var entry = f.Combat.Event(EventTrigger.UnitKilled);
        var permission = NegotiationEvent(f.World, 1, 2, deny: true);
        permission.Values.Actions = new() { Text = true, JustOnce = true }; permission.SetString(Event.TextBegin, new("After combat contact"));
        using var operation = f.Begin(); var pending = operation.Resume();
        Equal(true, pending is MovementDecision.ForeignUnit { Decision: ForeignUnitDecision.Combat { Decision: CombatDecision.Lifecycle } });
        Equal(true, f.Data.Unit(1).Token.Occupied); Equal(1, f.World.Casualties[2].Count); Equal(true, f.World.UnitMovementActive);
        Equal(pending, operation.Resume()); Equal(true, operation.Resume(new(0)) is MovementDecision.ForeignUnit);
        Equal(true, operation.Resume(new(0)) is MovementDecision.Finish { Decision: MovementCompletionDecision.Contact });
        Equal(false, f.Data.Unit(1).Token.Occupied); Equal(false, permission.Values.Actions.Consumed);
        operation.Resume(new(0)); f.Complete(operation); Equal(true, permission.Values.Actions.Consumed);
        Equal(true, entry.Values.Actions.Consumed); Equal((sbyte)4, f.F.Capture.City.Size); Equal((byte)3, f.Data.Unit(0).MovesUsed);
        Equal(1, f.Combat.Services.State.KillCount);
    }

    private static void SinglePlayerMovementCaptureAndContact()
    {
        var f = new SinglePlayerMovementFixture([CaptureParity(0) with { Key = new("C09.capture.building-parity", "move/0/finish/capture") }]);
        f.AtBirch(); var entry = f.Combat.Event(EventTrigger.CityTaken);
        using var operation = f.Begin(); var pending = operation.Resume();
        Equal(true, pending is MovementDecision.Finish { Decision: MovementCompletionDecision.Capture { Decision: CityCaptureDecision.Lifecycle } });
        Equal(new MapPoint(12, 8), f.Data.Unit(0).Position); Equal(new CivilizationSlot(1), f.F.Capture.City.Owner);
        Equal(true, operation.Resume(new(0)) is MovementDecision.Finish); Equal(true, operation.Resume(new(0)) is MovementDecision.Finish
        { Decision: MovementCompletionDecision.Capture { Decision: CityCaptureDecision.Window } });
        operation.Resume(new(0)); f.Complete(operation); Equal(true, entry.Values.Actions.Consumed); Equal((byte)1, f.Data.Unit(0).LastDirection);

        var contact = new SinglePlayerMovementFixture(); contact.Data.AddUnit(2, 2, 4, new(21, 13));
        contact.World.Multiplayer.LocalHuman = 2; contact.World.Events.Loaded = true;
        var permission = NegotiationEvent(contact.World, 1, 2, deny: true);
        permission.Values.Actions = new() { Text = true, JustOnce = true }; permission.SetString(Event.TextBegin, new("Movement contact"));
        using var arrival = contact.Begin(); pending = arrival.Resume();
        Equal(true, pending is MovementDecision.Finish { Decision: MovementCompletionDecision.Contact { Decision: DiplomaticContactDecision.Scenario } });
        Equal(new MapPoint(20, 12), contact.Data.Unit(0).Position); Equal((byte)0, contact.Data.Unit(0).LastDirection);
        Equal(pending, arrival.Resume()); arrival.Resume(new(0)); contact.Complete(arrival);
        Equal(true, permission.Values.Actions.Consumed); Equal((byte)1, contact.Data.Unit(0).LastDirection);
    }

    private static void SinglePlayerMovementHutCreation()
    {
        var f = new SinglePlayerMovementFixture(MoveChoices(HutStart(0), "hut", "move/0/finish/hut"));
        var destination = new MapPoint(20, 12); f.World.Map.PatternSeed = 31; f.World.Map.TileAt(destination).Owner = null;
        Equal(true, f.World.Map.Hut(destination));
        f.World.Map.TileAt(destination).Fertility = 1;
        f.Data.AddCity(2, 1, new(4, 4)); f.World.Civilizations[1].CityCount = 1;
        using var operation = f.Begin(); f.Complete(operation);
        var settlers = Enumerable.Range(1, f.World.Entities.Units.End).Where(i => f.Data.Unit(i).Token.Occupied
            && f.Data.Unit(i).Type.Value == UnitTypeIds.Settlers).ToArray();
        Equal(1, settlers.Length); Equal(destination, f.Data.Unit(settlers[0]).Position); Equal(null, f.Data.Unit(settlers[0]).Home);
        Equal(false, f.World.Map.Hut(destination));
        Equal(HutNoticeKind.Nomads, f.F.Finish.Huts.Single().Kind); Equal((byte)1, f.Data.Unit(0).LastDirection);
    }

    private static void SinglePlayerMovementFailureAndDisposal()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new SinglePlayerMovementFixture(MoveChoices([MissionDraw("V06.theft.advance-start", CityMission.Steal, 100, AdvanceIds.Monarchy)],
                "city", "move/0/land/city")); f.AtBirch(); f.Missions.Monarchy();
            using var operation = f.Begin(); operation.Resume(); var pending = operation.Resume(new((int)CityMission.Steal));
            Equal(true, pending is MovementDecision.Land); Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            if (dispose) { operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0))); }
            else
            {
                f.Life.Diplomacy.Grant.FailRevolution = true;
                Throws<InvalidOperationException>(() => operation.Resume(new(1))); Throws<InvalidOperationException>(() => operation.Resume());
            }
            Equal(false, f.World.UnitMovementActive); Equal(true, f.Data.Unit(0).Token.Occupied);
            Equal(false, f.F.Capture.City.Flags.TechnologyStolen); f.Consumed();
        }
        var cosmetic = new SinglePlayerMovementFixture(); cosmetic.F.Relocation.FailCosmetic = true;
        using var session = cosmetic.Session(); var context = cosmetic.Life.Diplomacy.Events.Context();
        var result = session.Resume(context, new()); Equal(CommandStatus.Failed, result.Status); Equal("movement_presentation_failed", result.Code);
        Equal(new MapPoint(20, 12), cosmetic.Data.Unit(0).Position); Equal((byte)3, cosmetic.Data.Unit(0).MovesUsed);
        Equal(result, session.Resume(context, new())); Equal(false, cosmetic.World.UnitMovementActive); cosmetic.Consumed();
    }

    private static void SinglePlayerMovementCaptureDiagnostics()
    {
        var f = new SinglePlayerMovementFixture([
            CaptureParity(0) with { Key = new("C09.capture.building-parity", "move/0/finish/capture") },
            Choice.Create(new("V03.emissary", "move/0/finish/capture/city-taken/event/1/technology/grant"),
                new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0))]);
        f.AtBirch(); var entry = f.Combat.Event(EventTrigger.CityTaken, library: false, text: false);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Diplomacy.EmissaryResponse = 0;
        f.World.Civilizations[1].Treaties[2].Contact = f.World.Civilizations[2].Treaties[1].Contact = true;
        f.World.Globals.Status.Scenario = true; f.World.Scenario.Flags.NoConquestTechnology = true;
        f.Life.Diplomacy.Grant.FailClose = true;
        using var session = f.Session(); var context = f.Life.Diplomacy.Events.Context();
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
        Equal(new CivilizationSlot(1), f.F.Capture.City.Owner); Equal(false, entry.Values.Actions.Consumed);
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(OperationInputKind.Cancel)).Status);
        var result = session.Resume(context, new(OperationInputKind.Choice, 0));
        Equal(CommandStatus.Failed, result.Status); Equal("movement_presentation_failed", result.Code);
        Equal(true, entry.Values.Actions.Consumed); Equal(new MapPoint(12, 8), f.Data.Unit(0).Position);
        Equal((byte)1, f.Data.Unit(0).LastDirection); Equal(false, f.World.UnitMovementActive); f.Consumed();
    }

    private static void SinglePlayerMovementCaravanAndExpulsion()
    {
        var f = new SinglePlayerMovementFixture(); f.AtBirch(); f.Data.Unit(0).Type = new(3);
        f.Data.Unit(0).Counter = 255; f.Data.Unit(0).Home = null; f.Data.Unit(0).Destination = new(-1, -1);
        f.F.Capture.City.Owner = new(1); f.World.Map.TileAt(new(12, 8)).Owner = 1; f.F.Capture.City.Production = -39;
        f.Data.Rules.Units[3] = f.Data.Rules.Units[3] with { CostRows = 5 };
        using var donation = f.Begin(); var menu = donation.Resume();
        Equal(true, menu is MovementDecision.Land { Decision: LandEntryDecision.Caravan { Decision: CaravanDecision.Menu } });
        donation.Resume(new((int)CaravanAction.HelpBuildWonder)); f.Complete(donation);
        Equal(0, donation.Result); Equal((short)50, f.F.Capture.City.ShieldBox); Equal(false, f.Data.Unit(0).Token.Occupied);
        Equal(1, f.F.Land.Donations);

        var expelled = new SinglePlayerMovementFixture(); expelled.AtBirch(); expelled.Data.AddUnit(1, 2, 4, new(12, 8));
        expelled.F.Foreign.Diplomat(); using var operation = expelled.Begin();
        var pending = operation.Resume(); Equal(true, pending is MovementDecision.ForeignUnit { Decision: ForeignUnitDecision.Expel });
        Equal(pending, operation.Resume()); operation.Resume(new(0)); expelled.Complete(operation);
        Equal(new MapPoint(10, 8), expelled.Data.Unit(0).Position);
        Equal(expelled.World.Entities.Cities.Storage[1].Position, expelled.Data.Unit(1).Position);
        Equal(true, expelled.F.Foreign.ExpulsionNotices.Any(n => n.Kind == ExpulsionNoticeKind.VictimReturned));
    }
}
