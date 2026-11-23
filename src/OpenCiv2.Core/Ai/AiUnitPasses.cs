using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A01 local two-pass reverse unit scan. Each continuation executes actual U04/A04 children.
/// Network handoff and message processing belong to the deferred network caller.</summary>
public sealed class AiUnitPasses(GameState world, RuleTables rules, StackOperations stacks,
    AutomatedOrders orders, Action redrawCursor)
{
    private GameState World => world;
    public Operation Begin(StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call) => new(this, starts, inputs, hold, call);
    public sealed class Operation : IDisposable
    {
        private readonly AiUnitPasses parent;
        private readonly IEnumerator<AutomatedOrderDecision?> steps;
        private readonly List<Exception> failures = [];
        private AutomatedOrderReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; internal set; }
        public AutomatedOrderDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(AiUnitPasses parent, StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, starts, inputs, hold, call).GetEnumerator();
        }
        internal AutomatedOrderReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Redraw() { try { parent.Redraw(); } catch (Exception error) { failures.Add(error); } }
        public AutomatedOrderDecision? Resume(AutomatedOrderReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Failed AI unit passes cannot resume");
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
    private void Redraw() => redrawCursor();
    private IEnumerable<AutomatedOrderDecision?> Execute(Operation op, StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call)
    {
        int owner = world.Globals.TurnCiv;
        bool historical = world.Globals.Status.Scenario && world.Scenario.Flags.HistoricalAi
            && world.Globals.Turn == 1 && world.Scenario.ObjectiveCiv == owner;
        for (int pass = 0; pass < 2; ++pass)
        {
            for (int i = world.Entities.Units.End - 1; i >= 0; --i)
            {
                var unit = new UnitSlot((ushort)i);
                if (!world.Entities.Units.Live(unit) || world.Entities.Units.At(unit).Owner.Value != owner
                    || historical && pass == 0 && Rule(unit).Domain != RuleDomain.Air)
                {
                    continue;
                }

                int actions = 0, iteration = 0;
                while (UnitActivation.AwakeWithMoves(world, rules, i))
                {
                    int bound = world.Entities.Units.End;
                    world.Globals.ActiveUnit = unit;
                    using (var child = orders.Begin(starts, inputs, hold, call + "/" + pass + "/" + i + "/" + iteration++))
                    {
                        while (!child.Completed)
                        {
                            var pending = child.Resume(op.TakeReply());
                            if (!child.Completed)
                            {
                                yield return pending;
                            }
                        }
                        op.Report(child.PresentationFailures);
                    }
                    if (world.Entities.Units.End != bound)
                    {
                        break;
                    }

                    if (world.Entities.Units.At(unit).Orders != UnitOrder.GoTo && Rule(unit).Domain != RuleDomain.Air)
                    {
                        ++actions;
                    }

                    if (++actions > 20)
                    { UnitMovement.Exhaust(world, rules, ref world.Entities.Units.At(unit)); break; }
                    if (pass == 0 && owner != 0 && Rule(unit).Capacity != 0
                        && UnitMovement.Left(world, rules, world.Entities.Units.At(unit)) == 0
                        && world.Map.TileAt(world.Entities.Units.At(unit).Position).Ocean && stacks.Count(i) > 1
                        && (world.Entities.Units.At(unit).Visibility.Intersects(world.Globals.HumanPlayers)
                            || SpatialControl.AdjacentNonalliedUnit(world, world.Entities.Units.At(unit).Position, owner) is not null))
                    {
                        using var child = orders.Begin(starts, inputs, hold, call + "/" + pass + "/" + i + "/extra-" + iteration);
                        while (!child.Completed)
                        {
                            var pending = child.Resume(op.TakeReply());
                            if (!child.Completed)
                            {
                                yield return pending;
                            }
                        }
                        op.Report(child.PresentationFailures);
                    }
                    yield return null;
                }
                op.Redraw();
                yield return null;
            }
        }
        op.Completed = true;
    }
    private UnitRule Rule(UnitSlot slot) => rules.Units[world.Entities.Units.At(slot).Type.Value];
}
