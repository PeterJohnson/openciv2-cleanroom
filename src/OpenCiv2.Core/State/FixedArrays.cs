using System.Runtime.CompilerServices;

namespace OpenCiv2.Core.State;

// Embedded value storage: copying a runtime record never shares mutable array payload.
[InlineArray(3)]
public struct Fixed3<T> : IEquatable<Fixed3<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed3<T> other)
    {
        for (int i = 0; i < 3; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed3<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 3; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}
[InlineArray(5)]
public struct Fixed5<T> : IEquatable<Fixed5<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed5<T> other)
    {
        for (int i = 0; i < 5; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed5<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 5; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}
[InlineArray(8)]
public struct Fixed8<T> : IEquatable<Fixed8<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed8<T> other)
    {
        for (int i = 0; i < 8; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed8<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 8; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(4)]
public struct Fixed4<T> : IEquatable<Fixed4<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed4<T> other)
    {
        for (int i = 0; i < 4; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed4<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 4; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(6)]
public struct Fixed6<T> : IEquatable<Fixed6<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed6<T> other)
    {
        for (int i = 0; i < 6; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed6<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 6; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(7)]
public struct Fixed7<T> : IEquatable<Fixed7<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed7<T> other)
    {
        for (int i = 0; i < 7; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed7<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 7; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(10)]
public struct Fixed10<T> : IEquatable<Fixed10<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed10<T> other)
    {
        for (int i = 0; i < 10; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed10<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 10; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(13)]
public struct Fixed13<T> : IEquatable<Fixed13<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed13<T> other)
    {
        for (int i = 0; i < 13; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed13<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 13; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(16)]
public struct Fixed16<T> : IEquatable<Fixed16<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed16<T> other)
    {
        for (int i = 0; i < 16; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed16<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 16; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(28)]
public struct Fixed28<T> : IEquatable<Fixed28<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed28<T> other)
    {
        for (int i = 0; i < 28; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed28<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 28; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(48)]
public struct Fixed48<T> : IEquatable<Fixed48<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed48<T> other)
    {
        for (int i = 0; i < 48; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed48<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 48; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(62)]
public struct Fixed62<T> : IEquatable<Fixed62<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed62<T> other)
    {
        for (int i = 0; i < 62; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed62<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 62; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(64)]
public struct Fixed64<T> : IEquatable<Fixed64<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed64<T> other)
    {
        for (int i = 0; i < 64; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed64<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 64; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(100)]
public struct Fixed100<T> : IEquatable<Fixed100<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed100<T> other)
    {
        for (int i = 0; i < 100; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed100<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 100; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(12)]
public struct Fixed12<T> : IEquatable<Fixed12<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed12<T> other)
    {
        for (int i = 0; i < 12; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed12<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 12; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(20)]
public struct Fixed20<T> : IEquatable<Fixed20<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed20<T> other)
    {
        for (int i = 0; i < 20; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed20<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 20; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(21)]
public struct Fixed21<T> : IEquatable<Fixed21<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed21<T> other)
    {
        for (int i = 0; i < 21; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed21<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 21; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(150)]
public struct Fixed150<T> : IEquatable<Fixed150<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed150<T> other)
    {
        for (int i = 0; i < 150; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed150<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 150; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}

[InlineArray(256)]
public struct Fixed256<T> : IEquatable<Fixed256<T>> where T : unmanaged, IEquatable<T>
{
    private T first;
    public readonly bool Equals(Fixed256<T> other)
    {
        for (int i = 0; i < 256; ++i) if (!this[i].Equals(other[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? obj) => obj is Fixed256<T> other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); for (int i = 0; i < 256; ++i) hash.Add(this[i]); return hash.ToHashCode();
    }
}
