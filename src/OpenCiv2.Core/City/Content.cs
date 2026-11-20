using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core.City;

public sealed record CityPreferenceUpdate(bool DeprioritizeDefense, OwnedList<byte> BuildOrder);
public sealed class CityPreferences
{
    public const int Capacity = 32;
    public int[] BuildOrder { get; } = new int[Capacity];
    public int Count { get; set; }
    public int NoDefend { get; set; }

    /// <summary>F03 §8: admit the complete update, then preserve inactive payload.</summary>
    public void Apply(CityPreferenceUpdate update)
    {
        if (update.BuildOrder.Count > Capacity || update.BuildOrder.Any(slot => slot is < 1 or >= RuleLimits.BuildingTypes))
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
public sealed record CityNameList(string Plural, OwnedList<string> Names, bool Complete = true);
public sealed record CityNameLists(OwnedList<CityNameList> Civilizations, OwnedList<string>? Fallback);
public enum NameOutcome { Unavailable, Exhausted, Selected }
public sealed record NameAttempt(NameOutcome Outcome, string Text);
/// <summary>C01 ordered owned layers. A matched list uses only its own layer's fallback.</summary>
public sealed class NameCatalog
{
    public OwnedList<CityNameLists> Layers { get; }
    private readonly Func<string, string, bool> comparison;

    public NameCatalog(IEnumerable<CityNameLists> layers, Func<string, string, bool> comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        Layers = new(layers);
        this.comparison = comparison;
    }

    public NameAttempt Select(string plural, byte remaining)
    {
        foreach (var layer in Layers)
        {
            CityNameList? found = null;
            foreach (var list in layer.Civilizations)
            {
                if (comparison(plural, list.Plural))
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
                return new(NameOutcome.Selected, "@" + found.Plural);
            }

            int offset = remaining - 1;
            if (offset < found.Names.Count)
            {
                return new(NameOutcome.Selected, found.Names[offset]);
            }

            if (!found.Complete)
            {
                throw new ArgumentException("City name selection exceeds an incomplete imported list");
            }

            int extra = offset - found.Names.Count;
            if (layer.Fallback is not null && extra < layer.Fallback.Count)
            {
                return new(NameOutcome.Selected, layer.Fallback[extra]);
            }

            return new(NameOutcome.Exhausted, "");
        }
        return new(NameOutcome.Unavailable, "");
    }
}
