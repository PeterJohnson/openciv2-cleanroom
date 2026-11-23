using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public sealed record MovementDispatchInputs(MovementViewerInputs Viewers, LandEntrySelection? EntrySelection,
    int CityArgument, int CaptureNumber, string CityName, string DiplomatName,
    string CombatArgument, int? ExpulsionChoice = null, bool? ScenarioShowsCombat = null, int RoundBudget = 64);
public sealed record MovementDispatchServices(LandEntry Land, ForeignUnitMovement ForeignUnit,
    TransportMovement Transport, ForeignCityMovement ForeignCity, MovementCharge Charge,
    MovementRelocation Relocation, MovementCompletion Completion);
public abstract record MovementDecision
{
    public sealed record Land(LandEntryDecision Decision) : MovementDecision;
    public sealed record ForeignUnit(ForeignUnitDecision Decision) : MovementDecision;
    public sealed record Landfall(LandfallChoice Choice) : MovementDecision;
    public sealed record ForeignCity(ForeignCityDecision Decision) : MovementDecision;
    public sealed record Finish(MovementCompletionDecision Decision) : MovementDecision;
}
public interface IMovementDispatcherEffects
{
    void StopBlinking();
    void NotOnMap(int recipient, UnitTarget unit);
}

/// <summary>U02 local dispatcher with actual ordered children and retained landfall restarts.
/// Session admits pending commands before Begin; cosmetic message pumping is not a gameplay callback.
/// Network authority and unrecovered entry/view inputs remain outside the implicit input domain.</summary>
public sealed class MovementDispatcher(GameState world, RuleTables rules, StackOperations stacks,
    MovementDispatchServices services, IMovementDispatcherEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    public Operation Begin(UnitSlot? unit, int direction, MovementOptions options, MovementDispatchInputs inputs, string call) =>
        new(this, unit, direction, options, inputs, call);

    public sealed class Operation : IDisposable
    {
        private readonly MovementDispatcher parent;
        private readonly IEnumerator<MovementDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool faulted, disposed;
        public int? Result { get; internal set; }
        public bool Completed => Result is not null;
        public MovementDecision? Pending { get; private set; }
        public MovementContinuation? Movement { get; internal set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(MovementDispatcher parent, UnitSlot? unit, int direction, MovementOptions options, MovementDispatchInputs inputs, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inputs.RoundBudget);
            this.parent = parent;
            steps = parent.Execute(this, unit, direction, options, inputs, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public MovementDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed movement dispatcher cannot resume"); }
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
    private IEnumerable<MovementDecision?> Execute(Operation operation, UnitSlot? requested, int direction,
        MovementOptions options, MovementDispatchInputs inputs, string call)
    {
        if (world.UnitMovementActive || requested is not { } initial || Unit(initial).Position.X < 0 || Unit(initial).Position.Y < 0)
        { operation.Result = 0; yield break; }
        world.UnitMovementActive = true;
        try
        {
            var movement = new MovementContinuation(initial, 0, default) { Options = options };
            operation.Movement = movement;
            int? choice = inputs.ExpulsionChoice;
            for (int iteration = 0; ; ++iteration)
            {
                UnitSlot original = movement.Unit;
                if (movement.Options.Prompt)
                { operation.Present(effects.StopBlinking); }
                movement.UnitBound = world.Entities.Units.End;
                world.Globals.TransientFlags.AttitudeDirty = true;
                var from = Unit(original).Position;
                movement.Origin = from;
                var to = direction < 0 ? from : MapNeighborhoods.Offset(world.Map.Geometry, from,
                    MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
                var domain = rules.Units[Unit(original).Type.Value].Domain;
                int owner = unchecked((sbyte)Unit(original).Owner.Value);
                if (CityQueries.At(world, from) is null)
                { world.Map.Mutate(new TileChange.Owner(from, unchecked((byte)owner)), synchronize: true); }
                movement.LocalHuman = !world.Multiplayer.Saved.AutoplayActive && world.Multiplayer.LocalHuman == owner;
                if (!world.Map.Geometry.InBounds(to))
                {
                    if (movement.Options.Prompt && movement.LocalHuman)
                    { effects.NotOnMap(owner, new(original, Unit(original).Token)); }
                    movement.Unit = original;
                    if (Unit(original).Flags.Automated)
                    { UnitMovement.Exhaust(world, rules, ref Unit(original)); }
                    Stop(0);
                    yield break;
                }
                bool originOcean = world.Map.TileAt(from).Ocean;
                var terrain = world.Map.TileAt(to).Terrain;
                int? destinationOwner = world.Map.CityOrUnitOwner(to);
                int first = stacks.FirstAt(to);
                UnitSlot? target = first < 0 ? null : new((ushort)first);
                if (target is { } occupant)
                { destinationOwner = unchecked((sbyte)Unit(occupant).Owner.Value); }
                var city = CityQueries.At(world, to);
                if (city is { } citySlot)
                { destinationOwner = unchecked((sbyte)world.Entities.Cities.At(citySlot).Owner.Value); }
                var step = new MovementStep(owner, domain, from, to, terrain, direction, target, destinationOwner, city);
                string iterationCall = call + "/" + iteration;
                bool ordinary = direction >= 0, boarding = false;
                if (direction < 0)
                { UnitMovement.Exhaust(world, rules, ref Unit(original)); }
                else
                {
                    Unit(original).Flags.Moved = true;
                    if (domain == RuleDomain.Land)
                    {
                        using var land = services.Land.Begin(original, movement, step, originOcean, inputs.EntrySelection, inputs.CityArgument, iterationCall + "/land");
                        while (!land.Completed)
                        {
                            if (land.Resume(operation.TakeReply()) is { } pending)
                            { yield return new MovementDecision.Land(pending); }
                            else if (!land.Completed)
                            { yield return null; }
                        }
                        operation.Report(land.PresentationFailures);
                        if (land.Result!.Stopped)
                        { Stop(0); yield break; }
                        step = step with { DestinationOwner = land.Result.DestinationOwner };
                    }
                    if (domain == RuleDomain.Land && terrain == Terrain.Ocean)
                    {
                        movement.Unit = original;
                        if (step.DestinationOwner != owner || stacks.SpareTransportCapacity(target?.Value ?? -1, rules) <= 0)
                        { Stop(0); yield break; }
                        boarding = true;
                    }
                    if (target is { } foreign && unchecked((sbyte)Unit(foreign).Owner.Value) != owner)
                    {
                        using var entry = services.ForeignUnit.Begin(original, foreign, movement, step, originOcean,
                            inputs.CityName, inputs.DiplomatName, choice, inputs.CombatArgument, iterationCall + "/foreign-unit", inputs.ScenarioShowsCombat, inputs.RoundBudget);
                        while (!entry.Completed)
                        {
                            if (entry.Resume(operation.TakeReply()) is { } pending)
                            { yield return new MovementDecision.ForeignUnit(pending); }
                            else if (!entry.Completed)
                            { yield return null; }
                        }
                        operation.Report(entry.PresentationFailures);
                        if (!entry.Result!.Finish)
                        { Stop(entry.Result.Value); yield break; }
                        step = step with { To = entry.Result.Destination };
                        ordinary = false;
                    }
                    else if (domain == RuleDomain.Sea && terrain != Terrain.Ocean && city is null)
                    {
                        using var landfall = services.Transport.BeginLandfall(original, movement);
                        while (!landfall.Completed)
                        {
                            var reply = operation.TakeReply();
                            if (reply is { } answered)
                            { choice = answered.Selected; }
                            if (landfall.Resume(reply?.Selected) is { } pending)
                            { yield return new MovementDecision.Landfall(pending); }
                        }
                        if (landfall.Restart)
                        { yield return null; continue; }
                        Stop(0);
                        yield break;
                    }
                    else if (city is { } foreignCity && unchecked((sbyte)world.Entities.Cities.At(foreignCity).Owner.Value) != owner)
                    {
                        using var entry = services.ForeignCity.Begin(original, movement, step, iterationCall + "/foreign-city");
                        while (!entry.Completed)
                        {
                            if (entry.Resume(operation.TakeReply()?.Selected) is { } pending)
                            { yield return new MovementDecision.ForeignCity(pending); }
                            else if (!entry.Completed)
                            { yield return null; }
                        }
                        operation.Report(entry.PresentationFailures);
                        if (!entry.Result!.Proceed)
                        { Stop(entry.Result.Value); yield break; }
                    }
                }
                bool hut = false;
                // Without relocation publication there is no optional enemy pause to schedule.
                bool seen = false;
                int unanimated = 0;
                if (ordinary)
                {
                    var charged = services.Charge.Apply(movement.Unit, step, movement.Options, movement.LocalHuman, iterationCall + "/charge");
                    if (!charged.Proceed)
                    { Stop(charged.ReturnValue); yield break; }
                    var moved = services.Relocation.Apply(movement.Unit, step, originOcean, charged.Cost, inputs.Viewers with { Unanimated = 0 });
                    operation.Report(moved.PresentationFailures);
                    hut = moved.HasHut;
                    seen = moved.LocalSeen;
                    unanimated = moved.Unanimated;
                }
                using var finish = services.Completion.Begin(movement, step,
                    new(ordinary, originOcean, hut, boarding, 0, seen, unanimated != 0, inputs.CaptureNumber), iterationCall + "/finish");
                while (!finish.Completed)
                {
                    if (finish.Resume(operation.TakeReply()) is { } pending)
                    { yield return new MovementDecision.Finish(pending); }
                    else if (!finish.Completed)
                    { yield return null; }
                }
                operation.Report(finish.PresentationFailures);
                operation.Result = finish.Result;
                yield break;

                void Stop(int result)
                { operation.Result = MovementCleanup.Apply(world, rules, movement, owner, result); }
            }
        }
        finally { world.UnitMovementActive = false; }
    }
}
