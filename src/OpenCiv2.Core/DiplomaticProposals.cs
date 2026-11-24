using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public abstract record DiplomaticProposalDecision
{
    public sealed record Treaty(TreatyNegotiationDecision Decision) : DiplomaticProposalDecision;
    public sealed record Demand(DiplomaticDemandDecision Decision) : DiplomaticProposalDecision;
}
public interface IDiplomaticProposalEffects
{
    void ClearActiveUnitBlink();
    void CancelledAlliance(int actor, DiplomaticParty other);
}

/// <summary>V05 §5 proposal admission and response dispatch. The audience separately owns favors/gifts.</summary>
public sealed class DiplomaticProposals(GameState world, DiplomaticStance stance, TreatyNegotiation treaties,
    DiplomaticDemands demands, DiplomaticWar war, IDiplomaticProposalEffects effects)
{
    private GameState World => world;
    public OwnedList<int> Options(int actor, int other)
    {
        var relation = world.Civilizations[other].Treaties[actor];
        bool allied = relation.Alliance, peace = relation.Peace && !allied;
        List<int> choices = [0];
        if (peace)
        { choices.Add(1); }
        if (!relation.Peace && !allied)
        { choices.Add(2); }
        if (allied)
        { choices.Add(3); }
        if (!allied && unchecked((byte)world.Civilizations[actor].Government) < (byte)Government.Republic)
        { choices.Add(4); }
        if (peace && !relation.ChangedThisTurn)
        { choices.Add(5); }
        if (allied)
        { choices.Add(6); }
        choices.Add(7);
        choices.Add(8);
        return new(choices);
    }
    public Operation BeginResponse(int actor, int other, int proposal, string call) => new(this, actor, other, proposal, call);
    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticProposals parent;
        private readonly IEnumerator<DiplomaticProposalDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public bool EndsAudience { get; internal set; }
        public DiplomaticProposalDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticProposals parent, int a, int b, int proposal, string call)
        { ArgumentException.ThrowIfNullOrEmpty(call); this.parent = parent; steps = parent.Run(this, a, b, proposal, call).GetEnumerator(); }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public DiplomaticProposalDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed proposal response cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<DiplomaticProposalDecision?> Run(Operation op, int a, int b, int proposal, string call)
    {
        op.Present(effects.ClearActiveUnitBlink);
        if (proposal is 1 or 2)
        {
            using var treaty = treaties.Begin(a, b, proposal == 1, call + "/treaty");
            while (!treaty.Completed)
            {
                var decision = treaty.Resume(op.TakeReply());
                if (!treaty.Completed)
                { yield return decision is null ? null : new DiplomaticProposalDecision.Treaty(decision); }
            }
            op.Report(treaty.PresentationFailures);
            op.EndsAudience = treaty.EndsAudience;
        }
        else if (proposal is >= 3 and <= 5)
        {
            using var demand = demands.Begin(a, b, proposal, call + "/demand");
            while (!demand.Completed)
            {
                var decision = demand.Resume(op.TakeReply());
                if (!demand.Completed)
                { yield return decision is null ? null : new DiplomaticProposalDecision.Demand(decision); }
            }
            op.Report(demand.PresentationFailures);
            op.EndsAudience = demand.EndsAudience;
        }
        else if (proposal == 6)
        {
            DiplomaticRelations.Clear(world, a, b, new() { Alliance = true });
            DiplomaticAttitudes.Adjust(world, b, a, 50);
            stance.Evaluate(a, b, call + "/stance");
            effects.CancelledAlliance(a, new(b, world.Names[b].Tribe, world.Names[b].Adjective));
            world.Civilizations[a].Reputation = unchecked((byte)(world.Civilizations[a].Reputation + 1));
            war.CancelAlliance(a, b);
        }
    }
}
