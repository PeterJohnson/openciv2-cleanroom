using System.Runtime.CompilerServices;
using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

// Embedded value storage: copying a runtime record never shares mutable array payload.
[InlineArray(CityLimits.TradeRoutes)]
public struct TradeRouteArray<T> : IEquatable<TradeRouteArray<T>>
{
    private T first;

    public readonly bool Equals(TradeRouteArray<T> other)
    {
        for (int i = 0; i < CityLimits.TradeRoutes; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is TradeRouteArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < CityLimits.TradeRoutes; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}
[InlineArray(5)]
public struct Fixed5<T> : IEquatable<Fixed5<T>>
{
    private T first;

    public readonly bool Equals(Fixed5<T> other)
    {
        for (int i = 0; i < 5; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is Fixed5<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < 5; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}
[InlineArray(EntityLimits.Civilizations)]
public struct CivilizationArray<T> : IEquatable<CivilizationArray<T>>
{
    private T first;

    public readonly bool Equals(CivilizationArray<T> other)
    {
        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is CivilizationArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(4)]
public struct Fixed4<T> : IEquatable<Fixed4<T>>
{
    private T first;

    public readonly bool Equals(Fixed4<T> other)
    {
        for (int i = 0; i < 4; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is Fixed4<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < 4; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(6)]
public struct Fixed6<T> : IEquatable<Fixed6<T>>
{
    private T first;

    public readonly bool Equals(Fixed6<T> other)
    {
        for (int i = 0; i < 6; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is Fixed6<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < 6; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(RuleLimits.Governments)]
public struct GovernmentArray<T> : IEquatable<GovernmentArray<T>>
{
    private T first;

    public readonly bool Equals(GovernmentArray<T> other)
    {
        for (int i = 0; i < RuleLimits.Governments; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is GovernmentArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < RuleLimits.Governments; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(10)]
public struct Fixed10<T> : IEquatable<Fixed10<T>>
{
    private T first;

    public readonly bool Equals(Fixed10<T> other)
    {
        for (int i = 0; i < 10; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is Fixed10<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < 10; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(13)]
public struct Fixed13<T> : IEquatable<Fixed13<T>>
{
    private T first;

    public readonly bool Equals(Fixed13<T> other)
    {
        for (int i = 0; i < 13; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is Fixed13<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < 13; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(CityLimits.Specialists)]
public struct SpecialistArray<T> : IEquatable<SpecialistArray<T>>
{
    private T first;

    public readonly bool Equals(SpecialistArray<T> other)
    {
        for (int i = 0; i < CityLimits.Specialists; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is SpecialistArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < CityLimits.Specialists; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(Civilization.StandingGoalCapacity)]
public struct StandingGoalArray<T> : IEquatable<StandingGoalArray<T>>
{
    private T first;

    public readonly bool Equals(StandingGoalArray<T> other)
    {
        for (int i = 0; i < Civilization.StandingGoalCapacity; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is StandingGoalArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < Civilization.StandingGoalCapacity; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(RuleLimits.Wonders)]
public struct WonderArray<T> : IEquatable<WonderArray<T>>
{
    private T first;

    public readonly bool Equals(WonderArray<T> other)
    {
        for (int i = 0; i < RuleLimits.Wonders; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is WonderArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < RuleLimits.Wonders; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(Civilization.CurrentGoalCapacity)]
public struct CurrentGoalArray<T> : IEquatable<CurrentGoalArray<T>>
{
    private T first;

    public readonly bool Equals(CurrentGoalArray<T> other)
    {
        for (int i = 0; i < Civilization.CurrentGoalCapacity; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is CurrentGoalArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < Civilization.CurrentGoalCapacity; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(RuleLimits.UnitTypes)]
public struct UnitTypeArray<T> : IEquatable<UnitTypeArray<T>>
{
    private T first;

    public readonly bool Equals(UnitTypeArray<T> other)
    {
        for (int i = 0; i < RuleLimits.UnitTypes; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is UnitTypeArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < RuleLimits.UnitTypes; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(MapLimits.Regions)]
public struct RegionArray<T> : IEquatable<RegionArray<T>>
{
    private T first;

    public readonly bool Equals(RegionArray<T> other)
    {
        for (int i = 0; i < MapLimits.Regions; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is RegionArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < MapLimits.Regions; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(RuleLimits.Advances)]
public struct AdvanceArray<T> : IEquatable<AdvanceArray<T>>
{
    private T first;

    public readonly bool Equals(AdvanceArray<T> other)
    {
        for (int i = 0; i < RuleLimits.Advances; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is AdvanceArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < RuleLimits.Advances; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(EliminationHistory.Capacity)]
public struct EliminationArray<T> : IEquatable<EliminationArray<T>>
{
    private T first;

    public readonly bool Equals(EliminationArray<T> other)
    {
        for (int i = 0; i < EliminationHistory.Capacity; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is EliminationArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < EliminationHistory.Capacity; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(RuleLimits.Tribes)]
public struct TribeArray<T> : IEquatable<TribeArray<T>>
{
    private T first;

    public readonly bool Equals(TribeArray<T> other)
    {
        for (int i = 0; i < RuleLimits.Tribes; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is TribeArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < RuleLimits.Tribes; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}

[InlineArray(GameState.PowerHistorySamples)]
public struct PowerHistoryArray<T> : IEquatable<PowerHistoryArray<T>>
{
    private T first;

    public readonly bool Equals(PowerHistoryArray<T> other)
    {
        for (int i = 0; i < GameState.PowerHistorySamples; ++i)
        {
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is PowerHistoryArray<T> other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < GameState.PowerHistorySamples; ++i)
        {
            hash.Add(this[i]);
        }

        return hash.ToHashCode();
    }
}
