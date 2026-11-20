using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.MapRendering;

namespace OpenCiv2.App.Map;

internal sealed record GalleryArtExpectation(MapPoint Point, string Key);
internal sealed record GalleryBoard(string Id, string Title, MapObservation Map,
    OwnedList<GalleryArtExpectation> TerrainArt, MapDisplay Display = default);

/// <summary>Explicit, deterministic presentation inputs. No gameplay rules are executed.
/// Used by both the native showcase and the renderer probe.</summary>
internal static class MapGallery
{
    private static readonly MapPoint[] Directions = [new(1, -1), new(2, 0), new(1, 1), new(0, 2),
        new(-1, 1), new(-2, 0), new(-1, -1), new(0, -2)];

    public static IReadOnlyList<string> Cases { get; } =
        ["MV01", "MV02", "MV03", "MV04", "MV05", "MV06", "MV07", "MV08", "MV09", "MV10", "MV11", "MV12",
         "MV13", "MV14", "MV15", "MV16", "MV17", "MV18", "MV19", "MV20", "MV21", "MV22", "MV23", "MV24", "MV25",
         "MV26", "MV27", "MV28", "MV29", "MV30", "Mixed"];

    public static GalleryBoard Open(string id) => id switch
    {
        "MV01" => Terrain(),
        "MV02" => Coasts(),
        "MV03" => Relief(),
        "MV04" => Rivers(),
        "MV05" => Resources(),
        "MV06" => Agriculture(),
        "MV07" => Networks(),
        "MV08" => Improvements(),
        "MV09" => Knowledge(),
        "MV10" => Units("MV10", "Unit bodies and leader", 64),
        "MV11" => Units("MV11", "Faction shields and stacks", 16, stacks: true),
        "MV12" => Units("MV12", "Health and withheld inputs", 12),
        "MV13" => Units("MV13", "Orders, waiting, automation and veteran", 18),
        "MV14" => Units("MV14", "Sleep, sentry and fortified units", 16, sleeping: true),
        "MV15" => Cities("MV15", "All city appearances", 48),
        "MV16" => Cities("MV16", "City names, flags, garrisons and disorder", 16),
        "MV17" => OwnerColors(),
        "MV18" => Units("MV18", "Selection and active focus", 12),
        "MV19" => Mixed("MV19", "Grid, radius, ownership and bare terrain", false),
        "MV20" => Mixed("MV20", "Authored route and trade lines", false),
        "MV21" => WorkerInspection(),
        "MV22" => Mixed("MV22", "Authored viewport notifications", false),
        "MV23" => Mixed("MV23", "Overview and wrapped footprint", false),
        "MV24" => Mixed("MV24", "Authored movement and combat effects", false),
        "MV25" => Mixed("MV25", "Camera, presets and picks", false),
        "MV26" => Mixed("MV26", "Detail bands and density", false),
        "MV27" => Cities("MV27", "Crowded names and long labels", 16, crowds: true),
        "MV28" => Cities("MV28", "Overlapping tall alpha layers", 16, crowds: true),
        "MV29" => Mixed("MV29", "Dirty updates and replacement", false),
        "MV30" => Mixed("MV30", "Display correction and texture recovery", false),
        "Mixed" => Mixed("Mixed", "Large mixed authored world", true),
        _ => throw new ArgumentException($"Unknown map gallery case '{id}'")
    };

    private static GalleryBoard Terrain()
    {
        var board = new Builder("MV01", "Eleven terrain types", 11);
        for (byte terrain = 0; terrain < 11; terrain++)
            board.Art(terrain, Tile(terrain), $"terrain/{terrain}");
        return board.Finish();
    }

    private static GalleryBoard Coasts()
    {
        var board = new Builder("MV02", "Shoreline contact combinations", 32, 10);
        for (int q = 0; q < 4; q++)
        {
            for (int mask = 0; mask < 8; mask++)
            {
                int spot = q * 8 + mask;
                board.Art(spot, Tile(10), $"coast/{q}/{mask}");
                for (int d = 0; d < 8; d++) board.Neighbor(spot, d, Tile(10));
                for (int bit = 0; bit < 3; bit++)
                    if ((mask & (1 << bit)) != 0) board.Neighbor(spot, (2 * q + 6 + bit) & 7, Tile(2));
            }
        }
        return board.Finish();
    }

