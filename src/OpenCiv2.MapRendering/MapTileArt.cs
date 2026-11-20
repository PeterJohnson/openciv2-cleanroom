using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;

namespace OpenCiv2.MapRendering;

internal enum TileArtLayer
{
    Ground, Coast, Blend, Relief, Agriculture, River, RouteNE, RouteE, RouteSE, RouteS,
    RouteSW, RouteW, RouteNW, RouteN, Resource, Mine, Pollution, Village, Fog
}

internal readonly record struct TileSprite(TileArtLayer Layer, string Key, ArtPoint Offset = default);
internal enum MapDetail { Full, Reduced, Overview }

/// <summary>MV01–MV09 selectors. The only neighbors are the current binding's owned observations.</summary>
internal static class MapTileArt
{
    private static readonly MapArtCatalog Catalog = MapArtCatalog.Stock;
    private const MapImprovements Transport = MapImprovements.Road | MapImprovements.Railroad;
    private const MapImprovements Buildings = MapImprovements.CitySite | MapImprovements.Fortification;

    public static MapDetail Detail(double displayedZoom) => displayedZoom >= 0.55 ? MapDetail.Full :
        displayedZoom >= 0.35 ? MapDetail.Reduced : MapDetail.Overview;

    public static bool HasCity(MapScene scene, MapPoint point, ObservedTile tile) =>
        (tile.Improvements & Buildings) != Buildings &&
        ((tile.Improvements & Buildings) == MapImprovements.CitySite || scene.HasCity(point));

    public static bool Airbase(ObservedTile tile) => (tile.Improvements & Buildings) == Buildings;

