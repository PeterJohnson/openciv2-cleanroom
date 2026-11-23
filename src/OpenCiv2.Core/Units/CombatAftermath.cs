using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum CombatFinalNoticeKind { MultipleLoss, MultipleWin, Ransom, CancelPeace, AllyUnderAttack, AllyAttacking }
public sealed record CombatFinalNotice(CombatFinalNoticeKind Kind, int Recipient, int Attacker, int Defender,
    string AttackerName, string DefenderName, int Count = 0, int Gold = 0);
public interface ICombatAftermathEffects
{
    void Notice(CombatFinalNotice notice);
    void Redraw(MapPoint origin, MapPoint target);
    void RefreshInfo();
    void RefreshMilitaryAdvisor();
}

/// <summary>U06 ordinary final effects before the returned movement result: publication,
/// multiple casualties, leader reward, actual V04 allies and V05 help, and advisor refresh.</summary>
public sealed class CombatAftermath(GameState world, CombatWorkspace state, DiplomaticWar war, AllyHelp help,
    Action publish, ICombatAftermathEffects effects)
{
    private GameState World => world;
    public Operation Begin(PreparedCombat combat, ResolvedCombat resolved, bool forceWar, string call) => new(this, combat, resolved, forceWar, call);
    private void Notice(CombatFinalNoticeKind kind, PreparedCombat combat, int count = 0, int gold = 0) =>
        effects.Notice(new(kind, world.Multiplayer.LocalHuman, combat.AttackerOwner, combat.DefenderOwner,
            world.Names[combat.AttackerOwner].Tribe, world.Names[combat.DefenderOwner].Tribe, count, gold));

    public sealed class Operation : IDisposable
    {
        private readonly CombatAftermath parent;
        private readonly IEnumerator<AllyHelpDecision> steps;
        private readonly List<Exception> presentationFailures = [];
        private int? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public AllyHelpDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(CombatAftermath parent, PreparedCombat combat, ResolvedCombat resolved, bool forceWar, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Finish(this, combat, resolved, forceWar, call).GetEnumerator();
        }
        internal int? TakeReply() { var value = reply; reply = null; return value; }
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        internal void Report(IEnumerable<Exception> errors) => presentationFailures.AddRange(errors);
        public AllyHelpDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed combat aftermath cannot resume"); }
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

    private IEnumerable<AllyHelpDecision> Finish(Operation operation, PreparedCombat combat, ResolvedCombat resolved, bool forceWar, string call)
    {
        publish();
        if (state.AnyViewer)
        { operation.Present(() => effects.Redraw(combat.Origin, combat.Target)); }
        if (!world.Multiplayer.Saved.AutoplayActive && state.KillCount > 1)
        {
            if (world.Multiplayer.LocalHuman == resolved.Loser)
            { Notice(CombatFinalNoticeKind.MultipleLoss, combat, state.KillCount); }
            else if (world.Multiplayer.LocalHuman == resolved.Winner)
            { Notice(CombatFinalNoticeKind.MultipleWin, combat, state.KillCount); }
        }
        if (resolved.Ransom)
        {
            int gold = world.Globals.BarbarianActivity * 100 / 2;
            world.Civilizations[combat.AttackerOwner].Gold = unchecked(world.Civilizations[combat.AttackerOwner].Gold + gold);
            if (world.Multiplayer.LocalHuman == combat.AttackerOwner && !world.Multiplayer.Saved.AutoplayActive)
            {
                Notice(CombatFinalNoticeKind.Ransom, combat, gold: gold);
                operation.Present(effects.RefreshInfo);
            }
        }
        if (forceWar)
        { war.ActivateAllies(combat.DefenderOwner, combat.AttackerOwner); }
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            if (state.AggregateTreatyNotice)
            { Notice(CombatFinalNoticeKind.CancelPeace, combat); }
            else if (state.AggregateAllyWarning == 1)
            {
                Notice(CombatFinalNoticeKind.AllyUnderAttack, combat);
                using var request = help.Begin(combat.DefenderOwner, combat.AttackerOwner, call + "/help");
                while (!request.Completed)
                {
                    if (request.Resume(operation.TakeReply()) is { } pending)
                    { yield return pending; }
                }
                operation.Report(request.PresentationFailures);
            }
            else if (state.AggregateAllyWarning == 2)
            { Notice(CombatFinalNoticeKind.AllyAttacking, combat); }
        }
        operation.Present(effects.RefreshMilitaryAdvisor);
    }
}