    private static GalleryBoard Relief()
    {
        var board = new Builder("MV03", "Relief and terrain transitions", 92);
        string[] kind = ["forest", "hills", "mountains"];
        for (int t = 0; t < kind.Length; t++)
        {
            for (int mask = 0; mask < 16; mask++)
            {
                int spot = t * 16 + mask;
                byte terrain = (byte)(t + 3);
                board.Art(spot, Tile(terrain), $"feature/{kind[t]}/{mask}");
                for (int q = 0; q < 4; q++)
                    if ((mask & (1 << q)) != 0) board.Neighbor(spot, q * 2, Tile(terrain));
            }
        }
        for (int t = 0; t <= 10; t++)
        {
            for (int q = 0; q < 4; q++)
            {
                int spot = 48 + t * 4 + q;
                board.Put(board.At(spot), Tile((byte)((t + 1) % 10)));
                board.Neighbor(spot, q * 2, Tile((byte)t));
                board.Expect(spot, $"dither/{(t == 10 ? 12 : t)}/{q}");
            }
        }
        return board.Finish();
    }

    private static GalleryBoard Rivers()
    {
        var board = new Builder("MV04", "River masks, mouths and crossings", 20);
        for (int mask = 0; mask < 16; mask++)
        {
            var features = mask % 3 == 0 ? MapImprovements.Railroad : mask % 3 == 1 ? MapImprovements.Road : default;
            board.Art(mask, Tile(2, features, river: true), $"river/{mask}");
            for (int q = 0; q < 4; q++)
                if ((mask & (1 << q)) != 0)
                    board.Neighbor(mask, q * 2, Tile(2, features, river: true));
        }
        for (int q = 0; q < 4; q++)
        {
            board.Art(16 + q, Tile(10), $"mouth/{q}");
            board.Neighbor(16 + q, 2 * q, Tile(2, river: true));
        }
        return board.Finish();
    }

    private static GalleryBoard Resources()
    {
        var board = new Builder("MV05", "Both resources per terrain and the grassland shield", 23);
        for (int t = 0; t < 11; t++)
            for (byte s = 1; s <= 2; s++)
                board.Art(t * 2 + s - 1, Tile((byte)t, MapImprovements.Road, river: true) with { Resource = s }, $"resource/{t * 2 + s - 1}");
        board.Art(22, Tile(2) with { GrasslandShield = true }, "resource/shield");
        return board.Finish();
    }

    private static GalleryBoard Agriculture()
    {
        var board = new Builder("MV06", "Irrigation, farmland and mines", 12);
        foreach (var (index, flags, key) in new[]
        {
            (0, MapImprovements.Irrigation, "irrigation"),
            (1, MapImprovements.Irrigation | MapImprovements.Mine, "farmland"),
            (2, MapImprovements.Mine, "mine")
        })
        {
            board.Art(index, Tile(2, flags), "improvement/" + key);
            board.Art(index + 4, Tile(2, flags, true) with { GrasslandShield = true }, "improvement/" + key);
            board.Art(index + 8, Tile(2, flags | MapImprovements.CitySite), "improvement/" + key);
            board.City(index + 8, 1, 0, 2, false, 5);
        }
        return board.Finish();
    }

    private static GalleryBoard Networks()
    {
        var board = new Builder("MV07", "Road, rail, mixed and city endpoints", 44);
        board.Art(0, Tile(2, MapImprovements.Road), "road/0");
        board.Art(1, Tile(2, MapImprovements.Railroad), "rail/0");
        for (int d = 0; d < 8; d++)
        {
            int index = 2 + d;
            board.Art(index, Tile(2, MapImprovements.Road), $"road/{d + 1}");
            board.Neighbor(index, d, Tile(2, MapImprovements.Road));
            board.Art(index + 8, Tile(2, MapImprovements.Railroad), $"rail/{d + 1}");
            board.Neighbor(index + 8, d, Tile(2, MapImprovements.Railroad));
            board.Art(index + 16, Tile(2, MapImprovements.Road), $"road/{d + 1}");
            board.Neighbor(index + 16, d, Tile(2, MapImprovements.Railroad));
            board.Art(index + 24, Tile(2, MapImprovements.Railroad), $"rail/{d + 1}");
            board.Neighbor(index + 24, d, Tile(2, MapImprovements.CitySite));
            board.Art(index + 32, Tile(2, MapImprovements.Railroad), "rail/0");
            board.Neighbor(index + 32, d, Tile(2, MapImprovements.CitySite | MapImprovements.Fortification));
        }
        return board.Finish();
    }

