using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>V03 §§2–4 shared eligibility, ordered prerequisite walks and randomized recommendation.</summary>
public static class Research
{
    /// <summary>F05 §8 / V03 §§5,9–10: actual research completion during local start balancing, before
    /// the recipient owns cities. Other civilizations may already have accelerated cities.
    /// This domain has no production-reset targets, prompts or network effects.
    /// It does not increment advance counts or first discoverers on turn zero.</summary>
    public static int CompleteStarting(
        GameState world, RuleState rules, int civilization, GameplayRandom random, string call)
    {
        if (world.Globals.Turn != 0 || world.Globals.Multiplayer != GameMode.SinglePlayer)
        {
            throw new InvalidOperationException("Starting research requires local turn zero before city creation.");
        }

        foreach (ref readonly var city in world.Entities.Cities.Scanned)
        {
            if (city.Token.Occupied && city.Owner.Value == civilization)
            {
                throw new InvalidOperationException("Starting research requires local turn zero before city creation.");
            }
        }

        ref var civ = ref world.Civilizations[civilization];
        int advance = civ.Researching;
        if (advance < 0)
        {
            advance = Recommend(world, rules, civilization, random, call);
            civ.Researching = unchecked((short)advance);
        }

        if (advance < 0)
        {
            return advance;
        }

        civ.ResearchProgress = 0;
        civ.Researching = -1;
        GrantStarting(world, civilization, advance);
        return advance;
    }

    /// <summary>V03 §10 local turn-zero grant. Duplicate grants still store the source; no ordinary
    /// advance count, first-discoverer, production, wonder, tutorial or free-advance effects run here.</summary>
    public static void GrantStarting(GameState world, int civilization, int advance, int source = 0)
    {
        if (world.Globals.Turn != 0 || world.Globals.Multiplayer != GameMode.SinglePlayer)
        {
            throw new InvalidOperationException("Starting grants require local turn zero");
        }
        ref var civ = ref world.Civilizations[civilization];
        if (civ.Researching == advance)
        {
            civ.Researching = -1;
        }

        if (advance == CivilizationKnowledge.FutureTechnology || advance >= RuleLimits.Advances)
        {
            civ.FutureTechs = unchecked((byte)(civ.FutureTechs + 1));
        }
        else
        {
            civ.Advances = civ.Advances.With(advance);
            civ.AdvanceSource[advance] = unchecked((byte)source);
            world.Globals.AdvanceKnownBy[advance] = world.Globals.AdvanceKnownBy[advance].With(civilization);
        }

    }

    /// <summary>Enabled, unknown and both prerequisites known, in that order.
    /// Future Technology remains eligible.</summary>
    public static bool Available(GameState world, RuleTables rules, int civilization, int advance)
    {
        var rule = rules.Advances[advance];
        return rule.Enabled && !CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, advance)
            && CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, (int)rule.Prerequisites[0])
            && CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, (int)rule.Prerequisites[1]);
    }

    /// <summary>Ordered first-prerequisite ancestry. A reached cycle stops explicitly, never pretending
    /// to be a negative answer or visiting the later branch. Equality precedes table access.</summary>
    public static bool DescendsFrom(RuleTables rules, int advance, int ancestor)
    {
        var active = new HashSet<int>();
        var completed = new Dictionary<int, bool>();
        return Visit(advance);

        bool Visit(int current)
        {
            if (current < 0)
            {
                return false;
            }

            if (current == ancestor)
            {
                return true;
            }

            if (!active.Add(current))
            {
                throw new InvalidOperationException("Research ancestry reached a prerequisite cycle.");
            }

            // This is a pure query on one synchronous engine state. Reusing a completed subtree
            // avoids exponential repeated work without changing which first branch fails or succeeds.
            if (!completed.TryGetValue(current, out bool result))
            {
                result = Visit((int)rules.Advances[current].Prerequisites[0])
                    || Visit((int)rules.Advances[current].Prerequisites[1]);
                completed.Add(current, result);
            }

            active.Remove(current);
            return result;
        }
    }

    /// <summary>V03 §3 science-report count: repeated positive prerequisite edges count independently.
    /// The entry-only bound permits pending siblings to raise a zero-based count as high as 202.</summary>
    public static int CountPrerequisites(RuleTables rules, int advance, int initialCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCount);
        int count = initialCount;
        Visit(advance);
        return count;

        void Visit(int current)
        {
            if (count >= 101)
            {
                return;
            }

            for (int i = 0; i < RuleLimits.AdvancePrerequisites; ++i)
            {
                int prerequisite = (int)rules.Advances[current].Prerequisites[i];
                if (prerequisite > 0)
                {
                    ++count;
                    Visit(prerequisite);
                }
            }
        }
    }

    /// <summary>V03 §4 / A08 §2 engine recommendation. Does not set the current topic or grant knowledge.
    /// Human modulo filtering and per-candidate sampling differ from AI valuation-weighted sampling.</summary>
    public static int Recommend(GameState world, RuleState rules, int civilization, GameplayRandom random, string call)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrEmpty(call);
        int bestScore = -1, selected = -1, admitted = 0;
        for (int advance = 0; advance < RuleLimits.Advances; ++advance)
        {
            var rule = rules.Definitions.Advances[advance];
            if (!Available(world, rules.Definitions, civilization, advance)
                || rule.Prerequisites[0] == AdvanceReference.Never && rule.Prerequisites[1] == AdvanceReference.Never)
            {
                continue;
            }

            bool human = world.Globals.HumanPlayers.Contains(civilization);
            if (human && admitted != 0 && world.Globals.Difficulty != Difficulty.Chieftain
                && (advance - world.Civilizations[civilization].NumAdvances) % 3 == 0)
            {
                continue;
            }

            ++admitted;
            int score;
            if (human)
            {
                int roll = random.PrimaryModulo(new ChoiceKey("V03.recommend.human", call, advance), 3);
                score = unchecked(roll + AdvanceValuation.Value(world, rules, civilization, advance) - 1);
            }
            else
            {
                int value = AdvanceValuation.Value(world, rules, civilization, advance);
                if (value == 1 || unchecked(value - 1) < 0)
                {
                    score = 0;
                }
                else
                {
                    int roll = random.PrimaryCode(new ChoiceKey("V03.recommend.ai", call, advance));
                    score = roll % AdvanceValuation.Value(world, rules, civilization, advance);
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                selected = advance;
            }
        }

        return selected;
    }
}
