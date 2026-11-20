using OpenCiv2.Base;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Formats.Content;

public sealed record CityPreferenceContent(CityPreferenceUpdate Update, ResolvedFile Source);
public sealed record CityNameContent(NameCatalog Catalog, ResolvedFile Source, bool StartingListsComplete);

public static class NativeCityContent
{
    private static bool Equal(NameComparison comparison, ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, string path)
    {
        try
        {
            return comparison(a, b);
        }
        catch (ComparisonUnavailableException error)
        {
            throw new ContentException("comparison-unavailable", path, error.Message);
        }
    }

    public static CityPreferenceContent LoadPreferences(
        ResolvedPack pack,
        RuleDefinitions rules,
        NameComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        if (!pack.Components.TryGetValue("cityPreferences", out var selected))
        {
            throw new ContentException("invalid-content", "cityPreferences", "missing selected component");
        }

        var root = new ContentReader(selected.Read(), "cityPreferences");
        root.Fields(["format", "humanCityDefensePriority", "buildPreferences"], true);
        root.At("format").Constant("openciv2-city-preferences/1");
        string priority = root.At("humanCityDefensePriority").Text();
        if (priority is not ("deprioritize" or "unchanged"))
        {
            root.At("humanCityDefensePriority").Error("unknown defense preference update");
        }

        var list = root.At("buildPreferences");
        if (list.ArraySize() > 32)
        {
            list.Error("at most 32 build preferences are supported");
        }

        var order = new List<byte>();
        for (int i = 0; i < list.ArraySize(); ++i)
        {
            var field = list.Element(i);
            string id = field.Id();
            int slot = 1;
            while (slot < 39 && rules.Improvements[slot].Identity.Id != id)
            {
                ++slot;
            }

            if (slot == 39)
            {
                field.Error("build preference must resolve to improvement slot 1..38");
            }

            byte[] name = rules.Improvements[slot].Identity.Name.Classic.ToArray();
            if (name.Length == 0 || name.AsSpan().SequenceEqual(" "u8))
            {
                field.Error("improvement name cannot round-trip through a trimmed classic token");
            }

            for (int earlier = 1; earlier < slot; ++earlier)
            {
                if (Equal(comparison, name, rules.Improvements[earlier].Identity.Name.Classic.ToArray(), field.Path))
                {
                    field.Error("classic preference name resolves to an earlier improvement");
                }
            }

            order.Add((byte)slot);
        }
        return new(new(priority == "deprioritize", new(order)), selected.File);
    }

    private static RuleText Text(ContentReader field, NameComparison comparison, bool cityName)
    {
        string unicode = field.Text();
        byte[] bytes = ClassicText.EncodeWindows1252(unicode, field.Path);
        if (bytes.Length == 0 || bytes.Length > 254 || bytes.Any(b => b is 0 or 13 or 10 or 26))
        {
            field.Error("city-name text must fit one nonempty classic line");
        }

        if (bytes[0] is 32 or 9 || bytes[^1] is 32 or 9)
        {
            field.Error("city-name text would change under classic edge trimming");
        }

        if (cityName && bytes.Length >= 5 && Equal(comparison, bytes.AsSpan(0, 5), "@STOP"u8, field.Path))
        {
            field.Error("city name would terminate a classic name list");
        }

        return new(unicode, new(bytes));
    }

    private static OwnedList<RuleText> Names(ContentReader field, NameComparison comparison)
    {
        var names = new List<RuleText>();
        for (int i = 0; i < field.ArraySize(); ++i)
        {
            names.Add(Text(field.Element(i), comparison, true));
        }

        return new(names);
    }

    public static CityNameContent LoadNames(ResolvedPack pack, RuleDefinitions rules, NameComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        if (!pack.Components.TryGetValue("cityNames", out var selected))
        {
            throw new ContentException("invalid-content", "cityNames", "missing selected component");
        }

        var root = new ContentReader(selected.Read(), "cityNames");
        root.Fields(["format", "civilizations", "fallbackNames"], true);
        root.At("format").Constant("openciv2-city-names/1");
        var lists = root.At("civilizations");
        var civilizations = new List<CityNameList>();
        for (int i = 0; i < lists.ArraySize(); ++i)
        {
            var entry = lists.Element(i);
            entry.Fields(["pluralName", "cityNames"]);
            var list = new CityNameList(
                Text(entry.At("pluralName"), comparison, false),
                Names(entry.At("cityNames"), comparison));
            foreach (var previous in civilizations)
            {
                if (Equal(comparison, previous.Plural.Classic.ToArray(), list.Plural.Classic.ToArray(), entry.Path))
                {
                    entry.Error("ambiguous civilization plural name");
                }
            }

            civilizations.Add(list);
        }
        var fallback = root.At("fallbackNames").Value is null ? null : Names(root.At("fallbackNames"), comparison);
        bool complete = true;
        foreach (var tribe in rules.Tribes)
        {
            var found = civilizations.FirstOrDefault(
                list => Equal(comparison, tribe.Plural.Classic.ToArray(), list.Plural.Classic.ToArray(), lists.Path));
            if (found is null || found.Names.Count == 0)
            {
                complete = false;
            }
        }
        if (pack.Manifests.Count <= 1 && !complete)
        {
            lists.Error("a complete base requires a nonempty list for every starting tribe plural");
        }

        return new(new([new(new(civilizations), fallback)], comparison), selected.File, complete);
    }
}
