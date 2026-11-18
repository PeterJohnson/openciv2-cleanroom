using OpenCiv2.Base;

namespace OpenCiv2.Core.Rules;

/// <summary>P04 §2 owned resource text, independent of source readers and live text identities.</summary>
public sealed record LabelCatalog(int DeclaredCount, OwnedList<OwnedList<byte>> Entries, OwnedList<OwnedList<byte>?> Popups)
{
    public LabelCatalog() : this(0, new([]), new(new OwnedList<byte>?[3])) { }
    public void Install(TextPool pool, Labels labels)
    {
        if (Entries.Count > Labels.Capacity || Popups.Count != 3) throw new ArgumentException("Label catalog has invalid resource bounds");
        if (pool.Count == 0) pool.Initialize();
        var interned = new Dictionary<OwnedList<byte>, uint>();
        uint Store(OwnedList<byte> text)
        {
            if (interned.TryGetValue(text, out uint id)) return id;
            id = pool.Store(text.ToArray()); interned.Add(text, id); return id;
        }
        var entries = Entries.Select(text => (uint?)Store(text)).ToArray(); var popups = labels.Popups.ToArray();
        for (int i = 0; i < 3; ++i) if (Popups[i] is { } text) popups[i] = Store(text);
        labels.Entries.Clear(); labels.Entries.AddRange(entries); popups.CopyTo(labels.Popups, 0); labels.DeclaredCount = DeclaredCount;
    }
}
public sealed record LoadedContent(RuleInput Rules, LabelCatalog? Labels = null);
