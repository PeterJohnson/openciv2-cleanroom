using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public abstract record CapturedTechnologyDecision
{
    public sealed record Goal(ResearchGoalDecision Decision) : CapturedTechnologyDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : CapturedTechnologyDecision;
}
public sealed record CapturedTechnologyChoice(int Civilization, int Victim, bool Interactive, int Recommendation, OwnedList<ResearchOption> Options) : CapturedTechnologyDecision;
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

    public sealed class Operation(CapturedTechnology parent, int civilization, int victim, string call) : IDisposable
    {
        private bool started, interactive, faulted, disposed;
        private int best = -1, bestValue = -999;
        private int selected;
        private ResearchGrants.Operation? grant;
        private ResearchGoals.Operation? goal;
        private readonly List<Exception> presentationFailures = [];
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        public bool Finished { get; private set; }
        public int? Result { get; private set; }
        public CapturedTechnologyDecision? Pending { get; private set; }
        public CapturedTechnologyDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed captured-technology operation cannot resume"); }
            if (Finished)
            { return null; }
            try
            { return Advance(reply); }
            catch { faulted = true; grant?.Dispose(); goal?.Dispose(); throw; }
        }
        private CapturedTechnologyDecision? Advance(ResearchReply? reply)
        {
            var w = parent.World;
            MapKnowledge.RequireLocal(w);
            if (grant is not null)
            { return AdvanceGrant(reply?.Selected); }
            if (goal is not null)
            {
                if (Pending is not null && reply is null)
                {
                    return Pending;
                }

                if (goal.Resume(reply) is { } browsing)
                {
                    return Pending = new CapturedTechnologyDecision.Goal(browsing);
                }

                goal.Dispose();
                goal = null;
                Pending = null;
                reply = null;
            }
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
                goal = parent.Goals.Begin(civilization, 0, victim);
                return Advance(null);
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
            return Pending = new CapturedTechnologyChoice(civilization, victim, interactive, best, new(options));
        }
        private CapturedTechnologyDecision? Finish(int advance)
        {
            // Unsupported negative/out-of-backing replies stop before name reads or grant writes.
            ArgumentOutOfRangeException.ThrowIfNegative(advance);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(advance, RuleLimits.Advances);
            var w = parent.World;
            if (!w.Multiplayer.Saved.AutoplayActive && parent.Human(victim) && w.Multiplayer.LocalHuman == victim)
            { parent.Taken(civilization, victim, advance); }
            selected = advance;
            grant = parent.Grants.Begin(civilization, advance, victim, false, call + "/grant");
            return AdvanceGrant(null);
        }
        private CapturedTechnologyDecision? AdvanceGrant(int? reply)
        {
            Pending = grant!.Resume(reply) is { } decision ? new CapturedTechnologyDecision.Grant(decision) : null;
            if (!grant.Completed)
            { return Pending; }
            presentationFailures.AddRange(grant.PresentationFailures);
            grant.Dispose();
            grant = null;
            if (!parent.Human(civilization))
            { parent.Production.ResetCivilization(civilization); }
            Result = selected;
            Finished = true;
            return null;
        }
        public void Dispose() { disposed = true; grant?.Dispose(); goal?.Dispose(); }
    }
}
