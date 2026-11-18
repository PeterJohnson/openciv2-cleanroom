namespace OpenCiv2.Base;

/// <summary>F01 §1: explicit wrapping intermediates, independent of project overflow settings.</summary>
public readonly record struct WrappingI32(int Value)
{
    public static explicit operator WrappingI32(int value) => new(value);
    public static explicit operator WrappingI32(uint value) => new(unchecked((int)value));
    public static explicit operator WrappingI32(long value) => new(unchecked((int)value));
    public static explicit operator WrappingI32(ulong value) => new(unchecked((int)value));
    public static explicit operator int(WrappingI32 value) => value.Value;
    public static explicit operator uint(WrappingI32 value) => unchecked((uint)value.Value);
    public static WrappingI32 operator +(WrappingI32 value) => value;
    public static WrappingI32 operator -(WrappingI32 value) => new(Arithmetic.Subtract32(0, value.Value));
    public static WrappingI32 operator +(WrappingI32 a, WrappingI32 b) => new(Arithmetic.Add32(a.Value, b.Value));
    public static WrappingI32 operator -(WrappingI32 a, WrappingI32 b) => new(Arithmetic.Subtract32(a.Value, b.Value));
    public static WrappingI32 operator *(WrappingI32 a, WrappingI32 b) => new(Arithmetic.Multiply32(a.Value, b.Value));
    public static WrappingI32 operator /(WrappingI32 a, WrappingI32 b) => new(Arithmetic.Divide32(a.Value, b.Value));
    public static WrappingI32 operator %(WrappingI32 a, WrappingI32 b) => new(Arithmetic.Remainder32(a.Value, b.Value));
    public static bool operator <(WrappingI32 a, WrappingI32 b) => a.Value < b.Value;
    public static bool operator >(WrappingI32 a, WrappingI32 b) => a.Value > b.Value;
    public static bool operator <=(WrappingI32 a, WrappingI32 b) => a.Value <= b.Value;
    public static bool operator >=(WrappingI32 a, WrappingI32 b) => a.Value >= b.Value;
}