    private static GalleryBoard Improvements()
    {
        var board = new Builder("MV08", "Villages, pollution, fortresses and airbases", 12);
        board.Art(0, Tile(2) with { Hut = Present(true) }, "improvement/village");
        board.Art(1, Tile(2, MapImprovements.Pollution), "improvement/pollution");
        board.Put(board.At(2), Tile(2, MapImprovements.Fortification));
        board.Unit(2, 3, 8, Status(2) with { Order = MapFact<UnitOrder>.Known(UnitOrder.Fortified) });
        board.Put(board.At(3), Tile(2, MapImprovements.CitySite | MapImprovements.Fortification) with
        { AirbaseOccupant = MapFact<MapTileOwnership>.Known(new(null)) });
        for (byte owner = 0; owner < 8; owner++)
            board.Put(board.At(4 + owner), Tile(2, MapImprovements.CitySite | MapImprovements.Fortification) with
            { AirbaseOccupant = MapFact<MapTileOwnership>.Known(new(new(owner))) });
        return board.Finish();
    }

    private static GalleryBoard Knowledge()
    {
        var board = new Builder("MV09", "Unknown edges and intersections", 17);
        board.Art(16, default, "fog/full");
        for (int mask = 0; mask < 16; mask++)
        {
            for (int q = 0; q < 4; q++)
            {
                if ((mask & (1 << q)) == 0) continue;
                board.Neighbor(mask, q * 2, default);
                board.Expect(mask, $"fog/{q}");
            }
        }
        return board.Finish();
    }

    public static MapObservation ChangeKnowledgeRegion(MapObservation map)
    {
        var annotations = map.Annotations ?? MapAnnotations.Stock;
        var region = annotations.Region is null
            ? new MapRegion(new([new(4, 4), new(3, 3), new(5, 3), new(3, 5), new(5, 5)]))
            : null;
        return map with { Revision = map.Revision + 1, Annotations = annotations with { Region = region } };
    }

    private static GalleryBoard OwnerColors()
    {
        var board = new Builder("MV17", "Civilization colors and overlays", 40);
        for (byte owner = 0; owner < 8; owner++)
        {
            var slot = new CivilizationSlot(owner);
            var details = owner == 1 ? Status(0) with { Order = MapFact<UnitOrder>.Withheld } : MapUnitDetails.Withheld;
            board.Unit(owner, owner, 1, details);
            board.Unit(owner, owner, 1, details);
            board.City(8 + owner, owner, 0, 0, false, 3, $"North {owner}");
            board.City(16 + owner, owner, 0, 0, false, 3, $"South {owner}");
            board.Put(board.At(24 + owner), Tile(1, MapImprovements.CitySite | MapImprovements.Fortification) with
            { AirbaseOccupant = MapFact<MapTileOwnership>.Known(new(slot)) });
            board.Put(board.At(32 + owner), Tile(1) with
            { Ownership = MapFact<MapTileOwnership>.Known(new(slot)) });
        }
        var gallery = board.Finish(new(Grid: true, CityRadius: true, Ownership: true, ReducedMotion: true));
        return gallery with
        {
            Map = gallery.Map with
            {
                Cities = new(gallery.Map.Cities.Select((city, index) => city with
                {
                    Details = city.Details! with { Garrison = Present(index % 2 == 0), Disorder = Present(true) }
                }))
            }
        };
    }

