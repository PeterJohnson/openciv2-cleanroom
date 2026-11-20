using OpenCiv2.Contracts;
using OpenCiv2.MapRendering;

namespace OpenCiv2.App.Map;

/// <summary>MV21. The same disclosure surface serves pointer, keyboard and selected inspection.</summary>
internal static class MapInspection
{
    private static readonly string[] TerrainNames =
        ["Desert", "Plains", "Grassland", "Forest", "Hills", "Mountains", "Tundra", "Glacier", "Swamp", "Jungle", "Ocean"];

    public static string Describe(MapScene scene, MapPick pick, UnitTarget? target = null)
    {
        var point = pick.Position;
        if (!scene.PermitsPick(pick)) return "";
        var cell = scene.Objects(point);
        var tile = scene.Tile(point);
        string position = $"({point.X}, {point.Y})";
        if (pick.Kind == MapPickKind.Unit && cell.Unit is { } representative)
        {
            var unit = target is null || target == representative.Target || scene.Locate(target.Value) != point ? representative :
                scene.Map.Units.FirstOrDefault(u => u.Target == target, representative);
            string[] facts =
            [
                unit.Target is { } own ? $"Unit {own.Slot.Value}" : "Seen unit",
                $"Civilization {unit.Owner.Value}",
                $"Type {unit.Type.Value}",
                $"{cell.Count} visible in stack",
                Health(unit.Details.Health),
                unit.Details.Order.Value is { } order ? $"Order {order}" : Field("Order", unit.Details.Order.State),
                unit.Details.Waiting.Value == MapPresence.Present ? "Waiting for input" : "",
                unit.Details.Veteran.Value == MapPresence.Present ? "Veteran" : "",
                unit.Details.Automated.Value == MapPresence.Present ? "Automated" : "",
                unit.Details.Work.Value is int work ? $"Accumulated work {work}" : Field("Work counter", unit.Details.Work.State)
            ];
            return string.Join(" · ", facts.Where(fact => fact.Length != 0)) + "  " + position;
        }
        if (pick.Kind == MapPickKind.City)
        {
            if (cell.City?.Details is not { } city) return "Remembered city site  " + position;
            string population = city.Population.Value is int size
                ? (city.Knowledge == CityKnowledge.Remembered ? "Remembered population " : "Population ") + size
                : Field("Population", city.Population.State);
            string name = string.IsNullOrEmpty(cell.Name) ? "Known city" : cell.Name;
            var garrison = city.Garrison.Value switch
            {
                MapPresence.Present => "Garrison present",
                MapPresence.Absent => "No garrison",
                _ => Field("Garrison", city.Garrison.State)
            };
            return $"{name} · Civilization {city.Owner.Value} · {population} · {garrison}" +
                (city.Disorder.Value == MapPresence.Present ? " · Disorder" : "") + "  " + position;
        }
        if (tile.Terrain is not byte id) return "Unexplored  " + position;
        string terrain = id < TerrainNames.Length ? TerrainNames[id] : $"Terrain {id}";
        var features = new List<string> { terrain };
        if (tile.River) features.Add("River");
        if (tile.GrasslandShield) features.Add("Grassland shield");
        if (tile.Resource != 0) features.Add($"Special resource {tile.Resource}");
        var improvements = tile.Improvements;
        if (MapScene.IsAirbase(tile))
        {
            features.Add("Airbase");
            improvements &= ~(MapImprovements.CitySite | MapImprovements.Fortification);
            if (tile.AirbaseOccupant.Value?.Owner is CivilizationSlot aircraft)
                features.Add($"Aircraft of Civilization {aircraft.Value}");
        }
        if (improvements != default) features.Add($"Remembered features {improvements}");
        if (tile.Hut.Value == MapPresence.Present) features.Add("Hut");
        if (tile.Ownership.Value?.Owner is CivilizationSlot owner) features.Add($"Civilization {owner.Value}");
        return string.Join(" · ", features) + "  " + position;
    }

    private static string Health(MapFact<MapHealth> health) => health.Value is { } value
        ? $"Health {value.Remaining}/{value.Maximum}" : Field("Health", health.State);

    private static string Field(string label, MapFactState state) =>
        $"{label} {(state == MapFactState.Withheld ? "withheld" : "unavailable")}";
}
