using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public abstract record CombatDecision
{
    public sealed record Attack(AttackWarning Warning) : CombatDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : CombatDecision;
    public sealed record Help(AllyHelpDecision Decision) : CombatDecision;
    public sealed record Continuation(CombatContinuationDecision Decision) : CombatDecision;
}
public sealed record CombatExecutionServices(CombatPreparation Preparation, CombatEntry Entry, CombatOpening Opening,
    CombatRounds Rounds, CombatResolution Resolution, CombatAftermath Aftermath, CombatContinuation Continuation);

/// <summary>U06 local execution parent, from actual preparation through the returned U02 movement
/// result. U02 owns movement admission and the incoming moving unit; preview is CombatPreparation.Preview.</summary>
public sealed class CombatExecution(GameState world, CombatWorkspace state, CombatExecutionServices services)
{
    private GameState World => world;
    public Operation Begin(UnitSlot attacker, int direction, MovementOptions movement, string incomingArgument0, string call,
        bool? scenarioShowsCombat = null, int roundBudget = 64) => new(this, attacker, direction, movement, incomingArgument0, call, scenarioShowsCombat, roundBudget);

    public sealed class Operation : IDisposable
    {
        private readonly CombatExecution parent;
        private readonly IEnumerator<CombatDecision?> steps;
        private readonly List<Exception> presentationFailures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public PreparedCombat? Combat { get; internal set; }
        public int? Result { get; internal set; }
        public bool Completed => Result is not null;
        public CombatDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(CombatExecution parent, UnitSlot attacker, int direction, MovementOptions movement, string argument,
            string call, bool? scenarioShowsCombat, int roundBudget)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentNullException.ThrowIfNull(argument);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roundBudget);
            this.parent = parent;
            steps = parent.Execute(this, attacker, direction, movement, argument, call, scenarioShowsCombat, roundBudget).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(Exception? error) { if (error is not null) { presentationFailures.Add(error); } }
        internal void Report(IEnumerable<Exception> errors) => presentationFailures.AddRange(errors);
        public CombatDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed combat execution cannot resume"); }
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

    private IEnumerable<CombatDecision?> Execute(Operation operation, UnitSlot attacker, int direction, MovementOptions movement,
        string argument, string call, bool? scenarioShowsCombat, int roundBudget)
    {
        var combat = services.Preparation.Prepare(attacker, direction, true);
        operation.Combat = combat;
        if (combat is null)
        { operation.Result = 999; yield break; }
        using var entry = services.Entry.Begin(combat, movement, call + "/entry");
        while (!entry.Completed)
        {
            if (entry.Resume(operation.TakeReply()?.Selected) is { } warning)
            { yield return new CombatDecision.Attack(warning); }
        }
        if (entry.Result!.Cancelled)
        { operation.Result = 1; yield break; }
        combat = entry.Result.Combat;
        operation.Combat = combat;
        operation.Report(services.Opening.Present(combat, direction, argument, scenarioShowsCombat).PresentationFailure);
        if (services.Continuation.IsNuclear(combat))
        {
            using var nuclear = services.Continuation.Nuclear(combat, call + "/delivery");
            foreach (var pending in Continue(operation, nuclear))
            { yield return pending; }
            yield break;
        }
        state.ExplosionShown = false;
        bool won = false;
        if (combat.AttackFirepower != 0 || combat.DefenseFirepower != 0)
        {
            using var rounds = services.Rounds.Begin(combat, call + "/rounds", roundBudget);
            while (!rounds.Completed)
            {
                if (rounds.Advance() is null)
                { yield return null; }
            }
            won = rounds.Result != 0;
        }
        using var resolution = services.Resolution.Begin(combat, won, call + "/resolution");
        while (!resolution.Completed)
        {
            if (resolution.Resume(operation.TakeReply()?.Selected) is { } pending)
            { yield return new CombatDecision.Lifecycle(pending); }
            else if (!resolution.Completed)
            { yield return null; }
        }
        using var aftermath = services.Aftermath.Begin(combat, resolution.Result!, entry.Result.ForceWar, call + "/aftermath");
        while (!aftermath.Completed)
        {
            if (aftermath.Resume(operation.TakeReply()?.Selected) is { } pending)
            { yield return new CombatDecision.Help(pending); }
        }
        operation.Report(aftermath.PresentationFailures);
        if (!won)
        { operation.Result = 0; yield break; }
        using var survivor = services.Continuation.Survive(combat, call + "/survivor");
        foreach (var pending in Continue(operation, survivor))
        { yield return pending; }
    }
    private static IEnumerable<CombatDecision?> Continue(Operation operation, CombatContinuation.Operation continuation)
    {
        while (!continuation.Completed)
        {
            if (continuation.Resume(operation.TakeReply()) is { } pending)
            { yield return new CombatDecision.Continuation(pending); }
            else if (!continuation.Completed)
            { yield return null; }
        }
        operation.Report(continuation.PresentationFailures);
        operation.Result = continuation.Result;
    }
}
