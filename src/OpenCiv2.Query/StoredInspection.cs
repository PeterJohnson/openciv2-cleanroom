using System.Globalization;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.Query;

public static class StoredInspection
{
    private sealed class Builder(byte[] bytes)
    {
        public List<Record> Records { get; } = [];
        private int position;

        public void Record(Section section, int slot, int size, IEnumerable<Field> fields)
        {
            if (size < 0 || size > bytes.Length - position)
            {
                throw new DecodeException(position, "Truncated record");
            }

            Records.Add(new(new(section, slot), position, new(bytes.AsSpan(position, size).ToArray()), new(fields)));
            position += size;
        }

        public void Bytes(Section section, int slot, OwnedList<byte> value) =>
            Record(section, slot, value.Count, [new("payload", "bytes", new FieldValue.Bytes(value))]);

        public void Finish()
        {
            if (position != bytes.Length)
            {
                throw new InvalidOperationException("Stored projection did not cover all file bytes");
            }
        }
    }

    private static IEnumerable<Field> Numbers<T>(
        string name,
        IEnumerable<T> values,
        string type) where T : System.Numerics.IBinaryInteger<T>
    {
        int i = 0;
        foreach (var value in values)
        {
            yield return new(
                name + "[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                type,
                type[0] == 'i'
                    ? new FieldValue.Signed(long.CreateChecked(value))
                    : new FieldValue.Unsigned(ulong.CreateChecked(value)));
            ++i;
        }
    }

    private static void Project(Builder b, RawMapFile map)
    {
        b.Record(Section.MapHeader, 0, 14, RecordFields.Of(map.Header));
        b.Record(Section.Starts, 0, 42, Numbers("x", map.StartX, "i16"));
        b.Record(Section.Starts, 1, 42, Numbers("y", map.StartY, "i16"));
        for (int i = 0; i < map.Tiles.Count; ++i)
        {
            b.Record(Section.Tiles, i, 6, RecordFields.Of(map.Tiles[i]));
        }

        b.Bytes(Section.Trailing, 0, map.Trailing);
    }

    private static readonly string[] EventNames = [
        "unit_name",
        "city_name",
        "who_name",
        "whom_name",
        .. Enumerable.Range(0, 20).Select(i => "text[" + i.ToString(CultureInfo.InvariantCulture) + "]"),
        "move_owner_name",
        "move_unit_name",
        "aggression_whom_name",
        "aggression_who_name",
        "create_owner_name",
        "create_unit_name",
        "create_homecity",
        "auxiliary_civ_name",
        "auxiliary_unit_name",
        "money_receiver_name",
        "wave_file"
    ];

    private static void Project(Builder b, StoredSave save)
    {
        b.Record(Section.Header, 0, 12, [
            new("magic", "bytes", new FieldValue.Bytes(new("CIVILIZE\0"u8.ToArray()))),
            new("separator", "u8", new FieldValue.Unsigned(save.Header.Separator)),
            new("version", "u16", new FieldValue.Unsigned(save.Header.Version))
        ]);
        b.Record(Section.Globals, 0, 330, RecordFields.Of(save.Globals));
        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        {
            b.Record(Section.CivilizationNames, i, 242, RecordFields.Of(save.Names[i]));
        }

        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        {
            b.Record(Section.Civilizations, i, 1428, RecordFields.Of(save.Civilizations[i]));
        }

        var map = save.Map;
        b.Record(Section.MapHeader, 0, 14, RecordFields.Of(map.Header));
        for (int i = 0; i < EntityLimits.PlayerCivilizations; ++i)
        {
            b.Bytes(Section.Remembered, i + 1, map.Remembered[i]);
        }

        for (int i = 0; i < map.Tiles.Count; ++i)
        {
            b.Record(Section.Tiles, i, 6, RecordFields.Of(map.Tiles[i]));
        }

        for (int i = 0; i < 2; ++i)
        {
            b.Bytes(Section.Coarse, i, map.Coarse[i]);
        }

        for (int i = 0; i < 64; ++i)
        {
            b.Record(Section.Regions, i, 16, RecordFields.Of(map.Regions[i]));
        }

        for (int i = 0; i < save.Units.Count; ++i)
        {
            b.Record(Section.Units, i, 32, RecordFields.Of(save.Units[i]));
        }

        for (int i = 0; i < save.Cities.Count; ++i)
        {
            b.Record(Section.Cities, i, 88, RecordFields.Of(save.Cities[i]));
        }

        for (int i = 0; i < RuleLimits.Tribes; ++i)
        {
            var tribe = save.Tribes[i];
            b.Record(Section.Tribes, i, 3, [
                new("times_used", "u8", new FieldValue.Unsigned(tribe.TimesUsed)),
                new("city_name_index", "u8", new FieldValue.Unsigned(tribe.CityNameIndex)),
                new("female", "u8", new FieldValue.Unsigned(tribe.Female))
            ]);
        }
        b.Record(
            Section.Cursor,
            0,
            4,
            [
                new("x", "i16", new FieldValue.Signed(save.CursorX)),
                new("y", "i16", new FieldValue.Signed(save.CursorY))
            ]);
        for (int i = 0; i < save.Leaders.Count; ++i)
        {
            b.Record(Section.Leaders, i, 48, RecordFields.Of(save.Leaders[i]));
        }

        for (int i = 0; i < 150; ++i)
        {
            b.Record(Section.PowerHistory, i, EntityLimits.Civilizations, Numbers("civilizations", save.PowerHistory[i], "u8"));
        }

        b.Record(Section.Layout, 0, 106, RecordFields.Of(save.Layout));
        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        {
            b.Record(Section.Views, i, 10, RecordFields.Of(save.Views[i]));
        }

        if (save.Scenario is not null)
        {
            b.Record(Section.Scenario, 0, 100, RecordFields.Of(save.Scenario));
        }

        b.Record(Section.Eliminations, 0, 338, RecordFields.Of(save.Eliminations));
        if (save.Multiplayer is not null)
        {
            b.Record(Section.Multiplayer, 0, 1172, RecordFields.Of(save.Multiplayer));
        }

        if (save.Events is not null)
        {
            b.Record(Section.EventHeader, 0, 8, [new("tag", "bytes", new FieldValue.Bytes(new("EVNT"u8.ToArray()))),
                new("count", "i32", new FieldValue.Signed(save.Events.Count))]);
            for (int i = 0; i < save.Events.Count; ++i)
            {
                b.Record(Section.Events, i, 444, RecordFields.Of(save.Events[i].Record));
            }

            for (int i = 0; i < save.Events.Count; ++i)
            {
                var strings = save.Events[i].Strings;
                int size = strings.Sum(s => s is null ? 0 : s.Count + 1);
                b.Record(Section.EventStrings, i, size, strings.Select((s, index) =>
                    new Field(EventNames[index], "classic-text", s is null ? null : new FieldValue.Bytes(s))));
            }
        }
        b.Bytes(Section.Trailing, 0, save.Trailing);
    }

