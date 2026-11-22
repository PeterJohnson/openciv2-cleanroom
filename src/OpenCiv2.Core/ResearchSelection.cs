using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum ResearchTopic { Advance, Unit, Improvement }
public enum ResearchAdviceKind { None, Unlocks, SubsequentAdvances }
public sealed record ResearchOption(int Identity, string Name, int? FutureNumber = null, int? Prerequisite = null, string? PrerequisiteName = null);
public sealed record ResearchAdvice(int Recommendation, string Name, int? FutureNumber, ResearchAdviceKind Kind, OwnedList<ResearchOption> Items);
public sealed record ResearchChoiceNotice(int Civilization, int Recommendation, bool HasInvention, bool AlternatePosition, OwnedList<ResearchOption> Options);
public readonly record struct ResearchReply(int Selected, int Extra = 0);
public sealed record ResearchGoalNotice(int Civilization, ResearchTopic Category, bool Icons, OwnedList<ResearchOption> Options, OwnedList<ResearchTopic> OtherCategories);
public sealed record ResearchPathNotice(int Civilization, int Goal, string Name, bool Stealing, OwnedList<ResearchOption> Path);

/// <summary>V03 semantic presentation and informational goal replies. Timing, localization and
/// native widget histories belong to presentation. Research selection itself can suspend for a reply.</summary>
public interface IResearchChoiceEffects
{
    bool DiplomacyActive { get; }
    void Advice(ResearchAdvice advice);
    void CloseAdvice();
    void Selected(int civilization, int advance);
    void Encyclopedia(ResearchTopic topic, int identity);
    ResearchReply GoalChoice(ResearchGoalNotice notice);
    int GoalHelp(ResearchTopic topic, int identity, int prerequisite);
    void GoalPath(ResearchPathNotice notice);
}

/// <summary>V03 §§5–6 advice and informational goal operations. Goals never select research or grant knowledge.</summary>
public sealed class ResearchGoals(GameState world, RuleTables rules, IResearchChoiceEffects effects)
{
    private bool Knows(int c, int a) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, a);
    public ResearchOption AdvanceOption(int c, int a) => new(a, rules.Advances[a].Identity.Name,
        a == CivilizationKnowledge.FutureTechnology ? world.Civilizations[c].FutureTechs + 1 : null);

    public ResearchAdvice Advice(int c, int recommendation)
    {
        var advance = AdvanceOption(c, recommendation);
        var items = new List<ResearchOption>();
        for (int u = 0; u < RuleLimits.UnitTypes; ++u)
        {
            if ((int)rules.Units[u].Prerequisite == recommendation && items.Count < 5)
            {
                items.Add(new(u, rules.Units[u].Identity.Name));
            }
        }
        for (int i = 1; i < RuleLimits.Improvements; ++i)
        {
            if ((int)rules.Improvements[i].Prerequisite == recommendation && items.Count < 5)
            {
                items.Add(new(i, rules.Improvements[i].Identity.Name));
            }
        }
        var kind = ResearchAdviceKind.Unlocks;
        if (items.Count == 0)
        {
            for (int a = 0; a < RuleLimits.Advances; ++a)
            {
                var rule = rules.Advances[a];
                if (rule.Enabled && ((int)rule.Prerequisites[0] == recommendation || (int)rule.Prerequisites[1] == recommendation) && items.Count < 5)
                {
                    items.Add(AdvanceOption(c, a));
                }
            }
            kind = items.Count == 0 ? ResearchAdviceKind.None : ResearchAdviceKind.SubsequentAdvances;
        }
        return new(recommendation, advance.Name, advance.FutureNumber, kind, new(items));
    }

    public ResearchOption[] Options(int c, ResearchTopic category, int excludeCurrent)
    {
        var options = new List<ResearchOption>();
        int count = category switch { ResearchTopic.Advance => RuleLimits.Advances, ResearchTopic.Unit => RuleLimits.UnitTypes, _ => RuleLimits.Improvements };
        for (int i = 0; i < count; ++i)
        {
            if (category == ResearchTopic.Advance)
            {
                var rule = rules.Advances[i];
                if (rule.Enabled && !Knows(c, i) && (excludeCurrent == 0 || i != world.Civilizations[c].Researching
                    && (rule.Prerequisites[0] != AdvanceReference.Never || rule.Prerequisites[1] != AdvanceReference.Never)))
                {
                    options.Add(AdvanceOption(c, i));
                }
            }
            else
            {
                int prerequisite = category == ResearchTopic.Unit ? (int)rules.Units[i].Prerequisite : (int)rules.Improvements[i].Prerequisite;
                if (prerequisite >= 0 && !Knows(c, prerequisite) && (excludeCurrent == 0 || prerequisite != world.Civilizations[c].Researching))
                {
                    options.Add(new(i, category == ResearchTopic.Unit ? rules.Units[i].Identity.Name : rules.Improvements[i].Identity.Name,
                        Prerequisite: prerequisite, PrerequisiteName: rules.Advances[prerequisite].Identity.Name));
                }
            }
        }
        return options.ToArray();
    }

    public int[] Path(int c, int goal, int excludeCurrent, int otherCiv)
    {
        var path = new List<int>();
        int admitted = 0;
        for (int a = 0; a < RuleLimits.Advances; ++a)
        {
            var rule = rules.Advances[a];
            if (!rule.Enabled || Knows(c, a))
            {
                continue;
            }

            if (otherCiv == 0)
            {
                if (!Knows(c, (int)rule.Prerequisites[0]) || !Knows(c, (int)rule.Prerequisites[1])
                    || excludeCurrent != 0 && a == world.Civilizations[c].Researching)
                {
                    continue;
                }

                if (admitted != 0 && unchecked((byte)world.Globals.Difficulty) != (byte)Difficulty.Chieftain && !world.Globals.Status.HasScenario
                    && unchecked(a - (world.Civilizations[c].NumAdvances + excludeCurrent)) % 3 == 0)
                {
                    continue;
                }
            }
            else if (!Knows(otherCiv, a))
            {
                continue;
            }

            if (rule.Prerequisites[0] == AdvanceReference.Never && rule.Prerequisites[1] == AdvanceReference.Never)
            {
                continue;
            }

            ++admitted;
            if (Research.DescendsFrom(rules, goal, a))
            {
                if (path.Count == 99)
                {
                    throw new InvalidOperationException("Research goal path exceeds the supported 99 advances.");
                }

                path.Add(a);
            }
        }
        return path.ToArray();
    }

    public void Run(int c, int excludeCurrent = 0, int otherCiv = 0)
    {
        var category = ResearchTopic.Advance;
        while (true)
        {
            var options = Options(c, category, excludeCurrent);
            if (options.Length == 0)
            {
                return;
            }

            var others = Enum.GetValues<ResearchTopic>().Where(t => t != category).ToArray();
            var reply = effects.GoalChoice(new(c, category, !effects.DiplomacyActive, new(options), new(others)));
            if (reply.Selected < 0)
            {
                return;
            }

            if (reply.Extra == 1)
            {
                if (!effects.DiplomacyActive)
                {
                    if (category == ResearchTopic.Advance)
                    {
                        effects.Encyclopedia(category, reply.Selected);
                    }
                    else
                    {
                        int prerequisite = category == ResearchTopic.Unit ? (int)rules.Units[reply.Selected].Prerequisite : (int)rules.Improvements[reply.Selected].Prerequisite;
                        int help = effects.GoalHelp(category, reply.Selected, prerequisite);
                        if (help == 0)
                        {
                            effects.Encyclopedia(category, reply.Selected);
                        }
                        else if (help == 1)
                        {
                            effects.Encyclopedia(ResearchTopic.Advance, prerequisite);
                        }
                    }
                }
                continue;
            }
            if (reply.Extra is 2 or 3)
            { category = others[reply.Extra - 2]; continue; }
            int goal = category switch
            {
                ResearchTopic.Advance => reply.Selected,
                ResearchTopic.Unit => (int)rules.Units[reply.Selected].Prerequisite,
                _ => (int)rules.Improvements[reply.Selected].Prerequisite
            };
            effects.GoalPath(new(c, goal, rules.Advances[goal].Identity.Name, otherCiv != 0,
                new(Path(c, goal, excludeCurrent, otherCiv).Select(a => AdvanceOption(c, a)))));
            return;
        }
    }
}

