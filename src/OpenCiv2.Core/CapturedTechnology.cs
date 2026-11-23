using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record CapturedTechnologyChoice(int Civilization, int Victim, bool Interactive, int Recommendation, OwnedList<ResearchOption> Options);
public sealed record TakenTechnologyNotice(int Civilization, int Victim, int Advance, string CivilizationName, string AdvanceName);

/// <summary>V03 §12 captured technology with actual A08 valuation, V03 grant/goal and C06 reset.
/// Owning capture/mission callers supply admission; accepted human choices are not revalidated.</summary>
public sealed class CapturedTechnology(GameState world, RuleState rules, ResearchGrants grants,
    ProductionOrders production, ResearchGoals goals, Action<TakenTechnologyNotice> notice)
{
    private GameState World => world;
    private RuleState Rules => rules;
    private ResearchGrants Grants => grants;
    private ProductionOrders Production => production;
    private ResearchGoals Goals => goals;
    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c);
    private void Taken(int c, int victim, int advance) => notice(new(c, victim, advance, world.Names[c].Tribe, rules.Definitions.Advances[advance].Identity.Name));
    public Operation Begin(int civilization, int victim, string call) => new(this, civilization, victim, call);

    public sealed class Operation(CapturedTechnology parent, int civilization, int victim, string call)
    {
        private bool started, interactive, faulted;
        private int best = -1, bestValue = -999;
        public bool Finished { get; private set; }
        public int? Result { get; private set; }
        public CapturedTechnologyChoice? Pending { get; private set; }
        public CapturedTechnologyChoice? Resume(ResearchReply? reply = null)
        {
            if (faulted)
            { throw new InvalidOperationException("A failed captured-technology operation cannot resume"); }
            if (Finished)
            { return null; }
            try
            { return Advance(reply); }
            catch { faulted = true; throw; }
        }
        private CapturedTechnologyChoice? Advance(ResearchReply? reply)
        {
            var w = parent.World;
            MapKnowledge.RequireLocal(w);
            if (!started)
            {
                ArgumentException.ThrowIfNullOrEmpty(call);
                started = true;
                if (civilization == 0 || victim == 0)
                { Finished = true; return null; }
                interactive = w.Multiplayer.LocalHuman == civilization && !w.Multiplayer.Saved.AutoplayActive;
            }
            else if (Pending is not null)
            {
                if (reply is null)
                { return Pending; }
                Pending = null;
                if (reply.Value.Extra == 0)
                { return Finish(reply.Value.Selected); }
                parent.Goals.Run(civilization, 0, victim);
            }
            List<ResearchOption> options = [];
            for (int a = 0; a < RuleLimits.Advances; ++a)
            {
                if (CivilizationKnowledge.KnowsAdvance(w.Civilizations, civilization, a)
                    || !CivilizationKnowledge.KnowsAdvance(w.Civilizations, victim, a))
                { continue; }
                if (w.Globals.Status.HasScenario && w.Scenario.Flags.GovernmentLock
                    && a is AdvanceIds.Monarchy or AdvanceIds.Communism or AdvanceIds.Fundamentalism or AdvanceIds.Republic or AdvanceIds.Democracy)
                { continue; }
                var rule = parent.Rules.Definitions.Advances[a];
                if (rule.Prerequisites[0] == AdvanceReference.Never && rule.Prerequisites[1] == AdvanceReference.Never)
                { continue; }
                if (interactive)
                { options.Add(new(a, rule.Identity.Name)); }
                int value = AdvanceValuation.Value(w, parent.Rules, civilization, a);
                if (value >= bestValue)
                { best = a; bestValue = value; }
            }
            if (best < 0)
            { Finished = true; return null; }
            if (!parent.Human(civilization))
            { return Finish(best); }
            return Pending = new(civilization, victim, interactive, best, new(options));
        }
        private CapturedTechnologyChoice? Finish(int advance)
        {
            // Unsupported negative/out-of-backing replies stop before name reads or grant writes.
            ArgumentOutOfRangeException.ThrowIfNegative(advance);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(advance, RuleLimits.Advances);
            var w = parent.World;
            if (!w.Multiplayer.Saved.AutoplayActive && parent.Human(victim) && w.Multiplayer.LocalHuman == victim)
            { parent.Taken(civilization, victim, advance); }
            parent.Grants.Grant(civilization, advance, victim, false, call + "/grant");
            if (!parent.Human(civilization))
            { parent.Production.ResetCivilization(civilization); }
            Result = advance;
            Finished = true;
            return null;
        }
    }
}
