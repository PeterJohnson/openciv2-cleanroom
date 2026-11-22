using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C02 local tile availability, yields and worker allocation. The C03 parent supplies
/// capital/overlap and C05 support before assignment; this service does not impersonate that parent.</summary>
public sealed partial class CityWorking(GameState world, RuleTables rules, StackOperations stacks)
{
    private CityCalculationWorkspace Work => world.Calculation;
    private MapPoint Point(CitySlot city, int index) => MapNeighborhoods.Offset(world.Map.Geometry,
        world.Entities.Cities.At(city).Position, MapNeighborhoods.Radius[index], world.Globals.Rules.FlatWorld);
    private bool Has(CitySlot city, int facility) => BuildingQueries.HasBuilding(world, city, facility);
    private bool Wonder(CitySlot city, int wonder) => BuildingQueries.ActiveCity(world, rules, wonder) == city.Value;
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    private static BaseTerrainWork BaseWork(TerrainRule terrain) => terrain.Work
        ?? throw new InvalidOperationException("This yield operation requires base terrain work rules");

    public void RefreshAvailability(CitySlot city)
    {
        MapKnowledge.RequireLocal(world);
        int owner = unchecked((sbyte)world.Entities.Cities.At(city).Owner.Value);
        for (int i = 0; i < Work.Availability.Length; ++i)
        {
            var point = Point(city, i);
            Work.Availability[i] = TileAvailability.None;
            if (!world.Map.Geometry.InBounds(point) || i > 20 || !world.Map.IsRevealedTo(point, owner))
            {
                Work.Availability[i] = TileAvailability.Unusable;
                continue;
            }
            ref var tile = ref world.Map.TileAt(point);
            // W05 counts visits, including repeated scans and Ocean; it is not a distinct-tile census.
            if (!world.Globals.HumanPlayers.Contains(owner) && Work.Processing && tile.Features.HasFlag(MapFeatures.Pollution))
            {
                world.Globals.WarmingPollution = unchecked((short)(world.Globals.WarmingPollution + 1));
            }

            for (int current = stacks.FirstAt(point); current >= 0; current = stacks.Next(current))
            {
                ref var unit = ref world.Entities.Units.Storage[current];
                if (unit.Owner.Value == owner || tile.Ocean || world.Civilizations[owner].Treaties[unit.Owner.Value].Alliance)
                {
                    break;
                }

                if (rules.Units[unit.Type.Value].Attack != 0)
                {
                    Work.Availability[i] |= TileAvailability.HostileUnit;
                    if (world.Globals.HumanPlayers.Contains(unit.Owner.Value))
                    {
                        Work.Availability[i] |= TileAvailability.HumanBlocker;
                    }

                    break;
                }
            }
            if (world.Map.CityOwner(point) is not null)
            {
                Work.Availability[i] |= TileAvailability.CityPresent;
            }
        }
    }

