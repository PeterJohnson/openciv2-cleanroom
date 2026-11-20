using OpenCiv2.Contracts;
namespace OpenCiv2.Core;

/// <summary>Membership of logical advance indices; no stored representation is exposed.</summary>
public readonly record struct AdvanceSet
{
    private readonly ulong lower, upper;
    private AdvanceSet(ulong lower, ulong upper) { this.lower = lower; this.upper = upper; }
    public bool Contains(int index) => index is >= 0 and < RuleLimits.Advances &&
        ((index < 64 ? lower : upper) & (1UL << (index % 64))) != 0;
    public AdvanceSet With(int index, bool present = true)
    {
        if (index is < 0 or >= RuleLimits.Advances)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        ulong bit = 1UL << (index % 64);
        return index < 64
            ? new(present ? lower | bit : lower & ~bit, upper)
            : new(lower, present ? upper | bit : upper & ~bit);
    }
}

/// <summary>Membership of logical city improvement indices; no stored representation is exposed.</summary>
public readonly record struct ImprovementSet
{
    private readonly ulong members;
    private ImprovementSet(ulong members) => this.members = members;
    public bool Contains(int index) => index is >= 0 and < CityLimits.ImprovementSlots && (members & (1UL << index)) != 0;
    public ImprovementSet With(int index, bool present = true)
    {
        if (index is < 0 or >= CityLimits.ImprovementSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        ulong bit = 1UL << index;
        return new(present ? members | bit : members & ~bit);
    }
}

/// <summary>Membership of logical city radius position indices; no stored representation is exposed.</summary>
public readonly record struct CityWorkSet
{
    private readonly ulong members;
    private CityWorkSet(ulong members) => this.members = members;
    public bool Contains(int index) => index is >= 0 and < CityLimits.WorkableTiles && (members & (1UL << index)) != 0;
    public CityWorkSet With(int index, bool present = true)
    {
        if (index is < 0 or >= CityLimits.WorkableTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        ulong bit = 1UL << index;
        return new(present ? members | bit : members & ~bit);
    }
}

/// <summary>Membership of logical map region indices; no stored representation is exposed.</summary>
public readonly record struct RegionSet
{
    private readonly ulong members;
    private RegionSet(ulong members) => this.members = members;
    public bool Contains(int index) => index is >= 0 and < MapLimits.Regions && (members & (1UL << index)) != 0;
    public RegionSet With(int index, bool present = true)
    {
        if (index is < 0 or >= MapLimits.Regions)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        ulong bit = 1UL << index;
        return new(present ? members | bit : members & ~bit);
    }
}
