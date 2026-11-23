using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A07 §4 proactive paratrooper policy, following A03's city-selector production.
/// A selected landing invokes actual U05 and returns zero even when that child refuses.</summary>
public sealed class ParatrooperTactics(GameState world, RuleTables rules, StackOperations stacks, Paradrop paradrop)
{
    private GameState World => world;
    private Paradrop Drop => paradrop;
    private ref Unit Actor(UnitTacticsContext c) => ref world.Entities.Units.At(c.Unit);
    private int Distance(MapPoint a, MapPoint b) => world.Map.Geometry.StepDistance(a, b, world.Globals.Rules.FlatWorld);
    private MapPoint Landing(MapPoint target, int direction) => direction >= 8
        ? new(world.Map.Geometry.WrapX(target.X, world.Globals.Rules.FlatWorld), target.Y)
        : MapNeighborhoods.Offset(world.Map.Geometry, target, MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
    public Operation Begin(UnitTacticsContext context, string call) => new(this, context, call);

    private SpecialUnitChoice? Select(UnitTacticsContext c, out MapPoint? destination)
    {
        destination = null;
        if (c.HomeDistance != 0)
        {
            if (c.HomeCity == c.NearestCity && c.Region == c.HomeRegion
                && SpatialControl.AdjacentNonalliedUnit(world, c.Position, c.Owner) is null)
            {
                var city = c.CitySelector ?? throw new InvalidOperationException("A07 return-home requires A03's retained city selector");
                UnitOrders.SetGoto(ref Actor(c), AiTaskIds.CityTarget, world.Entities.Cities.Storage[city.Value].Position);
                return new(SpecialUnitRoute.Finish);
            }
            if (c.Fortress && stacks.Count(c.Unit.Value) == 1)
            { Actor(c).AiTask = AiTaskIds.FortressGuard; return new(SpecialUnitRoute.Finish); }
            return new(SpecialUnitRoute.Continue);
        }
        if (Actor(c).MovesUsed == 0 && Actor(c).Orders != UnitOrder.GoTo)
        {
            int best = 0;
            MapPoint? target = null;
            for (int slot = 0; slot < world.Entities.Cities.End; ++slot)
            {
                ref var city = ref world.Entities.Cities.Storage[slot];
                if (!city.Token.Occupied || city.Owner.Value == c.Owner || !world.Civilizations[c.Owner].Treaties[city.Owner.Value].War
                    || Distance(c.Position, city.Position) > rules.Cosmic.ParadropRange)
                { continue; }
                int cityScore = unchecked((sbyte)city.Size) + 999;
                if (world.Map.UnitOwner(city.Position) is null && best < cityScore)
                { best = cityScore; target = city.Position; }
                if (best <= 0)
                { continue; }
                var retained = target ?? throw new InvalidOperationException("A07 positive landing score requires a retained target");
                best = 0;
                int? selected = null;
                // Extra center, ordinary center, then D7 through D0. Owned occupancy cancels the entire search.
                for (int direction = 9; direction >= 0; --direction)
                {
                    var p = Landing(retained, direction);
                    if (!world.Map.Geometry.InBounds(p) || Distance(c.Position, p) > rules.Cosmic.ParadropRange)
                    { continue; }
                    int? owner = world.Map.UnitOwner(p);
                    if (owner == c.Owner)
                    { selected = null; break; }
                    if (owner is not null)
                    { continue; }
                    int score = direction == 8 ? 500 : 0;
                    var tile = world.Map.TileAt(p);
                    if ((tile.Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification)
                    { score += 50; }
                    score += 3 * unchecked((sbyte)rules.Terrain[(int)tile.Terrain].DefenseFactor) + Distance(c.Position, p);
                    if (score > best)
                    { best = score; selected = direction; }
                }
                if (selected is { } found)
                { destination = Landing(retained, found); return null; }
            }
        }
        if (Actor(c).Orders == UnitOrder.GoTo)
        { return new(SpecialUnitRoute.Continue); }
        c.Hold = true;
        return new(SpecialUnitRoute.PlanMove);
    }

    public sealed class Operation : IDisposable
    {
        private readonly ParatrooperTactics parent;
        private readonly UnitTacticsContext context;
        private readonly string call;
        private bool started, faulted, disposed;
        private Paradrop.Operation? child;
        public SpecialUnitChoice? Result { get; private set; }
        public MapPoint? Destination { get; private set; }
        public bool Landed { get; private set; }
        internal Operation(ParatrooperTactics parent, UnitTacticsContext context, string call)
        { this.parent = parent; this.context = context; this.call = call; }
        public ParadropDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed paratrooper policy cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    ArgumentException.ThrowIfNullOrEmpty(call);
                    started = true;
                    Result = parent.Select(context, out var destination);
                    Destination = destination;
                    if (Result is not null)
                    { return null; }
                    child = parent.Drop.Begin(context.Unit, destination ?? throw new InvalidOperationException("A07 selected no landing"), call + "/paradrop");
                    reply = null;
                }
                var pending = child!.Resume(reply);
                Landed = child.Landed;
                if (child.Completed)
                { Result = new(SpecialUnitRoute.ReturnZero); child.Dispose(); child = null; }
                return pending;
            }
            catch { faulted = true; if (child is not null) { Landed = child.Landed; } child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