    public static MapObservation ChangeOwnerColors(MapObservation map)
    {
        var annotations = map.Annotations ?? MapAnnotations.Stock;
        var roles = annotations.FactionRoles.ToArray();
        int owner = map.Identity.Observer.Value;
        roles[owner] = (byte)((roles[owner] + 1) % roles.Length);
        return map with { Revision = map.Revision + 1, Annotations = annotations with { FactionRoles = new(roles) } };
    }

    public static MapObservation ChangeColorSupport(MapObservation map)
    {
        var annotations = map.Annotations ?? MapAnnotations.Stock;
        var roles = annotations.FactionRoles.ToArray();
        byte slot = map.Identity.Observer.Value;
        roles[slot] = roles[slot] == slot ? (byte)17 : slot;
        return map with { Revision = map.Revision + 1, Annotations = annotations with { FactionRoles = new(roles) } };
    }

    private static GalleryBoard Units(string id, string name, int count, bool stacks = false, bool sleeping = false)
    {
        var board = new Builder(id, name, count);
        for (int i = 0; i < count; i++)
        {
            byte owner = (byte)(i % 8);
            int type = i % 63;
            if (i == 63) { owner = 0; type = 46; }
            var state = Status(i);
            if (sleeping)
            {
                board.Put(board.At(i), Tile(2, MapImprovements.Fortification));
                state = state with
                {
                    Order = MapFact<UnitOrder>.Known(i % 2 == 0 ? UnitOrder.Sleeping : UnitOrder.Fortified),
                    StationaryAircraft = Present(i % 3 == 0)
                };
            }
            board.Unit(i, owner, type, state);
            if (stacks) board.Unit(i, owner, (type + 15) % 63, Status(i + 1));
        }
        return board.Finish(sleeping ? new(Sentry: SentryAppearance.Greyscale) : default);
    }

    private static GalleryBoard WorkerInspection()
    {
        var board = new Builder("MV21", "Worker counts and inspection", 8, spacing: 2);
        var worker = Status(5);
        board.Unit(0, 1, 0, worker with { Work = MapFact<int>.Known(0) });
        board.Unit(1, 1, 1, worker with { Work = MapFact<int>.Known(9) });
        board.Unit(2, 1, 1, worker with { Work = MapFact<int>.Known(231) });
        board.Unit(3, 1, 2, worker with { Work = default });
        board.Unit(4, 2, 0, MapUnitDetails.Withheld);
        board.City(5, 1, 0, 1, false, 4, "Workshop");
        board.City(6, 2, 2, 1, false, 5, "Old Harbor");
        board.Put(board.At(7), Tile(2, MapImprovements.CitySite | MapImprovements.Road));
        return board.Finish();
    }

    private static GalleryBoard Cities(string id, string name, int count, bool crowds = false)
    {
        var board = new Builder(id, name, count, spacing: crowds ? 2 : 6);
        for (int i = 0; i < count; i++)
        {
            int appearance = i % 48;
            board.City(i, (byte)(i % 8), appearance / 8, appearance % 8 / 2, (appearance & 1) != 0,
                i % 4 == 0 ? 0 : 2 + i % 23, crowds ? "North Bank · Château du Vent and the Eastern Port" : null);
            if (crowds)
                board.Unit(i, (byte)(i % 8), (i + 32) % 63, Status(i));
        }
        return board.Finish();
    }