/// <summary>V03 §5 retained recommendation and resumable player choice. Call Resume without a reply
/// to inspect a pending request; accepting a reply stores it without rerunning availability.</summary>
public sealed class ResearchSelection(GameState world, RuleState rules, GameplayRandom random, IResearchChoiceEffects effects,
    int civilization, int forced, string call)
{
    private readonly ResearchGoals goals = new(world, rules.Definitions, effects);
    private bool started, faulted;
    private int recommendation;
    public bool Finished { get; private set; }
    public int Result { get; private set; } = -1;
    public ResearchChoiceNotice? Pending { get; private set; }

    public ResearchChoiceNotice? Resume(ResearchReply? reply = null)
    {
        if (faulted)
        {
            throw new InvalidOperationException("A failed research selection cannot resume.");
        }

        try
        { return ResumeCore(reply); }
        catch { faulted = true; throw; }
    }

    private ResearchChoiceNotice? ResumeCore(ResearchReply? reply)
    {
        MapKnowledge.RequireLocal(world);
        if (Finished)
        {
            return null;
        }

        if (!started)
        {
            recommendation = Research.Recommend(world, rules, civilization, random, call);
            started = true;
            if (forced == 0 && (world.Globals.Turn == 0 || !world.Globals.HumanPlayers.Contains(civilization & 31) || world.Multiplayer.Saved.AutoplayActive))
            {
                return Finish(recommendation);
            }
        }
        else if (Pending is not null)
        {
            if (reply is not { } answer)
            {
                return Pending;
            }

            Pending = null;
            if (world.Globals.GameOptions.Advice)
            {
                effects.CloseAdvice();
            }

            if (answer.Extra == 2)
            {
                goals.Run(civilization);
            }
            else if (answer.Extra == 0)
            {
                return Finish(answer.Selected);
            }
            else
            {
                effects.Encyclopedia(ResearchTopic.Advance, answer.Selected);
            }
        }
        if (world.Globals.GameOptions.Advice && forced == 0)
        {
            effects.Advice(goals.Advice(civilization, recommendation));
        }

        bool invention = CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, AdvanceIds.Invention);
        var options = Research.Options(world, rules.Definitions, civilization, forced).Select(a => goals.AdvanceOption(civilization, a)).ToArray();
        if (options.Length == 0)
        {
            return Finish(recommendation);
        }

        return Pending = new(civilization, recommendation, invention, world.Globals.GameOptions.Advice, new(options));
    }

    private ResearchChoiceNotice? Finish(int selected)
    {
        world.Civilizations[civilization].Researching = unchecked((short)selected);
        Result = selected;
        Finished = true;
        Pending = null;
        effects.Selected(civilization, selected);
        return null;
    }
}
