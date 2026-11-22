using OpenCiv2.Contracts;
namespace OpenCiv2.Core;

/// <summary>Membership of the eight civilization slots, independent of file and wire encodings.</summary>
public readonly record struct CivilizationSet
{
    private readonly byte members;
    private CivilizationSet(byte members) => this.members = members;

    public bool Contains(int civilization) => civilization is >= 0 and < EntityLimits.Civilizations && (members & (1 << civilization)) != 0;

    public CivilizationSet With(int civilization, bool present = true)
    {
        if (civilization is < 0 or >= EntityLimits.Civilizations)
        {
            throw new ArgumentOutOfRangeException(nameof(civilization));
        }
        return new(unchecked((byte)(present ? members | (1 << civilization) : members & ~(1 << civilization))));
    }

    public int Count => System.Numerics.BitOperations.PopCount(members);
    public static CivilizationSet Only(int civilization) => new CivilizationSet().With(civilization);

    public CivilizationSet Union(CivilizationSet other) => new((byte)(members | other.members));

    /// <summary>Whether the two sets share at least one civilization, including the barbarian seat.</summary>
    public bool Intersects(CivilizationSet other) => (members & other.members) != 0;
    public CivilizationSet Except(CivilizationSet other) => new((byte)(members & ~other.members));
}