    public static void Select(List<TileSprite> output, MapScene scene, MapPoint point, MapDetail detail, bool bare)
    {
        output.Clear();
        var tile = scene.Tile(point);
        if (tile.Terrain is not byte terrain || terrain >= Catalog.Terrain.Count || !scene.InRegion(point))
        {
            output.Add(new(TileArtLayer.Ground, Catalog.Fog));
            return;
        }

        var terrainArt = Catalog.Terrain[terrain];
        output.Add(new(TileArtLayer.Ground, terrainArt.Body));
        const int diagonals = 0b01010101;
        Span<MapImprovements> improvements = stackalloc MapImprovements[8];
        improvements.Clear();
        int land = 0, same = 0, rivers = 0, mouths = 0, fog = 0, neighborCities = 0;
        bool detailed = detail != MapDetail.Overview;
        bool city = !bare && detailed && HasCity(scene, point, tile);
        bool showRoute = !bare && detailed && ((tile.Improvements & Transport) != 0 || city);
        int step = terrain == 10 || showRoute ? 1 : 2;
        for (int d = 0; d < improvements.Length; d += step)
        {
            if (scene.Neighbor(point, d) is not { } neighbor)
            {
                continue;
            }

            int direction = 1 << d;
            fog |= direction & diagonals;
            if (!scene.InRegion(neighbor))
            {
                continue;
            }

            var observed = scene.Tile(neighbor);
            if (observed.Terrain is not byte adjacent || adjacent >= Catalog.Terrain.Count)
            {
                continue;
            }

            improvements[d] = observed.Improvements;
            fog &= ~direction;
            if (adjacent != 10)
            {
                land |= direction;
            }

            if (showRoute && HasCity(scene, neighbor, observed))
            {
                neighborCities |= direction;
            }

            if ((d & 1) != 0)
            {
                continue;
            }

            int q = d / 2;
            if (adjacent == terrain)
            {
                same |= 1 << q;
            }

            if (observed.River || adjacent == 10)
            {
                rivers |= 1 << q;
            }

            if (observed.River)
            {
                mouths |= 1 << q;
            }

            if (terrain != 10 && adjacent != terrain && detailed)
            {
                output.Add(new(TileArtLayer.Blend, Catalog.Terrain[adjacent].Edge[q], Catalog.EdgeOffsets[q]));
            }
        }

        if (terrain == 10)
        {
            // W01 B order starts at NE. Coast contacts start one direction pair
            // before the leading diagonal and wrap from the left quadrant.
            for (int q = 0; q < 4; q++)
            {
                int start = (q * 2 + 6) & 7;
                int mask = ((land | land << 8) >> start) & 7;
                var coast = Catalog.Coasts[q];
                output.Add(new(TileArtLayer.Coast, coast.Variants[mask], coast.Offset));
                if ((mouths & (1 << q)) != 0)
                {
                    output.Add(new(TileArtLayer.River, Catalog.Mouths[q]));
                }
            }
        }
        else if (detailed && Catalog.Relief.TryGetValue(terrain, out var relief))
        {
            output.Add(new(TileArtLayer.Relief, relief[same]));
        }

        if (tile.River && terrain != 10)
        {
            output.Add(new(TileArtLayer.River, Catalog.Rivers[rivers]));
        }

        if (showRoute)
        {
            AddRoutes(output, tile.Improvements, city, improvements, neighborCities);
        }

        if (!bare && detail == MapDetail.Full)
        {
            const MapImprovements work = MapImprovements.Irrigation | MapImprovements.Mine;
            var agriculture = (tile.Improvements & work) switch
            {
                work => "farmland",
                MapImprovements.Irrigation => "irrigation",
                MapImprovements.Mine => "mine",
                _ => null
            };
            if (agriculture is null && city)
            {
                var southeast = improvements[2] & work;
                var southwest = improvements[4] & work;
                if (southeast == work || southwest == work)
                {
                    agriculture = "farmland";
                }
                else if (southeast == MapImprovements.Irrigation || southwest == MapImprovements.Irrigation)
                {
                    agriculture = "irrigation";
                }
            }
            if (agriculture == "mine")
            {
                output.Add(new(TileArtLayer.Mine, Catalog.Improvements[agriculture]));
            }
            else if (agriculture is not null)
            {
                output.Add(new(TileArtLayer.Agriculture, Catalog.Improvements[agriculture]));
            }

            if (tile.Hut.Value == MapPresence.Present)
            {
                output.Add(new(TileArtLayer.Village, Catalog.Improvements["village"]));
            }
        }
        if (detail != MapDetail.Overview || bare)
        {
            string? resource = tile.GrasslandShield ? Catalog.GrasslandShield : tile.Resource switch
            { 1 => terrainArt.FirstResource, 2 => terrainArt.SecondResource, _ => null };
            if (resource is not null)
            {
                output.Add(new(TileArtLayer.Resource, resource));
            }

            if ((tile.Improvements & MapImprovements.Pollution) != 0)
            {
                output.Add(new(TileArtLayer.Pollution, Catalog.Improvements["pollution"]));
            }
        }

        for (int q = 0; q < 4; q++)
        {
            if ((fog & (1 << (2 * q))) != 0)
            {
                output.Add(new(TileArtLayer.Fog, Catalog.FogEdges[q], Catalog.EdgeOffsets[q]));
            }
        }
    }

    private static void AddRoutes(List<TileSprite> output, MapImprovements flags, bool city,
        ReadOnlySpan<MapImprovements> neighbors, int neighborCities)
    {
        bool rail = city || (flags & MapImprovements.Railroad) != 0;
        bool connected = false;
        for (int d = 0; d < neighbors.Length; d++)
        {
            bool otherCity = (neighborCities & (1 << d)) != 0;
            var other = neighbors[d];
            if (!otherCity && (other & Transport) == 0)
            {
                continue;
            }

            bool railArm = rail && (otherCity || (other & MapImprovements.Railroad) != 0);
            output.Add(new(TileArtLayer.RouteNE + d, railArm ? Catalog.Rails[d + 1] : Catalog.Roads[d + 1]));
            connected = true;
        }
        if (!connected && !city)
        {
            output.Add(new(TileArtLayer.RouteNE, rail ? Catalog.Rails[0] : Catalog.Roads[0]));
        }
    }
}
