using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class ResearchFlowFixture : IResearchChoiceEffects, IResearchCompletionEffects
    {
        public GrantFixture Grant { get; }
        public GameState World => Grant.World;
        public RuleState Rules => Grant.Data.Process.Rules;
        public GameplayRandom Random { get; } = new(37, "research-flow");
        public ResearchFlow Flow { get; }
        public ResearchGoals Goals { get; }
        public bool DiplomacyActive { get; set; }
        public int NoticeMode { get; set; }
        public CitySlot? SelectedCity { get; set; }
        public List<string> Order { get; } = [];
        public List<ResearchAdvice> AdviceNotices { get; } = [];
        public List<ResearchCompletionNotice> Completions { get; } = [];
        public List<ResearchAbilityNotice> Abilities { get; } = [];
        public List<ResearchGoalNotice> GoalNotices { get; } = [];
        public List<ResearchPathNotice> Paths { get; } = [];
        public Queue<ResearchReply> GoalReplies { get; } = new();
        public int HelpReply;
        public Action? OnCompleted, OnFirst;
        public Func<CitySlot, int> ChooseProduction = _ => 2;

        public ResearchFlowFixture(GrantFixture? grant = null)
        {
            Grant = grant ?? new();
            for (int a = 0; a < RuleLimits.Advances; ++a)
                Rules.Definitions.Advances[a] = new()
                {
                    Identity = new("a" + a, "Advance " + a),
                    AiValue = 10,
                    Prerequisites = new([AdvanceReference.None, AdvanceReference.None])
                };
            for (int u = 0; u < RuleLimits.UnitTypes; ++u)
                Rules.Definitions.Units[u] = Rules.Definitions.Units[u] with { Prerequisite = AdvanceReference.Never, Identity = new("u" + u, "Unit " + u) };
            for (int i = 0; i < RuleLimits.Improvements; ++i)
                Rules.Definitions.Improvements[i] = Rules.Definitions.Improvements[i] with { Prerequisite = AdvanceReference.Never };
            Goals = new(World, Rules.Definitions, this);
            Flow = new(World, Rules, Random, Grant.Grants, Grant.GovernmentPrompts,
                new(World, Rules.Definitions, slot => ChooseProduction(slot), n => Order.Add("production:" + n.Kind)),
                new(World, Rules, Grant.Data.Stacks), this, this);
        }
        public void Enable(params int[] advances)
        {
            foreach (int a in advances) Rules.Definitions.Advances[a] = Rules.Definitions.Advances[a] with { Enabled = true };
        }
        public ResearchSelection Select(int forced = 0, string call = "choice") => new(World, Rules, Random, this, 1, forced, call);
        public void Advice(ResearchAdvice advice) { AdviceNotices.Add(advice); Order.Add("advice"); }
        public void CloseAdvice() => Order.Add("close");
        public void Selected(int c, int a) => Order.Add("selected:" + a);
        public void Encyclopedia(ResearchTopic topic, int a) => Order.Add("pedia:" + topic + ":" + a);
        public ResearchReply GoalChoice(ResearchGoalNotice n) { GoalNotices.Add(n); return GoalReplies.Dequeue(); }
        public int GoalHelp(ResearchTopic topic, int i, int a) { Order.Add("help:" + topic + ":" + i + ":" + a); return HelpReply; }
        public void GoalPath(ResearchPathNotice n) => Paths.Add(n);
        public void Completed(ResearchCompletionNotice n) { Completions.Add(n); Order.Add("completed:" + n.Advance); OnCompleted?.Invoke(); }
        public void Encyclopedia(int a) => Order.Add("pedia:" + a);
        public void Ability(ResearchAbilityNotice n) { Abilities.Add(n); Order.Add("ability:" + n.Ability); }
        public void FirstCivilization(int c) { Order.Add("first"); OnFirst?.Invoke(); }
    }

    // V03 §§5–6: retained recommendation, live choice/advice gates and informational goal semantics.
    private static void ResearchSelectionResumeAndAdvice()
    {
        var f = new ResearchFlowFixture(); f.Enable(0, 1); f.World.Globals.GameOptions.Advice = true;
        var selection = f.Select(); var first = selection.Resume()!; ulong draws = f.Random.Draws;
        Equal(2UL, draws); Equal(false, selection.Finished); Equal(first, selection.Resume());
        var again = selection.Resume(new(0, 1))!;
        Equal(draws, f.Random.Draws); Equal(first.Recommendation, again.Recommendation); Equal(2, f.AdviceNotices.Count);
        Sequence(new[] { "advice", "close", "pedia:Advance:0", "advice" }, f.Order);
        f.GoalReplies.Enqueue(new(-1)); selection.Resume(new(0, 2)); Equal(draws, f.Random.Draws); Equal(1, f.GoalNotices.Count);
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(1);
        Equal((ResearchChoiceNotice?)null, selection.Resume(new(1))); // No live availability recheck after accepted reply.
        Equal((short)1, f.World.Civilizations[1].Researching); Equal(1, selection.Result);
        Equal((ResearchChoiceNotice?)null, selection.Resume(new(0))); Equal((short)1, f.World.Civilizations[1].Researching);
        var negative = f.Select(call: "negative"); negative.Resume(); negative.Resume(new(-3)); Equal(-3, negative.Result);
    }

    private static void ResearchSelectionGatesAndEmptyOptions()
    {
        var f = new ResearchFlowFixture(); f.Enable(0); f.World.Globals.Turn = 0; f.World.Globals.GameOptions.Advice = true;
        var selection = f.Select(); Equal((ResearchChoiceNotice?)null, selection.Resume()); Equal(0, selection.Result); Equal(1UL, f.Random.Draws);
        Equal(0, f.AdviceNotices.Count);
        selection = f.Select(1, "forced"); Equal(true, selection.Resume()!.AlternatePosition); Equal(0, f.AdviceNotices.Count);
        selection.Resume(new(0)); Equal(true, f.Order.Contains("close"));
        f = new(); f.World.Civilizations[1].Researching = 4; selection = f.Select();
        Equal((ResearchChoiceNotice?)null, selection.Resume()); Equal(-1, selection.Result); Equal((short)-1, f.World.Civilizations[1].Researching);
        f.World.Globals.GameOptions.Advice = true;
        Throws<IndexOutOfRangeException>(() => f.Select(call: "missing-advice").Resume());
    }

    private static void ResearchAdviceOrderingAndFutureLabels()
    {
        var f = new ResearchFlowFixture(); f.World.Civilizations[1].FutureTechs = 255;
        for (int u = 0; u < 4; ++u) f.Rules.Definitions.Units[u] = f.Rules.Definitions.Units[u] with { Prerequisite = (AdvanceReference)89 };
        for (int i = 0; i < 4; ++i) f.Rules.Definitions.Improvements[i] = f.Rules.Definitions.Improvements[i] with { Prerequisite = (AdvanceReference)89 };
        var advice = f.Goals.Advice(1, 89); Equal((int?)256, advice.FutureNumber); Equal(ResearchAdviceKind.Unlocks, advice.Kind);
        Sequence(new[] { "Unit 0", "Unit 1", "Unit 2", "Unit 3", "Building 1" }, advice.Items.Select(x => x.Name));
        f.Enable(1, 2, 3, 4, 5, 6);
        for (int a = 1; a < 7; ++a) f.Rules.Definitions.Advances[a] = f.Rules.Definitions.Advances[a] with { Prerequisites = new([(AdvanceReference)0, AdvanceReference.None]) };
        advice = f.Goals.Advice(1, 0); Equal(ResearchAdviceKind.SubsequentAdvances, advice.Kind); Sequence(new[] { 1, 2, 3, 4, 5 }, advice.Items.Select(x => x.Identity));
        Equal(ResearchAdviceKind.None, f.Goals.Advice(1, 8).Kind);
    }

    private static void ResearchGoalOptionsPathsAndReplies()
    {
        var f = new ResearchFlowFixture(); f.Enable(0, 1, 3, 6, 10);
        f.World.Globals.Difficulty = Difficulty.Prince;
        f.Rules.Definitions.Advances[10] = f.Rules.Definitions.Advances[10] with { Prerequisites = new([(AdvanceReference)3, (AdvanceReference)6]) };
        Sequence(Array.Empty<int>(), f.Goals.Path(1, 10, 0, 0)); // Non-ancestor 0 consumes the first-candidate exemption.
        Sequence(new[] { 3, 6 }, f.Goals.Path(1, 10, 2, 0)); // Full exclude argument affects modulo arithmetic.
        f.World.Civilizations[2].Advances = default(AdvanceSet).With(3).With(10);
        Sequence(new[] { 3, 10 }, f.Goals.Path(1, 10, 0, 2)); // Theft ignores prerequisite availability.
        f.Rules.Definitions.Units[2] = f.Rules.Definitions.Units[2] with { Prerequisite = (AdvanceReference)12 };
        Equal(12, f.Goals.Options(1, ResearchTopic.Unit, 0).Single().Prerequisite!.Value); // Disabled prerequisite remains listed.
        f.World.Civilizations[1].Researching = 12; Equal(0, f.Goals.Options(1, ResearchTopic.Unit, 1).Length);
        f.World.Civilizations[1].Researching = -1;
        f.GoalReplies.Enqueue(new(0, 2)); f.GoalReplies.Enqueue(new(2, 1)); f.GoalReplies.Enqueue(new(2)); f.HelpReply = 1;
        f.Goals.Run(1); Sequence(new[] { ResearchTopic.Advance, ResearchTopic.Unit, ResearchTopic.Unit }, f.GoalNotices.Select(n => n.Category));
        Sequence(new[] { "help:Unit:2:12", "pedia:Advance:12" }, f.Order);
        Equal(12, f.Paths.Single().Goal); Equal((short)-1, f.World.Civilizations[1].Researching); Equal(0UL, f.Random.Draws);
        f.GoalReplies.Enqueue(new(-1, 3)); f.Goals.Run(1); Equal(1, f.Paths.Count); // Negative selection precedes category switching.
    }

    // V03 §§8–9 joined to actual grant/government/production services; replies are the only supplied decisions.
    private static void ResearchCompletionModesAndAbilityOrder()
    {
        var f = new ResearchFlowFixture(); f.Enable(0, 18); f.World.Civilizations[1].Researching = 18;
        f.World.Civilizations[1].ResearchProgress = 100; f.NoticeMode = 2;
        f.Rules.Definitions.Units[1] = f.Rules.Definitions.Units[1] with { Prerequisite = (AdvanceReference)18 };
        f.Rules.Definitions.Units[13] = f.Rules.Definitions.Units[13] with { Prerequisite = (AdvanceReference)18 };
        f.Rules.Definitions.Improvements[32] = f.Rules.Definitions.Improvements[32] with { Prerequisite = (AdvanceReference)18 };
        f.Rules.Definitions.Improvements[24] = f.Rules.Definitions.Improvements[24] with { Prerequisite = (AdvanceReference)18 };
        f.Rules.Definitions.Cosmic = f.Rules.Definitions.Cosmic with { ParadropRange = 255 };
        f.OnCompleted = () => { f.World.Globals.GameOptions.AdvanceCivilopedia = true; f.World.Civilizations[1].Researching = 7; };
        var op = f.Flow.Complete(1, 0, "completion"); var request = op.Resume()!;
        Equal((short)100, f.World.Civilizations[1].ResearchProgress); Equal(true, f.World.Civilizations[1].Advances.Contains(18));
        Sequence(new[] { ResearchAbility.Transform, ResearchAbility.Fortress, ResearchAbility.Airlift, ResearchAbility.Farmland, ResearchAbility.Paradrop }, f.Abilities.Select(n => n.Ability));
        Equal((int?)255, f.Abilities.Last().Range); Equal(true, f.Order.Contains("pedia:18")); Equal(0, f.Grant.Prompts.Count);
        op.Resume(new(request.Options[0].Identity)); Equal((int?)18, op.Result); Equal(true, op.Finished);
        Equal((short)0, f.World.Civilizations[1].Researching);
        f = new(); f.Enable(89); f.World.Civilizations[1].Researching = 89; f.World.Civilizations[1].FutureTechs = 255;
        op = f.Flow.Complete(1, 0, "future"); request = op.Resume()!;
        Equal((int?)0, f.Completions.Single().FutureNumber); Equal((int?)1, request.Options.Single().FutureNumber);
        op.Resume(new(89)); Equal((int?)89, op.Result);
    }

    private static void ResearchAccumulationPhilosophyAndFreeAdvance()
    {
        var f = new ResearchFlowFixture(); f.Enable(0, 1, 60); f.World.Civilizations[1].Researching = 60;
        f.World.Civilizations[1].ResearchProgress = 100; f.Grant.Data.AddCity(0, 1, new(10, 10), 1);
        var op = f.Flow.AddPoints(1, 1, "philosophy"); var first = op.Resume()!;
        Equal(true, f.World.Civilizations[1].Flags.PendingFreeAdvance); Equal(1, f.Grant.Golden.Count);
        Equal(false, op.Finished); Equal((short)0, f.World.Civilizations[1].ResearchProgress);
        Equal(first, op.Resume()); Equal(1, f.Completions.Count);
        var second = op.Resume(new(0))!;
        Equal(false, f.World.Civilizations[1].Flags.PendingFreeAdvance); Sequence(new[] { 60, 0 }, f.Completions.Select(n => n.Advance));
        Equal(true, f.World.Civilizations[1].Advances.Contains(0)); Equal(false, op.Finished);
        op.Resume(new(1)); Equal(true, op.Finished); Equal((short)1, f.World.Civilizations[1].Researching);
        Equal((byte)2, f.World.Civilizations[1].NumAdvances);
        f = new(); f.Enable(0, 1); f.World.Civilizations[1].Researching = 0; f.World.Civilizations[1].Flags.PendingFreeAdvance = true;
        op = f.Flow.AddPoints(1, 1, "below-threshold"); op.Resume(); Equal(1, f.Completions.Count); Equal(false, f.World.Civilizations[1].Flags.PendingFreeAdvance);
        op.Resume(new(1)); Equal(true, op.Finished);
    }

    private static void ResearchAccumulationWidthsAndInitialChoice()
    {
        var f = new ResearchFlowFixture(); f.Flow.AddPoints(999, 0, "zero").Resume(); f.Flow.AddPoints(999, -1, "negative").Resume();
        f.Enable(0); f.World.Civilizations[1].Researching = 0; f.World.Civilizations[1].ResearchProgress = 32760;
        var op = f.Flow.AddPoints(1, 10, "overflow"); op.Resume(); Equal((short)-32766, f.World.Civilizations[1].ResearchProgress); Equal(0, f.Completions.Count);
        f.World.Civilizations[1].Researching = -1; f.World.Civilizations[1].ResearchProgress = 0;
        f.World.Civilizations[1].Flags.PendingFreeAdvance = true; f.World.Globals.GameOptions.Tutorial = true;
        f.OnFirst = () => { f.World.Globals.GameOptions.Tutorial = false; f.World.Civilizations[1].Researching = 9; };
        op = f.Flow.AddPoints(1, 65537, "initial"); var choice = op.Resume()!;
        Equal((short)1, f.World.Civilizations[1].ResearchProgress); Equal("first", f.Order.Single());
        op.Resume(new(choice.Options[0].Identity)); Equal(true, op.Finished); Equal(0, f.Completions.Count);
        Equal(true, f.World.Civilizations[1].Flags.PendingFreeAdvance); // Initial absent-target branch does not consume it.
    }

    private static void ResearchCompletionAiProductionAndNoTarget()
    {
        var f = new ResearchFlowFixture(); f.Enable(0); f.World.Multiplayer.Saved.AutoplayActive = true;
        f.World.Civilizations[2].Researching = 0; f.Grant.Data.AddCity(0, 2, new(10, 10)); f.SelectedCity = new(0);
        f.World.Civilizations[2].UnitsInProduction[0] = 1; f.ChooseProduction = _ => 7;
        var op = f.Flow.Complete(2, 0, "ai"); Equal((ResearchChoiceNotice?)null, op.Resume());
        Equal((sbyte)7, f.World.Entities.Cities.Storage[0].Production); Equal((byte)0, f.World.Civilizations[2].UnitsInProduction[0]);
        Equal((byte)1, f.World.Civilizations[2].UnitsInProduction[7]); Equal(0, f.Completions.Count); Equal((int?)0, op.Result);
        f = new(); f.World.Civilizations[1].ResearchProgress = 77; op = f.Flow.Complete(1, 0, "none"); op.Resume();
        Equal((int?)-1, op.Result); Equal((short)77, f.World.Civilizations[1].ResearchProgress); Equal((byte)0, f.World.Civilizations[1].NumAdvances);
    }
    private static void ResearchCompletionGovernmentAndFailure()
    {
        var f = new ResearchFlowFixture(); f.Enable(0, 54); f.World.Civilizations[1].Researching = 54;
        f.World.Civilizations[1].Government = Government.Despotism; f.Grant.RevolutionReply = 1;
        var op = f.Flow.Complete(1, 0, "government"); op.Resume();
        Equal(Government.Despotism, f.World.Civilizations[1].Government); Equal(0, f.Grant.GovernmentChoices.Count);
        op.Resume(new(0)); Equal(Government.Anarchy, f.World.Civilizations[1].Government); Equal(1, f.Grant.GovernmentChoices.Count);
        f = new(); f.Enable(0); f.World.Civilizations[1].Researching = 0; f.World.Civilizations[1].ResearchProgress = 100;
        f.OnCompleted = () => throw new InvalidOperationException("Presentation stopped");
        op = f.Flow.Complete(1, 0, "failure"); Throws<InvalidOperationException>(() => op.Resume());
        Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((short)0, f.World.Civilizations[1].ResearchProgress);
        Equal((short)-1, f.World.Civilizations[1].Researching); Equal(false, op.Finished);
        Throws<InvalidOperationException>(() => op.Resume()); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        f.World.Globals.GameOptions.Advice = true;
        var selection = f.Select(call: "failed-choice"); Throws<IndexOutOfRangeException>(() => selection.Resume());
        Throws<InvalidOperationException>(() => selection.Resume());
    }

}