    public static StoredSnapshot Inspect(ReadOnlySpan<byte> input, FileKind kind, FileAccess access)
    {
        if (access != FileAccess.OfflineOmniscient || !Enum.IsDefined(kind))
        {
            throw new ArgumentException("Invalid offline inspection context or file kind");
        }

        byte[] bytes = input.ToArray();
        ushort? version = null;
        var b = new Builder(bytes);
        var diagnostics = new List<Diagnostic>();
        var checks = new List<string>();
        bool decoded = false;
        try
        {
            if (kind == FileKind.Save)
            {
                version = ClassicFiles.DecodeGameHeader(bytes).Version;
                if (version != 0x2c)
                {
                    diagnostics.Add(new(
                        Category.Compatibility,
                        Severity.Error,
                        "unsupported-version",
                        "header.version",
                        "Stored inspection supports version 0x2c; input version is " + version.Value.ToString(
                            CultureInfo.InvariantCulture),
                        10));
                    return new(kind, version, bytes, [], diagnostics, [], false);
                }
                var save = ClassicFiles.DecodeStoredSave(bytes);
                Project(b, save);
                StoredValidation.Validate(save, b.Records, diagnostics, checks);
            }
            else
            {
                var map = ClassicFiles.DecodeRawMap(bytes);
                Project(b, map);
                StoredValidation.Validate(map, b.Records, diagnostics, checks);
            }
            b.Finish();
            decoded = true;
            diagnostics.Add(new(
                Category.Compatibility,
                Severity.Information,
                "rules-unavailable",
                "",
                "External rules and language mapping were not loaded; rule-dependent validation and text interpretation are unavailable",
                null));
        }
        catch (DecodeException error)
        {
            b.Records.Clear();
            checks.Clear();
            diagnostics.Add(new(Category.Structural, Severity.Error, "decode-failed", "", error.Message, error.Offset));
        }
        return new(kind, version, bytes, b.Records, diagnostics, checks, decoded);
    }

    public static StoredDiff Diff(StoredSnapshot before, StoredSnapshot after)
    {
        var bytes = new List<ByteChange>();
        var fields = new List<FieldChange>();
        var left = before.SourceBytes;
        var right = after.SourceBytes;
        bool Same(int i) => i < left.Count && i < right.Count && left[i] == right[i];
        for (int offset = 0; offset < Math.Max(left.Count, right.Count);)
        {
            if (Same(offset))
            {
                ++offset;
                continue;
            }
            int start = offset;
            var l = new List<byte>();
            var r = new List<byte>();
            do
            {
                if (offset < left.Count)
                {
                    l.Add(left[offset]);
                }

                if (offset < right.Count)
                {
                    r.Add(right[offset]);
                }

                ++offset;
            } while (offset < Math.Max(left.Count, right.Count) && !Same(offset));
            bytes.Add(new(start, new(l), new(r)));
        }
        bool compared = before.Decoded && after.Decoded && before.Kind == after.Kind;
        if (compared)
        {
            var l = before.Records.ToDictionary(r => r.Key);
            var r = after.Records.ToDictionary(r => r.Key);
            foreach (var key in l.Keys.Union(r.Keys).OrderBy(k => k.Section).ThenBy(k => k.Slot))
            {
                var rightFields = r.TryGetValue(key, out var record)
                    ? record.Fields.ToDictionary(f => f.Name, StringComparer.Ordinal)
                    : [];
                if (l.TryGetValue(key, out var original))
                {
                    foreach (var field in original.Fields)
                    {
                        if (!rightFields.Remove(field.Name, out var changed))
                        {
                            fields.Add(new(key, field.Name, field, null));
                        }
                        else if (field != changed)
                        {
                            fields.Add(new(key, field.Name, field, changed));
                        }
                    }
                }

                if (record is not null)
                {
                    foreach (var field in record.Fields)
                    {
                        if (rightFields.ContainsKey(field.Name))
                        {
                            fields.Add(new(key, field.Name, null, field));
                        }
                    }
                }
            }
        }
        return new(before.SourceHash, after.SourceHash, compared, new(bytes), new(fields));
    }
}
