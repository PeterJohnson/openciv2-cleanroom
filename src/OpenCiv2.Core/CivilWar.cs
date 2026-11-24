using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record CivilWarNotice(int Parent, int Rebel, string ParentAdjective, string RebelAdjective);
public interface ICivilWarEffects
{
    void Schism(CivilWarNotice notice);
    void RedrawMaps();
}

/// <summary>V01 §§9–11 local civil war with actual creation, C01 transfer, C09 central selection and U01 stacks.
/// Scenario action dispatch is required when NoSchism matches; no events-loaded gate or default execution is supplied.</summary>
public sealed class CivilWar(GameState world, StackOperations stacks, Func<int, string, CivilizationCreation> create,
    Action<Event> executeEvent, ICivilWarEffects effects)
{
    private readonly ScenarioPermissionActions? retainedEvents;
    public CivilWar(GameState world, StackOperations stacks, Func<int, CivilizationCreation> create,
        Action<Event> executeEvent, ICivilWarEffects effects)
        : this(world, stacks, (owner, _) => create(owner), executeEvent, effects) { }
    public CivilWar(GameState world, StackOperations stacks, Func<int, string, CivilizationCreation> create,
        ScenarioPermissionActions events, ICivilWarEffects effects)
        : this(world, stacks, create, _ => throw new InvalidOperationException("Retained scenario events require their continuation"), effects)
    { retainedEvents = events; }
    public CivilWar(GameState world, StackOperations stacks, Func<int, CivilizationCreation> create,
        ScenarioPermissionActions events, ICivilWarEffects effects)
        : this(world, stacks, create, _ => throw new InvalidOperationException("Retained scenario events require their continuation"), effects)
    { retainedEvents = events; }
    private GameState World => world;
    public Operation Begin(int parent, string call = "civil-war") => new(this, parent, call);

    public sealed class Operation : IDisposable
    {
        private readonly CivilWar service;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private int candidateBudget;
        private ResearchReply? reply;
        public LifecycleDecision? Pending { get; private set; }
        private ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        private bool faulted, disposed;
        private readonly List<Exception> presentationFailures = [];
        public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
        public int? Result { get; private set; }
        internal Operation(CivilWar service, int parent, string call)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(parent);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(parent, EntityLimits.Civilizations);
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.service = service;
            steps = service.Execute(parent, () => candidateBudget, TakeReply, presentationFailures.AddRange, result => Result = result, call).GetEnumerator();
        }
        /// <summary>Null is a bounded creation search or a completed fallback city transfer; resume without restarting.</summary>
        public int? Advance(int candidateBudget = 64, int? answer = null) => AdvanceDecision(candidateBudget, answer is { } value ? new(value) : null);
        public int? AdvanceDecision(int candidateBudget = 64, ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
            if (faulted)
            {
                throw new InvalidOperationException("A failed civil war cannot resume");
            }

            if (Result is not null)
            {
                return Result;
            }

            try
            {
                MapKnowledge.RequireLocal(service.World);
                this.candidateBudget = candidateBudget;
                if (Pending is not null && answer is null)
                { return null; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (!steps.MoveNext())
                {
                    steps.Dispose();
                }
                else
                { Pending = steps.Current; }

                return Result;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private IEnumerable<LifecycleDecision?> Execute(int parent, Func<int> budget, Func<ResearchReply?> takeReply, Action<IEnumerable<Exception>> report, Action<int> complete, string call)
    {
        bool allowed;
        if (retainedEvents is null)
        { allowed = ScenarioPermissions.AllowSchism(world, parent, executeEvent); }
        else
        {
            using var permission = retainedEvents.BeginSchism(parent, call + "/permission");
            while (permission.Allowed is null)
            {
                var pending = permission.Resume(takeReply());
                if (permission.Allowed is null)
                { yield return pending; }
            }
            report(permission.PresentationFailures);
            allowed = permission.Allowed.Value;
        }
        if (!allowed)
        { complete(0); yield break; }
        int rebel = -1;
        for (int i = 1; i < EntityLimits.Civilizations; ++i)
        {
            if (world.Civilizations[i].MilitaryCount == 0 && world.Civilizations[i].CityCount == 0)
            {
                rebel = i;
            }
        }

        if (rebel < 0)
        { complete(0); yield break; }
        int[] regions = new int[MapLimits.Regions];
        int capitalRegion = -1;
        CitySlot? capital = null;
        MapPoint origin = default;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied || unchecked((sbyte)city.Owner.Value) != parent)
            {
                continue;
            }

            int region = world.Map.TileAt(city.Position).Region;
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(region, regions.Length);
            regions[region] = unchecked(regions[region] + city.Size);
            if (BuildingQueries.HasBuilding(world, new((ushort)i), BuildingIds.Palace)
                && (capital is null || world.Civilizations[parent].CapitalX == city.Position.X))
            { capital = new((ushort)i); capitalRegion = region; origin = city.Position; }
        }
        if (capital is null)
        { complete(0); yield break; }
        world.Globals.CivsAlive = world.Globals.CivsAlive.With(rebel, false);
        using var creation = create(rebel, call + "/creation");
        while (creation.AdvanceDecision(budget(), takeReply()) is null)
        {
            yield return creation.Pending;
        }
        report(creation.PresentationFailures);

        if (creation.Result == 0)
        { complete(0); yield break; }
        world.Globals.CivsAlive = world.Globals.CivsAlive.With(rebel);
        effects.Schism(new(parent, rebel, world.Names[parent].Adjective, world.Names[rebel].Adjective));
        InitializeRebel(parent, rebel);
        int loyalPopulation = regions[capitalRegion], rebelPopulation = 0;
        for (int region = 0; region < regions.Length; ++region)
        {
            int population = regions[region];
            if (population == 0)
            {
                continue;
            }

            if (loyalPopulation < rebelPopulation || region == capitalRegion)
            {
                if (region != capitalRegion)
                {
                    loyalPopulation = unchecked(loyalPopulation + population);
                }

                regions[region] = 1;
            }
            else
            { rebelPopulation = unchecked(rebelPopulation + population); regions[region] = 2; }
        }
        if (loyalPopulation < unchecked(2 * rebelPopulation) && rebelPopulation <= loyalPopulation)
        {
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                ref var city = ref world.Entities.Cities.Storage[i];
                if (!city.Token.Occupied || unchecked((sbyte)city.Owner.Value) != parent)
                {
                    continue;
                }

                var slot = new CitySlot((ushort)i);
                if (regions[world.Map.TileAt(city.Position).Region] == 2)
                { MapKnowledge.RevealCity(world, parent, slot); CityTransfer.Transfer(world, slot, parent, rebel); }
                else
                {
                    MapKnowledge.RevealCity(world, rebel, slot);
                }
            }
        }
        else
        {
            int transferred = 0, target = world.Civilizations[parent].TotalCitySize;
            while (unchecked(3 * transferred) < target)
            {
                CitySlot? selected = null;
                int bestDistance = 1;
                for (int i = 0; i < world.Entities.Cities.End; ++i)
                {
                    ref var city = ref world.Entities.Cities.Storage[i];
                    if (!city.Token.Occupied || unchecked((sbyte)city.Owner.Value) != parent)
                    {
                        continue;
                    }

                    var slot = new CitySlot((ushort)i);
                    MapKnowledge.RevealCity(world, rebel, slot);
                    MapKnowledge.RevealCity(world, parent, slot);
                    int distance = world.Map.Geometry.WeightedDistance(origin, city.Position, world.Globals.Rules.FlatWorld);
                    if (distance > bestDistance)
                    { bestDistance = distance; selected = slot; }
                }
                if (selected is not { } chosen)
                {
                    break;
                }

                transferred = unchecked(transferred + world.Entities.Cities.At(chosen).Size);
                CityTransfer.Transfer(world, chosen, parent, rebel);
                yield return null;
            }
        }
        TransferUnits(parent, rebel);
        InstallCapital(rebel);
        world.Entities.Cities.At(capital.Value).Owner = new(byte.MaxValue);
        InstallCapital(parent);
        world.Entities.Cities.At(capital.Value).Owner = new(unchecked((byte)parent));
        effects.RedrawMaps();
        complete(1);
    }

    private void InitializeRebel(int parent, int rebel)
    {
        world.Civilizations[rebel].Gold = world.Civilizations[parent].Gold / 2;
        world.Civilizations[parent].Gold = unchecked(world.Civilizations[parent].Gold - world.Civilizations[rebel].Gold);
        world.Civilizations[rebel].MilitaryStrength = (ushort)(world.Civilizations[parent].MilitaryStrength / 2);
        world.Civilizations[rebel].Government = world.Civilizations[parent].Government;
        world.Civilizations[rebel].ResearchProgress = world.Civilizations[parent].ResearchProgress;
        world.Civilizations[rebel].NumAdvances = world.Civilizations[parent].NumAdvances;
        world.Civilizations[rebel].Advances = world.Civilizations[parent].Advances;
        for (int other = 1; other < EntityLimits.Civilizations; ++other)
        {
            world.Civilizations[other].LastContactTurn[rebel] = unchecked((short)(world.Globals.Turn - 8));
        }

        world.Civilizations[rebel].Treaties[parent] = new() { Contact = true, War = true };
        world.Civilizations[parent].Treaties[rebel] = new() { Contact = true, War = true, Aggressor = true, ExtendedAggressor = true };
        int x = 0, y = 0;
        for (int i = 0; i < world.Map.Geometry.Area; ++i)
        {
            MapPoint point = new(x, y);
            if (world.Map.TileAt(point).RevealedTo.Contains(parent))
            {
                MapKnowledge.Explore(world, rebel, point);
            }

            x += 2;
            if (x >= world.Map.Geometry.Width)
            { ++y; x = y & 1; }
        }
    }

    private void TransferUnits(int parent, int rebel)
    {
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || unchecked((sbyte)unit.Owner.Value) != parent)
            {
                continue;
            }

            var city = CityQueries.At(world, unit.Position) ?? unit.Home;
            if (city is not { } slot)
            {
                continue;
            }

            int owner = unchecked((sbyte)world.Entities.Cities.At(slot).Owner.Value);
            if (owner != parent && owner != rebel)
            {
                continue;
            }

            world.Map.Mutate(new TileChange.Owner(unit.Position, unchecked((byte)owner)));
            for (int member = stacks.First(i); member >= 0; member = stacks.Next(member))
            {
                ref var record = ref world.Entities.Units.Storage[member];
                record.Owner = new(unchecked((byte)owner));
                if (record.Home is { } home && unchecked((sbyte)world.Entities.Cities.At(home).Owner.Value) != owner)
                {
                    byte value = unchecked((byte)slot.Value);
                    record.Home = value == 255 ? null : new(value);
                }
            }
        }
    }
    private void InstallCapital(int owner)
    {
        if (CityCaptureQueries.CentralCity(world, owner) is not { } city)
        {
            return;
        }

        BuildingQueries.SetBuilding(world, city, BuildingIds.Palace, true);
        world.Civilizations[owner].CapitalX = unchecked((short)world.Entities.Cities.At(city).Position.X);
    }
}
