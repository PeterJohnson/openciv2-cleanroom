using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public enum SpecialUnitRoute { Continue, Direction, Finish, PlanMove, ReturnOne }
public readonly record struct SpecialUnitChoice(SpecialUnitRoute Route, int Direction = 8);

/// <summary>A07 §§2–3 destination policies with actual cargo-aware destruction on the no-home/no-target paths.
/// Orders are executed by the A04/U04/U03/U02 caller, not by the candidate scan.</summary>
public sealed partial class SpecialUnitDestinations(GameState world, RuleTables rules, UnitLoss loss,
    CivilizationElimination lifecycle, EspionageIncidents incidents)
{
    private ref Unit Actor(UnitTacticsContext c) => ref world.Entities.Units.At(c.Unit);
    private ref Civilization Civ(UnitTacticsContext c) => ref world.Civilizations[c.Owner];
    private bool HasGoto(UnitTacticsContext c) => Actor(c).Orders == UnitOrder.GoTo;
    private int Distance(MapPoint a, MapPoint b) => world.Map.Geometry.WeightedDistance(a, b, world.Globals.Rules.FlatWorld);
    private MapPoint Neighbor(MapPoint p, int direction)
    {
        var d = MapNeighborhoods.Directions[direction];
        return new(world.Map.Geometry.WrapX(p.X + d.X, world.Globals.Rules.FlatWorld), p.Y + d.Y);
    }
    private GameState World => world;
    private CivilizationElimination.Operation Destroy(UnitTacticsContext c, string call) =>
        lifecycle.BeginDestroy(loss, c.Unit.Value, call + "/destroy", incidents);
    public Operation Caravan(UnitTacticsContext context, string call) => new(this, context, call, true);
    public Operation Diplomat(UnitTacticsContext context, string call) => new(this, context, call, false);

    public sealed class Operation : IDisposable
    {
        private readonly SpecialUnitDestinations parent;
        private readonly UnitTacticsContext context;
        private readonly string call;
        private readonly bool caravan;
        private bool started, faulted, disposed;
        private CivilizationElimination.Operation? child;
        public SpecialUnitChoice? Result { get; private set; }
        internal Operation(SpecialUnitDestinations parent, UnitTacticsContext context, string call, bool caravan)
        { this.parent = parent; this.context = context; this.call = call; this.caravan = caravan; }
        public LifecycleDecision? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed special-unit destination choice cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    ArgumentException.ThrowIfNullOrEmpty(call);
                    started = true;
                    Result = caravan ? parent.ChooseCaravan(context) : parent.ChooseDiplomat(context);
                    if (Result is not null)
                    { return null; }
                    child = parent.Destroy(context, call);
                }
                var pending = child!.Resume(reply);
                if (pending is not null)
                { return pending; }
                if (child.Result is null)
                { return null; }
                child.Dispose();
                child = null;
                Result = new(SpecialUnitRoute.ReturnOne);
                return null;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
