using OpenCiv2.Base;

namespace OpenCiv2.Core.Rules;

public readonly record struct NameReference(uint Id, bool Custom = false);
public sealed class TextPool
{
    private readonly List<OwnedList<byte>> entries = [];
    public int Count => entries.Count;

    public void Initialize()
    {
        entries.Clear();
        entries.Add(new("<nil>"u8.ToArray()));
    }

    public uint Store(ReadOnlySpan<byte> text)
    {
        int nul = text.IndexOf((byte)0);
        if (nul >= 0)
        {
            text = text[..nul];
        }

        if (text.Length == 0)
        {
            return 0;
        }

        uint id = checked((uint)entries.Count);
        entries.Add(new(text.ToArray()));
        return id;
    }

    public OwnedList<byte> Text(uint id)
    {
        if (id >= entries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Text reference is not present");
        }

        return entries[(int)id];
    }
}
public sealed class Labels
{
    public const int Capacity = 888;
    public int StatusLanguage { get; set; }
    public int DeclaredCount { get; set; }
    public List<uint?> Entries { get; } = [];
    public uint?[] Popups { get; } = new uint?[3];

    public OwnedList<byte> Label(int index, TextPool pool)
    {
        if (index < 0 || index >= Entries.Count || Entries[index] is null)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Label has no established table backing");
        }

        return pool.Text(Entries[index]!.Value);
    }

    public OwnedList<byte> Popup(int index, TextPool pool) => Popups[index] is { } id ? pool.Text(id) :
        new(
            index switch
            {
                0 => "OK"u8.ToArray(),
                1 => "Help"u8.ToArray(),
                2 => "Cancel"u8.ToArray(),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(index))
            });
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
    NameReference LeaderName,
    NameReference Plural,
    NameReference Adjective,
    OwnedList<NameReference> Names,
    OwnedList<OwnedList<NameReference>> Titles);
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
    public NameReference LeaderName { get; set; }
    public NameReference Plural;
    public NameReference Adjective;
    public NameReference[] Names { get; } = new NameReference[2];
    public NameReference[][] Titles { get; } = Enumerable.Range(0, 7).Select(_ => new NameReference[2]).ToArray();

    public LeaderSnapshot Snapshot() =>
        new(Attack, Expand, Civilize, TimesUsed, CityNameIndex, Female, Color, CityStyle,
            LeaderName, Plural, Adjective, new(Names), new(Titles.Select(t => new OwnedList<NameReference>(t))));
}
public sealed class RuleNameBindings
{
    public uint[] Advances { get; } = new uint[100];
    public uint[] Improvements { get; } = new uint[67];
    public uint[] Units { get; } = new uint[62];
    public uint[] Terrain { get; } = new uint[33];
    public uint[] Governments { get; } = new uint[7];
    public uint[][] GovernmentTitles { get; } = Enumerable.Range(0, 7).Select(_ => new uint[2]).ToArray();
    public uint[] Commodities { get; } = new uint[16];
    public uint[] Orders { get; } = new uint[13];
    public uint[] Difficulties { get; } = new uint[6];
    public uint[] Attitudes { get; } = new uint[9];
}
public sealed class RuleState
{
    public RuleTables Definitions { get; private set; } = new();
    public TextPool Text { get; private set; } = new();
    public Labels Labels { get; private set; } = new();
    public RuleNameBindings Names { get; private set; } = new();
    public Leader[] Leaders { get; private set; } = Enumerable.Range(0, 21).Select(_ => new Leader()).ToArray();
    public bool DoubleMovementApplied { get; set; }

    /// <summary>Transfer staged contents while retaining each RuleState's binding identity.
    /// Previously borrowed table/name references no longer describe the installed contents.</summary>
    public void SwapContents(RuleState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        (Definitions, other.Definitions) = (other.Definitions, Definitions);
        (Text, other.Text) = (other.Text, Text);
        (Labels, other.Labels) = (other.Labels, Labels);
        (Names, other.Names) = (other.Names, Names);
        (Leaders, other.Leaders) = (other.Leaders, Leaders);
        (DoubleMovementApplied, other.DoubleMovementApplied) = (other.DoubleMovementApplied, DoubleMovementApplied);
    }
}
