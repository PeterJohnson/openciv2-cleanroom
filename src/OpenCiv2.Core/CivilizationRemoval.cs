using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>V01 §8 local forced removal/retirement. Its inline asset changes are distinct
/// from ordinary unit/city deletion and last-city checks. Session owns network orchestration.</summary>
public sealed class CivilizationRemoval(GameState world, RuleTables rules, StackOperations stacks,
    CivilizationElimination lifecycle, EspionageIncidents incidents, Func<int, string, CivilizationCreation> create,
    Action redrawMaps)
{
    public CivilizationRemoval(GameState world, RuleTables rules, StackOperations stacks,
        CivilizationElimination lifecycle, EspionageIncidents incidents, Func<int, CivilizationCreation> create,
        Action redrawMaps) : this(world, rules, stacks, lifecycle, incidents, (owner, _) => create(owner), redrawMaps) { }

    private GameState World => world;
    public Operation Begin(int civilization, int kill, int recreate, string call) => new(this, civilization, kill, recreate, call);

    public sealed class Operation : IDisposable
    {
        private readonly CivilizationRemoval parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(CivilizationRemoval parent, int civilization, int kill, int recreate, string call)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(civilization);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(civilization, EntityLimits.Civilizations);
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, civilization, unchecked((byte)kill) != 0, unchecked((byte)recreate) != 0, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public LifecycleDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed civilization removal cannot resume"); }
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
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private IEnumerable<LifecycleDecision?> Execute(Operation op, int owner, bool kill, bool recreate, string call)
    {
        world.Globals.HumanPlayers = world.Globals.HumanPlayers.With(owner, false);
        if (world.Multiplayer.Saved.KillOnRetirement || kill)
        {
            bool relinked = false;
            for (int slot = 0; slot < world.Entities.Units.End; ++slot)
            {
                ref var unit = ref world.Entities.Units.Storage[slot];
                if (!unit.Token.Occupied)
                { continue; }
                if (unchecked((sbyte)unit.Owner.Value) == owner)
                { RemoveUnit(slot, owner, ref relinked); }
                else if (unit.Flags.SpyMission && unchecked((sbyte)unit.Counter) == owner)
                {
                    unit.Flags.SpyMission = false;
                    int victim = unchecked((sbyte)unit.Counter);
                    world.Civilizations[owner].Treaties[victim].SpyChat = false;
                    using var child = lifecycle.BeginEscape(slot, 0, victim, call + "/spy/" + slot, incidents);
                    while (child.Result is null)
                    {
                        var pending = child.ResumeDecision(op.TakeReply());
                        if (child.Result is null)
                        { yield return pending; }
                    }
                    op.Report(child.PresentationFailures);
                }
            }
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                if (!world.Entities.Cities.Storage[slot].Token.Occupied
                    || unchecked((sbyte)world.Entities.Cities.Storage[slot].Owner.Value) != owner)
                { continue; }
                owner = unchecked((sbyte)world.Entities.Cities.Storage[slot].Owner.Value);
                RemoveCity(slot, owner);
            }
            // The eliminated owner and dead city records intentionally participate in this pass.
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            { CityArea.Refresh(world, world.Entities.Cities.Storage[slot].Position, new((byte)owner)); }
            SpaceshipLifecycle.Reset(world, owner);
            world.Globals.CivsAlive = world.Globals.CivsAlive.With(owner, false);
            for (int x = 0; x < world.Map.Geometry.Width; ++x)
            {
                for (int y = 0; y < world.Map.Geometry.Height; ++y)
                { world.Map.Mutate(new TileChange.Revelation(new(x, y), CivilizationSet.Only(owner), false)); }
            }
        }
        var passwords = world.Multiplayer.Saved.Passwords.ToArray();
        passwords[owner] = string.Empty;
        world.Multiplayer.Saved.Passwords = new(passwords);
        if (world.Globals.HumanPlayers.With(owner, false).Count == 0)
        { yield break; }
        if (recreate)
        {
            using var child = create(owner, call + "/creation");
            while (child.AdvanceDecision(64, op.TakeReply()) is null)
            { yield return child.Pending; }
            op.Report(child.PresentationFailures);
        }
        op.Present(redrawMaps);
    }

    private void RemoveUnit(int slot, int owner, ref bool relinked)
    {
        var pool = world.Entities.Units;
        ref var unit = ref pool.Storage[slot];
        if (slot < EntityLimits.Units)
        {
            ref var civ = ref world.Civilizations[owner];
            if (civ.MilitaryCount != 0 && unchecked((sbyte)rules.Units[unit.Type.Value].Role) < 5)
            { civ.MilitaryCount = unchecked((short)(civ.MilitaryCount - 1)); }
            if (civ.UnitsActive[unit.Type.Value] != 0)
            { --civ.UnitsActive[unit.Type.Value]; }
            if (world.UnitTotals[owner] != 0)
            { world.UnitTotals[owner] = unchecked(world.UnitTotals[owner] - 1); }
        }
        if (unit.Previous is { } previous)
        { pool.At(previous).Next = unit.Next; relinked = true; }
        if (unit.Next is { } next)
        { pool.At(next).Previous = unit.Previous; relinked = true; }
        if (!relinked && world.Map.Geometry.InBounds(unit.Position))
        { world.Map.Mutate(new TileChange.Features(unit.Position, MapFeatures.Unit, false), synchronize: true); }
        unit.Previous = unit.Next = null;
        unit.Position = new(unchecked((short)(-100 * (unit.Owner.Value + 1))), unchecked((short)(-100 * (unit.Owner.Value + 1))));
        unit.Token = default;
        if (slot == pool.End - 1)
        { pool.SetEnd(pool.End - 1); world.Globals.UnitEnd = unchecked((short)pool.End); }
        foreach (ref var passenger in pool.Scanned)
        {
            if (passenger.Orders == UnitOrder.Sleeping && passenger.Carrier is { } carrier && carrier.Value == slot)
            { passenger.Destination = passenger.Destination with { X = carrier.Value }; passenger.Orders = UnitOrder.None; }
        }
    }

    private void RemoveCity(int slot, int owner)
    {
        var pool = world.Entities.Cities;
        var center = pool.Storage[slot].Position;
        world.Civilizations[owner].CityCount = unchecked((short)(world.Civilizations[owner].CityCount - 1));
        pool.Storage[slot].Token = default;
        if (slot == pool.End - 1)
        { pool.SetEnd(pool.End - 1); world.Globals.CityEnd = unchecked((short)pool.End); }
        for (int other = 0; other < pool.End; ++other)
        {
            if (!pool.Storage[other].Token.Occupied)
            { continue; }
            for (int route = unchecked((sbyte)pool.Storage[other].RouteCount) - 1; route >= 0; --route)
            {
                if (pool.Storage[other].RoutePartners[route]?.Value == slot)
                { TradeRoutes.Remove(world, new((ushort)other), route); }
            }
        }
        for (int wonder = 0; wonder < RuleLimits.Wonders; ++wonder)
        { if (world.Globals.WonderCity[wonder] == slot) { world.Globals.WonderCity[wonder] = BuildingQueries.Destroyed; } }
        CivilizationSet seen = default;
        world.Map.Mutate(new TileChange.Features(center, MapFeatures.CitySite, false), synchronize: true);
        for (int i = 0; i < MapNeighborhoods.Radius.Length; ++i)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, MapNeighborhoods.Radius[i], world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(point) || world.Map.TileAt(point).Ocean)
            { continue; }
            ref var tile = ref world.Map.TileAt(point);
            tile.Fertility |= 8;
            if (i > 20)
            { continue; }
            if (tile.Claimant == owner)
            { tile.Claimant = 0; }
            if (world.Map.CityOrUnitOwner(point) is > 0 and < EntityLimits.Civilizations and var other && other != owner)
            { seen = seen.With(other); }
        }
        if (seen.Count != 0)
        {
            for (int unit = stacks.FirstAt(center); unit >= 0; unit = stacks.Next(unit))
            { world.Entities.Units.Storage[unit].Visibility = world.Entities.Units.Storage[unit].Visibility.Union(seen); }
        }
    }
}
