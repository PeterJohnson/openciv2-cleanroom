using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>F05 §§9–10 local accelerated starts with actual research, city, unit, development and road
/// children. Advance processes one captured civilization start; failure preserves the completed prefix.</summary>
public sealed class AcceleratedStarts
{
    private readonly GameState world;
    private readonly ProcessContext process;
    private readonly CityCreation cities;
    private readonly UnitCreation units;
    private readonly StackOperations stacks;
    private readonly GameplayRandom random;
    private readonly string call;
    private readonly int era;
    private readonly MapPoint[] starts = Enumerable.Repeat(new MapPoint(-1, -1), EntityLimits.Civilizations).ToArray();
    // F05's documented safe initializer. Later actual unit distances persist across candidates and owners.
    private int lastUnitDistance = 5;
    private int nextCivilization = 1;
    private bool failed;

    public bool Completed { get; private set; }
    public int CurrentCivilization => nextCivilization;

    public AcceleratedStarts(
        GameState world, ProcessContext process, CityCreation cities, UnitCreation units, StackOperations stacks,
        GameplayRandom random, string call, int era)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrEmpty(call);
        if (world.Globals.Turn != 0 || world.Globals.Multiplayer != GameMode.SinglePlayer)
        {
            throw new InvalidOperationException("Accelerated starts require local turn zero");
        }
        this.world = world;
        this.process = process;
        this.cities = cities;
        this.units = units;
        this.stacks = stacks;
        this.random = random;
        this.call = call;
        this.era = era;
        foreach (ref readonly var unit in world.Entities.Units.Scanned)
        {
            if (unit.Token.Occupied)
            {
                starts[unit.Owner.Value] = unit.Position;
            }
        }
    }

    public bool Advance()
    {
        if (failed)
        {
            throw new InvalidOperationException("Failed acceleration cannot resume");
        }

        if (Completed)
        {
            return true;
        }

        try
        {
            int civilization = nextCivilization;
            if (world.Map.Geometry.InBounds(starts[civilization]))
            {
                Package(civilization, starts[civilization]);
            }

            if (++nextCivilization < EntityLimits.Civilizations)
            {
                return false;
            }

            world.Globals.Turn = unchecked((short)(20 * (unchecked((short)era) + 1) + 1));
            Completed = true;
            return true;
        }
        catch
        {
            failed = true;
            throw;
        }
    }

    private void Package(int civilization, MapPoint start)
    {
        foreach (int advance in new[] { AdvanceIds.HorsebackRiding, AdvanceIds.CeremonialBurial, AdvanceIds.Alphabet, AdvanceIds.BronzeWorking })
        {
            Research.GrantStarting(world, civilization, advance);
        }
        ref var civ = ref world.Civilizations[civilization];
        civ.NumAdvances = 4;
        if (era != 0)
        {
            for (int i = 0; i < 3; ++i)
            {
                Research.CompleteStarting(world, process.Rules, civilization, random, call + "/civ/" + civilization + "/advance/" + i);
                civ.NumAdvances = unchecked((byte)(civ.NumAdvances + 1));
            }
        }
        if (cities.Create(new((byte)civilization), start) is not { } citySlot)
        {
            return;
        }

        int treasury = random.PrimaryModulo(new ChoiceKey("F05.accelerated.treasury", call, civilization), 50);
        civ.Gold = unchecked((treasury + 25) * (era + 1));
        ref var city = ref world.Entities.Cities.At(citySlot);
        city.Size = (sbyte)(era == 0 ? 3 : 5);
        int first = stacks.FirstAt(start);
        if (stacks.Count(first) > 1)
        {
            world.Entities.Units.Storage[first].Home = new(unchecked((byte)citySlot.Value));
        }
        int production = city.Production;
        Fortified(production == UnitTypeIds.Pikemen ? UnitTypeIds.Pikemen : UnitTypeIds.Archers, civilization, start);
        Fortified(UnitTypeIds.Phalanx, civilization, start);
        Create(Knows(civilization, AdvanceIds.TheWheel) ? UnitTypeIds.Chariot : UnitTypeIds.Horsemen, civilization, start);
        if (era != 0)
        {
            Create(Knows(civilization, AdvanceIds.Polytheism) ? UnitTypeIds.Elephant : UnitTypeIds.Chariot, civilization, start);
        }
        BuildingQueries.SetBuilding(world, citySlot, BuildingIds.Temple, true);
        if (era != 0)
        {
            if (Knows(civilization, AdvanceIds.Pottery))
            {
                BuildingQueries.SetBuilding(world, citySlot, BuildingIds.Granary, true);
            }

            BuildingQueries.SetBuilding(world, citySlot,
                Knows(civilization, AdvanceIds.Currency) ? BuildingIds.Marketplace : BuildingIds.Barracks, true);
        }
        DevelopCity(world, citySlot, era == 0 ? 2 : 4);
        if (era != 0)
        {
            SecondCity(civilization, citySlot, start);
        }

        if (Knows(civilization, AdvanceIds.Monarchy))
        {
            civ.Government = Government.Monarchy;
        }
    }

    private bool Knows(int civilization, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, advance);

    private UnitSlot? Create(int type, int civilization, MapPoint position) => units.Create(new((byte)type), new((byte)civilization), position);

    private void Fortified(int type, int civilization, MapPoint position)
    {
        if (Create(type, civilization, position) is not { } slot)
        {
            throw new InvalidOperationException("F05 accelerated defender creation failed before its fortification write");
        }
        world.Entities.Units.At(slot).Orders = UnitOrder.Fortified;
    }

    private void SecondCity(int civilization, CitySlot first, MapPoint start)
    {
        int selected = -1, best = -1, region = world.Map.TileAt(start).Region;
        for (int d = 0; d < MapNeighborhoods.Directions.Length; ++d)
        {
            var offset = MapNeighborhoods.Directions[d];
            var candidate = MapNeighborhoods.Offset(world.Map.Geometry, start, new(5 * offset.X, 5 * offset.Y), world.Globals.Rules.FlatWorld);
            if (!world.Map.Geometry.InBounds(candidate))
            {
                continue;
            }

            ref var tile = ref world.Map.TileAt(candidate);
            if (tile.Ocean || tile.Region != region)
            {
                continue;
            }
            // The nearest-city/minimum result is not the acceptance distance. The last eligible unit is.
            _ = CityQueries.Nearest(world, candidate);
            foreach (ref readonly var unit in world.Entities.Units.Scanned)
            {
                if (unit.Token.Occupied && !world.Map.TileAt(unit.Position).City)
                {
                    lastUnitDistance = world.Map.Geometry.WeightedDistance(unit.Position, candidate, world.Globals.Rules.FlatWorld);
                }
            }
            if (lastUnitDistance <= 4)
            {
                continue;
            }

            int score = tile.Terrain switch { Terrain.Grassland => 4, Terrain.Plains => 2, _ => 0 };
            if (score != 0 && tile.River)
            {
                ++score;
            }

            if (score > best)
            { best = score; selected = d; }
        }
        if (selected < 0)
        {
            foreach (int advance in new[] { AdvanceIds.Currency, AdvanceIds.Masonry, AdvanceIds.Construction,
                AdvanceIds.Mysticism, AdvanceIds.CodeOfLaws, AdvanceIds.Monarchy })
            {
                Research.GrantStarting(world, civilization, advance);
            }
            ref var city = ref world.Entities.Cities.At(first);
            city.Size = 7;
            BuildingQueries.SetBuilding(world, first, BuildingIds.Colosseum, true);
            world.Civilizations[civilization].Gold = unchecked(world.Civilizations[civilization].Gold + 25);
            DevelopCity(world, first, 2);
            Create(UnitTypeIds.Archers, civilization, city.Position);
            Create(UnitTypeIds.Legion, civilization, city.Position);
            Create(UnitTypeIds.Chariot, civilization, city.Position);
            return;
        }
        var direction = MapNeighborhoods.Directions[selected];
        var position = MapNeighborhoods.Offset(world.Map.Geometry, start, new(5 * direction.X, 5 * direction.Y), world.Globals.Rules.FlatWorld);
        if (cities.Create(new((byte)civilization), position) is not { } second)
        {
            throw new InvalidOperationException("F05 accelerated second-city creation failed before its population write");
        }
        world.Entities.Cities.At(second).Size = 3;
        int pair = random.PrimaryModulo(new ChoiceKey("F05.accelerated.defenders", call, civilization), 2);
        Fortified(pair == 0 ? UnitTypeIds.Warriors : UnitTypeIds.Phalanx, civilization, position);
        Fortified(pair == 0 ? UnitTypeIds.Phalanx : UnitTypeIds.Archers, civilization, position);
        Create(UnitTypeIds.Horsemen, civilization, position);
        DevelopCity(world, second, 2);
        ConnectRoad(world, process.Rules.Definitions, second, start, civilization);
    }

    /// <summary>F05 §10. Selection sees every improvement/occupancy component; only improved tiles get
    /// remembered features, while the final same-region land survey publishes exploration alone.</summary>
    public static void DevelopCity(GameState world, CitySlot slot, int count)
    {
        MapKnowledge.RequireLocal(world);
        var map = world.Map;
        var center = world.Entities.Cities.At(slot).Position;
        for (int pass = 0; pass < count; ++pass)
        {
            MapPoint? selected = null;
            int best = 0;
            for (int i = 0; i < 20; ++i)
            {
                var point = MapNeighborhoods.Offset(map.Geometry, center, MapNeighborhoods.City[i], world.Globals.Rules.FlatWorld);
                if (!map.Geometry.InBounds(point))
                {
                    continue;
                }

                ref var tile = ref map.TileAt(point);
                if (tile.Features != MapFeatures.None)
                {
                    continue;
                }

                int score = tile.Terrain switch
                {
                    Terrain.Desert => 1,
                    Terrain.Plains => 4,
                    Terrain.Grassland => Geometry.GrasslandShield(point) ? 5 : 3,
                    Terrain.Hills => 2,
                    _ => 0
                };
                if (score == 0)
                {
                    continue;
                }

                if (i < 8)
                {
                    ++score;
                }

                if (map.SpecialResource(point) != 0)
                {
                    score += 3;
                }

                if (score > best)
                { best = score; selected = point; }
            }
            if (selected is not { } chosen)
            {
                break;
            }

            ref var changed = ref map.TileAt(chosen);
            changed.Features |= MapFeatures.Road | (changed.Terrain == Terrain.Hills ? MapFeatures.Mine : MapFeatures.Irrigation);
            MapKnowledge.Remember(world, world.Entities.Cities.At(slot).Owner.Value, chosen);
        }
        int owner = world.Entities.Cities.At(slot).Owner.Value;
        foreach (var offset in MapNeighborhoods.Radius)
        {
            var point = MapNeighborhoods.Offset(map.Geometry, center, offset, world.Globals.Rules.FlatWorld);
            if (map.Geometry.InBounds(point) && !map.TileAt(point).Ocean
                && map.TileAt(point).Region == map.TileAt(world.Entities.Cities.At(slot).Position).Region)
            {
                MapKnowledge.Explore(world, owner, point);
            }
        }
    }

    /// <summary>F05 accelerated road connection, using U03's actual uniform reverse flood and retained cache.</summary>
    public static void ConnectRoad(GameState world, RuleTables rules, CitySlot second, MapPoint goal, int civilization)
    {
        MapKnowledge.RequireLocal(world);
        var source = world.Entities.Cities.At(second).Position;
        var map = world.Map;
        bool flat = world.Globals.Rules.FlatWorld;
        if (map.Geometry.StepDistance(source, goal, flat) >= 23)
        {
            return;
        }

        var paths = world.Paths;
        paths.UniformCost = true;
        paths.Owner = -1;
        paths.SelectedUnitType = UnitTypeIds.Warriors;
        paths.Goal = goal;
        for (int step = 0; step < 50; ++step)
        {
            int direction = GenerationConnectivity.UniformDirection(map, flat, rules, paths, source, 99);
            if (direction is < 0 or >= 8)
            {
                break;
            }

            source = MapNeighborhoods.Offset(map.Geometry, source, MapNeighborhoods.Directions[direction], flat);
            if (source == goal)
            {
                break;
            }

            ref var tile = ref map.TileAt(source);
            if (!tile.City && !tile.Features.HasFlag(MapFeatures.Road)
                && (!tile.River || CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, AdvanceIds.BridgeBuilding))
                && map.CityOrUnitOwner(source) is null && TerritoryQueries.PeaceOwner(world, source, civilization) is null)
            {
                tile.Features |= MapFeatures.Road;
                MapKnowledge.Remember(world, civilization, source);
            }
        }
        paths.UniformCost = false;
    }
}
