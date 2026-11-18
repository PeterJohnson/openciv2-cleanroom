using System.Text;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Formats;

/// <summary>Strict reversible CP1252 mapping; undefined bytes and Unicode scalars reject.</summary>
public static class ClassicText
{
    private static readonly char[] Extended = ['\u20ac', '\0', '\u201a', '\u0192', '\u201e', '\u2026', '\u2020', '\u2021', '\u02c6', '\u2030', '\u0160', '\u2039', '\u0152', '\0', '\u017d', '\0',
        '\0', '\u2018', '\u2019', '\u201c', '\u201d', '\u2022', '\u2013', '\u2014', '\u02dc', '\u2122', '\u0161', '\u203a', '\u0153', '\0', '\u017e', '\u0178'];
    private static ContentException Error(string location) => new("text-encoding", location, "text is not reversibly representable in windows-1252");
    public static byte[] EncodeWindows1252(string text, string location)
    {
        var result = new byte[text.Length];
        for (int i = 0; i < text.Length; ++i)
        {
            char c = text[i];
            if (c < 0x80 || c is >= '\u00a0' and <= '\u00ff') result[i] = (byte)c;
            else
            {
                int index = Array.IndexOf(Extended, c);
                if (index < 0) throw Error(location);
                result[i] = (byte)(0x80 + index);
            }
        }
        return result;
    }
    public static string DecodeWindows1252(ReadOnlySpan<byte> bytes, string location)
    {
        var result = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; ++i)
        {
            byte b = bytes[i]; char c = b is >= 0x80 and < 0xa0 ? Extended[b - 0x80] : (char)b;
            if (c == 0 && b != 0) throw Error(location);
            result[i] = c;
        }
        return new(result);
    }
    public static byte[] EncodeWindows1252(ReadOnlySpan<byte> utf8, string location)
    {
        try { return EncodeWindows1252(new UTF8Encoding(false, true).GetString(utf8), location); }
        catch (DecoderFallbackException) { throw Error(location); }
    }
}
