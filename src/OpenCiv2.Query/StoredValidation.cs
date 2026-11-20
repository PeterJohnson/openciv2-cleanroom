using System.Globalization;
using OpenCiv2.Base;
using OpenCiv2.Core;
using OpenCiv2.Formats;

namespace OpenCiv2.Query;

internal sealed class StoredValidation(IReadOnlyList<Record> records, List<Diagnostic> diagnostics)
{
    private readonly Dictionary<RecordKey, int> offsets = records.ToDictionary(r => r.Key, r => r.Offset);

    private static string Index(int value) => value.ToString(CultureInfo.InvariantCulture);

    private void Error(Section section, int slot, string field, int localOffset, string code, string message) =>
        diagnostics.Add(new(Category.Semantic, Severity.Error, code, section.Name() + "[" + Index(slot) + "]." + field,
            message, offsets[new(section, slot)] + localOffset));

    private void Text(Section section, int slot, string field, int offset, OwnedList<byte> value)
    {
        if (!value.Contains((byte)0))
        {
            Error(
                section,
                slot,
                field,
                offset,
                "unterminated-text",
                "Fixed classic text has no NUL within its capacity (F06 section 1)");
        }
    }

    private void Map(MapHeader header, OwnedList<Tile> tiles)
    {
        var shape = new Geometry(header.Width, header.Height);
        if (header.Area != shape.Area)
        {
            Error(
                Section.MapHeader,
                0,
                "area",
                4,
                "map-size-mismatch",
                "Stored area disagrees with dimensions; decoding used the dimension-derived size");
        }

        if (header.CoarseWidth != shape.CoarseWidth)
        {
            Error(
                Section.MapHeader,
                0,
                "coarse_width",
                10,
                "map-size-mismatch",
                "Stored coarse width disagrees with dimensions");
        }

        if (header.CoarseHeight != shape.CoarseHeight)
        {
            Error(
                Section.MapHeader,
                0,
                "coarse_height",
                12,
                "map-size-mismatch",
                "Stored coarse height disagrees with dimensions");
        }

        for (int i = 0; i < tiles.Count; ++i)
        {
            if ((byte)tiles[i].Terrain > 10)
            {
                Error(
                    Section.Tiles,
                    i,
                    "terrain",
                    0,
                    "terrain-range",
                    "Base terrain is outside the eleven classic slots");
            }

            if (tiles[i].Owner is > 7)
            {
                Error(
                    Section.Tiles,
                    i,
                    "owner",
                    5,
                    "owner-range",
                    "Occupant owner is neither absent nor a civilization slot 0..7");
            }
        }
    }

    private void Trailing(OwnedList<byte> bytes)
    {
        if (bytes.Count != 0)
        {
            diagnostics.Add(
                new(
                    Category.Compatibility,
                    Severity.Warning,
                    "uninterpreted-trailing-bytes",
                    "trailing[0].payload",
                    "Trailing bytes are preserved without interpretation; F06 does not require exact EOF",
                    offsets[new(Section.Trailing, 0)]));
        }
    }

    public static void Validate(
        RawMapFile map,
        IReadOnlyList<Record> records,
        List<Diagnostic> diagnostics,
        List<string> checks)
    {
        var v = new StoredValidation(records, diagnostics);
        checks.AddRange(["F06.current-raw-map-structure", "F06.map-size-consistency", "W01.tile-scalar-domains"]);
        v.Map(map.Header, map.Tiles);
        v.Trailing(map.Trailing);
    }

