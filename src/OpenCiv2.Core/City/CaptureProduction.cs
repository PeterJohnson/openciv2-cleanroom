using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed record CaptureCityVisit(CityTarget City, int Owner, string Name);
public abstract record CaptureProductionDecision
{
    public sealed record Lifecycle(LifecycleDecision Decision) : CaptureProductionDecision;
    public sealed record Window(CaptureCityVisit Request) : CaptureProductionDecision;
}
public interface ICaptureProductionEffects
{
    void RedrawNeighbors(MapPoint point);
    void Redraw(MapPoint point);
}

/// <summary>C09 surviving-city support loss, disorder and production. The parent has already
/// transferred the city. Human completion requires the actual P06 city window to close.</summary>
public sealed class CaptureProduction(GameState world, RuleTables rules, StackOperations stacks,
    CityRemoval removal, CivilizationElimination lifecycle, EspionageIncidents incidents,
    ProductionOrders production, ICaptureProductionEffects effects, WonderBuildRestrictions? scenario = null)
{
    private GameState World => world;
    public Operation Begin(CitySlot city, int newOwner, int oldOwner, MapPoint capturedCenter, string call) =>
        new(this, city, newOwner, oldOwner, capturedCenter, call);
    private CivilizationElimination.Operation Support(CitySlot city, int owner, string call) =>
        lifecycle.BeginSupportedLoss(removal, city.Value, owner, call + "/support", incidents);

    private CaptureCityVisit? Repair(CitySlot slot, int newOwner, int oldOwner, MapPoint center)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        bool human = world.Globals.HumanPlayers.Contains(newOwner);
        if (human && ((byte)world.Civilizations[newOwner].Government > (byte)Government.Fundamentalism
            || (byte)world.Civilizations[oldOwner].Government > (byte)Government.Fundamentalism)
            && world.Globals.Difficulty != Difficulty.Chieftain)
        { city.Flags.Disorder = true; }
        if (!human)
        {
            city.Production = UnitTypeIds.Settlers;
            if (newOwner == 0)
            {
                city.Production = UnitTypeIds.Legion;
                int top = stacks.FirstAt(center);
                if (top >= 0)
                { city.Production = unchecked((sbyte)world.Entities.Units.Storage[top].Type.Value); }
            }
            unchecked
            { ++world.Civilizations[newOwner].UnitsInProduction[city.Production]; }
            production.Set(slot, 99);
            return null;
        }
        effects.RedrawNeighbors(city.Position);
        TransferredCityProduction.Repair(world, rules, slot, newOwner, scenario);
        foreach (var offset in MapNeighborhoods.Directions)
        {
            var point = MapNeighborhoods.Offset(world.Map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(point) && !world.Map.TileAt(point).RevealedTo.Contains(newOwner))
            { MapKnowledge.Explore(world, newOwner, point); effects.Redraw(point); }
        }
        world.Calculation.Processing = true;
        return new(new(slot, city.Token), newOwner, city.Name);
    }

    public sealed class Operation : IDisposable
    {
        private readonly CaptureProduction parent;
        private readonly CitySlot city;
        private readonly int newOwner, oldOwner;
        private readonly MapPoint center;
        private readonly string call;
        private CivilizationElimination.Operation? child;
        private CaptureProductionDecision? pending;
        private bool started, faulted, disposed;
        public bool Completed { get; private set; }
        internal Operation(CaptureProduction parent, CitySlot city, int newOwner, int oldOwner, MapPoint center, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            this.city = city;
            this.newOwner = newOwner;
            this.oldOwner = oldOwner;
            this.center = center;
            this.call = call;
        }
        /// <summary>Any window reply closes it. Incident replies go only to their pending child.
        /// Null without completion or a decision means bounded lifecycle work remains.</summary>
        public CaptureProductionDecision? Resume(int? reply = null) => ResumeDecision(reply is { } value ? new(value) : null);
        public CaptureProductionDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed capture production operation cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (pending is CaptureProductionDecision.Window)
                {
                    if (reply is null)
                    { return pending; }
                    parent.World.Calculation.Processing = false;
                    pending = null;
                    Completed = true;
                    return null;
                }
                if (!started)
                { started = true; child = parent.Support(city, oldOwner, call); }
                var incident = child!.ResumeDecision(pending is CaptureProductionDecision.Lifecycle ? reply : null);
                if (incident is not null)
                { return pending = new CaptureProductionDecision.Lifecycle(incident); }
                pending = null;
                if (child.Result is null)
                { return null; }
                child.Dispose();
                child = null;
                if (parent.Repair(city, newOwner, oldOwner, center) is { } window)
                { return pending = new CaptureProductionDecision.Window(window); }
                Completed = true;
                return null;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
