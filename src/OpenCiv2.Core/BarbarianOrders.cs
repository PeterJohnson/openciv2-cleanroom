using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record BarbarianLandingNotice(int Observer, UnitTarget Actor, string CityName, MapPoint Position);
public interface IBarbarianOrderEffects
{
    void EnsureVisible(MapPoint point, int observer);
    void Redraw(MapPoint point);
    void Landing(BarbarianLandingNotice notice);
}
public abstract record BarbarianOrderDecision
{
    public sealed record Lifecycle(LifecycleDecision Decision) : BarbarianOrderDecision;
    public sealed record Ransom(BarbarianRansomDecision Decision) : BarbarianOrderDecision;
    public sealed record Pillage(AttackWarning Warning) : BarbarianOrderDecision;
}

/// <summary>W06 barbarian orders, dispatched before ordinary A04 entry. Selected orders are
/// executed later by U04; landing wakes cargo without relocating it. Local modes only.</summary>
public sealed partial class BarbarianOrders(GameState world, RuleTables rules, GameplayRandom random,
    StackOperations stacks, UnitGoto path, Pillage pillage, BarbarianRansom ransom,
    UnitLoss loss, CivilizationElimination lifecycle, EspionageIncidents incidents, IBarbarianOrderEffects effects)
{
    private GameState World => world;
    private ref Unit Actor(Context c) => ref world.Entities.Units.At(c.Unit);
    private UnitRule Rule(Context c) => rules.Units[c.Type];
    private int Role(Context c) => unchecked((sbyte)Rule(c).Role);
    private bool Fortress(MapPoint point) => (world.Map.TileAt(point).Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification;
    private MapPoint Offset(MapPoint point, int direction) => MapNeighborhoods.Offset(world.Map.Geometry, point,
        MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
    private sealed class Context(UnitSlot unit, int owner, MapPoint origin)
    {
        public UnitSlot Unit { get; } = unit;
        public int Owner { get; } = owner;
        public MapPoint Origin { get; } = origin;
        public int Type, Near, PursuitX = -3, PursuitY = -3;
        public CitySlot? City;
        public bool Zone;
    }
    private void Nearest(Context c, MapPoint point) => (c.City, c.Near) = CityQueries.Nearest(world, point);
    public Operation Begin(UnitSlot actor, string call) => new(this, actor, call);
    public sealed class Operation : IDisposable
    {
        private readonly BarbarianOrders parent;
        private readonly IEnumerator<BarbarianOrderDecision?> steps;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        private int? answer;
        public int? Result { get; internal set; }
        public BarbarianOrderDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(BarbarianOrders parent, UnitSlot actor, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, actor, call).GetEnumerator();
        }
        internal int? TakeReply() { var reply = answer; answer = null; return reply; }
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public BarbarianOrderDecision? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed barbarian orders cannot resume"); }
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
    private int Finish(Context c)
    {
        if (Actor(c).Orders == UnitOrder.GoTo && Actor(c).Destination == c.Origin)
        { Actor(c).Orders = UnitOrder.None; }
        return 0;
    }
    private int Direction(Context c, int direction)
    {
        if (direction == 8)
        {
            Actor(c).LastDirection = byte.MaxValue;
            UnitMovement.Exhaust(world, rules, ref Actor(c));
            if (Role(c) == (int)RuleRole.Defend || Fortress(c.Origin))
            { Actor(c).Orders = Actor(c).Flags.Garrison ? UnitOrder.Fortified : UnitOrder.Fortifying; }
        }
        else
        {
            Actor(c).Orders = UnitOrder.AiGoTo;
            var point = Offset(c.Origin, direction);
            Actor(c).Destination = new(unchecked((short)point.X), unchecked((short)point.Y));
        }
        return Finish(c);
    }
    private IEnumerable<BarbarianOrderDecision?> Delete(Operation op, Context c, string call, bool cargo = false)
    {
        using var child = cargo ? lifecycle.BeginEraseCargo(loss, c.Unit.Value, call, incidents)
            : lifecycle.BeginDestroy(loss, c.Unit.Value, call, incidents);
        while (child.Result is null)
        {
            var pending = child.Resume(op.TakeReply());
            if (pending is not null)
            { yield return new BarbarianOrderDecision.Lifecycle(pending); }
            else if (child.Result is null)
            { yield return null; }
        }
    }
    private IEnumerable<BarbarianOrderDecision?> Execute(Operation op, UnitSlot slot, string call)
    {
        int owner = world.Entities.Units.At(slot).Owner.Value;
        if (world.Entities.Units.At(slot).Orders != UnitOrder.GoTo)
        { world.Entities.Units.At(slot).Orders = UnitOrder.None; }
        var c = new Context(slot, owner, world.Entities.Units.At(slot).Position);
        if (c.Origin.Y < 2 || c.Origin.Y >= world.Map.Geometry.Height - 2 || !world.Map.Geometry.InBounds(c.Origin))
        {
            foreach (var pending in Delete(op, c, call + "/polar", true))
            { yield return pending; }
            op.Result = 1;
            yield break;
        }
        c.Zone = SpatialControl.InEnemyZone(world, c.Origin, c.Owner) is not null;
        c.Type = Actor(c).Type.Value;
        Nearest(c, c.Origin);
        if (Rule(c).Capacity != 0)
        {
            foreach (var pending in Transport(op, c, call))
            { yield return pending; }
            yield break;
        }
        if (c.Near == 0 || Fortress(c.Origin))
        {
            int count = stacks.Count(slot.Value), leaders = stacks.CountRole(slot.Value, (int)RuleRole.Diplomacy, rules);
            if (!(count - leaders > 1 || c.Zone) || Role(c) == (int)RuleRole.Defend)
            {
                int threshold = 2 + (c.Near == 0 ? 1 : 0) + (c.Zone ? 0 : 1);
                if (stacks.Count(slot.Value) < threshold || unchecked((sbyte)Rule(c).Attack) < unchecked((sbyte)Rule(c).Defense))
                { Actor(c).Orders = UnitOrder.Fortifying; op.Result = Direction(c, 8); yield break; }
            }
        }
        if (c.City is { } nearby && ((world.Globals.Turn + slot.Value) & 3) == 0
            && (!world.Map.TileAt(c.Origin).Ocean || Rule(c).Capacity != 0)
            && (world.Map.TileAt(world.Entities.Cities.At(nearby).Position).Region != world.Map.TileAt(c.Origin).Region || c.Near > 8))
        {
            foreach (var pending in Delete(op, c, call + "/periodic"))
            { yield return pending; }
            op.Result = 1;
            yield break;
        }
        Pursuit(c);
        if (Role(c) > 4)
        {
            bool ocean = world.Map.TileAt(c.Origin).Ocean;
            int count = stacks.Count(slot.Value);
            if (!ocean && count > 1 && stacks.CountRole(slot.Value, Role(c), rules) < 2)
            { op.Result = Direction(c, 8); yield break; }
            if (ocean && c.Near < 3)
            {
                foreach (var pending in Delete(op, c, call + "/leader-sea"))
                { yield return pending; }
                op.Result = 1;
                yield break;
            }
            if (!ocean)
            {
                var escort = UnitQueries.Nearest(world, c.Origin, c.Owner, slot);
                if (escort.Distance < 4)
                {
                    var unit = escort.Slot ?? throw new InvalidOperationException("Barbarian escort distance has no unit");
                    if (unchecked((sbyte)rules.Units[world.Entities.Units.At(unit).Type.Value].Role) < 2)
                    {
                        UnitOrders.SetGoto(ref Actor(c), AiTaskIds.LeaderFollow, world.Entities.Units.At(unit).Position);
                        op.Result = Finish(c);
                        yield break;
                    }
                }
            }
            if ((world.Globals.Turn & 7) + 4 <= c.Near)
            {
                foreach (var pending in Delete(op, c, call + "/leader-distance"))
                { yield return pending; }
                op.Result = 1;
                yield break;
            }
        }
        foreach (var pending in Neighbors(op, c, call))
        { yield return pending; }
    }
    private void Pursuit(Context c)
    {
        if (c.City is not { } city)
        { return; }
        ref var target = ref world.Entities.Cities.At(city);
        if (world.Map.TileAt(target.Position).Region == world.Map.TileAt(c.Origin).Region
            && world.Globals.HumanPlayers.Contains(target.Owner.Value))
        {
            int dx = target.Position.X - c.Origin.X;
            if (!world.Globals.Rules.FlatWorld && Math.Abs(dx) >= (world.Map.Geometry.Width >> 1))
            { dx = -dx; }
            c.PursuitX = Math.Sign(dx);
            c.PursuitY = Math.Sign(target.Position.Y + 3);
        }
        if (target.Owner.Value == c.Owner || world.Civilizations[target.Owner.Value].CityCount < 2
            || world.Civilizations[target.Owner.Value].Gold < 100)
        { c.PursuitX = c.PursuitY = -3; }
        if (c.PursuitX != -3)
        {
            Actor(c).Destination = target.Position;
            Actor(c).LastDirection = unchecked((byte)path.Direction(c.Unit));
        }
    }
}
