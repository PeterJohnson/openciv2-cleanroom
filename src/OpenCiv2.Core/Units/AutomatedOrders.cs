using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public abstract record AutomatedOrderDecision
{
    public sealed record Tactics(UnitTacticsDecision Decision) : AutomatedOrderDecision;
    public sealed record Movement(MovementDecision Decision) : AutomatedOrderDecision;
}
public sealed record AutomatedOrderReply(UnitTacticsReply? Tactics = null, ResearchReply? Movement = null);

/// <summary>U04 automated active-order continuation with actual A04 choice and U04 execution.
/// Hold is an explicit admission input; flushing presentation input is not a gameplay callback.</summary>
public sealed class AutomatedOrders(GameState world, RuleTables rules, UnitTactics tactics, UnitOrderExecution orders)
{
    private GameState World => world;
    public Operation Begin(StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call) => new(this, starts, inputs, hold, call);
    public sealed class Operation : IDisposable
    {
        private readonly AutomatedOrders parent;
        private readonly IEnumerator<AutomatedOrderDecision?> steps;
        private readonly List<Exception> failures = [];
        private AutomatedOrderReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; internal set; }
        public AutomatedOrderDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(AutomatedOrders parent, StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, starts, inputs, hold, call).GetEnumerator();
        }
        internal AutomatedOrderReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public AutomatedOrderDecision? Resume(AutomatedOrderReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Failed automated orders cannot resume");
            }

            if (Completed)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                {
                    return Pending;
                }

                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                {
                    return Pending = steps.Current;
                }

                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<AutomatedOrderDecision?> Execute(Operation op, StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call)
    {
        var actor = world.Globals.ActiveUnit ?? throw new InvalidOperationException("Automated orders require an active unit");
        var unit = world.Entities.Units.At(actor);
        bool execute = unit.MovesUsed != 0 && unit.Orders == UnitOrder.GoTo && !hold
            && (rules.Units[unit.Type.Value].Role != RuleRole.NavalSuperiority
                || SpatialControl.AdjacentNonalliedUnit(world, unit.Position, unit.Owner.Value) is null);
        if (!execute)
        {
            using var child = tactics.Begin(world.Globals.ActiveUnit!.Value, starts, call + "/tactics");
            while (child.Result is null)
            {
                var pending = child.Resume(op.TakeReply()?.Tactics);
                if (pending is not null)
                {
                    yield return new AutomatedOrderDecision.Tactics(pending);
                }
                else if (child.Result is null)
                {
                    yield return null;
                }
            }
            op.Report(child.PresentationFailures);
            execute = child.Result == 0 && UnitMovement.Left(world, rules, world.Entities.Units.At(actor)) != 0;
        }
        if (execute)
        {
            using var child = orders.Begin(actor, inputs, call + "/orders");
            while (!child.Completed)
            {
                var pending = child.Resume(op.TakeReply()?.Movement);
                if (pending is not null)
                {
                    yield return new AutomatedOrderDecision.Movement(pending);
                }
                else if (!child.Completed)
                {
                    yield return null;
                }
            }
            op.Report(child.PresentationFailures);
            if (child.Result == 0)
            {
                UnitMovement.Exhaust(world, rules, ref world.Entities.Units.At(actor));
            }
        }
        op.Completed = true;
    }
}
