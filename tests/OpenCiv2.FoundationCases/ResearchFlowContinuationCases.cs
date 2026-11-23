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
    // V03 §§8–11 and C06 Darwin: retained real grants precede later live queries and children.
    private static void ResearchCompletionRetainedOrder()
    {
        foreach (int source in new[] { 0, 1 })
            foreach (bool preserve in new[] { false, true })
            {
                var f = new ResearchFlowFixture(retainGrants: true); f.Enable(0, 1, 54);
                f.World.Civilizations[1].Researching = 54; f.World.Civilizations[1].ResearchProgress = 73; f.NoticeMode = preserve ? 2 : 0;
                using var op = f.Flow.Complete(1, source, "complete"); var pending = op.Resume();
                Equal((short)(preserve ? 73 : 0), f.World.Civilizations[1].ResearchProgress); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
                if (source != 0)
                {
                    Equal(true, pending is ResearchDecision.Grant { Decision: ResearchGrantDecision.Government });
                    Equal(0, f.Completions.Count); Equal((byte)0, f.World.Globals.FirstDiscoverer[54]); Equal(pending, op.Resume());
                    f.World.Civilizations[1].ResearchProgress = 17;
                    pending = op.Resume(new(0)); Equal((short)17, f.World.Civilizations[1].ResearchProgress);
                }
                Equal(true, pending is ResearchChoiceNotice); Equal(1, f.Completions.Count); Equal((byte)1, f.World.Globals.FirstDiscoverer[54]);
                var government = op.Resume(new(0)); Equal(true, government is ResearchDecision.Grant { Decision: ResearchGrantDecision.Government });
                Equal((short)0, f.World.Civilizations[1].Researching); Equal(false, op.Finished); Equal(null, op.Result);
                Equal(government, op.Resume()); Equal(1, f.Completions.Count); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
                op.Resume(new(1)); Equal(true, op.Finished); Equal(54, op.Result); Equal(Government.Anarchy, f.World.Civilizations[1].Government);
                Equal(0, f.Grant.GovernmentChoices.Count); Equal((short)0, f.World.Civilizations[1].Researching); f.Grant.Random.RequireChoicesConsumed();
            }
    }

    private static void ResearchAccumulationRetainedFreeOrder()
    {
        var f = new ResearchFlowFixture(retainGrants: true); f.Enable(0, 1, 54);
        f.World.Civilizations[1].Researching = 54; f.World.Civilizations[1].ResearchProgress = 100;
        f.World.Civilizations[1].Flags.PendingFreeAdvance = true;
        using var op = f.Flow.AddPoints(1, 1, "points"); Equal(true, op.Resume() is ResearchChoiceNotice);
        var government = op.Resume(new(0)); Equal(true, government is ResearchDecision.Grant);
        Equal(true, f.World.Civilizations[1].Flags.PendingFreeAdvance); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        Equal(government, op.Resume()); Sequence(new[] { 54 }, f.Completions.Select(n => n.Advance));
        Equal(true, op.Resume(new(1)) is ResearchChoiceNotice);
        Equal(false, f.World.Civilizations[1].Flags.PendingFreeAdvance); Sequence(new[] { 54, 0 }, f.Completions.Select(n => n.Advance));
        Equal((byte)2, f.World.Civilizations[1].NumAdvances); op.Resume(new(1)); Equal(true, op.Finished);
        Equal((short)1, f.World.Civilizations[1].Researching); Equal((short)0, f.World.Civilizations[1].ResearchProgress);
        f.Grant.Random.RequireChoicesConsumed();
    }

    private static void ResearchCompletionRetainedEmissary()
    {
        foreach (bool accept in new[] { false, true })
        {
            var choices = new List<Choice> { Choice.Create(new("V03.emissary", "complete/grant"), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0)) };
            if (accept) choices.Add(GreetingChoice("complete/grant/emissary/greet"));
            var f = new ResearchFlowFixture(new GrantFixture(choices.ToArray()), retainGrants: true);
            f.World.Civilizations[2].Researching = 54; f.Grant.Data.AddCity(0, 2, new(10, 8));
            int production = 0; f.ChooseProduction = _ => { ++production; return 2; };
            f.Retained!.FailClose = true;
            using var op = f.Flow.Complete(2, 0, "complete"); var pending = op.Resume();
            Equal(true, pending is ResearchDecision.Grant { Decision: ResearchGrantDecision.Emissary });
            Equal(0, production); Equal((byte)1, f.World.Civilizations[2].NumAdvances); Equal((byte)0, f.World.Globals.FirstDiscoverer[54]);
            Equal(pending, op.Resume()); op.Resume(new(accept ? 0 : 1)); Equal(true, op.Finished); Equal(54, op.Result);
            Equal(1, production); Equal(1, f.Retained.Closed); Equal(1, op.PresentationFailures.Count);
            Equal(0, f.Completions.Count); Equal(accept, f.Grant.Order.Contains("gape:54")); f.Grant.Random.RequireChoicesConsumed();
        }
    }

    private static void ResearchFlowPrivateGovernmentAndFailure()
    {
        using (var runtime = new SessionRuntime(new(), new(274)))
        {
            var f = new ResearchFlowFixture(new GrantFixture(new RevelationFixture(runtime.Process, runtime.World)), retainGrants: true);
            f.Enable(0, 54); f.World.Civilizations[1].Researching = 54;
            ParticipantId ownerSeat = default, otherSeat = default;
            runtime.RegisterCommand(new(new(1, "test.research-flow", "Authored retained completion", AccessClass.Player, new([]), new([new("advance")])),
                (_, _, _) => new(), _ => new ResearchFlowOperation(f.Flow.Complete(1, 1, "complete"), c => c == 1 ? ownerSeat : otherSeat)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var owner = SessionClient(runtime);
            ownerSeat = owner.Identity.Participant; otherSeat = actor.Identity.Participant;
            var id = actor.Submit(new("test.research-flow", new([]))).Correlation.Command; runtime.AdvanceOne();
            var grant = RequiredInteraction(owner, id); Equal(null, RequiredResult(actor, id).Interaction); Equal(false, actor.Reply(grant, 1).Accepted);
            Equal(true, owner.Reply(grant, 0).Accepted); runtime.AdvanceOne();
            var selection = RequiredInteraction(owner, id); Equal(false, owner.Cancel(id).Accepted); Equal(true, owner.Reply(selection, 0).Accepted); runtime.AdvanceOne();
            var government = RequiredInteraction(owner, id); Equal(false, actor.Reply(government, 1).Accepted);
            Equal(true, owner.Reply(government, 1).Accepted); runtime.AdvanceOne();
            var result = RequiredResult(actor, id); Equal(CommandStatus.Completed, result.Status); Equal(new CommandValue.Integer(54), result.Result.Single().Value);
            Equal(Government.Anarchy, f.World.Civilizations[1].Government); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        }
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new ResearchFlowFixture(retainGrants: true); f.Enable(0, 54); f.World.Civilizations[1].Researching = 54;
            var context = new OperationContext(f.World, f.Grant.Data.Process, f.Random, new PresentationRandom(1, "research"),
                new(default, new(1)), default, (_, _) => { }, () => { });
            using var core = f.Flow.Complete(1, 0, "complete"); using var session = new ResearchFlowOperation(core, c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new(OperationInputKind.Choice, 0)).Status);
            Equal(CommandStatus.Completed, session.Resume(context, new(input)).Status);
            Equal(Government.Despotism, f.World.Civilizations[1].Government); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        }
        var failed = new ResearchFlowFixture(retainGrants: true); failed.Enable(0, 54); failed.World.Civilizations[1].Researching = 54;
        failed.Retained!.FailRevolution = true; using var broken = failed.Flow.Complete(1, 0, "complete"); broken.Resume(); broken.Resume(new(0));
        Throws<InvalidOperationException>(() => broken.Resume(new(1))); Throws<InvalidOperationException>(() => broken.Resume(new(0)));
        Equal(false, broken.Finished); Equal((byte)1, failed.World.Civilizations[1].NumAdvances); Equal(Government.Despotism, failed.World.Civilizations[1].Government);
        var stopped = new ResearchFlowFixture(retainGrants: true); stopped.Enable(0, 54); stopped.World.Civilizations[1].Researching = 54;
        stopped.World.Civilizations[1].Flags.PendingFreeAdvance = true;
        using var disposed = stopped.Flow.AddPoints(1, 32000, "points"); disposed.Resume(); disposed.Resume(new(0)); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(new(1))); Equal(true, stopped.World.Civilizations[1].Flags.PendingFreeAdvance);
        Equal((byte)1, stopped.World.Civilizations[1].NumAdvances); Equal(false, disposed.Finished);
    }

    private static void DarwinRetainedGovernmentOrder()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new CityProductionFixture(retainGrants: true); f.Research.Enable(0, 1, 54); f.World.Civilizations[1].Researching = 54;
            f.Units.Data.AddCity(1, 2, new(14, 10)); f.World.Entities.Cities.Storage[1].Production = -(39 + WonderIds.Darwin);
            f.Rules.Terrain[(int)Terrain.Grassland] = f.Rules.Terrain[(int)Terrain.Grassland] with
            { Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
            using var op = f.Start(39 + WonderIds.Darwin); Equal(true, op.Resume() is ResearchChoiceNotice);
            var pending = op.Resume(new(0)); Equal(true, pending is ResearchDecision.Grant { Decision: ResearchGrantDecision.Government });
            Equal(pending, op.Resume()); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
            Equal((short)0, f.City.ShieldBox); Equal((short)0, f.World.Globals.WonderCity[WonderIds.Darwin]); Equal(0, f.Choices.Count);
            Sequence(new[] { 54 }, f.Research.Completions.Select(n => n.Advance));
            if (dispose)
            {
                op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(1))); Equal(false, op.Finished);
                Equal((byte)1, f.World.Civilizations[1].NumAdvances); continue;
            }
            Equal(true, op.Resume(new(1)) is ResearchChoiceNotice); Sequence(new[] { 54, 0 }, f.Research.Completions.Select(n => n.Advance));
            Equal((byte)2, f.World.Civilizations[1].NumAdvances); Equal(0, f.Choices.Count);
            Equal(null, op.Resume(new(1))); Equal(true, op.Finished); Equal(Government.Anarchy, f.World.Civilizations[1].Government);
            Equal(1, f.Choices.Count); Equal((short)1, f.World.Civilizations[1].Researching); Equal(false, f.City.Flags.DefenderLost);
        }
    }
}
