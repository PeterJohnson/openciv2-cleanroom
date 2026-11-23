using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum NuclearNoticeKind { UseWeapons, Intercepted }
public sealed record NuclearNotice(NuclearNoticeKind Kind, int Recipient, string Name);
public interface INuclearEffects
{
    void Notice(NuclearNotice notice);
    /// <summary>P10 semantic effect completion before the first unit lookup; invisible targets still take damage.</summary>
    void Animate(MapPoint target);
    void Redraw(MapPoint point);
}

/// <summary>U07 local nuclear action and independent blast, with actual W05 placement and U01/V01 loss.
/// Delivery eligibility and missile consumption belong to the movement/combat/espionage caller.</summary>
public sealed class NuclearEffects(GameState world, GameplayRandom random, StackOperations stacks,
    UnitLoss loss, CivilizationElimination lifecycle, EspionageIncidents incidents, INuclearEffects effects)
{
    private GameState World => world;
    private EspionageIncidents Incidents => incidents;
    public Operation Begin(int attacker, MapPoint target, bool checkSdi, string call) => new(this, attacker, target, checkSdi, call);

    private MapPoint Neighbor(MapPoint center, int index) => MapNeighborhoods.Offset(world.Map.Geometry, center,
        index == 8 ? new(0, 0) : MapNeighborhoods.Directions[index], world.Globals.Rules.FlatWorld);
    private void Notice(NuclearNoticeKind kind, int recipient, string name)
    {
        if (world.Globals.HumanPlayers.Contains(recipient & 31))
        { effects.Notice(new(kind, recipient, name)); }
    }

    /// <summary>Direct blast deliberately retains units, diplomacy, city ownership and aggregate population.</summary>
    public void Blast(MapPoint center, string call)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        if (!world.Map.Geometry.InBounds(center))
        { return; }
        for (int index = 0; index <= 8; ++index)
        {
            var point = Neighbor(center, index);
            if (!world.Map.Geometry.InBounds(point))
            { continue; }
            if (CityQueries.At(world, point) is { } slot)
            {
                ref var city = ref world.Entities.Cities.At(slot);
                city.Size = unchecked((sbyte)(city.Size - (city.Size >> 1)));
                effects.Redraw(point);
                continue;
            }
            ref var tile = ref world.Map.TileAt(point);
            if (tile.Ocean)
            { continue; }
            if ((tile.Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification)
            { Clear(point, MapFeatures.Fortification); }
            if (Draw("railroad", index, 2) != 0)
            { Clear(point, MapFeatures.Railroad); }
            bool farmland = (tile.Features & (MapFeatures.Mine | MapFeatures.Irrigation)) == (MapFeatures.Mine | MapFeatures.Irrigation);
            if (Draw("mine", index, 2) != 0)
            { Clear(point, MapFeatures.Mine); }
            if (!farmland && Draw("irrigation", index, 2) != 0)
            { Clear(point, MapFeatures.Irrigation); }
            for (int observer = 1; observer < EntityLimits.Civilizations; ++observer)
            { MapKnowledge.Remember(world, observer, point); }
            if (Draw("pollution", index, 3) != 0)
            { PollutionPlacement.Place(world, point); }
            effects.Redraw(point);
        }
        int Draw(string site, int index, int bound) => random.PrimaryModulo(new ChoiceKey("U07.blast." + site, call, index), bound);
    }
    private void Clear(MapPoint point, MapFeatures features) => world.Map.Mutate(new TileChange.Features(point, features, false));

    public sealed class Operation : IDisposable
    {
        private readonly NuclearEffects parent;
        private readonly IEnumerator<CivilizationElimination.Operation> steps;
        private CivilizationElimination.Operation? child;
        private bool faulted, disposed;
        public int? Result { get; private set; }
        internal Operation(NuclearEffects parent, int attacker, MapPoint target, bool checkSdi, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentOutOfRangeException.ThrowIfNegative(attacker);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(attacker, EntityLimits.Civilizations);
            this.parent = parent;
            steps = parent.Action(attacker, target, checkSdi, call, value => Result = value).GetEnumerator();
        }
        /// <summary>Returns a pending incident; null with no Result is a bounded lifecycle search slice.</summary>
        public LifecycleDecision? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed nuclear action cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                while (true)
                {
                    if (child is not null)
                    {
                        var pending = child.Resume(reply);
                        reply = null;
                        if (pending is not null)
                        { return pending; }
                        if (child.Result is null)
                        { return null; }
                        child.Dispose();
                        child = null;
                    }
                    if (!steps.MoveNext())
                    { return null; }
                    child = steps.Current;
                    reply = null;
                }
            }
            catch { faulted = true; Release(); throw; }
        }
        private void Release() { child?.Dispose(); steps.Dispose(); }
        public void Dispose() { disposed = true; Release(); }
    }

    private IEnumerable<CivilizationElimination.Operation> Action(int attacker, MapPoint target, bool checkSdi,
        string call, Action<int> complete)
    {
        int targetOwner = world.Map.CityOrUnitOwner(target) is { } owner ? owner : -1;
        Notice(NuclearNoticeKind.UseWeapons, targetOwner, world.Names[attacker].Tribe);
        if (checkSdi)
        {
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                ref var city = ref world.Entities.Cities.Storage[i];
                if (!city.Token.Occupied || city.Owner.Value == attacker
                    || !BuildingQueries.HasBuilding(world, new((ushort)i), BuildingIds.SdiDefense)
                    || world.Map.Geometry.StepDistance(city.Position, target, world.Globals.Rules.FlatWorld) >= 4)
                { continue; }
                string name = city.Name;
                Notice(NuclearNoticeKind.Intercepted, city.Owner.Value, name);
                Notice(NuclearNoticeKind.Intercepted, attacker, name);
                complete(0);
                yield break;
            }
        }
        effects.Animate(target);
        for (int index = 0; index <= 8; ++index)
        {
            var point = Neighbor(target, index);
            if (!world.Map.Geometry.InBounds(point))
            { continue; }
            int first = stacks.FirstAt(point);
            if (first < 0)
            { continue; }
            int victim = world.Entities.Units.Storage[first].Owner.Value;
            if (victim != attacker)
            {
                world.Civilizations[victim].Treaties[attacker].Vendetta = true;
                world.Civilizations[victim].Treaties[attacker].NuclearThreat = true;
                world.Civilizations[attacker].Treaties[victim].Nuked = true;
                DiplomaticAttitudes.Adjust(world, victim, attacker, 100);
            }
            yield return lifecycle.BeginEraseStack(loss, first, call + "/tile/" + index, Incidents);
        }
        Blast(target, call);
        complete(1);
    }
}
