using OpenCiv2.Base;

namespace OpenCiv2.Core.Rules;

/// <summary>P04 §2 owned resource text, independent of source readers and live text identities.</summary>
public sealed record LabelCatalog(
    int DeclaredCount,
    OwnedList<string> Entries,
    OwnedList<string?> Popups)
{
    public LabelCatalog() : this(0, new([]), new(new string?[Labels.PopupCount])) { }

    public void Install(Labels labels)
    {
        if (Entries.Count > Labels.Capacity || Popups.Count != Labels.PopupCount)
        {
            throw new ArgumentException("Label catalog has invalid resource bounds");
        }

        var entries = Entries.Select(RuleText.Display).ToArray();
        var popups = labels.Popups.ToArray();
        for (int i = 0; i < 3; ++i)
        {
            if (Popups[i] is { } text)
            {
                popups[i] = RuleText.Display(text);
            }
        }

        labels.Entries.Clear();
        labels.Entries.AddRange(entries);
        popups.CopyTo(labels.Popups, 0);
        labels.DeclaredCount = DeclaredCount;
    }
}
public sealed record LoadedContent(RuleInput Rules, LabelCatalog? Labels = null);
