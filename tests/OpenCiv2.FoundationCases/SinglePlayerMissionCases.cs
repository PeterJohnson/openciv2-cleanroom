using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V06 local mission bodies and fallbacks joined to production V03/C09/U07/S02 services.
    private sealed class SinglePlayerMissionFixture
    {
        public SinglePlayerCombatFixture Combat { get; }
        public SinglePlayerLifecycleFixture Life => Combat.Life;
        public RevelationFixture Data => Combat.Data;
        public GameState World => Data.World;
        public CityMissionFixture F { get; }
        public UnitMissionFixture UnitEffects { get; } = new();
        public List<PlantedNuclearNotice> Devices { get; } = [];
        public SinglePlayerMissions Services { get; }
        public SinglePlayerMissionFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Combat = new(choices, data); F = new(data: Data, capture: Combat.F.F.Drop.Capture);
            Life.Life.Deletion.Erase(1, false);
            Data.Unit(0).Type = new(47); Data.Unit(0).MovesUsed = 0;
            Data.Rules.Units[47] = Data.Rules.Units[47] with { Role = RuleRole.Diplomacy };
            Services = new(World, Data.Rules, Life.Random, Data.Stacks, Life.Services,
                Life.Diplomacy.Grant.Services, Life.Diplomacy.Services, Combat.Services,
                () => { ++F.Reads; using var reader = F.Text.Reader(); return ClassicCityMissionOptions.Read(reader, "missions", " (Spy authored)"); },
                () => { ++F.Revolt.Reads; using var reader = F.Revolt.Text.Reader(); return ClassicRevoltOptions.Read(reader, "revolt"); },
                new(F, F.Capture.Theft.Damage, F.Capture.Theft, F.Revolt, UnitEffects, Devices.Add, () => ++F.Stops));
        }
        public CityMissions.Operation Begin() => Services.Cities.Begin(0, new(0), 77, "city");
        public void Complete(CityMissions.Operation operation)
        {
            for (int i = 0; i < 50 && !operation.Completed; ++i) Equal(null, operation.Resume());
            Equal(true, operation.Completed); Combat.Consumed();
        }
        public void Monarchy()
        {
            Data.Unit(0).Type = new(46);
            World.Civilizations[2].Advances = World.Civilizations[2].Advances.With(AdvanceIds.Monarchy);
            World.Globals.AdvanceKnownBy[AdvanceIds.Monarchy] = CivilizationSet.Only(2);
        }
    }

    private static void SinglePlayerMissionPrivateFallback()
    {
        using var runtime = new SessionRuntime(new(), new(275));
        var f = new SinglePlayerMissionFixture(data: new(runtime.Process, runtime.World));
        f.World.Civilizations[1].Treaties[2].Embassy = true;
        ParticipantId human = default; int visits = 0;
        runtime.RegisterCommand(new(new(1, "test.composed-mission", "Composed mission", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CityMissionOperation(f.Begin(), _ => human, request =>
            { ++visits; return new("Authored visit", human, new CommandValue.City(request.City), new([0])); },
            _ => throw new InvalidOperationException("No capture"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var recipient = SessionClient(runtime, 1);
        human = recipient.Identity.Participant;
        var command = actor.Submit(new("test.composed-mission", new([]))).Correlation.Command; runtime.AdvanceOne();
        var menu = RequiredInteraction(recipient, command); Equal(false, actor.Reply(menu, 0).Accepted);
        // A hidden reply is a core contract, but the session only admits presented options.
        Equal(false, recipient.Reply(menu, (int)CityMission.Embassy).Accepted);
        Equal(true, recipient.Reply(menu, (int)CityMission.Investigate).Accepted); runtime.AdvanceOne();
        var visit = RequiredInteraction(recipient, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(visit, 0).Accepted); Equal(1, visits); Equal(false, f.F.Capture.City.Flags.Investigated);
        Equal(true, recipient.Reply(visit, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, f.F.Capture.City.Flags.Investigated);
        Equal((byte)1, f.Data.Unit(0).MovesUsed); Equal(1, f.F.Reads); f.Combat.Consumed();

        // An admitted Embassy becomes a fallback if the embassy is acquired while its menu waits.
        var fallback = new SinglePlayerMissionFixture();
        using var session = new CityMissionOperation(fallback.Begin(), c => new((ulong)c),
            request => new("Visit", new(1), new CommandValue.City(request.City), new([0])),
            _ => throw new InvalidOperationException("No capture"));
        var context = fallback.Life.Diplomacy.Events.Context(); session.Resume(context, new());
        fallback.World.Civilizations[1].Treaties[2].Embassy = true;
        var yield = session.Resume(context, new(OperationInputKind.Choice, 0));
        Equal(CommandStatus.Pending, yield.Status); Equal(true, yield.ReadyToContinue);
        fallback.Data.Unit(0).Type = new(46);
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
        Equal(CommandStatus.Completed, session.Resume(context, new(OperationInputKind.Timeout)).Status);
        Equal(true, fallback.Data.Unit(0).Token.Occupied); Equal((byte)1, fallback.Data.Unit(0).MovesUsed); fallback.Combat.Consumed();
    }

    private static void SinglePlayerMissionTheftGrant()
    {
        foreach (int ending in new[] { 0, 1, 2 })
        {
            var f = new SinglePlayerMissionFixture([MissionDraw("V06.theft.advance-start", CityMission.Steal, 100, AdvanceIds.Monarchy)]);
            f.Monarchy(); using var operation = f.Begin(); operation.Resume();
            var grant = operation.Resume(new((int)CityMission.Steal));
            Equal(true, grant is CityMissionDecision.Mission { Decision: CityEspionageDecision.Grant { Decision: ResearchGrantDecision.Government } });
            Equal(grant, operation.Resume()); Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            Equal(false, f.F.Capture.City.Flags.TechnologyStolen); Equal(true, f.Data.Unit(0).Token.Occupied);
            if (ending == 1)
            {
                f.Life.Diplomacy.Grant.FailRevolution = true;
                Throws<InvalidOperationException>(() => operation.Resume(new(1))); Throws<InvalidOperationException>(() => operation.Resume());
            }
            else if (ending == 2)
            { operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0))); }
            else
            {
                operation.Resume(new(0)); f.Complete(operation);
                Equal(true, f.F.Capture.City.Flags.TechnologyStolen); Equal(false, f.Data.Unit(0).Token.Occupied);
            }
            f.Combat.Consumed();
        }
    }

    private static void SinglePlayerMissionCaptureDiagnostics()
    {
        var choices = new List<Choice> { MissionEscape(CityMission.Incite, 0), Choice.Create(
            new("V03.emissary", "city/mission/6/transfer/city-taken/event/1/technology/grant"),
            new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0)) };
        var f = new SinglePlayerMissionFixture(choices); var entry = f.Combat.Event(EventTrigger.CityTaken, library: false, text: false);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Diplomacy.EmissaryResponse = 0;
        f.World.Globals.Status.Scenario = true; f.World.Scenario.Flags.NoConquestTechnology = true;
        f.Life.Diplomacy.Grant.FailClose = true;
        using var session = new CityMissionOperation(f.Begin(), c => new((ulong)c),
            _ => throw new InvalidOperationException("No intelligence visit"),
            request => new("Capture", new(1), new CommandValue.City(request.City), new([0])));
        var context = f.Life.Diplomacy.Events.Context(); session.Resume(context, new());
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(OperationInputKind.Choice, (int)CityMission.Incite)).Status);
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(OperationInputKind.Choice, 2)).Status);
        Equal(new CivilizationSlot(1), f.F.Capture.City.Owner); Equal(false, entry.Values.Actions.Consumed);
        Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(OperationInputKind.Cancel)).Status);
        Equal(true, entry.Values.Actions.Consumed); Equal(false, f.Data.Unit(0).Token.Occupied);
        var result = session.Resume(context, new(OperationInputKind.Choice, 0));
        Equal(CommandStatus.Failed, result.Status); Equal("city_mission_presentation_failed", result.Code);
        Equal(result, session.Resume(context, new())); Equal(true, f.World.Civilizations[1].Gold < 1000);
        Equal((byte)41, f.F.Capture.City.TurnsCaptured); f.Combat.Consumed();
    }

    private static void SinglePlayerMissionDamageAndBribe()
    {
        var f = new SinglePlayerMissionFixture([MissionDraw("V06.poison.escape-mode", CityMission.Poison, 2, 0)]);
        f.Data.Unit(0).Type = new(46); using var poison = f.Begin(); poison.Resume(); poison.Resume(new((int)CityMission.Poison));
        f.Complete(poison); Equal((sbyte)4, f.F.Capture.City.Size); Equal(false, f.Data.Unit(0).Token.Occupied);
        var bribe = new SinglePlayerMissionFixture(); bribe.Data.AddUnit(1, 2, 4, new(12, 8));
        bribe.Data.Rules.Units[4] = bribe.Data.Rules.Units[4] with { CostRows = 6, Role = RuleRole.Attack };
        using var operation = bribe.Services.Units.Bribe(1, 1, "bribe");
        var offer = ((UnitMissionDecision.Bribe)operation.Resume()!).Offer;
        Equal(true, offer.Affordable); int gold = bribe.World.Civilizations[1].Gold;
        operation.Resume(1); Equal(0, operation.Result);
        Equal(gold - offer.Price, bribe.World.Civilizations[1].Gold); Equal(new CivilizationSlot(1), bribe.Data.Unit(1).Owner);
        Equal((byte)0, bribe.Data.Unit(1).MovesUsed); Equal(true, bribe.Data.Unit(0).Token.Occupied); bribe.Combat.Consumed();
    }

    private static void SinglePlayerMissionNuclearDelivery()
    {
        var choices = DeviceChoices().Select(c => c with { Key = c.Key with { Call = c.Key.Call.Replace("device", "city/mission/5", StringComparison.Ordinal) } }).ToList();
        var f = new SinglePlayerMissionFixture(choices); f.Combat.F.F.Ocean();
        f.World.Civilizations[1].Advances = f.World.Civilizations[1].Advances.With(AdvanceIds.Rocketry).With(AdvanceIds.NuclearFission);
        f.World.Globals.WonderCity[WonderIds.Manhattan] = BuildingQueries.Destroyed;
        using var operation = f.Begin(); var menu = (CityMissionDecision.Menu)operation.Resume()!;
        Equal(true, menu.Request.Options.Any(o => o.Mission == CityMission.PlantNuclear));
        var confirmation = operation.Resume(new((int)CityMission.PlantNuclear));
        Equal(true, confirmation is CityMissionDecision.Mission { Decision: CityEspionageDecision.Question { Kind: CityEspionageQuestion.MajorIncident } });
        operation.Resume(new(1)); f.Complete(operation); Equal((sbyte)3, f.F.Capture.City.Size);
        Equal(1, f.Devices.Count); Equal(false, f.Data.Unit(0).Token.Occupied);
    }
}