    /// <summary>Direct evaluation retains score/job effects but does not publish a tile result or total.</summary>
    public int Yield(CitySlot city, int index, CityYield kind)
    {
        MapKnowledge.RequireLocal(world);
        if (kind is < CityYield.Food or > CityYield.Trade)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        int owner = unchecked((sbyte)world.Entities.Cities.At(city).Owner.Value);
        var point = Point(city, index);
        if (!world.Map.Geometry.InBounds(point))
        {
            return 0;
        }

        ref var tile = ref world.Map.TileAt(point);
        if (Work.ApplyYield)
        {
            tile.Claimant = (byte)owner;
            if (world.Map.CityOrUnitOwner(point) is null)
            {
                tile.Owner = (byte)owner;
            }
        }
        var terrain = rules.Terrain[(int)tile.Terrain];
        var row = rules.Terrain[(int)tile.Terrain + 11 * world.Map.SpecialResource(point)];
        int value = unchecked((sbyte)(kind switch { CityYield.Food => row.Food, CityYield.Shields => row.Shields, _ => row.Trade }));
        if (world.Globals.Multiplayer != GameMode.SinglePlayer && world.Multiplayer.Saved.DoubleProduction)
        {
            value = unchecked(value * 2);
        }

        var features = tile.Features;
        bool cityComponent = features.HasFlag(MapFeatures.CitySite);
        bool irrigated = features.HasFlag(MapFeatures.Irrigation), mined = features.HasFlag(MapFeatures.Mine);
        bool road = features.HasFlag(MapFeatures.Road), rail = features.HasFlag(MapFeatures.Railroad);
        if (tile.Ocean)
        {
            if (kind == CityYield.Food && Has(city, BuildingIds.Harbor))
            {
                ++value;
            }

            if (kind == CityYield.Shields && Has(city, BuildingIds.OffshorePlatform))
            {
                ++value;
            }

            if (kind == CityYield.Trade && Wonder(city, WonderIds.Colossus))
            {
                ++value;
            }
        }
        else if (kind == CityYield.Food)
        {
            if (!cityComponent && !irrigated)
            {
                NoteTerraform(owner, point, false);
            }
            else
            {
                value += unchecked((sbyte)BaseWork(terrain).Irrigation.YieldBonus);
                if (Has(city, BuildingIds.Supermarket))
                {
                    if (!cityComponent && !mined)
                    {
                        NoteTerraform(owner, point, false);
                    }
                    else
                    {
                        value += value >> 1;
                    }
                }
            }
        }
        else if (kind == CityYield.Shields)
        {
            if ((mined && !irrigated) || (cityComponent && BaseWork(terrain).Irrigation.YieldBonus == 0))
            {
                value += unchecked((sbyte)BaseWork(terrain).Mining.YieldBonus);
            }
            else if (!irrigated && !mined)
            {
                NoteTerraform(owner, point, true);
            }

            if (tile.Terrain == Terrain.Grassland && !Geometry.GrasslandShield(point))
            {
                value = 0;
            }

            if (index == 20 && value == 0)
            {
                value = 1;
            }
        }
        else
        {
            if (tile.River)
            {
                ++value;
            }

            if (tile.Terrain is Terrain.Desert or Terrain.Plains or Terrain.Grassland || value != 0)
            {
                if (cityComponent || road)
                {
                    ++value;
                }
                else
                {
                    NoteRoad(owner, point);
                }
            }
            if (value != 0 && Wonder(city, WonderIds.Colossus))
            {
                ++value;
            }
        }
        if (kind == CityYield.Shields)
        {
            if (Wonder(city, WonderIds.KingRichard))
            {
                ++value;
            }

            if (rail || (cityComponent && Knows(owner, AdvanceIds.Railroad)))
            {
                value += value >> 1;
            }
            else if (!cityComponent && !rail && Knows(owner, AdvanceIds.Railroad) && !tile.Ocean)
            {
                NoteRoad(owner, point);
            }
        }
        ref var civ = ref world.Civilizations[owner];
        if (value > 2 && !world.Entities.Cities.At(city).Flags.Celebrating)
        {
            if (Work.Processing)
            {
                civ.GovernmentScore[(int)Government.Despotism] = unchecked((short)(civ.GovernmentScore[(int)Government.Despotism] - 1));
            }

            if (unchecked((byte)civ.Government) < (int)Government.Monarchy)
            {
                --value;
            }
        }
        if (value != 0 && kind == CityYield.Trade)
        {
            if (Work.Processing)
            {
                civ.GovernmentScore[(int)Government.Republic] = unchecked((short)(civ.GovernmentScore[(int)Government.Republic] + 1));
                civ.GovernmentScore[(int)Government.Democracy] = unchecked((short)(civ.GovernmentScore[(int)Government.Democracy] + 1));
            }
            if (unchecked((byte)civ.Government) >= (int)Government.Republic
                || (unchecked((byte)civ.Government) >= (int)Government.Monarchy && world.Entities.Cities.At(city).Flags.Celebrating))
            {
                ++value;
            }
        }
        if (kind == CityYield.Trade && (cityComponent || road) && Has(city, BuildingIds.Superhighways))
        {
            value += value / 2;
        }

        if (features.HasFlag(MapFeatures.Pollution))
        {
            value = unchecked(value + 1) >> 1;
            Work.JobPriority = unchecked(Work.JobPriority + 1);
            Work.JobFound = true;
            Work.JobPosition = point;
        }
        return Math.Max(value, 0);
    }

    private void NoteTerraform(int owner, MapPoint point, bool mining)
    {
        if (!Work.ApplyYield || !Work.Processing || Work.JobFound)
        {
            return;
        }

        var terrain = rules.Terrain[(int)world.Map.TileAt(point).Terrain];
        var work = BaseWork(terrain);
        var job = mining ? work.Mining : work.Irrigation;
        if (job.Result == TerrainResult.NoResult || unchecked((sbyte)job.AiGovernmentThreshold) == 0)
        {
            return;
        }

        if (!mining && !TerrainSources.Irrigation(world, point))
        {
            return;
        }

        int government = unchecked((byte)world.Civilizations[owner].Government);
        if (government < (int)Government.Monarchy)
        {
            government = 1;
        }

        if (unchecked((sbyte)job.AiGovernmentThreshold) > government)
        {
            return;
        }

        Work.JobPriority = unchecked(Work.JobPriority + 1);
        Work.JobFound = true;
        Work.JobPosition = point;
    }

    private void NoteRoad(int owner, MapPoint point)
    {
        if (!Work.ApplyYield || !Work.Processing || Work.JobFound)
        {
            return;
        }

        if (world.Map.TileAt(point).River && !Knows(owner, AdvanceIds.BridgeBuilding))
        {
            return;
        }

        Work.JobFound = true;
        Work.JobPosition = point;
    }

    public void Accumulate(CitySlot city, int index, bool apply)
    {
        Work.ApplyYield = apply;
        for (var kind = CityYield.Food; kind <= CityYield.Trade; ++kind)
        {
            Work.TileYield[(int)kind] = Yield(city, index, kind);
            if (Work.ApplyYield)
            {
                Work.Totals[(int)kind] = unchecked(Work.Totals[(int)kind] + Work.TileYield[(int)kind]);
            }
        }
        Work.ApplyYield = false;
    }
}