    public static void Validate(
        StoredSave save,
        IReadOnlyList<Record> records,
        List<Diagnostic> diagnostics,
        List<string> checks)
    {
        var v = new StoredValidation(records, diagnostics);
        checks.AddRange(
            [
                "F06.current-save-structure",
                "F06.map-size-consistency",
                "W01.tile-scalar-domains",
                "F06.fixed-text-termination",
                "F06.selector-view-history-domains",
                "F02.live-entity-references",
                "F02.stack-reciprocity-position-cycles"
            ]);
        v.Map(save.Map.Header, save.Map.Tiles);
        v.Trailing(save.Trailing);
        if (save.Globals.Multiplayer > 6)
        {
            v.Error(
                Section.Globals,
                0,
                "multiplayer",
                0x1a,
                "mode-range",
                "Saved mode is outside the documented 0..6 encodings");
        }

        sbyte[] selectors = [save.Globals.PlayerCiv, save.Globals.SavedCurrentCiv, save.Globals.TurnCiv];
        string[] selectorNames = ["player_civ", "saved_current_civ", "turn_civ"];
        for (int i = 0; i < 3; ++i)
        {
            if (selectors[i] is < 0 or > 7)
            {
                v.Error(
                    Section.Globals,
                    0,
                    selectorNames[i],
                    0x1b + i,
                    "civilization-range",
                    "Saved civilization selector is outside 0..7");
            }
        }

        diagnostics.Add(
            new(
                Category.Compatibility,
                Severity.Information,
                "view-civ-domain-unresolved",
                "globals[0].view_civ",
                "Saved view-civilization sentinel semantics are unspecified; this field is preserved without semantic validation",
                12 + 0x1e));
        for (int i = 0; i < 8; ++i)
        {
            var names = save.Names[i];
            v.Text(Section.CivilizationNames, i, "leader", 2, names.Leader);
            v.Text(Section.CivilizationNames, i, "tribe", 0x1a, names.Tribe);
            v.Text(Section.CivilizationNames, i, "adjective", 0x32, names.Adjective);
            for (int j = 0; j < 7; ++j)
            {
                v.Text(Section.CivilizationNames, i, "titles[" + Index(j) + "]", 0x4a + 24 * j, names.Titles[j]);
            }

            if ((save.Globals.CivsAlive & (1 << i)) != 0 && save.Civilizations[i].Tribe is < 0 or > 20)
            {
                v.Error(
                    Section.Civilizations,
                    i,
                    "tribe",
                    6,
                    "tribe-range",
                    "Living civilization's tribe is outside 0..20");
            }
        }
        for (int i = 0; i < 21; ++i)
        {
            if (save.Tribes[i].Female > 1)
            {
                v.Error(Section.Tribes, i, "female", 2, "gender-range", "Leader gender is outside 0/1");
            }
        }

        for (int i = 0; i < save.Leaders.Count; ++i)
        {
            if (save.Leaders[i].Female > 1)
            {
                v.Error(Section.Leaders, i, "female", 4, "gender-range", "Leader gender is outside 0/1");
            }
        }

        for (int i = 0; i < 8; ++i)
        {
            if (save.Views[i].Zoom is < -7 or > 8)
            {
                v.Error(Section.Views, i, "zoom", 8, "zoom-range", "Saved zoom is outside -7..8");
            }
        }

        if (save.Scenario is not null)
        {
            v.Text(Section.Scenario, 0, "name", 2, save.Scenario.Name);
        }

        if (save.Eliminations.Count is < 0 or > 12)
        {
            v.Error(
                Section.Eliminations,
                0,
                "count",
                0,
                "elimination-count-range",
                "Elimination count is outside 0..12");
        }

        for (int i = 0; i < Math.Clamp((int)save.Eliminations.Count, 0, 12); ++i)
        {
            v.Text(Section.Eliminations, 0, "names[" + Index(i) + "]", 0x32 + 24 * i, save.Eliminations.Names[i]);
        }

        bool CityExists(int slot) => slot >= 0 && slot < save.Cities.Count && save.Cities[slot].Token.Occupied;
        bool UnitExists(int slot) => slot >= 0 && slot < save.Units.Count && save.Units[slot].Token.Occupied;
        for (int i = 0; i < save.Cities.Count; ++i)
        {
            var city = save.Cities[i];
            if (!city.Token.Occupied)
            {
                continue;
            }

            v.Text(Section.Cities, i, "name", 0x20, city.Name);
            if (city.Owner.Value > 7)
            {
                v.Error(Section.Cities, i, "owner", 8, "owner-range", "Live city's owner is outside 0..7");
            }

            if (city.Founder.Value > 7)
            {
                v.Error(Section.Cities, i, "founder", 10, "civilization-range", "Live city's founder is outside 0..7");
            }

            if (city.RouteCount > 3)
            {
                v.Error(
                    Section.Cities,
                    i,
                    "route_count",
                    0x3a,
                    "route-count-range",
                    "City has more than three active route slots");
            }

            for (int j = 0; j < Math.Min((int)city.RouteCount, 3); ++j)
            {
                if (!CityExists(city.RoutePartners[j]))
                {
                    v.Error(
                        Section.Cities,
                        i,
                        "route_partners[" + Index(j) + "]",
                        0x44 + 2 * j,
                        "missing-city-reference",
                        "Active route partner does not identify a live scanned city");
                }
            }
        }
        for (int i = 0; i < save.Units.Count; ++i)
        {
            var unit = save.Units[i];
            if (!unit.Token.Occupied)
            {
                continue;
            }

            if (unit.Owner.Value > 7)
            {
                v.Error(Section.Units, i, "owner", 7, "owner-range", "Live unit's owner is outside 0..7");
            }

            if (unit.Home is { } home && !CityExists(home.Value))
            {
                v.Error(
                    Section.Units,
                    i,
                    "home",
                    0x10,
                    "missing-city-reference",
                    "Home does not identify a live scanned city");
            }

            short[] links = [unit.Previous, unit.Next];
            for (int j = 0; j < 2; ++j)
            {
                int target = links[j];
                if (target < 0)
                {
                    continue;
                }

                string field = j == 0 ? "previous" : "next";
                if (!UnitExists(target))
                {
                    v.Error(
                        Section.Units,
                        i,
                        field,
                        0x16 + 2 * j,
                        "missing-unit-reference",
                        "Stack link does not identify a live scanned unit");
                    continue;
                }
                var other = save.Units[target];
                if ((j == 0 ? other.Next : other.Previous) != i)
                {
                    v.Error(
                        Section.Units,
                        i,
                        field,
                        0x16 + 2 * j,
                        "stack-not-reciprocal",
                        "Stack link has no reciprocal predecessor/successor");
                }

                if (unit.X != other.X || unit.Y != other.Y)
                {
                    v.Error(
                        Section.Units,
                        i,
                        field,
                        0x16 + 2 * j,
                        "stack-position-mismatch",
                        "Linked units have different exact coordinates");
                }
            }
        }
        var color = new byte[save.Units.Count];
        for (int start = 0; start < save.Units.Count; ++start)
        {
            if (color[start] != 0 || !UnitExists(start))
            {
                continue;
            }

            var path = new List<int>();
            int current = start;
            while (UnitExists(current) && color[current] == 0)
            {
                color[current] = 1;
                path.Add(current);
                current = save.Units[current].Next;
            }
            if (UnitExists(current) && color[current] == 1)
            {
                v.Error(Section.Units, current, "next", 0x18, "stack-cycle", "Stack successor links form a cycle");
            }

            foreach (int slot in path)
            {
                color[slot] = 2;
            }
        }
    }
}
