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
    // V03 §§5–6: informational goals retain their own replies, without selecting or granting.
    private static void ResearchGoalRetainedHelpAndLiveList()
    {
        var f = new ResearchFlowFixture(); f.Enable(0, 1, 12, 13);
        f.Rules.Definitions.Units[2] = f.Rules.Definitions.Units[2] with { Prerequisite = (AdvanceReference)12 };
        short target = f.World.Civilizations[1].Researching;
        using var op = f.Goals.Begin(1);
        var first = op.Resume(); Equal(first, op.Resume());
        Equal(ResearchTopic.Unit, ((ResearchGoalNotice)op.Resume(new(0, 2))!).Category);
        var help = (ResearchGoalHelp)op.Resume(new(2, 1))!; Equal(12, help.Prerequisite);
        f.Rules.Definitions.Units[2] = f.Rules.Definitions.Units[2] with { Prerequisite = (AdvanceReference)13 };
        Equal(help, op.Resume());
        var page = (ResearchEncyclopediaNotice)op.Resume(new(1))!; Equal(12, page.Identity); Equal(ResearchTopic.Advance, page.Topic);
        Equal(page, op.Resume()); var rebuilt = (ResearchGoalNotice)op.Resume(new(0))!;
        Equal(13, rebuilt.Options.Single().Prerequisite);
        var path = (ResearchPathNotice)op.Resume(new(2))!; Equal(13, path.Goal); Equal(false, op.Completed);
        Equal(path, op.Resume()); op.Resume(new(0)); Equal(true, op.Completed); Equal(null, op.Resume(new(9)));
        Equal(target, f.World.Civilizations[1].Researching); Equal(0UL, f.Random.Draws);
        Equal(0, f.GoalNotices.Count + f.Paths.Count + f.Order.Count);
    }

    private static void ResearchGoalCancellationFailureAndDisposal()
    {
        var f = new ResearchFlowFixture(); f.Enable(0);
        using (var op = f.Goals.Begin(1))
        {
            op.Resume(); Equal(null, op.Resume(new(-1, 3))); Equal(true, op.Completed);
        }
        using (var op = f.Goals.Begin(1))
        {
            op.Resume(); f.DiplomacyActive = true;
            var list = (ResearchGoalNotice)op.Resume(new(0, 1))!; Equal(false, list.Icons);
            Equal(null, op.Resume(new(0, 2))); Equal(true, op.Completed); // Empty unit list.
        }
        using (var op = f.Goals.Begin(1))
        {
            op.Resume(); op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(new(0)));
        }
        using (var op = f.Goals.Begin(1))
        {
            op.Resume(); Throws<IndexOutOfRangeException>(() => op.Resume(new(100)));
            Throws<InvalidOperationException>(() => op.Resume());
        }
        Equal(0UL, f.Random.Draws); Equal(0, f.GoalNotices.Count + f.Order.Count);
        f.World.Globals.Multiplayer = GameMode.Tcp;
        short target = f.World.Civilizations[1].Researching;
        using (var op = f.Goals.Begin(1))
        {
            Equal(true, op.Resume() is ResearchGoalNotice);
            Equal(null, op.Resume(new(-1))); Equal(true, op.Completed);
        }
        Equal(target, f.World.Civilizations[1].Researching); Equal(0UL, f.Random.Draws);
        var synchronous = new ResearchFlowFixture(); synchronous.Enable(0);
        using var selection = synchronous.Select(); selection.Resume();
        Throws<InvalidOperationException>(() => selection.Resume(new(0, 2))); // Missing synchronous reply.
        Throws<InvalidOperationException>(() => selection.Resume()); Equal(1, synchronous.GoalNotices.Count);
    }

    private static void ResearchSelectionRetainsGoalAndRecommendation()
    {
        var f = new ResearchFlowFixture(); f.Enable(0, 1); f.World.Globals.GameOptions.Advice = true;
        using var selection = f.Select(); var first = (ResearchChoiceNotice)selection.ResumeDecision()!; ulong draws = f.Random.Draws;
        Equal(true, selection.ResumeDecision(new(0, 2)) is ResearchDecision.Goal { Decision: ResearchGoalNotice });
        var path = selection.ResumeDecision(new(1)); Equal(true, path is ResearchDecision.Goal { Decision: ResearchPathNotice });
        Equal(path, selection.ResumeDecision()); Equal(draws, f.Random.Draws);
        var rebuilt = (ResearchChoiceNotice)selection.ResumeDecision(new(0))!; Equal(first.Recommendation, rebuilt.Recommendation);
        Equal(true, selection.ResumeDecision(new(1, 1)) is ResearchDecision.Goal { Decision: ResearchEncyclopediaNotice { Identity: 1 } });
        Equal(true, selection.ResumeDecision(new(0)) is ResearchChoiceNotice);
        selection.ResumeDecision(new(1)); Equal(true, selection.Finished); Equal(1, selection.Result);
        Equal(draws, f.Random.Draws); Equal(0, f.GoalNotices.Count + f.Paths.Count);
        Equal(false, f.Order.Any(s => s.StartsWith("pedia:", StringComparison.Ordinal)));
        selection.Dispose(); Throws<ObjectDisposedException>(() => selection.ResumeDecision());
    }

    private static void ResearchGoalSessionPrivacyAndHelp()
    {
        using var runtime = new SessionRuntime(new(), new(279));
        var f = new ResearchFlowFixture(new GrantFixture(new RevelationFixture(runtime.Process, runtime.World)), retainGrants: true);
        f.Enable(0, 1); f.World.Civilizations[1].Researching = -1;
        f.Rules.Definitions.Units[2] = f.Rules.Definitions.Units[2] with { Prerequisite = (AdvanceReference)1 };
        ParticipantId seat = default;
        runtime.RegisterCommand(new(new(1, "test.goal-flow", "Authored research", AccessClass.Player, new([]), new([new("advance")])),
            (_, _, _) => new(), _ => new ResearchFlowOperation(f.Flow.AddPoints(1, 1, "goal"), _ => seat)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var other = SessionClient(runtime, 2); seat = actor.Identity.Participant;
        var command = actor.Submit(new("test.goal-flow", new([]))).Correlation.Command; runtime.AdvanceOne();
        var choice = RequiredInteraction(actor, command); Equal(true, actor.Reply(choice, 100).Accepted); runtime.AdvanceOne();
        var list = RequiredInteraction(actor, command); Equal(false, other.Reply(list, 0).Accepted);
        Equal(false, actor.Reply(choice, 0).Accepted); Equal(true, actor.Reply(list, 200).Accepted); runtime.AdvanceOne();
        var units = RequiredInteraction(actor, command); Equal(true, actor.Reply(units, 1002).Accepted); runtime.AdvanceOne();
        var help = RequiredInteraction(actor, command); Equal(true, actor.Reply(help, 0).Accepted); runtime.AdvanceOne();
        var page = RequiredInteraction(actor, command); var text = RequiredResult(actor, command).Interaction!.Spec.Prompt;
        Equal(true, text.Contains("Unit 2", StringComparison.Ordinal)); Equal(true, text.Contains("Advance 1", StringComparison.Ordinal));
        Equal(true, actor.Reply(page, 0).Accepted); runtime.AdvanceOne();
        Equal(true, actor.Cancel(command).Accepted); runtime.AdvanceOne(); // Leave only the informational goal browser.
        choice = RequiredInteraction(actor, command); Equal(false, actor.Cancel(command).Accepted);
        Equal(true, actor.Reply(choice, 1).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal((short)1, f.World.Civilizations[1].Researching);
        Equal(false, f.World.Civilizations[1].Advances.Contains(1)); Equal(0, f.GoalNotices.Count + f.Paths.Count);
    }

    private static void ResearchGoalReferenceAndTimeout()
    {
        var f = new ResearchFlowFixture(); f.Enable(0, 1); f.World.Civilizations[1].Researching = -1;
        f.Rules.Definitions.Improvements[1] = new() { Identity = new("custom", "Authored workshop"), Prerequisite = (AdvanceReference)1, Upkeep = 3 };
        var context = new OperationContext(f.World, f.Grant.Data.Process, f.Random, new PresentationRandom(1, "reference"),
            new(new(1), new(1)), default, (_, _) => { }, () => { });
        using var op = new ResearchFlowOperation(f.Flow.AddPoints(1, 1, "reference"), c => new((ulong)c));
        op.Resume(context, new()); op.Resume(context, new(OperationInputKind.Choice, 100));
        op.Resume(context, new(OperationInputKind.Choice, 201)); op.Resume(context, new(OperationInputKind.Choice, 1001));
        var page = op.Resume(context, new(OperationInputKind.Choice, 0));
        Equal(true, page.Interaction!.Prompt.Contains("Authored workshop", StringComparison.Ordinal));
        Equal(true, page.Interaction.Prompt.Contains("Maintenance: 3 gold", StringComparison.Ordinal));
        var list = op.Resume(context, new(OperationInputKind.Timeout));
        Equal(true, list.Interaction!.Prompt.StartsWith("Choose a research goal", StringComparison.Ordinal));
        var choice = op.Resume(context, new(OperationInputKind.Timeout)); Equal("Choose the next advance.", choice.Interaction!.Prompt);
        page = op.Resume(context, new(OperationInputKind.Choice, 1001));
        Equal(true, page.Interaction!.Prompt.Contains("Unlocks: Authored workshop", StringComparison.Ordinal));
        Equal("Choose the next advance.", op.Resume(context, new(OperationInputKind.Cancel)).Interaction!.Prompt);
        Equal(CommandStatus.Completed, op.Resume(context, new(OperationInputKind.Choice, 1)).Status);
        Equal(false, f.World.Civilizations[1].Advances.Contains(1)); Equal(0, f.GoalNotices.Count + f.Paths.Count);
    }
}
