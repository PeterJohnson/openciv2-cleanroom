using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum FoundingOutcome { Founded, Joined, Cancelled, Unavailable }
public enum FoundingRejection { RequiresSettler, Ocean, AdjacentCity, PopulationLimit }
public sealed record FoundingProblem(int Owner, UnitSlot Unit, FoundingRejection Reason, CitySlot? City = null, int Limit = 0);
public sealed record FoundedCity(int Owner, CityTarget City, string Name, int Year);
public abstract record FoundingDecision
{
    public sealed record Name(int Owner, CityTarget City, string InitialName, int MaximumLength = 15) : FoundingDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : FoundingDecision;
    public sealed record Window(CaptureCityVisit Request) : FoundingDecision;
}
/// <summary>Zero accepts naming; nonzero cancels. Lifecycle replies retain their own choice values.
/// Any reply closes the final city visit. Omitted name accepts the retained initial text.</summary>
public readonly record struct FoundingReply(int Choice, string? Name = null, ResearchReply? Research = null)
{
    public ResearchReply Decision => Research ?? new(Choice);
}
public interface ICityFoundingEffects
{
    void StopBlinking();
    void EnsureVisible(MapPoint position, int owner);
    void Redraw(MapPoint position, int observer);
    void RefreshCity(CitySlot city);
    void Rejected(FoundingProblem problem);
    void BuildSound();
    void Founded(FoundedCity city);
    void FirstProduction(int owner, CitySlot city);
}

/// <summary>C01 founding from a unit and player Build/Join City. Creation, basic unit deletion,
/// cancellation city removal and nested civilization checks use the actual owning services.</summary>
public sealed partial class CityFounding(GameState world, ProcessContext process, CityCreation creation,
    CityRemoval removal, CivilizationElimination lifecycle, EspionageIncidents incidents,
    CalendarValues calendar, ICityFoundingEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private ref State.City City(CitySlot slot) => ref world.Entities.Cities.At(slot);
    public Operation Begin(UnitSlot unit, string call, CitySlot? city = null, string? name = null) => new(this, unit, call, city, name, false);
    public Operation BeginPlayer(UnitSlot unit, string call) => new(this, unit, call, null, null, true);

    public sealed class Operation : IDisposable
    {
        private readonly CityFounding parent;
        private readonly IEnumerator<FoundingDecision?> steps;
        private readonly List<Exception> failures = [];
        private FoundingReply? reply;
        private bool disposed, faulted;
        public FoundingOutcome? Result { get; internal set; }
        public CitySlot? City { get; internal set; }
        public bool Completed => Result is not null;
        public FoundingDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(CityFounding parent, UnitSlot unit, string call, CitySlot? city, string? name, bool player)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = (player ? parent.Player(this, unit, call) : parent.Found(this, unit, city, name, call)).GetEnumerator();
        }
        internal FoundingReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action)
        { try { action(); } catch (Exception error) { failures.Add(error); } }
        public FoundingDecision? Resume(FoundingReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Failed city founding cannot resume");
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

    private IEnumerable<FoundingDecision?> Delete(Operation operation, UnitSlot unit, string call)
    {
        using var child = lifecycle.BeginDelete(unit.Value, true, call + "/delete", incidents);
        while (child.Result is null)
        {
            var pending = child.ResumeDecision(operation.TakeReply()?.Decision);
            if (pending is not null)
            {
                yield return new FoundingDecision.Lifecycle(pending);
            }
            else if (child.Result is null)
            {
                yield return null;
            }
        }
    }

    private IEnumerable<FoundingDecision?> Found(Operation operation, UnitSlot unit, CitySlot? suppliedCity, string? name, string call,
        bool complete = true)
    {
        var position = Unit(unit).Position;
        if (world.Globals.RevealMap != 0 || Unit(unit).Owner.Value == world.Multiplayer.LocalHuman)
        { operation.Present(() => effects.EnsureVisible(position, Unit(unit).Owner.Value)); }
        var selected = suppliedCity;
        if (selected is null)
        {
            using var child = creation.Begin(Unit(unit).Owner, position, call + "/create");
            while (!child.Completed)
            {
                var contact = child.Resume(operation.TakeReply()?.Decision);
                if (!child.Completed)
                { yield return contact is null ? null : new FoundingDecision.Lifecycle(new LifecycleContact(contact)); }
            }
            operation.Report(child.PresentationFailures);
            selected = child.Result;
        }
        operation.City = selected;
        if (selected is not { } city)
        { operation.Result = FoundingOutcome.Unavailable; yield break; }
        if (name is not null)
        {
            if (name.EnumerateRunes().Count() > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(name), "Founding names support at most 15 characters");
            }

            City(city).Name = name;
        }
        foreach (var pending in Delete(operation, unit, call))
        {
            yield return pending;
        }

        world.Map.Mutate(new TileChange.Features(position,
            MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad | MapFeatures.Fortification, false), true);
        for (int observer = 1; observer < EntityLimits.Civilizations; ++observer)
        { MapKnowledge.Remember(world, observer, position); }
        operation.Present(() => effects.Redraw(position, world.Multiplayer.LocalHuman));
        if (complete)
        {
            operation.Result = FoundingOutcome.Founded;
        }
    }
}
