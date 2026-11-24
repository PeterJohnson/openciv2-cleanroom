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
    // C05 retirement / U01 loss / V01 elimination / V06 incident: specification-derived.
    // A foreign homed settler wins selection; its loss interrupts another civilization's
    // mission and leaves the actual human victim's decision pending before C05 restarts.
    private static (AssessmentFixture F, IncidentFixture Incidents, GameplayRandom Restart,
        RetirementNotices Notices, CityRetirement Service) RetainedCityRetirementFixture(RevelationFixture? data = null)
    {
        var (f, incidents, restart) = RetainedAssessmentFixture(true, data, "city-retirement/retire/2/unit/2/check");
        f.Data.AddCity(0, 4, new(8, 10));
        f.Data.AddUnit(3, 4, 4, new(8, 10));
        f.Data.Unit(2).Home = f.Data.Unit(3).Home = new(0);
        f.World.Civilizations[4].Government = Government.Republic;
        f.World.Globals.HumanPlayers = f.World.Globals.HumanPlayers.With(4);
        f.World.Calculation.Shields = 0;
        var notices = new RetirementNotices();
        return (f, incidents, restart, notices, new(f.World, f.Rules, f.Lifecycle.Deletion, f.Lifecycle.Loss, notices));
    }

    private static void RetirementRetainedNestedRestart()
    {
        foreach (int reply in new[] { 0, 1 })
        {
            var (f, incidents, restart, notices, service) = RetainedCityRetirementFixture();
            using var op = service.Begin(new(0), f.Lifecycle.Elimination, incidents.Incidents, "city-retirement");
            var pending = op.ResumeDecision();
            Equal(2, ((EspionagePretext)pending!).Victim.Civilization);
            Equal(false, op.Completed); Equal(false, f.Data.Unit(2).Token.Occupied);
            Equal(true, f.Data.Unit(3).Token.Occupied); Equal(0, notices.Removed.Count);
            Equal(1, f.World.Calculation.ShieldSupport); Equal(0, f.Lifecycle.Restarts);
            Sequence(new[] { 2 }, notices.Shortages);
            int count = f.Lifecycle.Order.Count;
            Equal(pending, op.ResumeDecision()); Equal(count, f.Lifecycle.Order.Count);
            // Captured owner/government survive a real decision; human membership stays live.
            f.World.Entities.Cities.Storage[0].Owner = new(3);
            f.World.Civilizations[4].Government = Government.Despotism;
            f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            f.World.TurnLoopActive = false;
            Equal(null, op.ResumeDecision(new(reply))); Equal(true, op.Completed);
            Equal(false, f.Data.Unit(3).Token.Occupied);
            Sequence(new[] { (2, true), (3, true) }, notices.Removed);
            Sequence(new[] { 2 }, notices.Shortages);
            Equal(0, f.World.Calculation.ShieldSupport); Equal(0, f.World.Calculation.SupportedUnits);
            Equal(reply == 1, f.World.Civilizations[3].Treaties[2].War);
            Equal(1, f.Lifecycle.Restarts); Equal(0, f.Lifecycle.Checks);
            Equal(true, f.Lifecycle.Redraws.Contains(new(10, 10)));
            count = f.Lifecycle.Order.Count; Equal(null, op.ResumeDecision(new(1))); Equal(count, f.Lifecycle.Order.Count);
            f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
        }
    }

    private static void RetirementRetainedBranchesAndFailure()
    {
        var f = new EliminationFixture(data: WorkingFixture()); var notices = new RetirementNotices();
        f.World.Globals.HumanPlayers = default; f.World.Globals.Turn = 16;
        f.World.Civilizations[1].Government = Government.Republic;
        f.World.Entities.Cities.Storage[0].Flags.Disorder = true;
        f.Data.AddUnit(0, 1, 4, new(10, 6)); f.Data.AddUnit(1, 1, 4, new(18, 6));
        f.Data.Unit(0).Home = f.Data.Unit(1).Home = new(0); f.World.Calculation.Shields = 9;
        var incidents = new IncidentFixture(f.Random, f.Data); f.World.Globals.Turn = 16;
        var service = new CityRetirement(f.World, f.Data.Rules, f.Deletion, f.Loss, notices);
        using (var op = service.Begin(new(0), f.Elimination, incidents.Incidents, "affordable"))
        {
            Equal(null, op.ResumeDecision()); Equal(true, op.Completed);
            Equal((1, false), notices.Removed.Single()); Equal(0, f.Redraws.Count);
            Equal(false, f.World.Entities.Cities.Storage[0].Flags.Disorder);
            Equal(true, f.Order.Contains("unit:18")); Equal(true, f.Order.Contains("home:0"));
        }
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Calculation.Shields = 0;
        notices.FailNotice = true;
        using var failed = service.Begin(new(0), f.Elimination, incidents.Incidents, "failure");
        Throws<IOException>(() => failed.ResumeDecision()); Equal(true, f.Data.Unit(0).Token.Occupied);
        notices.FailNotice = false;
        Throws<InvalidOperationException>(() => failed.ResumeDecision()); Equal(true, f.Data.Unit(0).Token.Occupied);
        f.Random.RequireChoicesConsumed();
    }

    private static void RetirementRetainedDisposalAndCancellation()
    {
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var (f, incidents, restart, notices, service) = RetainedCityRetirementFixture();
            var effects = new List<string>(); var context = OutcomeContext(f.Data, effects);
            using var op = new CityRetirementOperation(service.Begin(new(0), f.Lifecycle.Elimination,
                incidents.Incidents, "city-retirement"), c => new((ulong)c));
            Equal(new ParticipantId(2), op.Resume(context, new()).Interaction!.Recipient!.Value);
            var result = op.Resume(context, new(kind)); Equal(CommandStatus.Completed, result.Status);
            Equal(false, f.World.Civilizations[3].Treaties[2].War);
            Equal(2, notices.Removed.Count); Equal(1, effects.Count(e => e == "city.retirement.completed"));
            Equal(result, op.Resume(context, new())); Equal(1, effects.Count(e => e == "city.retirement.completed"));
            f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
        }
        var (disposed, i, _, n, s) = RetainedCityRetirementFixture();
        using var core = s.Begin(new(0), disposed.Lifecycle.Elimination, i.Incidents, "city-retirement");
        Equal(true, core.ResumeDecision() is EspionagePretext); core.Dispose(); core.Dispose();
        Throws<ObjectDisposedException>(() => core.ResumeDecision(new(0)));
        Equal(false, disposed.Data.Unit(2).Token.Occupied); Equal(true, disposed.Data.Unit(3).Token.Occupied);
        Equal(0, n.Removed.Count); Equal(0, disposed.Lifecycle.Restarts);
        Equal(false, disposed.Lifecycle.Redraws.Contains(new(10, 10)));
    }

    private static void RetirementRetainedPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(265));
        var (f, incidents, restart, notices, service) = RetainedCityRetirementFixture(CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.city-retirement", "City retirement", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CityRetirementOperation(service.Begin(new(0), f.Lifecycle.Elimination,
                incidents.Incidents, "city-retirement"), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 4); var human = SessionClient(runtime, 2); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.city-retirement", new([]))).Correlation.Command; runtime.AdvanceOne();
        var pending = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(0, notices.Removed.Count); Equal(false, actor.Reply(pending, 1).Accepted);
        Equal(true, human.Reply(pending, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, human.Reply(pending, 1).Accepted);
        Equal(true, f.World.Civilizations[3].Treaties[2].War); Equal(2, notices.Removed.Count);
        Equal(1, RequiredResult(actor, command).Committed.Count(e => e.Name == "city.retirement.completed"));
        f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
    }
}