    private static GalleryBoard Mixed(string id, string title, bool large)
    {
        int w = large ? 252 : 56, h = large ? 252 : 52, area = w / 2 * h;
        var tiles = new ObservedTile[area];
        for (int i = 0; i < area; i++)
        {
            int y = i / (w / 2), x = 2 * (i % (w / 2)) + (y & 1);
            if (x > w * 3 / 4 && (y < h / 4 || y > h * 3 / 4)) continue;
            byte terrain = (byte)((x / 11 + y / 9) % 11);
            MapImprovements improvements = (x % 16 < 2 ? MapImprovements.Railroad : default) |
                (y % 11 < 2 ? MapImprovements.Road : default);
            if (i % 73 == 0) improvements |= MapImprovements.Irrigation | MapImprovements.Mine;
            if (i % 89 == 0) improvements |= MapImprovements.Fortification;
            if (i % 137 == 0) improvements |= MapImprovements.Pollution;
            tiles[i] = Tile(terrain, improvements, river: x - y / 2 is >= 6 and <= 8) with
            {
                Resource = (byte)(i % 29 == 0 ? 1 : i % 31 == 0 ? 2 : 0),
                GrasslandShield = terrain == 2 && i % 8 < 3,
                Hut = Present(i % 149 == 1),
                Ownership = MapFact<MapTileOwnership>.Known(new(new((byte)(x * 8 / w))))
            };
        }
        var units = new List<ObservedMapUnit>();
        var cities = new List<ObservedMapCity>();
        var identity = Identity(id);
        int unitCount = large ? 2048 : 85, cityCount = large ? 256 : 18;
        for (int i = 0; i < unitCount; i++)
        {
            int index = (64 + i * 101) % area;
            byte owner = (byte)(i % 8);
            bool own = owner == identity.Observer.Value;
            if (!own && tiles[index].Terrain is null) continue;
            units.Add(new(Coordinate(index, w), new((byte)(i % 63)), new(owner),
                own ? new UnitTarget(new((ushort)i), new((uint)i + 1)) : null,
                own ? Status(i) : MapUnitDetails.Withheld));
        }
        for (int i = 0; i < cityCount; i++)
        {
            int index = (96 + i * 173) % area;
            byte owner = (byte)(i % 8);
            if (tiles[index].Terrain is null) continue;
            var p = Coordinate(index, w);
            tiles[index] = tiles[index] with { Improvements = tiles[index].Improvements | MapImprovements.CitySite };
            cities.Add(City(p, i, owner, i % 6, i % 4, i % 3 == 0, i % 23, $"Harbor {i + 1}"));
        }
        UnitTarget? active = units.FirstOrDefault(u => u.Target is not null).Target;
        if (id is "MV22" or "MV24") active = units.First(u => u.Target?.Slot.Value == 33).Target;
        OwnedList<MapPath>? routes = null;
        if (id is "MV20" or "Mixed")
        {
            // The small and large fixtures start at known tiles visible on their authored route.
            var actor = units.First(u => u.Target?.Slot.Value == (large ? 1 : 33));
            active = actor.Target;
            var start = actor.Position;
            routes = new(
            [
                new(MapPathKind.ActiveRoute, new([start, new(start.X - 1, start.Y + 1), new(start.X - 2, start.Y + 2)]), active),
                new(MapPathKind.TradePartner, new([new MapPoint(4, 6), new(14, 6)])),
                new(MapPathKind.TradeBonusSatisfied, new([new MapPoint(4, 8), new(12, 8)])),
                new(MapPathKind.TradeBonusUnsatisfied, new([new MapPoint(4, 10), new(16, 10)])),
                new(MapPathKind.TradeBonusUnsatisfied, new([new MapPoint(w - 2, h / 2 + (h / 2 & 1)), new(0, h / 2 + (h / 2 & 1))]))
            ]);
        }
        var display = id switch
        {
            "MV19" => new MapDisplay(Grid: true, CityRadius: true, Ownership: true),
            "MV30" => new MapDisplay(Color: MapColorCorrection.HighContrast, ReducedMotion: true),
            _ => default
        };
        var annotations = new MapAnnotations(MapAnnotations.Stock.FactionRoles, active, routes);
        return new(id, title, new(identity, 0, w, h, false, new(tiles), new(units), new(cities), annotations),
            OwnedList<GalleryArtExpectation>.Empty, display);
    }

