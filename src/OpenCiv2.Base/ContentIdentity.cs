using System.Security.Cryptography;

namespace OpenCiv2.Base;

public static class ContentIdentity
{
    public static string HexBytes(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(bytes);
    public static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

public sealed class ComparisonUnavailableException()
    : Exception("Non-ASCII name comparison requires an established profile");

public delegate bool NameComparison(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);

public static class TextComparison
{
    /// <summary>Equal bytes always compare. Unequal high bytes need an explicit character profile.</summary>
    public static bool AsciiNameEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; ++i)
            if (left[i] != right[i] && Upper(left[i]) != Upper(right[i])) return false;
        return true;
    }
    private static byte Upper(byte value)
    {
        if (value >= 128) throw new ComparisonUnavailableException();
        return value is >= (byte)'a' and <= (byte)'z' ? (byte)(value - 'a' + 'A') : value;
    }
}
