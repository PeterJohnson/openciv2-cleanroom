using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Query;

/// <summary>P02 map information and W03 §1 knowledge channels. Only Session supplies the admitted identity.</summary>
public static class MapProjection
{
    public static MapObservation? Observe(GameState world, ClientIdentity identity, ulong revision, RuleState rules)
    {
        var map = world.Map;
        if (!map.Allocated)
        {
            return null;
        }

        if (identity.Runtime.Value == 0 || identity.Session.Value == 0
            || identity.Observer.Value >= EntityLimits.Civilizations)
        {
            throw new ArgumentException("Map projection requires an admitted observer", nameof(identity));
        }

        var geometry = map.Geometry;
        int observer = identity.Observer.Value;
        var memory = map.RememberedLayer(observer);
        var tiles = new ObservedTile[geometry.Area];
        for (int i = 0; i < tiles.Length; i++)
        {
            ref readonly var tile = ref map.Tiles[i];
            if (!tile.RevealedTo.Contains(observer))
            {
                continue;
            }

            var point = geometry.Coordinates(i);
            // A remembered unit bit is never current detection. City sites come from memory,
            // independent of live foreign city names, size, ownership or lifetime.
            tiles[i] = new((byte)tile.Terrain, i < memory.Length ? ObserveImprovements(memory[i]) : default,
                tile.River, map.SpecialResource(point),
                tile.Terrain == Terrain.Grassland && Geometry.GrasslandShield(point),
                map.HutCandidate(point) ? MapFact<MapPresence>.Withheld : Present(false));
        }

        var units = new List<ObservedMapUnit>();
        var pool = world.Entities.Units.Scanned;
        var own = new bool[pool.Length];
        for (int i = 0; i < pool.Length; i++)
        {
            ref readonly var unit = ref pool[i];
            var point = unit.Position;
            if (!unit.Token.Occupied || !geometry.Canonical(point))
            {
                continue;
            }

            if (unit.Owner == identity.Observer)
            {
                own[i] = true;
            }
            else if (unit.Visibility.Contains(observer)
                && tiles[geometry.TileIndex(point)!.Value].Terrain is not null)
            {
                units.Add(new(unit.Position, unit.Type, unit.Owner, null, MapUnitDetails.Withheld));
            }
        }

        UnitTarget? active = null;
        var ownPositions = new HashSet<int>();

        // Display order follows consecutive own links only, with a bounded fallback for malformed
        // links. Foreign links never contribute a rank, count, identity or traversal.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (!own[i])
                {
                    continue;
                }

                ref readonly var unit = ref pool[i];
                int previous = (unit.Previous?.Value ?? -1);
                if (pass == 0 && previous >= 0 && previous < own.Length && own[previous]
                    && (pool[previous].Next?.Value ?? -1) == i
                    &&
                    pool[previous].Position.X == unit.Position.X && pool[previous].Position.Y == unit.Position.Y)
                {
                    continue;
                }

                for (int j = i; j >= 0;)
                {
                    ref readonly var member = ref pool[j];
                    own[j] = false;
                    var target = new UnitTarget(new((ushort)j), member.Token);
                    if ((world.Globals.ActiveUnit?.Value ?? -1) == j)
                    {
                        active = target;
                    }

                    int tileIndex = geometry.TileIndex(member.Position)!.Value;
                    ownPositions.Add(tileIndex);
                    var unitRule = member.Type.Value < rules.Definitions.Units.Length
                        ? rules.Definitions.Units[member.Type.Value]
                        : null;
                    var tile = tiles[tileIndex];
                    bool airbase = (tile.Improvements & (MapFeatures.CitySite | MapFeatures.Fortification)) ==
                        (MapFeatures.CitySite | MapFeatures.Fortification);
                    bool stationary = airbase && unitRule is { Domain: RuleDomain.Air };
                    if (tile.Terrain is not null)
                    {
                        tiles[tileIndex] = tile with
                        {
                            Ownership = MapFact<MapTileOwnership>.Known(new(identity.Observer)),
                            Hut = Present(false),
                            AirbaseOccupant = stationary
                                ? MapFact<MapTileOwnership>.Known(new(identity.Observer))
                                : tile.AirbaseOccupant
                        };
                    }
                    units.Add(new(member.Position, member.Type, member.Owner, target,
                        UnitDetails(member, unitRule, stationary, !world.Globals.Rules.NormalCombat)));
                    int next = (member.Next?.Value ?? -1);
                    j = next >= 0 && next < own.Length && own[next] && (pool[next].Previous?.Value ?? -1) == j &&
                        pool[next].Position.X == member.Position.X && pool[next].Position.Y == member.Position.Y ? next : -1;
                }
            }
        }

        var cities = new List<ObservedMapCity>();
        var cityPool = world.Entities.Cities.Scanned;
        for (int i = 0; i < cityPool.Length; i++)
        {
            ref readonly var city = ref cityPool[i];
            if (!city.Token.Occupied || city.Owner != identity.Observer || !geometry.Canonical(city.Position))
            {
                continue;
            }

            int tileIndex = geometry.TileIndex(city.Position)!.Value;
            if (tiles[tileIndex].Terrain is not null)
            {
                tiles[tileIndex] = tiles[tileIndex] with
                {
                    Ownership = MapFact<MapTileOwnership>.Known(new(identity.Observer)),
                    Hut = Present(false)
                };
            }

            cities.Add(new(city.Position, new((ushort)i), city.Token,
                city.Name, city.Size,
                CityDetails(world, rules, city, ownPositions.Contains(tileIndex))));
        }
        var admittedOwners = CivilizationSet.Only(observer);
        foreach (var unit in units)
        {
            admittedOwners = admittedOwners.With(unit.Owner.Value);
        }

        var colors = new byte[EntityLimits.Civilizations];
        for (byte i = 0; i < colors.Length; i++)
        {
            int tribe = world.Civilizations[i].Tribe;
            // Uninstalled rules identify an authored state. Stock roles
            // remain explicit in that profile. A selected role outside 0..7 stays diagnostic.
            short role = admittedOwners.Contains(i) && rules.IsLoaded && tribe >= 0
                && tribe < rules.Leaders.Length
                ? rules.Leaders[tribe].Color
                : i;
            colors[i] = role is >= 0 and <= 255 ? (byte)role : byte.MaxValue;
        }
        return new(identity, revision, geometry.Width, geometry.Height, world.Globals.Rules.FlatWorld,
            new(tiles), new(units), new(cities), new(new(colors), active));
    }

    // Remembered occupancy does not authorize disclosing a current unit.
    private static MapFeatures ObserveImprovements(MapFeatures features) => features & ~MapFeatures.Unit;

    private static MapFact<MapPresence> Present(bool value) =>
        MapFact<MapPresence>.Known(value ? MapPresence.Present : MapPresence.Absent);

    // P02 §§4–5. Own tall-object details are read-only. W04 counters retain the
    // stored value and never imply a time to finish. Foreign AI values are withheld.
    private static MapUnitDetails UnitDetails(Unit unit, UnitRule? rule, bool stationary, bool simplified)
    {
        bool defined = rule is not null && rule.Identity != RuleName.Empty;
        var health = defined
            ? MapFact<MapHealth>.Known(new(unchecked((sbyte)rule!.HitPoints), unit.Damage, simplified))
            : default;
        var order = unit.Orders is UnitOrder.AiIdle or UnitOrder.AiGoTo
            ? MapFact<UnitOrder>.Withheld
            : MapFact<UnitOrder>.Known(unit.Orders);
        var work = defined && rule!.Role == RuleRole.Settle
            ? MapFact<int>.Known(unit.Counter) : default;
        // P02 calls this awaiting input; F02/U03 name the same state Automated.
        return new(health, order, Present(unit.Flags.Automated), Present(unit.Flags.Veteran),
            default, work, defined ? Present(stationary) : default);
    }

    private static MapCityDetails CityDetails(GameState world, RuleState rules, City city, bool knownGarrison)
    {
        int owner = city.Owner.Value;
        int tribe = world.Civilizations[owner].Tribe;
        int style = tribe >= 0 && tribe < rules.Leaders.Length ? rules.Leaders[tribe].CityStyle : -1;
        var appearance = style is >= 0 and <= 5
            ? MapFact<MapCityAppearance>.Known(MapCityAppearance.Resolve(style, city.Size,
                CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, 37),
                CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, 5),
                CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, 24),
                Improvement(city, 1), Improvement(city, 8) || HasGreatWall(world, rules, city.Owner)))
            : default;
        // Only own occupants are admitted here. No observed own garrison does not imply
        // the absence of other occupants.
        return new(city.Owner, CityKnowledge.Own, appearance, MapFact<int>.Known(city.Size),
            knownGarrison ? Present(true) : default, Present(city.Flags.Disorder));
    }

    private static bool Improvement(City city, int index) => city.Improvements.Contains(index);

    private static bool HasGreatWall(GameState world, RuleState rules, CivilizationSlot owner)
    {
        if (!rules.IsLoaded)
        {
            return false;
        }

        int slot = world.Globals.WonderCity[6];
        if (slot < 0 || slot >= world.Entities.Cities.End || world.Entities.Cities.Storage[slot].Owner != owner)
        {
            return false;
        }

        int expiry = (int)rules.Definitions.WonderObsolescence[6];
        for (int civ = 1; expiry >= 0 && civ < EntityLimits.Civilizations; civ++)
        {
            if (CivilizationKnowledge.KnowsAdvance(world.Civilizations, civ, expiry))
            {
                return false;
            }
        }

        return true;
    }
}
