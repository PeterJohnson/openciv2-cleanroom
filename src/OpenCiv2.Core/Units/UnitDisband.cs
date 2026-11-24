using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public abstract record DisbandDecision
{
    public sealed record Unit(int Actor, UnitTarget Target, string Name) : DisbandDecision;
    public sealed record City(int Actor, CityTarget Target, string Name) : DisbandDecision;
    public sealed record Lifecycle(LifecycleDecision Decision) : DisbandDecision;
}
public interface IDisbandEffects
{
    CitySlot? DisplayedCity { get; }
    void RefreshCity(CitySlot city);
    void RedrawArea(MapPoint position, int observer);
    void RedrawMaps(int observer);
}

/// <summary>U04 Disband: accepted live shield credit, actual C06 build snapshot and
/// U01 cargo loss with retained V01/V06 children. The local editor branch joins C01
/// removal and the captured owner's elimination check. Network admission is separate.</summary>
public sealed class UnitDisband(GameState world, RuleTables rules, StackOperations stacks,
    ProductionOrders production, UnitLoss loss, CityRemoval removal, CivilizationElimination lifecycle,
    EspionageIncidents incidents, IDisbandEffects effects)
{
    private GameState World => world;
    public Operation Begin(MapPoint cursor, string call) => new(this, cursor, call);

    public sealed class Operation : IDisposable
    {
        private readonly UnitDisband parent;
        private readonly IEnumerator<DisbandDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? answer;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public bool Disbanded { get; internal set; }
        public DisbandDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(UnitDisband parent, MapPoint cursor, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, cursor, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = answer; answer = null; return value; }
        internal void Present(Action action)
        { try { action(); } catch (Exception error) { failures.Add(error); } }
        internal void Failures(IReadOnlyList<Exception> errors) => failures.AddRange(errors);
        public DisbandDecision? Resume(int? reply = null) => ResumeDecision(reply is { } value ? new(value) : null);
        public DisbandDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed disband operation cannot resume"); }
            if (Completed)
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
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private IEnumerable<DisbandDecision?> Child(Operation operation, CivilizationElimination.Operation child)
    {
        using (child)
        {
            while (child.Result is null)
            {
                var decision = child.ResumeDecision(operation.TakeReply());
                if (decision is not null)
                { yield return new DisbandDecision.Lifecycle(decision); }
                else if (child.Result is null)
                { yield return null; }
            }
            operation.Failures(child.PresentationFailures);
        }
    }
    private IEnumerable<DisbandDecision?> Execute(Operation operation, MapPoint cursor, string call)
    {
        int unit = world.MovementMode == MovementMode.MoveUnits ? world.Globals.ActiveUnit?.Value ?? -1 : stacks.FirstAt(cursor);
        if (unit >= 0)
        {
            ref var selected = ref world.Entities.Units.Storage[unit];
            if (selected.Owner.Value != world.Multiplayer.LocalHuman && world.Globals.RevealMap == 0)
            { yield break; }
            yield return new DisbandDecision.Unit(world.Multiplayer.LocalHuman,
                new(new((ushort)unit), selected.Token), rules.Units[selected.Type.Value].Identity.Name);
            if (operation.TakeReply()?.Selected != 1)
            { yield break; }
            // The slot is captured; position, type and price are read after the real decision.
            var position = world.Entities.Units.Storage[unit].Position;
            if (CityQueries.At(world, position) is { } city)
            {
                int credit = unchecked((sbyte)rules.Units[world.Entities.Units.Storage[unit].Type.Value].CostRows * rules.Cosmic.ShieldBoxRows) / 2;
                world.Entities.Cities.At(city).ShieldBox = unchecked((short)(world.Entities.Cities.At(city).ShieldBox + credit));
                if (effects.DisplayedCity == city)
                {
                    production.SaveBuildState(city);
                    if (effects.DisplayedCity == city)
                    { operation.Present(() => effects.RefreshCity(city)); }
                }
            }
            foreach (var decision in Child(operation, lifecycle.BeginEraseCargo(loss, unit, call + "/unit", incidents)))
            { yield return decision; }
            operation.Disbanded = true;
            operation.Present(() => effects.RedrawArea(position, world.Multiplayer.LocalHuman));
        }
        else if (world.Globals.GameOptions.CheatMode && world.MovementMode == MovementMode.View
            && CityQueries.At(world, cursor) is { } city)
        {
            int owner = unchecked((sbyte)world.Entities.Cities.At(city).Owner.Value);
            yield return new DisbandDecision.City(world.Multiplayer.LocalHuman,
                new(city, world.Entities.Cities.At(city).Token), world.Entities.Cities.At(city).Name);
            if (operation.TakeReply()?.Selected != 1)
            { yield break; }
            foreach (var decision in Child(operation, lifecycle.BeginRemoveCity(removal, city.Value, call + "/city", incidents)))
            { yield return decision; }
            foreach (var decision in Child(operation, lifecycle.Begin(owner, 0, call + "/owner", incidents)))
            { yield return decision; }
            operation.Disbanded = true;
            operation.Present(() => effects.RedrawMaps(world.Multiplayer.LocalHuman));
        }
    }
}
