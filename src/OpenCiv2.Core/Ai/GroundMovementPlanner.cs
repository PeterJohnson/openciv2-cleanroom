using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A null direction retains existing orders and requests common Finish.</summary>
public sealed record GroundMoveChoice(int? Direction, int Score, int UnscaledScore, bool Attack, int AttackClass);

/// <summary>A04 §§6–7 eight-direction planner, with actual stack/preview/bribery children.
/// The caller applies the returned direction or common Finish through GroundOrders.</summary>
public sealed partial class GroundMovementPlanner(GameState world, RuleTables rules, StackOperations stacks,
    CombatStrength strength, CombatPreparation combat, UnitEspionage espionage, GameplayRandom random)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private int CountRole(UnitSlot slot, RuleRole role) => stacks.CountRole(slot.Value, (int)role, rules);
    private bool Adjacent(MapPoint point, int owner, bool sameMedium = false) => SpatialControl.AdjacentNonalliedUnit(world, point, owner, sameMedium) is not null;
    private bool Fortress(MapPoint point) => (world.Map.TileAt(point).Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification;
    private int Roll(string site, string call, int direction, int hi) => random.SecondaryRange(new ChoiceKey("A04." + site, call, direction), 0, hi);
    public Operation Begin(UnitTacticsContext context, string call) => new(this, context, call);

    public sealed class Operation : IDisposable
    {
        private readonly GroundMovementPlanner parent;
        private readonly IEnumerator<UnitMissionDecision?> steps;
        private int? reply;
        private bool faulted, disposed;
        public GroundMoveChoice? Result { get; internal set; }
        public bool Completed => Result is not null;
        public UnitMissionDecision? Pending { get; private set; }
        internal Operation(GroundMovementPlanner parent, UnitTacticsContext context, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, context, call).GetEnumerator();
        }
        internal int? TakeReply() { var answer = reply; reply = null; return answer; }
        public UnitMissionDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed ground planning cannot resume"); }
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
    private sealed class Scores
    {
        public int Best = -999, Unscaled = -999, Direction = 8, AttackClass;
        public bool Attack, Explore;
    }
    private sealed class Candidate(MapPoint point, int? owner, Terrain terrain, UnitSlot? unit)
    {
        public readonly MapPoint Point = point;
        public readonly int? Owner = owner;
        public readonly Terrain Terrain = terrain;
        public bool Ocean => Terrain == Terrain.Ocean;
        public UnitSlot? Unit = unit;
        public int Score, AttackClass;
        public bool Attack, Continue;
    }
    private IEnumerable<UnitMissionDecision?> Execute(Operation operation, UnitTacticsContext c, string call)
    {
        var scores = new Scores();
        if (c.Role == (int)RuleRole.SeaTransport && c.HomeDistance != 0 && stacks.Count(c.Unit.Value) > 1 && Unit(c.Unit).Orders == UnitOrder.GoTo)
        { c.HasOrders = true; }
        if (!Adjacent(c.Position, c.Owner, true))
        { scores.Explore = !c.HasOrders; }
        else
        { Unit(c.Unit).LastDirection = byte.MaxValue; }
        if (world.Globals.Status.Scenario && world.Scenario.Flags.WholeMapReveal)
        { scores.Explore = false; }
        if (c.Role == (int)RuleRole.Defend)
        {
            int attack = unchecked((sbyte)rules.Units[c.Type].Attack), defense = unchecked((sbyte)rules.Units[c.Type].Defense);
            if (attack > defense && !world.Civilizations[c.Owner].ContinentFlags[c.Region].StrongerEnemy
                || attack >= defense && c.Policy == (int)RuleRole.Attack)
            { c.Role = (int)RuleRole.Attack; }
        }
        for (int k = 0; k < MapLimits.Directions; ++k)
        {
            var candidate = Admit(c, k);
            if (candidate is null || !BaseScore(c, candidate, k, call))
            { continue; }
            if (c.Domain == RuleDomain.Air)
            { candidate.Score = Roll("ground-air", call, k, 2); }
            if (unchecked((sbyte)Unit(c.Unit).LastDirection) >= 0)
            {
                int h = Math.Abs(Unit(c.Unit).LastDirection - k);
                if (h > 4)
                { h = 8 - h; }
                candidate.Score = unchecked(candidate.Score - 2 * h * h);
            }
            if (!Territory(c, candidate))
            { continue; }
            if (candidate.Unit is { } occupant && candidate.Owner != c.Owner)
            {
                foreach (var pending in Foreign(operation, c, candidate, scores, k, call))
                { yield return pending; }
                if (!candidate.Continue)
                { continue; }
            }
            else if (candidate.Unit is not null)
            { candidate.Score = unchecked(candidate.Score - (sbyte)Rule(c.Unit).Defense); }
            Select(c, candidate, scores, k);
        }
        if (scores.Attack && UnitMovement.Left(world, rules, Unit(c.Unit)) < rules.Cosmic.RoadMovementMultiplier - scores.AttackClass)
        { scores.Direction = 8; }
        if (scores.Unscaled != scores.Best)
        { scores.Direction = 8; }
        if (scores.Direction == 8)
        { scores.Attack = false; }
        if (c.Hold && !scores.Attack)
        { scores.Direction = 8; }
        if (!c.HasOrders || scores.Attack)
        { Unit(c.Unit).AiTask = AiTaskIds.ChooseMove; }
        operation.Result = new(c.HasOrders && !scores.Attack ? null : scores.Direction,
            scores.Best, scores.Unscaled, scores.Attack, scores.AttackClass);
    }
    private Candidate? Admit(UnitTacticsContext c, int k)
    {
        var point = MapNeighborhoods.Offset(world.Map.Geometry, c.Position, MapNeighborhoods.Directions[k], world.Globals.Rules.FlatWorld);
        if (!world.Map.Geometry.InBounds(point))
        { return null; }
        int? owner = world.Map.Owner(point);
        var terrain = world.Map.TileAt(point).Terrain;
        if (c.Domain == RuleDomain.Land && terrain == Terrain.Ocean)
        { return null; }
        int current = stacks.FirstAt(point);
        while (current >= 0 && unchecked((sbyte)Rule(new((ushort)current)).Role) > 4)
        {
            int next = stacks.Next(current);
            if (next < 0)
            { break; }
            current = next;
        }
        UnitSlot? unit = current >= 0 ? new((ushort)current) : null;
        if (c.EnemyZone && c.Domain == RuleDomain.Land && unit is null
            && SpatialControl.InEnemyZone(world, point, c.Owner) is not null && !c.Abilities.IgnoreZonesOfControl && terrain != Terrain.Ocean)
        { return null; }
        if (c.Domain == RuleDomain.Sea && terrain != Terrain.Ocean
            && !(unit is not null && owner != c.Owner && c.Terrain == Terrain.Ocean && !c.Abilities.Submarine))
        { return null; }
        if (unit is { } friendly && owner == c.Owner && stacks.Count(friendly.Value) >= 2
            && world.Map.CityOwner(point) is null && !Fortress(point))
        { return null; }
        return new(point, owner, terrain, unit);
    }
    private void Select(UnitTacticsContext c, Candidate q, Scores scores, int direction)
    {
        unchecked
        {
            if (c.HomeDistance != 0 && c.Domain == RuleDomain.Sea && (q.Unit is null || q.Owner == c.Owner))
            {
                var home = c.HomeCity ?? throw new InvalidOperationException("A04 sea scoring requires a captured home city");
                if (world.Map.TileAt(q.Point).Region == CityQueries.AdjacentSea(world, home).LastRegion)
                { q.Score += 10; }
            }
            if (c.Domain == RuleDomain.Air && q.Unit is null && world.Map.AirbaseOwner(q.Point) is { } owner && owner != c.Owner)
            { return; }
            if (scores.Explore)
            {
                var offset = MapNeighborhoods.Directions[direction];
                var look = MapNeighborhoods.Offset(world.Map.Geometry, c.Position, new(4 * offset.X, 4 * offset.Y), world.Globals.Rules.FlatWorld);
                if (world.Map.Geometry.InBounds(look) && !world.Map.TileAt(look).RevealedTo.Contains(c.Owner))
                { q.Score += world.Map.TileAt(look).Ocean ? 2 : 8; }
                foreach (var neighbor in MapNeighborhoods.Directions)
                {
                    var point = MapNeighborhoods.Offset(world.Map.Geometry, look, neighbor, world.Globals.Rules.FlatWorld);
                    if (!world.Map.Geometry.InBounds(point))
                    { continue; }
                    var tile = world.Map.TileAt(point);
                    if (!tile.RevealedTo.Contains(c.Owner) && (!tile.Ocean || c.Domain == RuleDomain.Sea))
                    { q.Score += 2; }
                    if (world.Map.UnitOwner(point) is not null)
                    { q.Score -= 2; }
                    if (c.SettlerType)
                    {
                        q.Score += (sbyte)rules.Terrain[(int)world.Map.TileAt(point).Terrain].Food;
                        if (world.Globals.Multiplayer != GameMode.SinglePlayer && world.Multiplayer.Saved.DoubleProduction)
                        { q.Score += (sbyte)rules.Terrain[(int)world.Map.TileAt(point).Terrain].Food; }
                    }
                }
            }
            if (q.Point.Y == 0 || q.Point.Y == world.Map.Geometry.Height - 1)
            { q.Score /= 3; }
            int unscaled = q.Score;
            if (q.Attack && UnitMovement.Left(world, rules, Unit(c.Unit)) < rules.Cosmic.RoadMovementMultiplier && world.Map.CityOwner(q.Point) is null)
            {
                int cost = stacks.ProductionCost(c.Unit.Value, rules), attack = Math.Clamp(stacks.Attack(c.Unit.Value, rules), 1, 99);
                q.Score = cost / attack * q.Score;
            }
            if (q.Score > scores.Best)
            { scores.Best = q.Score; scores.Direction = direction; scores.Attack = q.Attack; scores.AttackClass = q.AttackClass; }
            if (unscaled > scores.Unscaled)
            { scores.Unscaled = unscaled; }
        }
    }
}
