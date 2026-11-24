using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record BarbarianRansomText(bool Found, string? Message);
/// <summary>Required authored CITY/BARBARIANS content. Null message means no positive variant count.</summary>
public interface IBarbarianRansomText
{
    BarbarianRansomText Read(MapPoint city);
}
public interface IBarbarianRansomEffects
{
    void EnsureVisible(MapPoint point, int observer);
    void RefreshStatus();
    void Redraw(MapPoint point);
}
public enum BarbarianRansomOutcome { Continue, Finish, Paid }
public abstract record BarbarianRansomDecision
{
    public sealed record Offer(int Victim, UnitTarget Actor, CityTarget City, string CityName, string Message, int Demand) : BarbarianRansomDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : BarbarianRansomDecision;
}

/// <summary>W06 city ransom inside the empty foreign-city neighbor branch. The caller supplies
/// its retained nearest city and consumes the replacement city and Continue/Finish/Paid route.</summary>
public sealed class BarbarianRansom(GameState world, StackOperations stacks, UnitLoss loss,
    CivilizationElimination lifecycle, EspionageIncidents incidents, IBarbarianRansomText text,
    IBarbarianRansomEffects effects)
{
    private GameState World => world;
    public Operation Begin(UnitSlot actor, MapPoint neighbor, CitySlot? nearest, string call) => new(this, actor, neighbor, nearest, call);

    public sealed class Operation : IDisposable
    {
        private readonly BarbarianRansom parent;
        private readonly IEnumerator<BarbarianRansomDecision?> steps;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        private ResearchReply? answer;
        public CitySlot? City { get; internal set; }
        public BarbarianRansomOutcome? Result { get; internal set; }
        public BarbarianRansomDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(BarbarianRansom parent, UnitSlot actor, MapPoint neighbor, CitySlot? nearest, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            City = nearest;
            steps = parent.Execute(this, actor, neighbor, call).GetEnumerator();
        }
        internal ResearchReply? TakeDecision() { var result = answer; answer = null; return result; }
        internal int? TakeReply() => TakeDecision()?.Selected;
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public BarbarianRansomDecision? Resume(int? reply = null) => ResumeDecision(reply is { } value ? new(value) : null);
        public BarbarianRansomDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed barbarian ransom cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && reply is null)
                { return Pending; }
                answer = Pending is null ? null : reply;
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

    private IEnumerable<BarbarianRansomDecision?> Execute(Operation operation, UnitSlot actor, MapPoint neighbor, string call)
    {
        int victim = world.Map.CityOwner(neighbor)
            ?? throw new InvalidOperationException("Ransom requires the admitted neighbor city owner");
        var nearest = operation.City ?? throw new InvalidOperationException("Ransom requires the chooser's retained nearest city");
        int demand = Math.Max(50, Math.Max(world.Civilizations[victim].Gold / 2, 25 * world.Entities.Cities.At(nearest).Size));
        if (world.Globals.PowerRank[victim] == 7 && world.Globals.Year > 200 && world.Civilizations[victim].CityCount > 4)
        {
            demand = unchecked(world.Civilizations[victim].Gold / 100 * 100);
            if (demand < 1000)
            { demand = unchecked(world.Civilizations[victim].Gold + 1); }
        }
        if (!world.Globals.HumanPlayers.Contains(victim) || world.Civilizations[victim].Gold < demand)
        { operation.Result = BarbarianRansomOutcome.Continue; yield break; }
        operation.City = CityQueries.At(world, neighbor)
            ?? throw new InvalidOperationException("Ransom neighbor has no live city");
        var city = operation.City.Value;
        string name = world.Entities.Cities.At(city).Name;
        var content = text.Read(world.Entities.Cities.At(city).Position);
        if (!content.Found)
        { operation.Result = BarbarianRansomOutcome.Finish; yield break; }
        if (content.Message is null)
        { operation.Result = BarbarianRansomOutcome.Continue; yield break; }
        operation.Present(() => effects.EnsureVisible(world.Entities.Cities.At(city).Position, victim));
        if (world.Multiplayer.Saved.AutoplayActive)
        { operation.Result = BarbarianRansomOutcome.Continue; yield break; }
        yield return new BarbarianRansomDecision.Offer(victim, new(actor, world.Entities.Units.At(actor).Token),
            new(city, world.Entities.Cities.At(city).Token), name, content.Message, demand);
        if (operation.TakeReply() == 0)
        { operation.Result = BarbarianRansomOutcome.Continue; yield break; }

        world.Civilizations[victim].Gold = unchecked(world.Civilizations[victim].Gold - demand);
        operation.Present(effects.RefreshStatus);
        for (int i = 0; i < 20; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, world.Entities.Cities.At(city).Position,
                MapNeighborhoods.City[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point))
            { continue; }
            int first = stacks.FirstAt(point);
            if (first < 0 || world.Entities.Units.Storage[first].Owner.Value != 0)
            { continue; }
            using var child = lifecycle.BeginEraseStack(loss, first, call + "/stack-" + i, incidents);
            while (child.Result is null)
            {
                var decision = child.ResumeDecision(operation.TakeDecision());
                if (decision is not null)
                { yield return new BarbarianRansomDecision.Lifecycle(decision); }
                else if (child.Result is null)
                { yield return null; }
            }
            operation.Present(() => effects.Redraw(point));
        }
        operation.Result = BarbarianRansomOutcome.Paid;
    }
}
