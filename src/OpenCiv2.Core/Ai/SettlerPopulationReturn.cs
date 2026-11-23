using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public interface ISettlerPopulationEffects
{
    /// <summary>Cosmetic: refresh this city's window if it is displayed.</summary>
    void RefreshCity(CitySlot city);
    /// <summary>Cosmetic: redraw the retained map area.</summary>
    void Redraw(MapPoint point);
}

/// <summary>A03 §8 crowded-region absorption/return, after retained Goto and defender
/// decisions. Absorption uses actual cargo-aware U01 destruction and nested lifecycle checks.</summary>
public sealed class SettlerPopulationReturn(GameState world, UnitLoss loss, CivilizationElimination lifecycle,
    EspionageIncidents incidents, ISettlerPopulationEffects effects)
{
    public Operation Begin(UnitTacticsContext context, string call) => new(this, context, call);
    private GameState World => world;
    private ref State.City Home(UnitTacticsContext c) => ref world.Entities.Cities.At(c.HomeCity
        ?? throw new InvalidOperationException("A03 population return requires a produced home-city reference"));
    private SpecialUnitChoice? Select(UnitTacticsContext c)
    {
        ref var actor = ref world.Entities.Units.At(c.Unit);
        if (!c.SettlerKind || (world.Map.Regions[c.Region].Fertile >> 3) >= world.Civilizations[c.Owner].ContinentCities[c.Region]
            || actor.Home is null)
        {
            return new(SpecialUnitRoute.Continue);
        }

        if (c.HomeDistance == 0 && Home(c).Size < 10)
        { Home(c).Size = unchecked((sbyte)(Home(c).Size + 1)); return null; }
        if (c.HomeCity is not null && c.Region == c.HomeRegion && Home(c).Size < 10)
        { UnitOrders.SetGoto(ref actor, AiTaskIds.ReturnForPopulation, Home(c).Position); return new(SpecialUnitRoute.Finish); }
        return new(SpecialUnitRoute.Continue);
    }
    private CivilizationElimination.Operation Destroy(UnitTacticsContext c, string call) => lifecycle.BeginDestroy(loss, c.Unit.Value, call + "/destroy", incidents);
    private void Present(Operation operation, CitySlot city, MapPoint position)
    {
        operation.Present(() => effects.RefreshCity(city));
        operation.Present(() => effects.Redraw(position));
    }
    public sealed class Operation : IDisposable
    {
        private readonly SettlerPopulationReturn parent;
        private readonly UnitTacticsContext context;
        private readonly string call;
        private bool started, faulted, disposed;
        private MapPoint position;
        private CivilizationElimination.Operation? child;
        private readonly List<Exception> failures = [];
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        public SpecialUnitChoice? Result { get; private set; }
        internal Operation(SettlerPopulationReturn parent, UnitTacticsContext context, string call)
        { this.parent = parent; this.context = context; this.call = call; }
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public LifecycleDecision? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Failed population return cannot resume");
            }

            if (Result is not null)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    ArgumentException.ThrowIfNullOrEmpty(call);
                    started = true;
                    Result = parent.Select(context);
                    if (Result is not null)
                    {
                        return null;
                    }

                    position = context.Position;
                    child = parent.Destroy(context, call);
                    reply = null;
                }
                var pending = child!.Resume(reply);
                if (child.Result is null)
                {
                    return pending;
                }

                child.Dispose();
                child = null;
                parent.Present(this, context.HomeCity!.Value, position);
                Result = new(SpecialUnitRoute.ReturnOne);
                return null;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
