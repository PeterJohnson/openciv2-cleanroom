using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public abstract record ActiveUnitDecision
{
    public sealed record PlayerInput(int Observer) : ActiveUnitDecision;
    public sealed record Movement(MovementDecision Value) : ActiveUnitDecision;
    public sealed record Automated(AutomatedOrderDecision Value) : ActiveUnitDecision;
}
public abstract record ActiveUnitReply
{
    public sealed record InputHandled : ActiveUnitReply;
    public sealed record Movement(ResearchReply Value) : ActiveUnitReply;
    public sealed record Automated(AutomatedOrderReply Value) : ActiveUnitReply;
}

/// <summary>U04 gameplay portion of the active-unit cycle, after host tutorial/focus presentation.
/// Real U04/A04 children retain decisions. PlayerInput releases the host's command lane until an
/// ordered input has been handled; a child dialog alone does not count as player input.
/// Native blink/music/message-pump procedures are presentation policy. Hold remains explicit.</summary>
public sealed class ActiveUnitCycle(GameState world, RuleTables rules, UnitOrderExecution orders, AutomatedOrders automated)
{
    private GameState World => world;
    public Operation Begin(StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call) =>
        new(this, starts, inputs, hold, call);

    public sealed class Operation : IDisposable
    {
        private readonly ActiveUnitCycle parent;
        private readonly IEnumerator<ActiveUnitDecision?> steps;
        private readonly List<Exception> failures = [];
        private ActiveUnitReply? reply;
        private bool faulted, disposed;
        public bool Completed { get; internal set; }
        public bool EnteredLoop { get; internal set; }
        public bool WaitedForInput { get; internal set; }
        public int AutomatedIterations { get; internal set; }
        public UnitSlot? Actor { get; internal set; }
        public bool StopRequested { get; private set; }
        public ActiveUnitDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(ActiveUnitCycle parent, StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, starts, inputs, hold, call).GetEnumerator();
        }
        /// <summary>Stops at the next iteration boundary; an accepted child still finishes first.</summary>
        public void RequestStop() => StopRequested = true;
        internal ActiveUnitReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public ActiveUnitDecision? Resume(ActiveUnitReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed active-unit cycle cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is ActiveUnitDecision.PlayerInput && StopRequested)
                { answer = new ActiveUnitReply.InputHandled(); }
                if (Pending is not null && answer is null)
                { return Pending; }
                if (Pending is ActiveUnitDecision.PlayerInput && answer is not ActiveUnitReply.InputHandled
                    || Pending is ActiveUnitDecision.Movement && answer is not ActiveUnitReply.Movement
                    || Pending is ActiveUnitDecision.Automated && answer is not ActiveUnitReply.Automated)
                { throw new InvalidOperationException("Reply does not match the active-unit boundary"); }
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
    private IEnumerable<ActiveUnitDecision?> Execute(Operation op, StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call)
    {
        int bound = world.Entities.Units.End, iteration = 0;
        op.Actor = world.Globals.ActiveUnit;
        while (UnitActivation.Selectable(world, rules, world.Globals.ActiveUnit?.Value ?? -1)
            && !op.StopRequested && world.PlayerPhaseActive && world.TurnLoopActive
            && world.Entities.Units.End == bound && world.Globals.HumanPlayers.Contains(world.Multiplayer.LocalHuman))
        {
            op.EnteredLoop = true;
            var actor = op.Actor ?? throw new InvalidOperationException("Active-unit cycle requires a captured actor");
            string step = call + "/iteration-" + iteration;
            if (!world.Entities.Units.At(actor).Flags.Automated)
            {
                using var child = orders.Begin(actor, inputs, step + "/orders");
                while (!child.Completed)
                {
                    var pending = child.Resume((op.TakeReply() as ActiveUnitReply.Movement)?.Value);
                    if (pending is not null)
                    { yield return new ActiveUnitDecision.Movement(pending); }
                    else if (!child.Completed)
                    { yield return null; }
                }
                op.Report(child.PresentationFailures);
                if (child.Result == 0)
                {
                    op.WaitedForInput = true;
                    yield return new ActiveUnitDecision.PlayerInput(world.Multiplayer.LocalHuman);
                    op.TakeReply();
                }
            }
            else
            {
                using var child = automated.Begin(starts, inputs, hold, step + "/automated");
                while (!child.Completed)
                {
                    var pending = child.Resume((op.TakeReply() as ActiveUnitReply.Automated)?.Value);
                    if (pending is not null)
                    { yield return new ActiveUnitDecision.Automated(pending); }
                    else if (!child.Completed)
                    { yield return null; }
                }
                op.Report(child.PresentationFailures);
                op.AutomatedIterations = unchecked(op.AutomatedIterations + 1);
                if (op.AutomatedIterations > 20)
                { UnitMovement.Exhaust(world, rules, ref world.Entities.Units.At(actor)); }
            }
            iteration = unchecked(iteration + 1);
            // Yield bounded completed work before considering the next live selection/phase gates.
            yield return null;
        }
        if (!op.EnteredLoop)
        { world.Globals.ActiveUnit = null; }
        op.Completed = true;
    }
}