    public static MapObservation Changed(MapObservation map, MapPoint? focus = null)
    {
        var tiles = map.Tiles.ToArray();
        var units = map.Units.ToArray();
        var cities = map.Cities.ToArray();
        int columnCount = map.Width / 2;
        var point = focus ?? new MapPoint(4, 2);
        int at = point.Y * columnCount + point.X / 2;
        int alternate = point.X + 2 < map.Width ? at + 1 : map.Flat ? at - 1 : point.Y * columnCount;
        var changed = tiles[at];
        byte terrain = (byte)(((changed.Terrain ?? 1) + 1) % 11);
        tiles[at] = changed with { Terrain = terrain, Improvements = changed.Improvements ^ MapImprovements.Road };
        tiles[alternate] = tiles[alternate] with { Improvements = tiles[alternate].Improvements ^ MapImprovements.Railroad };
        if (units.Length != 0)
        {
            int index = focus is null ? 0 : Enumerable.Range(0, units.Length)
                .Where(i => units[i].Target is not null).DefaultIfEmpty(0).MinBy(i => Distance(units[i].Position));
            var current = units[index];
            var pos = new MapPoint(current.Position.X ^ 2, current.Position.Y);
            units[index] = current with
            {
                Position = pos.X < map.Width ? pos : current.Position,
                Details = current.Details with { Health = MapFact<MapHealth>.Known(new(30, (int)(map.Revision % 31))), Order = MapFact<UnitOrder>.Known(UnitOrder.GoTo) }
            };
        }
        if (cities.Length != 0)
        {
            int index = focus is null ? 0 : Enumerable.Range(0, cities.Length)
                .Where(i => cities[i].Details?.Knowledge == CityKnowledge.Own).DefaultIfEmpty(0).MinBy(i => Distance(cities[i].Position));
            if (cities[index].Details is { } info && info.Appearance.Value is { } appearance)
            {
                int size = 3 + (int)(map.Revision % 17);
                cities[index] = cities[index] with
                {
                    Name = new(System.Text.Encoding.ASCII.GetBytes($"Harbor revision {map.Revision + 1}")),
                    Details = info with
                    {
                        Appearance = MapFact<MapCityAppearance>.Known(appearance with { Walled = !appearance.Walled }),
                        Population = MapFact<int>.Known(size)
                    }
                };
            }
        }
        var annotations = map.Annotations ?? MapAnnotations.Stock;
        var roles = annotations.FactionRoles.ToArray();
        roles[1] = (byte)((roles[1] + 1) % 8);
        return map with
        {
            Revision = map.Revision + 1,
            Tiles = new(tiles),
            Units = new(units),
            Cities = new(cities),
            Annotations = annotations with { FactionRoles = new(roles) }
        };

        int Distance(MapPoint other)
        {
            int dx = Math.Abs(other.X - point.X);
            if (!map.Flat) dx = Math.Min(dx, map.Width - dx);
            return 2 * dx + Math.Abs(other.Y - point.Y);
        }
    }

    public static MapEffect[] Effects(MapObservation map)
    {
        var eligible = map.Units.Where(u => u.Target is not null && u.Position.X >= 2 && u.Position.X < map.Width - 3 &&
            u.Position.Y < map.Height * 3 / 4 && map.Tiles[u.Position.Y * (map.Width / 2) + u.Position.X / 2].Terrain is not null);
        var unit = eligible.FirstOrDefault(u => u.Target == map.Annotations?.ActiveUnit);
        if (unit.Target is null) unit = eligible.FirstOrDefault();
        var from = unit.Position;
        MapEffect[] stationary =
        [
            new(4, MapEffectKind.CombatParticipant, new(8, 4), new(8, 4), 1500),
            new(5, MapEffectKind.Explosion, new(8, 4), new(8, 4), 1600),
            new(6, MapEffectKind.Nuclear, new(10, 6), new(10, 6), 2200)
        ];
        return unit.Target is not null
            ? [new(3, MapEffectKind.Move, from, new(from.X - 2, from.Y), 2600, unit), .. stationary]
            : stationary;
    }

    private static ClientIdentity Identity(string id) => new(new(7242), new((ulong)(100 + Array.IndexOf(Cases.ToArray(), id))),
        new(1), new(1), new(1), ControllerKind.Frontend);
    private static ObservedTile Tile(byte terrain, MapImprovements improvements = default, bool river = false) =>
        new(terrain, improvements, river, 0, false);
    private static MapPoint Coordinate(int index, int width)
    {
        int row = index / (width / 2);
        return new(2 * (index % (width / 2)) + (row & 1), row);
    }

