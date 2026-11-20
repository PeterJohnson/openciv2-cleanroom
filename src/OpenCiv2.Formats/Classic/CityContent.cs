using OpenCiv2.Base;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Formats.Classic;

public static class ClassicCityContent
{
    public static CityPreferenceUpdate LoadPreferences(TextReader reader, RuleDefinitions rules, TextCase comparison) =>
        LoadPreferences(
            reader,
            rules.Improvements.Skip(1).Take(38).Select(r => r.Identity.Name.Classic.ToArray()).ToArray(),
            comparison);

    public static CityPreferenceUpdate LoadPreferences(TextReader reader, RuleState rules, TextCase comparison) =>
        LoadPreferences(
            reader,
            rules.Names.Improvements.Skip(1).Take(38).Select(id => rules.Text.Text(id).ToArray()).ToArray(),
            comparison);

    private static CityPreferenceUpdate LoadPreferences(TextReader reader, byte[][] names, TextCase comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison.Equal);
        bool deprioritize;
        var order = new List<byte>();
        try
        {
            deprioritize = reader.Open("CITYPREF.TXT", "NODEFEND"u8.ToArray());
            reader.Close();
            if (!reader.Open("CITYPREF.TXT", "AUTOBUILD"u8.ToArray()))
            {
                return new(deprioritize, new(order));
            }

            while (order.Count < 32 && reader.NextLine())
            {
                byte[] token = reader.NextToken();
                int index = 0;
                for (; index < names.Length; ++index)
                {
                    if (comparison.Equal(token, names[index]))
                    {
                        break;
                    }
                }

                if (index == names.Length)
                {
                    break;
                }

                order.Add((byte)(index + 1));
            }
            reader.Close();
        }
        catch
        {
            try
            {
                reader.Close();
            }
            catch { }

            throw;
        }
        return new(deprioritize, new(order));
    }

    /// <summary>Read the open CITY source into owned lists, preserving first matches and incomplete prefixes.</summary>
    public static CityNameLists ReadNames(TextReader reader, TextCase comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison.Equal);
        var lines = new List<byte[]>();
        int size = 0;
        while (reader.NextLine())
        {
            size += reader.Line.Length + 1;
            if (size > 16 * 1024 * 1024)
            {
                throw new ArgumentException("CITY source exceeds 16 MiB");
            }

            lines.Add(reader.Line.ToArray());
        }
        bool Stop(byte[] line) => line.Length >= 5 && comparison.Equal(line.AsSpan(0, 5), "@STOP"u8);
        static RuleText Text(byte[] bytes) => new(ClassicText.DecodeWindows1252(bytes, "CITY"), new(bytes));
        var civilizations = new List<CityNameList>();
        OwnedList<RuleText>? fallback = null;
        int materialized = 0;
        for (int i = 0; i < lines.Count; ++i)
        {
            if (lines[i].Length == 0 || lines[i][0] != '@')
            {
                continue;
            }

            var plural = Text(lines[i][1..]);
            var names = new List<RuleText>();
            int j = i + 1;
            // Other headers/comments are names until a blank or STOP terminates this list.
            for (; j < lines.Count && lines[j].Length != 0 && !Stop(lines[j]); ++j)
            {
                materialized += lines[j].Length;
                if (materialized > 16 * 1024 * 1024)
                {
                    throw new ArgumentException("Materialized CITY lists exceed 16 MiB");
                }

                names.Add(Text(lines[j]));
            }
            var list = new CityNameList(plural, new(names), j < lines.Count || names.Count >= 255);
            if (fallback is null && comparison.Equal(plural.Classic.ToArray(), "EXTRA"u8))
            {
                fallback = list.Names;
            }

            civilizations.Add(list);
        }
        return new(new(civilizations), fallback);
    }

    /// <summary>Snapshot scenario/home sources as ordered layers; leave the working directory at home.</summary>
    public static NameCatalog LoadNames(TextReader reader, TextDirectories directories, TextCase comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison.Equal);
        string savedSearch = directories.Search;
        var layers = new List<CityNameLists>();
        try
        {
            if (reader.OpenSource(savedSearch, "CITY"))
            {
                layers.Add(ReadNames(reader, comparison));
            }

            reader.Close();
            if (savedSearch != directories.Home && reader.OpenSource(directories.Home, "CITY"))
            {
                layers.Add(ReadNames(reader, comparison));
            }

            reader.Close();
        }
        catch
        {
            directories.Search = savedSearch;
            directories.Working = directories.Home;
            try
            {
                reader.Close();
            }
            catch { }

            throw;
        }
        finally
        {
            directories.Search = savedSearch;
            directories.Working = directories.Home;
        }
        return new(layers, comparison.Equal);
    }
}
