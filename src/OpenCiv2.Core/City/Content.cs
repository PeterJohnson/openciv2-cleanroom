using OpenCiv2.Base;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core.City;

public sealed record CityPreferenceUpdate(bool DeprioritizeDefense, OwnedList<byte> BuildOrder);
public sealed class CityPreferences
{
    public int[] BuildOrder { get; } = new int[32];
    public int Count { get; set; }
    public int NoDefend { get; set; }

    /// <summary>F03 §8: admit the complete update, then preserve inactive payload.</summary>
    public void Apply(CityPreferenceUpdate update)
    {
        if (update.BuildOrder.Count > 32 || update.BuildOrder.Any(slot => slot is < 1 or > 38))
        {
            throw new ArgumentException("City preferences require at most 32 improvement slots 1..38");
        }

        Count = 0;
        if (update.DeprioritizeDefense)
        {
            NoDefend = 1;
        }

        foreach (byte slot in update.BuildOrder)
        {
            BuildOrder[Count++] = slot;
        }
    }
}
public sealed record CityNameList(RuleText Plural, OwnedList<RuleText> Names, bool Complete = true);
public sealed record CityNameLists(OwnedList<CityNameList> Civilizations, OwnedList<RuleText>? Fallback);
public enum NameOutcome { Unavailable, Exhausted, Selected }
public sealed record NameAttempt(NameOutcome Outcome, OwnedList<byte> Bytes);
/// <summary>C01 ordered owned layers. A matched list uses only its own layer's fallback.</summary>
public sealed class NameCatalog
{
    public OwnedList<CityNameLists> Layers { get; }
    private readonly NameComparison comparison;

    public NameCatalog(IEnumerable<CityNameLists> layers, NameComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        Layers = new(layers);
        this.comparison = comparison;
    }

    public NameAttempt Select(ReadOnlySpan<byte> plural, byte remaining)
    {
        foreach (var layer in Layers)
        {
            CityNameList? found = null;
            foreach (var list in layer.Civilizations)
            {
                if (comparison(plural, list.Plural.Classic.ToArray()))
                {
                    found = list;
                    break;
                }
            }

            if (found is null)
            {
                continue;
            }

            if (remaining == 0)
            {
                return new(NameOutcome.Selected, new([(byte)'@', .. found.Plural.Classic]));
            }

            int offset = remaining - 1;
            if (offset < found.Names.Count)
            {
                return new(NameOutcome.Selected, found.Names[offset].Classic);
            }

            if (!found.Complete)
            {
                throw new ArgumentException("City name selection exceeds an incomplete imported list");
            }

            int extra = offset - found.Names.Count;
            if (layer.Fallback is not null && extra < layer.Fallback.Count)
            {
                return new(NameOutcome.Selected, layer.Fallback[extra].Classic);
            }

            return new(NameOutcome.Exhausted, new([]));
        }
        return new(NameOutcome.Unavailable, new([]));
    }
}