    private static MapFact<MapPresence> Present(bool available) => MapFact<MapPresence>.Known(available ? MapPresence.Present : MapPresence.Absent);
    private static MapUnitDetails Status(int index)
    {
        UnitOrder[] orders = [UnitOrder.None, UnitOrder.Fortifying, UnitOrder.Fortified, UnitOrder.Sleeping, UnitOrder.Fortress,
            UnitOrder.Road, UnitOrder.Irrigate, UnitOrder.Mine, UnitOrder.Transform, UnitOrder.Clean, UnitOrder.Airbase, UnitOrder.GoTo];
        var health = (index % 12) switch
        {
            0 => new MapHealth(0, 0),
            1 => new(30, 9),
            2 => new(30, 10),
            3 => new(30, 19),
            4 => new(30, 20),
            5 => new(30, 30),
            6 => new(30, 90),
            7 => new(-8, 5),
            _ => new MapHealth(30, 0)
        };
        var mightKnow = index % 12 == 8 ? default : index % 12 == 9 ? MapFact<MapHealth>.Withheld : MapFact<MapHealth>.Known(health);
        return new(mightKnow, MapFact<UnitOrder>.Known(orders[index % orders.Length]), Present(index % 13 == 12),
            Present(index % 3 == 0), Present(index % 13 == 11), index % 4 == 0 ? default :
            MapFact<int>.Known(index % 4 == 1 ? 0 : index % 4 == 2 ? 9 : 231));
    }

    private static ObservedMapCity City(MapPoint point, int index, byte owner, int style, int sizeClass, bool walls, int size, string name)
    {
        var knowledge = owner == 1 ? CityKnowledge.Own : index % 5 == 0 ? CityKnowledge.AuthorizedReveal : CityKnowledge.Remembered;
        return new(point, new((ushort)index), new((uint)index + 1),
            new(OpenCiv2.Formats.ClassicText.EncodeWindows1252(name, "authored gallery label")), (sbyte)size,
            new(new(owner), knowledge, MapFact<MapCityAppearance>.Known(new(style, sizeClass, walls)), MapFact<int>.Known(size),
                index % 4 == 3 ? MapFact<MapPresence>.Withheld : Present(index % 2 == 0), Present(index % 3 == 0)));
    }

    private sealed class Builder
    {
        private readonly string id, title;
        private readonly int width, height, spacing;
        private const int Columns = 8;
        private readonly ObservedTile[] tiles;
        private readonly List<ObservedMapUnit> units = [];
        private readonly List<ObservedMapCity> cities = [];
        private readonly List<GalleryArtExpectation> expected = [];

        public Builder(string id, string title, int count, byte terrain = 1, int spacing = 6)
        {
            (this.id, this.title, this.spacing) = (id, title, spacing);
            width = Columns * spacing + 8;
            height = (count + Columns - 1) / Columns * spacing + 8;
            tiles = Enumerable.Repeat(Tile(terrain), width / 2 * height).ToArray();
        }
        public MapPoint At(int i) => new(4 + i % Columns * spacing, 4 + i / Columns * spacing);
        public void Put(MapPoint p, ObservedTile tile) => tiles[p.Y * (width / 2) + p.X / 2] = tile;
        public void Art(int i, ObservedTile tile, string key) { Put(At(i), tile); Expect(i, key); }
        public void Expect(int i, string key) => expected.Add(new(At(i), key));
        public void Neighbor(int i, int d, ObservedTile tile)
        {
            var center = At(i);
            Put(new(center.X + Directions[d].X, center.Y + Directions[d].Y), tile);
        }
        public void Unit(int i, byte owner, int type, MapUnitDetails details)
        {
            var target = new UnitTarget(new((ushort)units.Count), new((uint)(units.Count + 1)));
            units.Add(new(At(i), new((byte)type), new(owner), owner == 1 ? target : null, details));
        }
        public void City(int i, byte owner, int style, int sizeClass, bool walls, int size, string? name = null) =>
            cities.Add(MapGallery.City(At(i), i, owner, style, sizeClass, walls, size, name ?? $"Harbor {i + 1}"));

        public GalleryBoard Finish(MapDisplay display = default) => new(id, title,
            new(Identity(id), 0, width, height, true, new(tiles), new(units), new(cities),
                new(MapAnnotations.Stock.FactionRoles, units.FirstOrDefault(u => u.Target is not null).Target)),
            new(expected), display);
    }
}
