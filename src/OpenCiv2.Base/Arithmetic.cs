namespace OpenCiv2.Base;

/// <summary>F01 §§1,3–5: explicit numeric boundaries and sampling laws.</summary>
public static class Arithmetic
{
    public static int Signed32(uint value) => unchecked((int)value);
    public static int Add32(int a, int b) => unchecked(a + b);
    public static int Subtract32(int a, int b) => unchecked(a - b);
    public static int Multiply32(int a, int b) => unchecked(a * b);

    public static int Divide32(int a, int b)
    {
        CheckDivision(a, b);
        return a / b;
    }

    public static int Remainder32(int a, int b)
    {
        CheckDivision(a, b);
        return a % b;
    }

    // F01 §1: reversed bounds deliberately distinguish these two operations.
    public static int ClampAB(int value, int lo, int hi) => Math.Min(Math.Max(value, lo), hi);
    public static int ClampC(int value, int lo, int hi) => value > hi ? hi : Math.Max(value, lo);

    public static int ShiftSigned(int value, int shift)
    {
        if (shift >= 0)
            return unchecked(value << (shift & 31));
        int count = (int)(unchecked(0u - (uint)shift) & 31u);
        return value >> count;
    }

    public static int PrimaryModuloFromCode(int code, int n)
    {
        if (code is < 0 or > 32767 || n <= 0)
            throw new ArgumentOutOfRangeException(nameof(code), "F01 requires a 15-bit code and positive divisor.");
        return code % n;
    }

    public static double SecondaryUnitFromCode(uint code) => code / 4294967295.0;

    public static int SecondaryRangeFromCode(int lo, int hi, uint code)
    {
        if (lo == hi)
            return lo;
        if (hi < lo)
            (lo, hi) = (hi, lo);
        int difference = Subtract32(hi, lo);
        // F01 §4: exact binary64 adjustment and separately rounded operations.
        double span = difference + BitConverter.Int64BitsToDouble(0x3FEFFFFDE7210BE9);
        double fraction = SecondaryUnitFromCode(code);
        double product = fraction * span;
        long integral = (long)product;
        return unchecked((int)((uint)integral + (uint)lo));
    }

    public static uint GameTicksFromMilliseconds(uint milliseconds) => unchecked(milliseconds * 6u) / 100u;

    public static ushort MapPatternSeedFromTicks(uint ticks)
    {
        ushort value = (ushort)(ticks & 32767u);
        return value == 0 ? (ushort)1 : value;
    }

    private static void CheckDivision(int a, int b)
    {
        if (b == 0)
            throw new DivideByZeroException("F01 signed division has no result for divisor zero.");
        if (a == int.MinValue && b == -1)
            throw new OverflowException("F01 signed division has no representable i32 result.");
    }
}
