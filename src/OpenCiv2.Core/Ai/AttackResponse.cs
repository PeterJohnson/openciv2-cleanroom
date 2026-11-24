using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A04 §9 immediate response, including the first eligible U05 paradrop continuation and
/// U03 local path queries for the ground response. Does not execute the assigned Goto orders.</summary>
public sealed class AttackResponse(GameState world, RuleTables rules, StackOperations stacks, Paradrop paradrop)
{
    private GameState World => world;
    private Paradrop Drop => paradrop;
    private ref Unit Unit(int slot) => ref world.Entities.Units.Storage[slot];
    public Operation Begin(int owner, MapPoint target, string call) => new(this, owner, target, call);

    public sealed class Operation : IDisposable
    {
        private readonly AttackResponse parent;
        private readonly int owner;
        private readonly MapPoint target;
        private readonly string call;
        private Paradrop.Operation? child;
        private bool started, disposed, faulted;
        private readonly List<Exception> failures = [];
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        public bool Completed { get; private set; }
        public UnitSlot? Paratrooper { get; private set; }
        public int RetaskedUnits { get; private set; }
        public ParadropDecision? Pending => child?.Pending;
        internal Operation(AttackResponse parent, int owner, MapPoint target, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentOutOfRangeException.ThrowIfNegative(owner);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(owner, EntityLimits.Civilizations);
            this.parent = parent;
            this.owner = owner;
            this.target = target;
            this.call = call;
        }
        public ParadropDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed attack response cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    started = true;
                    Paratrooper = parent.FindParatrooper(owner, target);
                    if (Paratrooper is not { } actor)
                    { RetaskedUnits = parent.Retask(owner, target); Completed = true; return null; }
                    child = parent.Drop.Begin(actor, target, call + "/paradrop");
                    reply = null;
                }
                var pending = child!.Resume(reply);
                if (child.Completed)
                { failures.AddRange(child.PresentationFailures); Completed = true; child.Dispose(); }
                return pending;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
    private UnitSlot? FindParatrooper(int owner, MapPoint target)
    {
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref Unit(i);
            if (unit.Token.Occupied && unit.Owner.Value == owner && rules.Units[unit.Type.Value].Abilities.Paradrop
                && world.Map.CityOwner(unit.Position) is not null && (unit.MovesUsed == 0 || unit.Flags.Garrison)
                && !unit.Flags.Paradropped
                && world.Map.Geometry.StepDistance(unit.Position, target, world.Globals.Rules.FlatWorld) <= rules.Cosmic.ParadropRange)
            { return new((ushort)i); }
        }
        return null;
    }
    private int Retask(int owner, MapPoint target)
    {
        int? occupant = world.Map.Owner(target);
        int region = world.Map.TileAt(target).Region;
        int count = 0;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref Unit(i);
            if (!unit.Token.Occupied)
            { continue; }
            int currentOwner = unit.Owner.Value;
            if (currentOwner != owner && (currentOwner == occupant || !world.Civilizations[owner].Treaties[currentOwner].Alliance
                || world.Globals.HumanPlayers.Contains(currentOwner)))
            { continue; }
            var rule = rules.Units[unit.Type.Value];
            if (rule.Domain != RuleDomain.Land || rule.Attack == 0 || UnitMovement.Left(world, rules, unit) == 0
                || rule.Role == RuleRole.Defend && world.Map.CityOwner(unit.Position) is not null
                || world.Map.Geometry.StepDistance(unit.Position, target, world.Globals.Rules.FlatWorld) >= 7
                || world.Map.TileAt(unit.Position).Region != region)
            { continue; }
            world.Paths.Owner = unit.Owner.Value;
            world.Paths.SelectedUnitType = unit.Type.Value;
            world.Paths.Goal = target;
            int direction = LocalPathfinding.NextDirection(world, rules, stacks, unit.Position, unchecked(2 * UnitMovement.Left(world, rules, unit)));
            if (direction <= 0 || direction == 8)
            { continue; }
            unit.Orders = UnitOrder.GoTo;
            unit.Destination = target;
            unit.AiTask = AiTaskIds.RespondToAttack;
            ++count;
        }
        return count;
    }
}
