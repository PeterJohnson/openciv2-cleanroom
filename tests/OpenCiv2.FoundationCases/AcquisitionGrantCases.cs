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
    // V03 §12, V06 §5, W06 hut entry and U02 finish: actual grants suspend inside their owners.
    private static void CapturedTechnologyRetainedGrant()
    {
        foreach (int answer in new[] { 0, 1 })
        {
            var f = new TheftFixture(retainGrants: true); f.Known(AdvanceIds.Monarchy);
            using var op = f.Captured.Begin(1, 2, "take");
            Equal(54, ((CapturedTechnologyChoice)op.Resume()!).Recommendation);
            var pending = op.Resume(new(54)); Equal(true, pending is CapturedTechnologyDecision.Grant { Decision: ResearchGrantDecision.Government });
            Equal(pending, op.Resume()); Equal(null, op.Result); Equal(false, op.Finished);
            Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)2, f.World.Civilizations[1].AdvanceSource[54]);
            Equal(null, op.Resume(new(answer, 1))); // This is a government reply, not technology Help.
            Equal(0, f.GoalNotices.Count); Equal(54, op.Result); Equal(true, op.Finished);
            Equal(answer == 1 ? Government.Anarchy : Government.Despotism, f.World.Civilizations[1].Government);
            Equal((byte)1, f.World.Civilizations[1].NumAdvances); f.Random.RequireChoicesConsumed();
        }
        var stopped = new TheftFixture(retainGrants: true); stopped.Known(54);
        using var disposed = stopped.Captured.Begin(1, 2, "take"); disposed.Resume(); disposed.Resume(new(54)); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(new(1))); Equal(false, disposed.Finished); Equal(null, disposed.Result);
        Equal((byte)1, stopped.World.Civilizations[1].NumAdvances); Equal(Government.Despotism, stopped.World.Civilizations[1].Government);
        var failed = new TheftFixture(retainGrants: true); failed.Known(54); failed.Retained!.FailRevolution = true;
        using var broken = failed.Captured.Begin(1, 2, "take"); broken.Resume(); broken.Resume(new(54));
        Throws<InvalidOperationException>(() => broken.Resume(new(1))); Throws<InvalidOperationException>(() => broken.Resume(new(0)));
        Equal(null, broken.Result); Equal((byte)1, failed.World.Civilizations[1].NumAdvances);
    }

    private static void TheftRetainedGrantOrdering()
    {
        foreach (bool targeted in new[] { false, true })
        {
            var choices = new List<Choice> { TheftStart(54) };
            if (targeted) choices.Add(EscapeChoice("theft/capture", 3, 1));
            choices.Add(EscapeChoice("theft/escape", 2, targeted ? 1 : 0, targeted));
            choices.Add(IncidentChoice(0, call: "theft/escape/incident"));
            var f = new TheftFixture(choices, retainGrants: true); f.Known(54);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Civilizations[2].Treaties[1].Peace = true;
            using var op = f.Begin(); DamageQuestion(op.Resume(), CityEspionageQuestion.SpecificTheft);
            var pending = op.Resume(new(targeted ? 1 : 0));
            if (targeted) { Equal(true, pending is CityEspionageDecision.Technology); pending = op.Resume(new(54)); }
            Equal(true, pending is CityEspionageDecision.Grant { Decision: ResearchGrantDecision.Government });
            Equal(pending, op.Resume()); Equal(false, f.City.Flags.TechnologyStolen); Equal(true, f.Data.Unit(0).Token.Occupied);
            Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal(0, f.Damage.Lifecycle.SpyNotices.Count);
            var incident = op.Resume(new(1)); Equal(true, incident is CityEspionageDecision.Lifecycle);
            Equal(!targeted, f.City.Flags.TechnologyStolen); Equal(false, f.Data.Unit(0).Token.Occupied);
            Equal(Government.Anarchy, f.World.Civilizations[1].Government); Equal(false, op.Completed);
            op.Resume(new(0)); Equal(true, op.Completed); Equal(true, f.City.Flags.TechnologyStolen);
            Equal((byte)1, f.World.Civilizations[1].NumAdvances); f.Random.RequireChoicesConsumed();
        }
    }

    private static void AcquisitionGrantPrivateSessions()
    {
        foreach (bool capture in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(272));
            var data = CreationFixture(new(runtime.Process, runtime.World));
            var taken = capture ? new CityCaptureFixture(data: data, retainGrants: true) : null;
            var f = taken?.Theft ?? new TheftFixture([TheftStart(54)], data, retainGrants: true);
            f.Rules.Terrain[(int)Terrain.Grassland] = f.Rules.Terrain[(int)Terrain.Grassland] with
            { Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
            f.Known(54); f.Data.Unit(0).Type = new(46);
            ParticipantId actorSeat = default, victimSeat = default; int windows = 0;
            runtime.RegisterCommand(new(new(1, "test.acquire", "Authored acquisition", AccessClass.Player, new([]), new([new(capture ? "city" : "fallback")])),
                (_, _, _) => new(), _ => capture
                    ? new CityCaptureOperation(taken!.Begin(), c => c == 1 ? actorSeat : victimSeat, request =>
                    { ++windows; return new("Captured city", actorSeat, new CommandValue.City(request.City), new([0])); })
                    : new CityTechnologyTheftOperation(f.Begin(), c => c == 1 ? actorSeat : victimSeat)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2);
            actorSeat = actor.Identity.Participant; victimSeat = victim.Identity.Participant;
            var id = actor.Submit(new("test.acquire", new([]))).Correlation.Command; runtime.AdvanceOne();
            if (capture) { var choice = RequiredInteraction(actor, id); Equal(true, actor.Reply(choice, 54).Accepted); runtime.AdvanceOne(); }
            var government = RequiredInteraction(actor, id); Equal(null, victim.Result(id));
            Equal(false, victim.Reply(government, 1).Accepted); Equal(0, windows);
            Equal((byte)1, f.World.Civilizations[1].NumAdvances);
            Equal(true, actor.Reply(government, 1).Accepted); runtime.AdvanceOne();
            if (capture) { Equal(1, windows); Equal(true, actor.Reply(RequiredInteraction(actor, id), 0).Accepted); runtime.AdvanceOne(); }
            Equal(CommandStatus.Completed, RequiredResult(actor, id).Status); Equal(Government.Anarchy, f.World.Civilizations[1].Government);
            Equal((byte)1, f.World.Civilizations[1].NumAdvances); f.Random.RequireChoicesConsumed();
        }
    }

    private static void HutRetainedGrantAndCancellation()
    {
        foreach (var input in new[] { OperationInputKind.Choice, OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var f = new HutFixture(HutStart(4, HutDraw("scroll-start", 100, 54)), CreationFixture(), retainGrants: true);
            f.Data.Rules.Terrain[(int)Terrain.Grassland] = f.Data.Rules.Terrain[(int)Terrain.Grassland] with
            { Work = new(new(TerrainResult.ImproveInPlace, 1), new(TerrainResult.ImproveInPlace, 2)) };
            f.Data.Rules.Advances[54] = f.Data.Rules.Advances[54] with { Enabled = true };
            var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(1, "hut"),
                new(default, new(3)), default, (_, _) => { }, () => { });
            using var core = f.Begin(); using var session = new HutOperation(core, c => new((ulong)c),
                _ => throw new InvalidOperationException("Scrolls do not create a city"));
            Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
            Equal(HutNoticeKind.Scrolls, f.Notices.Single().Kind); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
            Equal(false, core.Completed); Equal(CommandStatus.AwaitingInteraction, session.Resume(context, new()).Status);
            Equal(CommandStatus.Completed, session.Resume(context, new(input, 1)).Status);
            Equal(input == OperationInputKind.Choice ? Government.Anarchy : Government.Despotism, f.World.Civilizations[1].Government);
            Equal(1, f.Notices.Count); Equal((byte)1, f.World.Globals.FirstDiscoverer[54]); f.Random.RequireChoicesConsumed();
        }
        var stopped = new HutFixture(HutStart(4, HutDraw("scroll-start", 100, 54)), retainGrants: true);
        stopped.Data.Rules.Advances[54] = stopped.Data.Rules.Advances[54] with { Enabled = true };
        using var op = stopped.Begin(); Equal(true, op.Resume() is HutDecision.Grant); op.Dispose();
        Throws<ObjectDisposedException>(() => op.Resume(1)); Equal((byte)1, stopped.World.Civilizations[1].NumAdvances);
        Equal((byte)0, stopped.World.Globals.FirstDiscoverer[54]); Equal(false, op.Completed);
    }

    private static void MovementRetainedHutGrant()
    {
        using var runtime = new SessionRuntime(new(), new(273));
        var choices = HutStart(4, HutDraw("scroll-start", 100, 54)).Select(c => c with { Key = c.Key with { Call = "move/0/finish/hut" } }).ToList();
        var f = new DispatcherFixture(choices, CreationFixture(new(runtime.Process, runtime.World)), retainGrants: true);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Civilizations[1].Government = Government.Despotism;
        f.Data.Rules.Advances[54] = f.Data.Rules.Advances[54] with { Enabled = true };
        var target = Enumerable.Range(4, 28).SelectMany(x => Enumerable.Range(4, 16).Select(y => new MapPoint(x, y)))
            .First(p => f.World.Map.Geometry.InBounds(p) && f.World.Map.HutCandidate(p) && f.Data.Stacks.FirstAt(p) < 0 && f.World.Map.CityOwner(p) is null);
        f.World.Map.TileAt(target).Owner = null; f.Data.Stacks.Relocate(0, new(target.X - 2, target.Y), false);
        Equal(true, f.World.Map.Hut(target)); f.Data.Unit(0).LastDirection = 99;
        ParticipantId actorSeat = default, otherSeat = default;
        runtime.RegisterCommand(new(new(1, "test.move-grant", "Authored movement acquisition", AccessClass.Player, new([]), new([new("result")])),
            (_, _, _) => new(), _ => new MovementOperation(f.Begin(), c => c == 1 ? actorSeat : otherSeat,
                _ => throw new InvalidOperationException("No mission visit"), _ => throw new InvalidOperationException("No intelligence"),
                _ => throw new InvalidOperationException("No capture"), _ => throw new InvalidOperationException("No hut city"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var other = SessionClient(runtime, 2);
        actorSeat = actor.Identity.Participant; otherSeat = other.Identity.Participant;
        var id = actor.Submit(new("test.move-grant", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(actor, id); Equal(null, other.Result(id));
        Equal(target, f.Data.Unit(0).Position); Equal((byte)3, f.Data.Unit(0).MovesUsed); Equal((byte)99, f.Data.Unit(0).LastDirection);
        Equal(true, f.World.UnitMovementActive); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        Equal(false, other.Reply(prompt, 1).Accepted); Equal(true, actor.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, id).Status); Equal(false, f.World.UnitMovementActive);
        Equal((byte)1, f.Data.Unit(0).LastDirection); Equal(Government.Anarchy, f.World.Civilizations[1].Government);
        Equal(1, f.Finish.Huts.Count); Equal(2, f.Relocation.Publications.Count); f.Capture.Life.Random.RequireChoicesConsumed();
    }

    private static void HutGrantEmissaryRouting()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var choices = new List<Choice> { HutDraw("outcome", 5, 4), HutDraw("scroll-start", 100, 54),
                Choice.Create(new("V03.emissary", "hut/scroll"), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0)) };
            var f = new HutFixture(choices, retainGrants: true);
            f.Data.Unit(0).Owner = new(2); f.Movement.LocalHuman = false; f.World.Civilizations[2].CityCount = 0;
            f.Data.Rules.Advances[54] = f.Data.Rules.Advances[54] with { Enabled = true };
            var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(1, "hut"),
                new(default, new(3)), default, (_, _) => { }, () => { });
            using var core = f.Begin(); using var session = new HutOperation(core, c => new((ulong)c),
                _ => throw new InvalidOperationException("No city"));
            var pending = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, pending.Status);
            Equal(new ParticipantId(1), pending.Interaction!.Recipient); Equal((byte)1, f.World.Civilizations[2].NumAdvances);
            Equal(CommandStatus.Completed, session.Resume(context, new(input)).Status);
            Equal(1, f.Retained!.Closed); Equal(false, f.Grant.Order.Contains("gape:54"));
            Equal((byte)2, f.World.Globals.FirstDiscoverer[54]); Equal(false, f.World.Diplomacy.MeetingActive); f.Random.RequireChoicesConsumed();
        }
    }
}
