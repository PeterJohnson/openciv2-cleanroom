using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>Retained U02 values consumed or changed by U05. The movement dispatcher owns this
/// object across landfall restarts and common finish; helpers do not initialize its incoming values.</summary>
public sealed class MovementContinuation(UnitSlot unit, int unitBound, MapPoint origin)
{
    public UnitSlot Unit = unit;
    public int UnitBound = unitBound, Answer;
    public MapPoint Origin = origin;
    public bool LocalHuman, WorkFlag, AttackDone;
    public MovementOptions Options;
}
public sealed record LandfallChoice(int Actor, UnitTarget Ship, string ShipName);
public interface ITransportMovementEffects
{
    void NoLandfall(UnitSlot ship, string name);
    void Redraw(MapPoint point);
    void Publish();
    /// <summary>Present initial focus for the already selected unit: cursor, status, visibility and menus.</summary>
    void FocusSelectedUnit(UnitSlot unit, MapPoint position);
}

/// <summary>U05 local landfall, boarding, unload and wakeup operations. Actual U01 cargo/stack and
/// U02/U04 allowance/selectability children are required; directional admission remains with U02.</summary>
public sealed class TransportMovement(GameState world, RuleTables rules, StackOperations stacks,
    UnitCargo cargo, ITransportMovementEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(int slot) => ref world.Entities.Units.Storage[slot];
    public LandfallOperation BeginLandfall(UnitSlot ship, MovementContinuation movement) => new(this, ship, movement);

    public sealed class LandfallOperation : IDisposable
    {
        private readonly TransportMovement parent;
        private readonly UnitSlot ship;
        private readonly MovementContinuation movement;
        private UnitSlot? passenger;
        private LandfallChoice? pending;
        private bool started, disposed, faulted;
        public bool Completed { get; private set; }
        public bool Restart { get; private set; }
        internal LandfallOperation(TransportMovement parent, UnitSlot ship, MovementContinuation movement)
        { this.parent = parent; this.ship = ship; this.movement = movement; }
        public LandfallChoice? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed landfall cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    started = true;
                    passenger = parent.Select(ship, movement);
                    if (passenger is null)
                    { Completed = true; return null; }
                    if (!parent.World.Multiplayer.Saved.AutoplayActive && movement.Options.Prompt)
                    {
                        ref var unit = ref parent.Unit(ship.Value);
                        return pending = new(unit.Owner.Value, new(ship, unit.Token), parent.Name(ship));
                    }
                }
                else if (pending is not null)
                {
                    if (reply is null)
                    { return pending; }
                    movement.Answer = reply.Value;
                    pending = null;
                    if (reply == 0)
                    { movement.Unit = ship; Completed = true; return null; }
                }
                movement.Unit = passenger!.Value;
                parent.Unit(passenger.Value.Value).Orders = UnitOrder.None;
                Restart = Completed = true;
                return null;
            }
            catch { faulted = true; throw; }
        }
        public void Dispose() => disposed = true;
    }
    private string Name(UnitSlot slot) => rules.Units[Unit(slot.Value).Type.Value].Identity.Name;
    private UnitSlot? Select(UnitSlot ship, MovementContinuation movement)
    {
        movement.Unit = ship;
        if (!movement.LocalHuman || world.Map.CityOwner(movement.Origin) is not null)
        { return null; }
        if (world.Entities.Units.End == movement.UnitBound)
        { Unit(ship.Value).Orders = UnitOrder.None; }
        movement.UnitBound = 0;
        bool eligible = false;
        for (int current = stacks.First(ship.Value); current >= 0; current = stacks.Next(current))
        {
            ref var unit = ref Unit(current);
            if (rules.Units[unit.Type.Value].Domain != RuleDomain.Land
                || unit.Orders == UnitOrder.Sleeping && unit.Carrier is { } carrier && carrier != ship)
            { continue; }
            eligible = true;
            if (UnitMovement.Left(world, rules, unit) >= rules.Cosmic.RoadMovementMultiplier)
            { return new((ushort)current); }
        }
        if (!world.Multiplayer.Saved.AutoplayActive && eligible && movement.Options.Prompt)
        { effects.NoLandfall(ship, Name(ship)); }
        return null;
    }

    public void Board(UnitSlot boarded, MapPoint destination)
    {
        MapKnowledge.RequireLocal(world);
        UnitOrders.SetSleep(ref Unit(boarded.Value));
        UnitMovement.Exhaust(world, rules, ref Unit(boarded.Value));
        for (int current = stacks.First(boarded.Value); current >= 0; current = stacks.Next(current))
        {
            ref var ship = ref Unit(current);
            if (rules.Units[ship.Type.Value].Domain != RuleDomain.Sea)
            { continue; }
            int spent = ship.MovesUsed;
            if (spent > UnitMovement.Allowance(world, rules, ship))
            { continue; }
            cargo.Gather(current, false, false);
            if (Unit(boarded.Value).Carrier?.Value == current)
            {
                ship.Orders = UnitOrder.None;
                world.Globals.ActiveUnit = new((ushort)current);
                break;
            }
        }
        effects.Redraw(destination);
        effects.Publish();
    }

    public void Unload()
    {
        MapKnowledge.RequireLocal(world);
        if (world.Globals.ActiveUnit is not { } active)
        { return; }
        if (rules.Units[Unit(active.Value).Type.Value].Domain == RuleDomain.Sea)
        { Unit(active.Value).Flags.Waiting = true; }
        UnitSlot? selected = null;
        for (int current = stacks.First(world.Globals.ActiveUnit!.Value.Value); current >= 0; current = stacks.Next(current))
        {
            ref var unit = ref Unit(current);
            if (rules.Units[unit.Type.Value].Domain == RuleDomain.Land && unit.Orders == UnitOrder.Sleeping)
            {
                unit.Orders = UnitOrder.None;
                if (UnitActivation.Selectable(world, rules, current))
                { selected = new((ushort)current); }
            }
        }
        if (selected is { } next)
        {
            world.Globals.ActiveUnit = next;
            // U05 enters View then calls U04 focusActive(0). The chosen unit was just proven
            // selectable, with no intervening child: focus takes its initial-focus branch and
            // returns to MoveUnits. Keep that gameplay change here, not in a display adapter.
            world.MovementMode = MovementMode.MoveUnits;
            effects.FocusSelectedUnit(next, Unit(next.Value).Position);
        }
    }

    public void ArriveInPort(UnitSlot mover, bool originOcean, CitySlot? city, int owner)
    {
        MapKnowledge.RequireLocal(world);
        if (!originOcean || city is null || !world.Globals.HumanPlayers.Contains(owner)
            || rules.Units[Unit(mover.Value).Type.Value].Capacity == 0)
        { return; }
        for (int current = stacks.First(mover.Value); current >= 0; current = stacks.Next(current))
        { if (Unit(current).Orders == UnitOrder.Sleeping) { Unit(current).Orders = UnitOrder.None; } }
    }

    public void Explore(MovementContinuation movement, int owner, RuleDomain domain, MapPoint destination)
    {
        MapKnowledge.RequireLocal(world);
        int mover = movement.Unit.Value;
        if (domain != RuleDomain.Sea || rules.Units[Unit(mover).Type.Value].Capacity == 0 || !world.Globals.HumanPlayers.Contains(owner)
            || UnitMovement.Left(world, rules, Unit(mover)) != 0 || stacks.CountRole(mover, (int)RuleRole.Attack, rules) == 0)
        { return; }
        movement.WorkFlag = false;
        bool flat = world.Globals.Rules.FlatWorld;
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var neighbor = MapNeighborhoods.Offset(world.Map.Geometry, destination, offset, flat);
            if (!world.Map.Geometry.InBounds(neighbor) || world.Map.TileAt(neighbor).Ocean
                || world.Civilizations[owner].ContinentPolicy[world.Map.TileAt(neighbor).Region] == 4)
            { continue; }
            foreach (var next in MapNeighborhoods.Directions)
            {
                var point = MapNeighborhoods.Offset(world.Map.Geometry, neighbor, next, flat);
                if (world.Map.Geometry.InBounds(point) && !world.Map.TileAt(point).RevealedTo.Contains(owner))
                { movement.WorkFlag = true; break; }
            }
            if (movement.WorkFlag)
            { break; }
        }
        if (!movement.WorkFlag)
        { return; }
        for (int current = stacks.First(mover); current >= 0; current = stacks.Next(current))
        {
            ref var unit = ref Unit(current);
            var rule = rules.Units[unit.Type.Value];
            if (rule.Role == RuleRole.Attack && rule.Attack == 0 && unit.MovesUsed == 0)
            { unit.Orders = UnitOrder.None; }
        }
    }
}
