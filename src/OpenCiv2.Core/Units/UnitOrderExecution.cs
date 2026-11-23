using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U04 order execution with actual W04 work and retained U02 movement.
/// A handled order returns one even when its child refuses, cancels or removes the actor.</summary>
public sealed class UnitOrderExecution(GameState world, RuleTables rules, UnitFortification fortification,
    TerrainWorkOrders work, UnitGoto directions, MovementDispatcher movement)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    public Operation Begin(UnitSlot unit, MovementDispatchInputs inputs, string call) => new(this, unit, inputs, call);

    public sealed class Operation : IDisposable
    {
        private readonly UnitOrderExecution parent;
        private readonly IEnumerator<MovementDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool faulted, disposed;
        public int? Result { get; internal set; }
        public int? MovementResult { get; internal set; }
        public bool Completed => Result is not null;
        public MovementDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(UnitOrderExecution parent, UnitSlot unit, MovementDispatchInputs inputs, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, unit, inputs, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public MovementDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed unit orders cannot resume"); }
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

    private IEnumerable<MovementDecision?> Execute(Operation operation, UnitSlot unit, MovementDispatchInputs inputs, string call)
    {
        var order = Unit(unit).Orders;
        switch (order)
        {
            case UnitOrder.Fortifying:
                fortification.Apply(unit);
                break;
            case UnitOrder.Fortress:
            case UnitOrder.Road:
            case UnitOrder.Irrigate:
            case UnitOrder.Mine:
            case UnitOrder.Transform:
            case UnitOrder.Clean:
            case UnitOrder.Airbase:
                work.Execute(unit, Unit(unit).Orders);
                break;
            case UnitOrder.GoTo:
            case UnitOrder.AiGoTo:
                int owner = Unit(unit).Owner.Value;
                bool ignoreOwner = ((sbyte)order & 0x10) != 0;
                world.Paths.Owner = ignoreOwner ? -1 : owner;
                int direction = directions.Direction(unit);
                if (direction < 0)
                {
                    Unit(unit).Orders = UnitOrder.None;
                    if (Rule(unit).Role == RuleRole.SeaTransport)
                    { Unit(unit).Counter = unchecked((byte)(world.Globals.Turn % 8)); }
                }
                else if (direction != 8)
                {
                    using var child = movement.Begin(unit, direction, new(true, true), inputs, call);
                    while (!child.Completed)
                    {
                        var pending = child.Resume(operation.TakeReply());
                        if (!child.Completed)
                        { yield return pending; }
                    }
                    operation.Report(child.PresentationFailures);
                    operation.MovementResult = child.Result;
                    if (child.Result == 0)
                    { operation.Result = 1; yield break; }
                    Unit(unit).Flags.GoalReached = false;
                    if (Unit(unit).Position == Unit(unit).Destination && !ignoreOwner)
                    {
                        Unit(unit).Orders = UnitOrder.None;
                        Unit(unit).Flags.GoalReached = true;
                        if (!world.Globals.HumanPlayers.Contains(owner)
                            && (Rule(unit).Domain == RuleDomain.Sea || unchecked((sbyte)Unit(unit).AuxiliaryTimer) > 9
                                && SpatialControl.AdjacentNonalliedUnit(world, Unit(unit).Position, owner) is null))
                        {
                            UnitMovement.Exhaust(world, rules, ref Unit(unit));
                            if (Rule(unit).Role == RuleRole.SeaTransport)
                            { Unit(unit).Counter = unchecked((byte)(world.Globals.Turn % 8)); }
                        }
                    }
                }
                world.Paths.Owner = -1;
                break;
            default:
                operation.Result = 0;
                yield break;
        }
        operation.Result = 1;
    }
}
