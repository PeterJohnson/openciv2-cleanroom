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
        if (!map.Allocated) return null;
        if (identity.Runtime.Value == 0 || identity.Session.Value == 0 || identity.Observer.Value >= EntityLimits.Civilizations)
            throw new ArgumentException("Map projection requires an admitted observer", nameof(identity));

        var geometry = map.Geometry;
        int observer = identity.Observer.Value;
        int observerBit = 1 << observer;
        var memory = map.RememberedLayer(observer);
        var tiles = new ObservedTile[geometry.Area];
        for (int i = 0; i < tiles.Length; i++)
        {
            ref readonly var tile = ref map.Tiles[i];
            if ((tile.Revelation & observerBit) == 0) continue;
            var point = geometry.Coordinates(i);
            // A remembered unit bit is never current detection. City sites come from memory,
            // independent of live foreign city names, size, ownership or lifetime.
            tiles[i] = new((byte)tile.Terrain, i < memory.Length ? (MapImprovements)(memory[i] & 0xfe) : default,
                (tile.TerrainFlags & 0x80) != 0, map.SpecialResource(point),
                tile.Terrain == Terrain.Grassland && Geometry.GrasslandShield(point),
                map.HutCandidate(point) ? MapFact<MapPresence>.Withheld : Present(false));
        }

        var units = new List<ObservedMapUnit>();
        var pool = world.Entities.Units.Scanned;
        var own = new bool[pool.Length];
        for (int i = 0; i < pool.Length; i++)
        {
            ref readonly var unit = ref pool[i];
            var point = new Position(unit.X, unit.Y);
            if (!unit.Token.Occupied || !geometry.Canonical(point)) continue;
            if (unit.Owner == identity.Observer) own[i] = true;
            else if ((unit.Visibility & observerBit) != 0 && tiles[geometry.TileIndex(point)!.Value].Terrain is not null)
                units.Add(new(new(unit.X, unit.Y), unit.Type, unit.Owner, null, MapUnitDetails.Withheld));
        }

        UnitTarget? active = null;
        var ownPositions = new HashSet<int>();

        // Display order follows consecutive own links only, with a bounded fallback for malformed
        // links. Foreign links never contribute a rank, count, identity or traversal.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (!own[i]) continue;
                ref readonly var unit = ref pool[i];
                int previous = unit.Previous.Value;
                if (pass == 0 && previous >= 0 && previous < own.Length && own[previous] && pool[previous].Next.Value == i &&
                    pool[previous].X == unit.X && pool[previous].Y == unit.Y) continue;
                for (int j = i; j >= 0;)
                {
                    ref readonly var member = ref pool[j];
                    own[j] = false;
                    var target = new UnitTarget(new((ushort)j), member.Token);
                    if (world.Globals.ActiveUnit.Value == j) active = target;
                    int tileIndex = geometry.TileIndex(new(member.X, member.Y))!.Value;
                    ownPositions.Add(tileIndex);
                    var unitRule = member.Type.Value < rules.Definitions.Units.Length ? rules.Definitions.Units[member.Type.Value] : null;
                    var tile = tiles[tileIndex];
                    bool airbase = (tile.Improvements & (MapImprovements.CitySite | MapImprovements.Fortification)) ==
                        (MapImprovements.CitySite | MapImprovements.Fortification);
                    bool stationary = airbase && unitRule is { Domain: RuleDomain.Air };
                    if (tile.Terrain is not null)
                    {
                        tiles[tileIndex] = tile with
                        {
                            Ownership = MapFact<MapTileOwnership>.Known(new(identity.Observer)),
                            Hut = Present(false),
                            AirbaseOccupant = stationary ? MapFact<MapTileOwnership>.Known(new(identity.Observer)) : tile.AirbaseOccupant
                        };
                    }
                    units.Add(new(new(member.X, member.Y), member.Type, member.Owner, target,
                        UnitDetails(member, unitRule, stationary, (world.Globals.Rules & 0x0010) == 0)));
                    int next = member.Next.Value;
                    j = next >= 0 && next < own.Length && own[next] && pool[next].Previous.Value == j &&
                        pool[next].X == member.X && pool[next].Y == member.Y ? next : -1;
                }
            }
        }

        var cities = new List<ObservedMapCity>();
        var cityPool = world.Entities.Cities.Scanned;
        for (int i = 0; i < cityPool.Length; i++)
        {
            ref readonly var city = ref cityPool[i];
            if (!city.Token.Occupied || city.Owner != identity.Observer || !geometry.Canonical(new(city.X, city.Y))) continue;
            int tileIndex = geometry.TileIndex(new(city.X, city.Y))!.Value;
            if (tiles[tileIndex].Terrain is not null)
                tiles[tileIndex] = tiles[tileIndex] with { Ownership = MapFact<MapTileOwnership>.Known(new(identity.Observer)), Hut = Present(false) };
            cities.Add(new(new(city.X, city.Y), new((ushort)i), city.Token,
                new(city.Name.TakeWhile(b => b != 0)), city.Size,
                CityDetails(world, rules, city, ownPositions.Contains(tileIndex))));
        }
        int admittedOwners = observerBit;
        foreach (var unit in units) admittedOwners |= 1 << unit.Owner.Value;
        var colors = new byte[EntityLimits.Civilizations];
        for (byte i = 0; i < colors.Length; i++)
        {
            int tribe = world.Civilizations[i].Tribe;
            // Empty rule text identifies an authored/uninstalled rules state. Stock roles
            // remain explicit in that profile. A selected role outside 0..7 stays diagnostic.
            short role = (admittedOwners & (1 << i)) != 0 && rules.Text.Count != 0 && tribe >= 0 && tribe < rules.Leaders.Length ? rules.Leaders[tribe].Color : i;
            colors[i] = role is >= 0 and <= 255 ? (byte)role : byte.MaxValue;
        }
        return new(identity, revision, geometry.Width, geometry.Height, (world.Globals.Rules & 0x8000) != 0,
            new(tiles), new(units), new(cities), new(new(colors), active));
    }

    private static MapFact<MapPresence> Present(bool value) => MapFact<MapPresence>.Known(value ? MapPresence.Present : MapPresence.Absent);

    // P02 §§4–5. Own tall-object details are read-only. W04 counters retain the
    // stored value and never imply a time to finish. Foreign AI values are withheld.
    private static MapUnitDetails UnitDetails(Unit unit, UnitRule? rule, bool stationary, bool simplified)
    {
        bool defined = rule is not null && rule.Identity != RuleName.Empty;
        var health = defined ? MapFact<MapHealth>.Known(new(unchecked((sbyte)rule!.HitPoints), unit.Damage, simplified)) : default;
        var order = unit.Orders is UnitOrder.AiIdle or UnitOrder.AiGoTo ? MapFact<UnitOrder>.Withheld : MapFact<UnitOrder>.Known(unit.Orders);
        var work = defined && rule!.Role == RuleRole.Settle
            ? MapFact<int>.Known(unit.Counter) : default;
        return new(health, order, Present((unit.Flags & 0x8000) != 0), Present((unit.Flags & 0x2000) != 0),
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
            knownGarrison ? Present(true) : default, Present((city.Flags & 1) != 0));
    }

    private static bool Improvement(City city, int index) => (city.Improvements[index / 8] & (1 << (index % 8))) != 0;

    private static bool HasGreatWall(GameState world, RuleState rules, CivilizationSlot owner)
    {
        if (rules.Text.Count == 0) return false;
        int slot = world.Globals.WonderCity[6];
        if (slot < 0 || slot >= world.Entities.Cities.End || world.Entities.Cities.Storage[slot].Owner != owner) return false;
        int expiry = (int)rules.Definitions.WonderObsolescence[6];
        for (int civ = 1; expiry >= 0 && civ < EntityLimits.Civilizations; civ++)
            if (CivilizationKnowledge.KnowsAdvance(world.Civilizations, civ, expiry)) return false;
        return true;
    }
}
