using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.Rules;

/// <summary>Owned live leader text and the F06 custom-name state; no pool ordinal or file marker.</summary>
public sealed record LeaderText(string Text, bool Custom = false)
{
    public static LeaderText Empty { get; } = new(RuleText.Nil);
}

/// <summary>P04 installed resources. Entry absence is distinct from present empty text.</summary>
public sealed class Labels
{
    public const int Capacity = 888;
    public const int PopupCount = 3;
    public int StatusLanguage { get; set; }
    public int DeclaredCount { get; set; }
    public List<string?> Entries { get; } = [];
    public string?[] Popups { get; } = new string?[PopupCount];

    public string Label(int index)
    {
        if (index < 0 || index >= Entries.Count || Entries[index] is null)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Label has no established table backing");
        }

        return Entries[index]!;
    }

    public string Popup(int index) => index is >= 0 and < PopupCount
        ? Popups[index] ?? (index switch { 0 => "OK", 1 => "Help", _ => "Cancel" })
        : throw new ArgumentOutOfRangeException(nameof(index));
}
public sealed record LeaderSnapshot(
    sbyte Attack,
    sbyte Expand,
    sbyte Civilize,
    byte TimesUsed,
    byte CityNameIndex,
    bool Female,
    short Color,
    short CityStyle,
    LeaderText LeaderName,
    LeaderText Plural,
    LeaderText Adjective,
    OwnedList<LeaderText> Names,
    OwnedList<OwnedList<LeaderText>> Titles);
public sealed class Leader
{
    public sbyte Attack { get; set; }
    public sbyte Expand { get; set; }
    public sbyte Civilize { get; set; }
    public byte TimesUsed { get; set; }
    public byte CityNameIndex { get; set; }
    public bool Female { get; set; }
    public short Color { get; set; }
    public short CityStyle { get; set; }
    public LeaderText LeaderName { get; set; } = LeaderText.Empty;
    public LeaderText Plural = LeaderText.Empty;
    public LeaderText Adjective = LeaderText.Empty;
    public LeaderText[] Names { get; } = Enumerable.Repeat(LeaderText.Empty, RuleLimits.LeaderGenders).ToArray();
    public LeaderText[][] Titles { get; } = Enumerable.Range(0, RuleLimits.Governments)
        .Select(_ => Enumerable.Repeat(LeaderText.Empty, RuleLimits.LeaderGenders).ToArray()).ToArray();

    public LeaderSnapshot Snapshot() =>
        new(Attack, Expand, Civilize, TimesUsed, CityNameIndex, Female, Color, CityStyle,
            LeaderName, Plural, Adjective, new(Names), new(Titles.Select(t => new OwnedList<LeaderText>(t))));
}
public sealed class RuleState
{
    public RuleTables Definitions { get; private set; } = new();
    /// <summary>Whether a complete definition set has been installed for rule-dependent observations.</summary>
    public bool IsLoaded { get; set; }
    public Labels Labels { get; private set; } = new();
    public Leader[] Leaders { get; private set; } = Enumerable.Range(0, RuleLimits.Tribes).Select(_ => new Leader()).ToArray();
    public bool DoubleMovementApplied { get; set; }

    /// <summary>Transfer staged contents while retaining each RuleState's binding identity.
    /// Previously borrowed table/name references no longer describe the installed contents.</summary>
    public void SwapContents(RuleState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        (Definitions, other.Definitions) = (other.Definitions, Definitions);
        (IsLoaded, other.IsLoaded) = (other.IsLoaded, IsLoaded);
        (Labels, other.Labels) = (other.Labels, Labels);
        (Leaders, other.Leaders) = (other.Leaders, Leaders);
        (DoubleMovementApplied, other.DoubleMovementApplied) = (other.DoubleMovementApplied, DoubleMovementApplied);
    }
}
