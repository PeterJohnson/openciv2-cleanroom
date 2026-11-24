using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public readonly record struct HumanMovementView(MapPoint Cursor, CitySlot? SelectedCity);
public interface IHumanMovementPhaseEffects
{
    void FocusInput();
    void EndOfTurn(bool moveUnits);
    void CloseCity(CitySlot city);
    void FlushInput();
}
public abstract record HumanMovementDecision
{
    public sealed record IdleInput(int Observer) : HumanMovementDecision;
    public sealed record Contact(DiplomaticContactDecision Value) : HumanMovementDecision;
    public sealed record Active(ActiveUnitDecision Value) : HumanMovementDecision;
}
public abstract record HumanMovementReply
{
    public sealed record InputHandled : HumanMovementReply;
    public sealed record Contact(ResearchReply Value) : HumanMovementReply;
    public sealed record Active(ActiveUnitReply Value) : HumanMovementReply;
}

/// <summary>F04 human movement phase with actual U04 selection, focus and execution.
/// Input boundaries release scheduling to the host; native blink/music/empty pump loops are
/// presentation policy. Idle input never sets WaitedForInput. Hold is an explicit U04 input.</summary>
public sealed class HumanMovementPhase(GameState world, RuleTables rules, UnitSelection selection,
    UnitFocus focus, ActiveUnitCycle cycle, IHumanMovementPhaseEffects effects)
{
    private GameState World => world;
    public Operation Begin(StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call) =>
        new(this, starts, inputs, hold, call);

    public sealed class Operation : IDisposable
    {
        private readonly HumanMovementPhase parent;
        private readonly IEnumerator<HumanMovementDecision?> steps;
        private readonly List<Exception> failures = [];
        private HumanMovementReply? reply;
        private bool faulted, disposed;
        internal HumanMovementView View { get; private set; }
        public bool Completed { get; internal set; }
        public bool WaitedForInput { get; internal set; }
        public bool AutomaticallyEnded { get; internal set; }
        public bool StopRequested { get; private set; }
        public HumanMovementDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(HumanMovementPhase parent, StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, starts, inputs, hold, call).GetEnumerator();
        }
        /// <summary>Ends this phase after an accepted child completes. This is a host stop,
        /// not U04's End Player Turn command, which only exhausts the local units.</summary>
        public void RequestStop() => StopRequested = true;
        internal HumanMovementReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public HumanMovementDecision? Resume(HumanMovementView view, HumanMovementReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed human movement phase cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                View = view;
                if (StopRequested && Pending is HumanMovementDecision.IdleInput)
                { answer = new HumanMovementReply.InputHandled(); }
                if (StopRequested && Pending is HumanMovementDecision.Active { Value: ActiveUnitDecision.PlayerInput })
                { answer = new HumanMovementReply.Active(new ActiveUnitReply.InputHandled()); }
                if (Pending is not null && answer is null)
                { return Pending; }
                if (Pending is HumanMovementDecision.IdleInput && answer is not HumanMovementReply.InputHandled
                    || Pending is HumanMovementDecision.Contact && answer is not HumanMovementReply.Contact
                    || Pending is HumanMovementDecision.Active && answer is not HumanMovementReply.Active)
                { throw new InvalidOperationException("Reply does not match the human movement boundary"); }
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

    private IEnumerable<HumanMovementDecision?> Execute(Operation op, StartingPositions starts, MovementDispatchInputs inputs, bool hold, string call)
    {
        world.Globals.ActiveUnit = null;
        op.Present(effects.FocusInput);
        world.PlayerPhaseActive = true;
        int previousBound = -1, iteration = 0;
        while (true)
        {
            string step = call + "/iteration-" + iteration;
            if (world.MovementMode == MovementMode.MoveUnits)
            {
                if (previousBound != world.Entities.Units.End
                    || !UnitActivation.Selectable(world, rules, world.Globals.ActiveUnit?.Value ?? -1))
                {
                    using var child = selection.BeginNext(op.View.Cursor, 0, world.Multiplayer.LocalHuman, step + "/select");
                    while (!child.Completed)
                    {
                        var pending = child.Resume((op.TakeReply() as HumanMovementReply.Contact)?.Value);
                        if (pending is not null)
                        { yield return new HumanMovementDecision.Contact(pending); }
                        else if (!child.Completed)
                        { yield return null; }
                    }
                    op.Report(child.PresentationFailures);
                }
                else
                {
                    using var child = focus.Begin(op.View.Cursor, 0, world.Multiplayer.LocalHuman, step + "/focus");
                    while (!child.Completed)
                    {
                        var pending = child.Resume((op.TakeReply() as HumanMovementReply.Contact)?.Value);
                        if (pending is not null)
                        { yield return new HumanMovementDecision.Contact(pending); }
                        else if (!child.Completed)
                        { yield return null; }
                    }
                    op.Report(child.PresentationFailures);
                }
            }
            previousBound = world.Entities.Units.End;
            bool automatic = false;
            if (world.Globals.ActiveUnit is null)
            {
                world.Globals.TransientFlags.EndOfTurn = true;
                op.Present(() => effects.EndOfTurn(world.MovementMode == MovementMode.MoveUnits));
                automatic = op.WaitedForInput && !world.Globals.GameOptions.Wait && !world.Globals.TransientFlags.CityWindowOpened;
            }
            if (automatic)
            {
                op.AutomaticallyEnded = true;
                world.PlayerPhaseActive = false;
            }
            else
            {
                world.Globals.TransientFlags.CityWindowOpened = false;
                if (world.Globals.ActiveUnit is null)
                {
                    // An event-driven host waits without spinning the original empty native pump.
                    // This scheduling boundary does not manufacture the active-cycle waited flag.
                    if (!op.StopRequested && world.PlayerPhaseActive && world.TurnLoopActive
                        && world.Globals.HumanPlayers.Contains(world.Multiplayer.LocalHuman))
                    { yield return new HumanMovementDecision.IdleInput(world.Multiplayer.LocalHuman); op.TakeReply(); }
                }
                else
                {
                    world.Globals.TransientFlags.EndOfTurn = false;
                    using var child = cycle.Begin(starts, inputs, hold, step + "/active");
                    while (!child.Completed)
                    {
                        if (op.StopRequested)
                        { child.RequestStop(); }
                        var pending = child.Resume((op.TakeReply() as HumanMovementReply.Active)?.Value);
                        if (pending is not null)
                        { yield return new HumanMovementDecision.Active(pending); }
                        else if (!child.Completed)
                        { yield return null; }
                    }
                    op.Report(child.PresentationFailures);
                    op.WaitedForInput |= child.WaitedForInput;
                }
                if (op.View.SelectedCity is null)
                { world.Globals.TransientFlags.CityWindowOpened = false; }
                // F04 deliberately returns here on session stop, before the common phase tail.
                if (!world.TurnLoopActive)
                { op.Completed = true; yield break; }
                if (world.Globals.TransientFlags.CityWindowOpened)
                { op.WaitedForInput = false; }
            }
            if (op.StopRequested || !world.TurnLoopActive || !world.PlayerPhaseActive
                || !world.Globals.HumanPlayers.Contains(world.Multiplayer.LocalHuman))
            {
                if (op.View.SelectedCity is { } city && world.Entities.Cities.Live(city)
                    && world.Entities.Cities.At(city).Owner.Value != world.Multiplayer.LocalHuman)
                { op.Present(() => effects.CloseCity(city)); }
                op.Present(effects.FlushInput);
                world.PlayerPhaseActive = false;
                op.Completed = true;
                yield break;
            }
            iteration = unchecked(iteration + 1);
            yield return null;
        }
    }
}
