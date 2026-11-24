using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public sealed record MovementFinishInputs(bool OrdinaryArrival, bool OriginOcean, bool HasHut, bool Boarding,
    int PriorResult, bool LocalSeen, bool Unanimated, int CaptureNumber);
public abstract record MovementCompletionDecision
{
    public sealed record Contact(DiplomaticContactDecision Decision) : MovementCompletionDecision;
    public sealed record Capture(CityCaptureDecision Decision) : MovementCompletionDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : MovementCompletionDecision;
    public sealed record Hut(HutCityVisit Visit) : MovementCompletionDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : MovementCompletionDecision;
    public sealed record LongMove(LongMoveQuestion Question) : MovementCompletionDecision;
}
public sealed record MovementCompletionServices(CityCapture Capture, UnitRevelation Revelation,
    SpecialMovementFinish Special, HutEntry Huts, TransportMovement Transport, LongMove LongMove);
public interface IMovementCompletionEffects
{
    void CityUnitsChanged(UnitTarget unit, MapPoint oldPosition);
    /// <summary>Optional cosmetic delay; skip when Shift is pressed. Does not admit a gameplay interleaving.</summary>
    void PauseEnemyMovement(int ticks);
}

/// <summary>U02 local ordinary arrival, common finish and cleanup with actual gameplay children.
/// The dispatcher admits/relocates or supplies the combat/negative-direction finish route.</summary>
public sealed class MovementCompletion(GameState world, RuleTables rules, StackOperations stacks,
    MovementCompletionServices services, IMovementCompletionEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    public Operation Begin(MovementContinuation movement, MovementStep step, MovementFinishInputs inputs, string call) => new(this, movement, step, inputs, call);
    public sealed class Operation : IDisposable
    {
        private readonly MovementCompletion parent;
        private readonly IEnumerator<MovementCompletionDecision?> steps;
        private readonly List<Exception> presentationFailures = [];
        private ResearchReply? reply;
        private bool faulted, disposed;
        public int? Result { get; internal set; }
        public bool Completed => Result is not null;
        public MovementCompletionDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        internal Operation(MovementCompletion parent, MovementContinuation movement, MovementStep step, MovementFinishInputs inputs, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, movement, step, inputs, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var result = reply; reply = null; return result; }
        internal void Report(IEnumerable<Exception> failures) => presentationFailures.AddRange(failures);
        internal void Present(Action action) { try { action(); } catch (Exception error) { presentationFailures.Add(error); } }
        public MovementCompletionDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed movement completion cannot resume"); }
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
    private IEnumerable<MovementCompletionDecision?> Execute(Operation operation, MovementContinuation movement, MovementStep step,
        MovementFinishInputs inputs, string call)
    {
        UnitSlot finishing = movement.Unit;
        if (inputs.OrdinaryArrival)
        {
            if (step.Domain == RuleDomain.Land && inputs.OriginOcean && step.DestinationTerrain != Terrain.Ocean)
            {
                int region = world.Map.TileAt(step.To).Region;
                ref var count = ref world.Civilizations[step.Owner].ContinentMilitary[region];
                count = unchecked((ushort)(count + 1));
            }
            if (step.City is { } city && step.DestinationOwner is { } victim && victim != step.Owner)
            {
                Unit(finishing).Damage = 0;
                world.Globals.MovingUnit = finishing;
                using var capture = services.Capture.Begin(city, step.Owner, 0, inputs.CaptureNumber, call + "/capture");
                while (!capture.Completed)
                {
                    if (capture.Resume(operation.TakeReply()) is { } decision)
                    { yield return new MovementCompletionDecision.Capture(decision); }
                    else if (!capture.Completed)
                    { yield return null; }
                }
                operation.Report(capture.PresentationFailures);
                finishing = world.Globals.MovingUnit ?? throw new InvalidOperationException("Capture did not retain a finishing moving unit");
                movement.Unit = finishing;
            }
            int observer = world.Multiplayer.LocalHuman;
            if (step.Owner == 0 && step.DestinationTerrain != Terrain.Ocean && world.Map.TileAt(step.To).RevealedTo.Contains(observer)
                && !Unit(finishing).Visibility.Contains(step.Owner) && Unit(finishing).Owner.Value != step.Owner)
            {
                var nearest = CityQueries.Nearest(world, step.To).Slot;
                if (nearest is { } nearby && world.Entities.Cities.At(nearby).Owner.Value == observer)
                {
                    for (int current = stacks.First(finishing.Value); current >= 0; current = stacks.Next(current))
                    { world.Entities.Units.Storage[current].Visibility = world.Entities.Units.Storage[current].Visibility.Union(world.Map.TileAt(step.To).RevealedTo); }
                }
            }
            services.Transport.ArriveInPort(finishing, inputs.OriginOcean, step.City, step.Owner);
        }
        movement.Unit = finishing;
        if (Unit(finishing).Next is not null && Rule(finishing).Attack == 0)
        { stacks.MoveToBottom(finishing.Value, false); }
        using (var revelation = services.Revelation.Begin(finishing.Value, true, call + "/revelation"))
        {
            while (!revelation.Completed)
            {
                var decision = revelation.Resume(operation.TakeReply());
                if (!revelation.Completed)
                { yield return decision is null ? null : new MovementCompletionDecision.Contact(decision); }
            }
            operation.Report(revelation.PresentationFailures);
        }
        operation.Present(() => effects.CityUnitsChanged(new(finishing, Unit(finishing).Token), step.From));
        if (world.Multiplayer.LocalHuman != step.Owner
            && inputs.LocalSeen
            && !world.Globals.GameOptions.SkipEnemyPauses)
        { operation.Present(() => effects.PauseEnemyMovement(!world.Globals.GameOptions.EnemyMoves || inputs.Unanimated ? 10 : 15)); }
        if (Rule(finishing).Domain == RuleDomain.Air)
        {
            using var aircraft = services.Special.BeginAircraft(movement, step.To, call + "/aircraft");
            while (!aircraft.Completed)
            {
                if (aircraft.ResumeDecision(operation.TakeReply()) is { } question)
                { yield return new MovementCompletionDecision.Lifecycle(question); }
                else if (!aircraft.Completed)
                { yield return null; }
            }
            operation.Report(aircraft.PresentationFailures);
            if (aircraft.Stopped)
            { Finish(inputs.PriorResult); yield break; }
        }
        using (var coast = services.Special.BeginCoastBound(movement, step.Owner, step.To, call + "/coast"))
        {
            while (!coast.Completed)
            {
                if (coast.ResumeDecision(operation.TakeReply()) is { } question)
                { yield return new MovementCompletionDecision.Lifecycle(question); }
                else if (!coast.Completed)
                { yield return null; }
            }
            operation.Report(coast.PresentationFailures);
            if (coast.Stopped)
            { Finish(inputs.PriorResult); yield break; }
        }
        if (inputs.HasHut && Rule(finishing).Domain == RuleDomain.Land)
        {
            using var hut = services.Huts.Begin(finishing, movement, call + "/hut");
            while (!hut.Completed)
            {
                var pending = hut.ResumeDecision(operation.TakeReply());
                if (pending is HutCityVisit visit)
                { yield return new MovementCompletionDecision.Hut(visit); }
                else if (pending is HutDecision.Grant grant)
                { yield return new MovementCompletionDecision.Grant(grant.Decision); }
                else if (pending is HutDecision.Contact contact)
                { yield return new MovementCompletionDecision.Contact(contact.Decision); }
                else if (!hut.Completed)
                { yield return null; }
            }
            operation.Report(hut.PresentationFailures);
        }
        movement.Unit = finishing;
        services.Transport.Explore(movement, step.Owner, step.Domain, step.To);
        if (inputs.Boarding)
        { operation.Report(services.Transport.Board(finishing, step.To)); }
        using (var longMove = services.LongMove.Begin(finishing, step.Direction, movement.LocalHuman))
        {
            while (!longMove.Completed)
            {
                if (longMove.Resume(operation.TakeReply()?.Selected) is { } question)
                { yield return new MovementCompletionDecision.LongMove(question); }
            }
        }
        Unit(finishing).LastDirection = unchecked((byte)step.Direction);
        Finish(1);
        void Finish(int result)
        { movement.Unit = finishing; operation.Result = MovementCleanup.Apply(world, rules, movement, step.Owner, result); }
    }
}
