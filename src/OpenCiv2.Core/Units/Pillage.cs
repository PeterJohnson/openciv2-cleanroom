using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>W04 core pillage. Player command eligibility/menu/war declaration belong to the caller.
/// A completed, allowed operation consumes movement even when no feature changed.</summary>
public sealed class Pillage(GameState world, RuleTables rules, AttackConfirmation confirmation,
    SharedGoals goals, Action<MapPoint, int> redraw)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    public Operation Begin(UnitSlot unit, MapFeatures selection = MapFeatures.None) => new(this, unit, selection);

    public sealed class Operation : IDisposable
    {
        private readonly Pillage parent;
        private readonly IEnumerator<AttackWarning> steps;
        private readonly List<Exception> failures = [];
        private int? reply;
        private bool disposed, faulted;
        public bool? Applied { get; internal set; }
        public bool Completed => Applied is not null;
        public AttackWarning? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(Pillage parent, UnitSlot unit, MapFeatures selection)
        { this.parent = parent; steps = parent.Execute(this, unit, selection).GetEnumerator(); }
        internal int? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Present(Action action)
        { try { action(); } catch (Exception error) { failures.Add(error); } }
        public AttackWarning? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed pillage cannot resume"); }
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

    private IEnumerable<AttackWarning> Execute(Operation operation, UnitSlot unit, MapFeatures selection)
    {
        var point = Unit(unit).Position;
        int owner = Unit(unit).Owner.Value;
        var nearest = CityQueries.Nearest(world, point).Slot;
        if (nearest is { } city && world.Entities.Cities.At(city).Owner.Value != owner
            && world.Globals.HumanPlayers.Contains(owner))
        {
            using var child = confirmation.Begin(owner, world.Entities.Cities.At(city).Owner.Value,
                new() { Ceasefire = true, Peace = true, Alliance = true });
            while (!child.Completed)
            {
                var pending = child.Resume(operation.TakeReply());
                if (pending is not null)
                { yield return pending; }
            }
            if (child.Blocked)
            { operation.Applied = false; yield break; }
        }
        var features = world.Map.TileAt(point).Features;
        if (selection == MapFeatures.None)
        {
            selection = Unit(unit).Type.Value == UnitTypeIds.Partisans && features.HasFlag(MapFeatures.Road)
                ? features.HasFlag(MapFeatures.Railroad) ? MapFeatures.Railroad : MapFeatures.Road
                : (features & (MapFeatures.Mine | MapFeatures.Irrigation)) != 0
                    ? features.HasFlag(MapFeatures.Mine) ? MapFeatures.Mine : MapFeatures.Irrigation
                    : features.HasFlag(MapFeatures.Fortification) ? MapFeatures.Fortification | MapFeatures.CitySite
                    : features.HasFlag(MapFeatures.Railroad) ? MapFeatures.Railroad : MapFeatures.Road;
        }
        world.Map.Mutate(new TileChange.Features(point, selection, false), true);
        nearest = CityQueries.Nearest(world, point).Slot;
        if (nearest is { } selected)
        {
            MapKnowledge.Remember(world, Unit(unit).Owner.Value, point);
            MapKnowledge.Remember(world, world.Entities.Cities.At(selected).Owner.Value, point);
            DiplomaticRelations.Set(world, Unit(unit).Owner.Value, world.Entities.Cities.At(selected).Owner.Value, new() { War = true });
            if (Unit(unit).Owner != world.Entities.Cities.At(selected).Owner)
            { goals.AddCurrent(Unit(unit).Owner.Value, point, (int)RuleRole.Attack, 4); }
        }
        operation.Present(() => redraw(point, 1));
        UnitMovement.Exhaust(world, rules, ref Unit(unit));
        operation.Applied = true;
    }
}
