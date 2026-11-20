using OpenCiv2.Base;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Formats.Classic;

public static class ClassicLabels
{
    /// <summary>P04 §2: selected labels, installation fallback, then independent popups.
    /// Parsing never mutates a live pool. The supplied reader is closed on every exit.</summary>
    public static LabelCatalog Load(TextReader reader, TextDirectories directories, Action? missingLabels = null)
    {
        string search = directories.Search;
        var entries = new List<OwnedList<byte>>();
        var popups = new OwnedList<byte>?[3];
        int declared = 0;
        void Append(int count)
        {
            if (count > Labels.Capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Label count exceeds supported resource table");
            }

            while (entries.Count < count)
            {
                reader.NextLine();
                entries.Add(new(reader.Line.ToArray()));
            }
        }
        try
        {
            if (reader.Open("LABELS", "LABELS"u8.ToArray()))
            {
                reader.NextLine();
                declared = reader.NextInteger();
                Append(declared);
                if (entries.Count < Labels.Capacity)
                {
                    directories.Search = directories.Home;
                    if (reader.Open("LABELS", "LABELS"u8.ToArray()))
                    {
                        reader.NextLine();
                        int count = reader.NextInteger();
                        for (int i = 0; i < declared; ++i)
                        {
                            reader.NextLine();
                        }

                        Append(count);
                    }
                }
                var error = new OwnedList<byte>("[ERROR:LABELS.TXT]"u8.ToArray());
                while (entries.Count < Labels.Capacity)
                {
                    entries.Add(error);
                }
            }
            else
            {
                missingLabels?.Invoke();
            }

            reader.Close();
            directories.Search = search;
            if (reader.Open("LABELS", "POPUPS"u8.ToArray()))
            {
                for (int i = 0; i < 3; ++i)
                {
                    reader.NextLine();
                    popups[i] = new(reader.Line.ToArray());
                }
            }

            reader.Close();
            return new(declared, new(entries), new(popups));
        }
        finally
        {
            directories.Search = search;
            reader.Dispose();
        }
    }
}
