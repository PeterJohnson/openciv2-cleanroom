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
    // A01 §§4–7: real V01 elimination interrupts a foreign mission, whose last-settler
    // loss enters another V01 check and leaves a human victim's real V06 choice pending.
    private static (AssessmentFixture F, IncidentFixture Incidents, GameplayRandom Restart) RetainedAssessmentFixture(
        bool retirement, RevelationFixture? data = null)
    {
        string call = retirement ? "assessment/retire/2/unit/2/check" : "assessment/eliminate";
        var f = new AssessmentFixture(data, EscapeChoice(call + "/spy/1", 2, 0), IncidentChoice(0, call: call + "/spy/1/incident"));
        f.Ai(); f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3); f.World.Multiplayer.LocalHuman = 2;
        f.Rule(4, RuleRole.Attack); f.Rule(47, RuleRole.Settle);
        f.Data.AddUnit(0, 3, 4, new(20, 10)); f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).Counter = 2;
        f.Data.AddUnit(1, 2, 47, new(22, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1;
        f.World.Civilizations[2].CityCount = 0; f.World.Civilizations[2].UnitsActive[47] = 1;
        DiplomaticRelations.Set(f.World, 3, 2, new() { Peace = true });
        DiplomaticRelations.Set(f.World, 2, 1, new() { Peace = true });
        if (retirement)
        {
            f.Rule(0, RuleRole.Settle);
            f.Rules.Units[0] = f.Rules.Units[0] with { ObsoleteBy = (AdvanceReference)3 };
            f.Civ.Advances = default(AdvanceSet).With(3);
            f.Data.AddUnit(2, 1, 0, new(10, 10));
        }
        var restart = CreationRandom(CreationAttitudes());
        f.Lifecycle.Recreate = owner => Creation(f.Data, owner, restart);
        var incidents = new IncidentFixture(f.Random, f.Data);
        f.World.Globals.Turn = 16;
        return (f, incidents, restart);
    }

    private static void AssessmentRetainedElimination()
    {
        foreach (int reply in new[] { 0, 1 })
        {
            var (f, incidents, restart) = RetainedAssessmentFixture(false);
            using var op = f.Assessment.Begin(1, "assessment", incidents.Incidents);
            var pending = op.ResumeDecision();
            Equal(2, ((EspionagePretext)pending!).Victim.Civilization);
            Equal(false, op.Completed); Equal(1, f.Lifecycle.Destructions.Count); // Nested last-settler checks use killer -1.
            Equal(false, f.Data.Unit(0).Token.Occupied || f.Data.Unit(1).Token.Occupied);
            Equal(0, f.Lifecycle.Restarts); Equal(0, f.Lifecycle.Checks);
            int count = f.Lifecycle.Order.Count;
            Equal(pending, op.ResumeDecision()); Equal(count, f.Lifecycle.Order.Count);
            f.World.TurnLoopActive = false; // Accepted gameplay continues after a cleared active flag.
            Equal(null, op.ResumeDecision(new(reply))); Equal(true, op.Completed);
            Equal(reply == 1, f.World.Civilizations[3].Treaties[2].War);
            Equal(EndReason.Defeat, f.World.PendingEndReason); Equal(1, f.Lifecycle.Restarts);
            count = f.Lifecycle.Order.Count; Equal(null, op.ResumeDecision(new(1))); Equal(count, f.Lifecycle.Order.Count);
            f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
        }
    }

    private static void AssessmentRetainedRetirementAndDisposal()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var (f, incidents, restart) = RetainedAssessmentFixture(true);
            using var op = f.Assessment.Begin(1, "assessment", incidents.Incidents);
            Equal(2, ((EspionagePretext)op.ResumeDecision()!).Victim.Civilization);
            Equal(false, f.Data.Unit(2).Token.Occupied); Equal((byte)0, f.Civ.UnitsActive[0]);
            Equal(false, f.Lifecycle.Redraws.Contains(new(10, 10))); Equal(0, f.Lifecycle.Checks);
            if (dispose)
            {
                op.Dispose(); Throws<ObjectDisposedException>(() => op.ResumeDecision(new(0)));
                Equal(0, f.Lifecycle.Restarts); Equal(false, op.Completed);
            }
            else
            {
                Equal(null, op.ResumeDecision(new(0))); Equal(true, op.Completed);
                Equal(true, f.Lifecycle.Redraws.Contains(new(10, 10)));
                f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
            }
            Equal(false, f.World.UnitDeletion.DestroyActive); Equal(false, f.World.UnitDeletion.Active);
        }
    }

    private static void AssessmentRetainedSearchAndFailure()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new AssessmentFixture(); var w = f.World;
            w.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            f.Data.Process.Rules.Leaders[2].Color = 2; w.Map.TileAt(new(10, 8)).Terrain = Terrain.Ocean;
            var choices = CreationAttitudes(); CreationChoice(choices, "gender", 3, 1); CreationSiteChoices(choices, dispose ? 1 : 2);
            if (!dispose) { choices[^2] = Choice.Create(new("F05.start-site.x", "create/site", 1), new SamplingLaw.PrimaryModulo(40), ChoiceCode.Primary(12)); }
            var random = CreationRandom(choices); f.Lifecycle.Recreate = owner => Creation(f.Data, owner, random);
            var incidents = new IncidentFixture(f.Random, f.Data);
            using var op = f.Assessment.Begin(2, "search", incidents.Incidents, 1);
            Equal(null, op.ResumeDecision()); Equal(false, op.Completed); Equal(1, f.Lifecycle.Restarts);
            Equal(0, w.Entities.Units.End);
            if (dispose) { op.Dispose(); Throws<ObjectDisposedException>(() => op.ResumeDecision()); }
            else
            {
                Equal(null, op.ResumeDecision()); Equal(true, op.Completed);
                Equal(new MapPoint(12, 8), f.Data.Unit(0).Position); Equal(1, f.Lifecycle.MapRefreshes);
            }
            random.RequireChoicesConsumed(); f.Random.RequireChoicesConsumed();
        }
        var failed = new AssessmentFixture(); failed.Ai();
        var effects = new IncidentFixture(failed.Random, failed.Data);
        using var fault = failed.Assessment.Begin(1, "failure", effects.Incidents);
        Throws<InvalidOperationException>(() => fault.ResumeDecision()); // Required recreation provider is missing.
        Equal(1, failed.Lifecycle.Destructions.Count); Equal(false, fault.Completed);
        failed.Lifecycle.Recreate = c => Creation(failed.Data, c, CreationRandom(CreationAttitudes()));
        Throws<InvalidOperationException>(() => fault.ResumeDecision()); Equal(1, failed.Lifecycle.Destructions.Count);
        using var remote = failed.Assessment.Begin(1, "remote", effects.Incidents);
        failed.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => remote.ResumeDecision());
        Equal(1, failed.Lifecycle.Destructions.Count);
    }

    private static void AssessmentRetainedPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(260));
        var (f, incidents, restart) = RetainedAssessmentFixture(false, CreationFixture(new(runtime.Process, runtime.World)));
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.assessment", "Assessment", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new CivilizationAssessmentOperation(f.Assessment.Begin(1, "assessment", incidents.Incidents), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 1); var human = SessionClient(runtime, 2); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.assessment", new([]))).Correlation.Command; runtime.AdvanceOne();
        var pending = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(pending, 0).Accepted); Equal(true, human.Reply(pending, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(false, human.Reply(pending, 0).Accepted);
        Equal(1, RequiredResult(actor, command).Committed.Count(e => e.Name == "civilization.assessment.completed"));
        f.Random.RequireChoicesConsumed(); restart.RequireChoicesConsumed();
    }
}
