using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public abstract record CombatContinuationDecision
{
    public sealed record Lifecycle(LifecycleDecision Decision) : CombatContinuationDecision;
    public sealed record Response(ParadropDecision Decision) : CombatContinuationDecision;
}
public interface ICombatContinuationEffects
{
    void Redraw(MapPoint point);
    void RedrawNeighbors(MapPoint point);
}

/// <summary>U06 surviving-attacker and nuclear-delivery continuations with actual U01/V01 deletion,
/// U07 nuclear action and A04 response. The parent owns final rewards/notices before Survive,
/// and calls Nuclear only after opening presentation and the live signed-attack gate.</summary>
public sealed class CombatContinuation(GameState world, RuleTables rules, CombatWorkspace state,
    StackOperations stacks, CivilizationElimination lifecycle, EspionageIncidents incidents,
    NuclearEffects nuclear, AttackResponse response, ICombatContinuationEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    public bool IsNuclear(PreparedCombat combat) => unchecked((sbyte)Rule(combat.Attacker).Attack) >= 99;
    public Operation Survive(PreparedCombat combat, string call) => new(this, combat, false, call);
    public Operation Nuclear(PreparedCombat combat, string call) => new(this, combat, true, call);

    public sealed class Operation : IDisposable
    {
        private readonly CombatContinuation parent;
        private readonly IEnumerator<CombatContinuationDecision?> steps;
        private readonly List<Exception> presentationFailures = [];
        private ResearchReply? reply;
        private bool faulted, disposed;
        public int? Result { get; internal set; }
        public bool Completed => Result is not null;
        public CombatContinuationDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(CombatContinuation parent, PreparedCombat combat, bool nuclear, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = (nuclear ? parent.Deliver(this, combat, call) : parent.FinishSurvivor(this, combat, call)).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void RememberFailures(IEnumerable<Exception> failures) => presentationFailures.AddRange(failures);
        internal void Redraw(Action draw)
        {
            try
            { draw(); }
            catch (Exception error) { presentationFailures.Add(error); }
        }
        public CombatContinuationDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed combat continuation cannot resume"); }
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
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private IEnumerable<CombatContinuationDecision?> Delete(Operation operation, UnitSlot actor, string call)
    {
        using var child = lifecycle.BeginDelete(actor.Value, true, call, incidents);
        while (child.Result is null)
        {
            if (child.ResumeDecision(operation.TakeReply()) is { } pending)
            { yield return new CombatContinuationDecision.Lifecycle(pending); }
            else if (child.Result is null)
            { yield return null; }
        }
    }
    private IEnumerable<CombatContinuationDecision?> Respond(Operation operation, PreparedCombat combat, string call)
    {
        using var child = response.Begin(combat.AttackerOwner, combat.Target, call);
        while (!child.Completed)
        {
            if (child.Resume(operation.TakeReply()) is { } pending)
            { yield return new CombatContinuationDecision.Response(pending); }
            else if (!child.Completed)
            { yield return null; }
        }
    }
    private IEnumerable<CombatContinuationDecision?> FinishSurvivor(Operation operation, PreparedCombat combat, string call)
    {
        var actor = world.Globals.MovingUnit ?? throw new InvalidOperationException("Combat did not retain a continuing moving unit");
        if (Rule(actor).Domain == RuleDomain.Air && Rule(actor).Range != 1)
        { UnitMovement.Exhaust(world, rules, ref Unit(actor)); }
        bool aiCity = !Human(combat.AttackerOwner) && state.City is not null && stacks.FirstAt(combat.Target) < 0;
        if (Rule(actor).Abilities.DestroyedAfterAttack)
        {
            foreach (var pending in Delete(operation, actor, call + "/delete"))
            { yield return pending; }
            if (aiCity)
            {
                operation.Redraw(() => effects.RedrawNeighbors(combat.Origin));
                foreach (var pending in Respond(operation, combat, call + "/response"))
                { yield return pending; }
            }
            operation.Result = 0;
            yield break;
        }
        if (aiCity)
        {
            if (Rule(actor).Domain == RuleDomain.Air && Rule(actor).Range != 0)
            {
                Unit(actor).Counter = unchecked((byte)(Unit(actor).Counter + 1));
                if (unchecked((sbyte)Unit(actor).Counter) >= unchecked((sbyte)Rule(actor).Range))
                { operation.Result = 1; yield break; }
            }
            foreach (var pending in Respond(operation, combat, call + "/response"))
            { yield return pending; }
            operation.Result = 0;
            yield break;
        }
        operation.Result = 1;
    }
    private IEnumerable<CombatContinuationDecision?> Deliver(Operation operation, PreparedCombat combat, string call)
    {
        state.AnimationDefender = null;
        using var child = nuclear.Begin(combat.AttackerOwner, combat.Target, true, call + "/nuclear");
        while (child.Result is null)
        {
            if (child.ResumeDecision(operation.TakeReply()) is { } pending)
            { yield return new CombatContinuationDecision.Lifecycle(pending); }
            else if (child.Result is null)
            { yield return null; }
        }
        operation.RememberFailures(child.PresentationFailures);
        if (child.Result == 0)
        {
            foreach (var pending in Delete(operation, combat.Attacker, call + "/delete"))
            { yield return pending; }
            operation.Redraw(() => effects.Redraw(combat.Origin));
        }
        else
        {
            world.Civilizations[combat.AttackerOwner].AggressionCounter[combat.DefenderOwner] = 0;
            if (!Human(combat.AttackerOwner) && state.City is not null)
            {
                foreach (var pending in Respond(operation, combat, call + "/response"))
                { yield return pending; }
            }
        }
        operation.Result = 0;
    }
}
