using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V01 §5/§6, U01 §5, W03/V04: actual placement and restart children, specification-derived expectations.
    private sealed class PlacementContactFixture
    {
        public ContactFixture Contact { get; }
        public RevelationFixture Data => Contact.Data;
        public GameState World => Data.World;
        public GameplayRandom Random { get; }
        public PlacementContactFixture(RevelationFixture? data = null, bool exchange = false)
        {
            Contact = new(exchange ? [GreetingChoice("create/placement/first/revelation/adjacent-1/second-audience/emissary/greet")] : [], data);
            CreationFixture(Data);
            // The restart has no surviving cities. Its neighbor is a real human unit.
            foreach (ref var city in World.Entities.Cities.Storage) { city.Token = default; }
            World.Entities.Cities.SetEnd(0);
            foreach (ref var tile in World.Map.Tiles) { tile.Features &= ~MapFeatures.CitySite; }
            Data.Stacks.Relocate(0, new(12, 8), false);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            World.Globals.Turn = 41;
            Data.Process.Rules.Leaders[2].Color = 2;
            World.Civilizations[2].CapitalX = 77;
            Data.Rules.Units[3] = Data.Rules.Units[0] with { Role = RuleRole.Defend, Prerequisite = AdvanceReference.None };
            var choices = CreationAttitudes(); CreationChoice(choices, "gender", 3, 1); CreationSiteChoices(choices);
            Random = CreationRandom(choices);
        }
        public CivilizationCreation Create() => new(World, Data.Process, 2, new(), Random, "create",
            new(RetainedCreation(Contact), new(World, Data.Rules, new(5_000_000, 64), Data), Contact.F.T.T.Research,
                (owner, type) => UnitAvailability.CanBuild(World, Data.Rules, owner, null, type)));
        public void Consumed() { Random.RequireChoicesConsumed(); Contact.F.T.T.Random.RequireChoicesConsumed(); }
    }
    private static void CreationPlacementContactOrder()
    {
        foreach (bool explored in new[] { false, true })
        {
            var f = new PlacementContactFixture(); using var op = f.Create();
            Equal(null, op.AdvanceDecision(1)); Equal(true, op.Pending is LifecycleContact);
            Equal((short)77, f.World.Civilizations[2].CapitalX); Equal(1, f.World.UnitTotals[2]);
            var first = f.Data.Stacks.FirstAt(new(10, 8)); var token = f.Data.Unit(first).Token;
            var pending = op.Pending; Equal(null, op.AdvanceDecision(1)); Equal(pending, op.Pending);
            // A retained contact decision is an admitted boundary for the live exploration test.
            f.World.Map.Mutate(new TileChange.Revelation(new(10, 8), CivilizationSet.Only(1), explored));
            Equal(1, op.AdvanceDecision(1, new(1))); Equal((short)10, f.World.Civilizations[2].CapitalX);
            Equal(explored ? 1 : 3, f.World.UnitTotals[2]); Equal(token, f.Data.Unit(first).Token);
            Equal(true, f.World.Map.TileAt(new(12, 6)).RevealedTo.Contains(2));
            Equal(1, f.Contact.F.C.Closed); Equal(1, op.AdvanceDecision(1, new(1))); f.Consumed();
        }
    }
    private static void CreationPlacementContactFailure()
    {
        var f = new PlacementContactFixture(); using var held = f.Create(); held.AdvanceDecision(1); held.Dispose();
        Throws<ObjectDisposedException>(() => held.AdvanceDecision(1, new(1)));
        Equal(1, f.World.UnitTotals[2]); Equal((short)77, f.World.Civilizations[2].CapitalX);
        f = new(); f.World.Events.Loaded = true; NegotiationEvent(f.World, 2, 1); f.Contact.F.FailEvent = true;
        using var failed = f.Create(); Throws<InvalidOperationException>(() => failed.AdvanceDecision(1));
        Throws<InvalidOperationException>(() => failed.AdvanceDecision(1)); Equal(1, f.World.UnitTotals[2]);
        Equal((short)77, f.World.Civilizations[2].CapitalX);
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            f = new(); f.Contact.F.C.FailClose = true;
            var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(1, "placement"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new CivilizationCreationOperation(f.Create(), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
            var result = op.Resume(context, new(input)); Equal(CommandStatus.Failed, result.Status);
            Equal("civilization_creation_presentation_failed", result.Code); Equal((short)10, f.World.Civilizations[2].CapitalX);
            Equal(result, op.Resume(context, new(input))); Equal(1, f.Contact.F.C.Closed); f.Consumed();
        }
    }
    private static void CreationPlacementPrivateRestart()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = new PlacementContactFixture(new(runtime.Process, runtime.World));
        var life = f.Contact.F.T.T.Damage.Lifecycle; life.Recreate = owner => { Equal(2, owner); return f.Create(); };
        ParticipantId seat = default;
        runtime.RegisterCommand(new(new(1, "test.restart-contact", "Restart civilization", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new CivilizationEliminationOperation(life.Elimination.Begin(2, -1, "restart", f.Contact.F.T.T.Damage.Incidents.Incidents), _ => seat)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); seat = human.Identity.Participant;
        var command = actor.Submit(new("test.restart-contact", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(human, command); Equal(false, actor.Reply(prompt, 1).Accepted);
        Equal(null, RequiredResult(actor, command).Interaction); Equal((short)77, f.World.Civilizations[2].CapitalX);
        Equal(true, human.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(null, human.Result(command));
        Equal(false, human.Reply(prompt, 1).Accepted); Equal((short)10, f.World.Civilizations[2].CapitalX);
        Equal(1, life.Restarts); Equal(1, life.MapRefreshes); f.Consumed();
    }
    private static void CreationPlacementNestedResearchHelp()
    {
        var f = new PlacementContactFixture(exchange: true); var life = f.Contact.F.T.T.Damage.Lifecycle;
        life.Recreate = _ => f.Create();
        var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(1, "help"), new(default, new(1)), default, (_, _) => { }, () => { });
        using var op = new CivilizationEliminationOperation(life.Elimination.Begin(2, -1, "restart", f.Contact.F.T.T.Damage.Incidents.Incidents), c => new((ulong)c));
        Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
        f.Contact.F.T.Pair(); f.World.Civilizations[2].Treaties[1].Peace = true;
        f.World.Diplomacy.Fear = 3; f.World.Diplomacy.Aggressive = false;
        Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Choice, 0)).Status);
        var choose = op.Resume(context, new(OperationInputKind.Choice, 1)); Equal(CommandStatus.AwaitingInteraction, choose.Status);
        Equal(true, f.World.Civilizations[2].Advances.Contains(95)); Equal(false, f.World.Civilizations[1].Advances.Contains(90));
        f.Data.Rules.Advances[90] = f.Data.Rules.Advances[90] with { Enabled = true };
        f.Contact.F.T.T.GoalReplies.Enqueue(new(90));
        var help = op.Resume(context, new(OperationInputKind.Choice, 100)); Equal(CommandStatus.AwaitingInteraction, help.Status);
        Equal(1, f.Contact.F.T.T.GoalNotices.Count); Equal(false, f.World.Civilizations[1].Advances.Contains(0));
        Equal((short)77, f.World.Civilizations[2].CapitalX); Equal(1, f.World.UnitTotals[2]);
        Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new(OperationInputKind.Choice, 90)).Status);
        var done = op.Resume(context, new(OperationInputKind.Choice, 0)); Equal(CommandStatus.Completed, done.Status);
        Equal(true, f.World.Civilizations[1].Advances.Contains(90)); Equal((short)10, f.World.Civilizations[2].CapitalX); f.Consumed();
    }
}
