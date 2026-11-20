using OpenCiv2.Base;

namespace OpenCiv2.Formats.Content;

public sealed record ContentSource(string Identity, OwnedList<byte> Bytes);
public sealed record FileReceipt(string Path, string Identity, string Sha256);
public delegate ContentSource ContentResolver(string path);
public static class ContentPaths
{
    public static void ValidateRelative(string path)
    {
        void Bad() =>
            throw new ContentException("invalid-path", path, "expected a relative slash-separated mounted path");
        if (path.Length == 0 || path[0] == '/' || path.Contains('\\') || path.Any(c => c < 32 || c == 127))
        {
            Bad();
        }

        int colon = path.IndexOf(':');
        if (colon > 0 && char.IsAsciiLetter(path[0])
            && path[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))
        {
            Bad();
        }

        if (path.Split('/').Any(p => p is "" or "." or ".."))
        {
            Bad();
        }
    }
}
