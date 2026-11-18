using OpenCiv2.Base;

namespace OpenCiv2.Formats.Classic;

public enum TextFault { UnavailableStream, UnbackedCursor, UnterminatedBuffer, UnsupportedControl, InvalidServiceResult, SeekFailed, ComparisonUnavailable }
public sealed class TextException(TextFault fault, string message) : Exception(message)
{
    public TextFault Fault { get; } = fault;
}
/// <summary>F03 §2 and Appendix A. Retained byte buffer and token cursor, independent of encoding.</summary>
public sealed class TextWorkspace
{
    public byte[] Buffer { get; } = new byte[255];
    public int BackedBytes { get; set; } = 255;
    public int? Cursor { get; set; }
    public ReadOnlySpan<byte> Line => BackedString(0);
    internal ReadOnlySpan<byte> BackedString(int start)
    {
        if (BackedBytes > Buffer.Length || start < 0 || start >= BackedBytes)
            throw new TextException(TextFault.UnbackedCursor, "Classic text cursor is outside supplied backing");
        var remaining = Buffer.AsSpan(start, BackedBytes - start); int end = remaining.IndexOf((byte)0);
        if (end < 0) throw new TextException(TextFault.UnterminatedBuffer, "Classic text has no backed terminator");
        return remaining[..end];
    }
}
public enum TextReadStatus { Line, End, Failure }
public sealed record TextRead(TextReadStatus Status, OwnedList<byte> Characters)
{
    public TextRead() : this(TextReadStatus.End, new([])) { }
}
public interface ITextStream
{
    TextRead Read();
    bool Seek(long physicalOffset);
    void Close();
}
/// <summary>Owned raw bytes, CRLF translation, physical seeks. NUL/control-Z need an explicit service.</summary>
public sealed class MemoryTextStream(ReadOnlySpan<byte> bytes) : ITextStream
{
    private readonly byte[] bytes = bytes.ToArray();
    private int position;
    private bool closed;
    public int Position => position;
    public TextRead Read()
    {
        if (closed) throw new TextException(TextFault.UnavailableStream, "Read from closed classic text stream");
        var output = new List<byte>();
        while (position < bytes.Length && output.Count < 254)
        {
            byte c = bytes[position];
            if (c is 0 or 26) throw new TextException(TextFault.UnsupportedControl, "NUL/control-Z needs a supplied classic text-mode service");
            ++position;
            if (c == 13 && position < bytes.Length && bytes[position] == 10) { ++position; c = 10; }
            output.Add(c); if (c == 10) break;
        }
        return new(output.Count == 0 ? TextReadStatus.End : TextReadStatus.Line, new(output));
    }
    public bool Seek(long physicalOffset)
    {
        if (closed || physicalOffset < 0 || physicalOffset > bytes.Length) return false;
        position = (int)physicalOffset; return true;
    }
    public void Close() => closed = true;
}
public sealed record TextCase(NameComparison Equal, Func<string, string> UppercasePath)
{
    public static TextCase Ascii { get; } = new((a, b) =>
    {
        try { return TextComparison.AsciiNameEqual(a, b); }
        catch (ComparisonUnavailableException error) { throw new TextException(TextFault.ComparisonUnavailable, error.Message); }
    }, value =>
    {
        var chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; ++i)
        {
            if (chars[i] >= 128) throw new TextException(TextFault.ComparisonUnavailable, "Non-ASCII classic path conversion requires an established profile");
            if (chars[i] is >= 'a' and <= 'z') chars[i] = (char)(chars[i] - 'a' + 'A');
        }
        return new(chars);
    });
}
public sealed class TextDirectories(string working, string search, string home, string extension = "TXT")
{
    public string Working { get; set; } = working;
    public string Search { get; set; } = search;
    public string Home { get; set; } = home;
    public string Extension { get; set; } = extension;
}
public sealed record OpenedText(string Path, ITextStream? Stream)
{
    public OpenedText() : this("", null) { }
}
public delegate long? SectionOffset(string path, ReadOnlySpan<byte> section);
public sealed record TextFiles(Func<string, string, OpenedText> Open, SectionOffset? SectionOffset = null);

/// <summary>F03 byte-oriented sections/tokens with explicit file, workspace and comparison services.</summary>
public sealed class TextReader : IDisposable
{
    private readonly TextWorkspace workspace;
    private readonly TextDirectories directories;
    private readonly TextFiles files;
    private readonly TextCase comparison;
    private ITextStream? stream;
    public string SourcePath { get; private set; } = "";
    public bool IsOpen => stream is not null;
    public ReadOnlySpan<byte> Line => workspace.Line;
    public TextReader(TextWorkspace workspace, TextDirectories directories, TextFiles files, TextCase comparison)
    {
        ArgumentNullException.ThrowIfNull(files.Open); ArgumentNullException.ThrowIfNull(comparison.Equal); ArgumentNullException.ThrowIfNull(comparison.UppercasePath);
        this.workspace = workspace; this.directories = directories; this.files = files; this.comparison = comparison;
    }
    public void Close() { var closing = stream; stream = null; closing?.Close(); }
    public void Dispose() { try { Close(); } catch { /* Cleanup must preserve an earlier failure. */ } }
    internal static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
    {
        while (!bytes.IsEmpty && bytes[0] is 32 or 9) bytes = bytes[1..];
        while (!bytes.IsEmpty && bytes[^1] is 32 or 9) bytes = bytes[..^1];
        return bytes;
    }
    private TextReadStatus ReadBuffer()
    {
        if (stream is null) throw new TextException(TextFault.UnavailableStream, "Classic text has no open stream");
        var read = stream.Read();
        if (read.Status is not (TextReadStatus.Line or TextReadStatus.End or TextReadStatus.Failure))
            throw new TextException(TextFault.InvalidServiceResult, "Unknown classic text read status");
        if (read.Status != TextReadStatus.Line)
        {
            if (read.Characters.Count != 0) throw new TextException(TextFault.InvalidServiceResult, "Failed classic text read supplied unspecified partial buffer bytes");
            workspace.Buffer[0] = 0; return read.Status;
        }
        ReadOnlySpan<byte> line = read.Characters.ToArray(); int newline = line.IndexOf((byte)10);
        if (line.Length is 0 or > 254 || newline >= 0 && newline + 1 != line.Length)
            throw new TextException(TextFault.InvalidServiceResult, "Invalid classic text read window");
        if (line.Contains((byte)0)) throw new TextException(TextFault.UnsupportedControl, "Embedded NUL needs explicit buffer semantics");
        if (line[^1] == 10) line = line[..^1]; line = Trim(line);
        line.CopyTo(workspace.Buffer); workspace.Buffer[line.Length] = 0; workspace.BackedBytes = line.Length + 1; workspace.Cursor = 0;
        return TextReadStatus.Line;
    }
    public bool NextLine()
    {
        if (ReadBuffer() == TextReadStatus.Line) return true;
        Close(); return false;
    }
    private bool FindSection(ReadOnlySpan<byte> section, bool rewindOnce)
    {
        if (stream is null) throw new TextException(TextFault.UnavailableStream, "Section search has no classic text stream");
        if (files.SectionOffset?.Invoke(SourcePath, section) is > 0 and var offset && !stream.Seek(offset))
            throw new TextException(TextFault.SeekFailed, "Cannot seek to supplied classic section offset");
        byte[] wanted = [(byte)'@', .. section];
        for (; ; )
        {
            var status = ReadBuffer();
            if (status == TextReadStatus.Line)
            {
                if (comparison.Equal(workspace.Line, wanted)) { workspace.Cursor = workspace.Line.Length; return true; }
            }
            else if (status == TextReadStatus.End && rewindOnce)
            {
                rewindOnce = false;
                if (!stream!.Seek(0)) throw new TextException(TextFault.SeekFailed, "Cannot rewind classic text stream");
            }
            else { Close(); return false; }
        }
    }
    private string Filename(string name) => comparison.UppercasePath(name.Contains('.', StringComparison.Ordinal) ? name : name + "." + directories.Extension);
    public bool OpenSource(string directory, string filename)
    {
        string name = Filename(filename); Close(); string saved = directories.Working;
        try
        {
            directories.Working = directory; var opened = files.Open(directory, name); SourcePath = opened.Path; stream = opened.Stream; return stream is not null;
        }
        finally { directories.Working = saved; }
    }
    public bool OpenLow(string? filename, ReadOnlyMemory<byte>? section)
    {
        // Copy section memory before callbacks; immutable filename strings already own their request.
        byte[]? retainedSection = section?.ToArray();
        if (filename is not null)
        {
            Close(); string name = Filename(filename); string saved = directories.Working;
            try
            {
                directories.Working = directories.Search; var opened = files.Open(directories.Working, name);
                if (opened.Stream is null) { directories.Working = directories.Home; opened = files.Open(directories.Working, name); }
                SourcePath = opened.Path; stream = opened.Stream;
                if (stream is null) return false;
            }
            finally { directories.Working = saved; }
        }
        else if (stream is null) throw new TextException(TextFault.UnavailableStream, "Reused classic text stream is closed");
        return retainedSection is null || FindSection(retainedSection, filename is null);
    }
    public bool Open(string? filename, ReadOnlyMemory<byte>? section)
    {
        ReadOnlyMemory<byte>? retainedSection = section is null ? null : new ReadOnlyMemory<byte>(section.Value.ToArray());
        if (OpenLow(filename, retainedSection)) return true;
        string saved = directories.Search;
        try { directories.Search = directories.Home; return OpenLow(filename, retainedSection); }
        finally { directories.Search = saved; }
    }
    public byte[] NextToken()
    {
        if (workspace.Cursor is null) throw new TextException(TextFault.UnbackedCursor, "Classic token cursor was not supplied or initialized");
        var remaining = workspace.BackedString(workspace.Cursor.Value); int comma = remaining.IndexOf((byte)',');
        if (comma < 0) workspace.Cursor += remaining.Length;
        else { workspace.Cursor += comma + 1; remaining = remaining[..comma]; }
        return Trim(remaining).ToArray();
    }
    public int NextInteger() => ParseInteger(NextToken());
    public ushort NextBinaryFlags() => ParseBinaryFlags(NextToken());
    public byte[] NextName(bool padded) { var result = NextToken(); return padded && result.Length == 0 ? [(byte)' '] : result; }
    public static int ParseInteger(ReadOnlySpan<byte> token)
    {
        uint radix = 10;
        if (token.Length >= 2 && token[0] == '0')
        {
            if (token[1] is (byte)'x' or (byte)'X') radix = 16;
            else if (token[1] is (byte)'b' or (byte)'B') radix = 2;
            if (radix != 10 || token[1] is (byte)'d' or (byte)'D') token = token[2..];
        }
        bool negative = false;
        if (radix == 10)
        {
            while (!token.IsEmpty && (token[0] == 32 || token[0] is >= 9 and <= 13)) token = token[1..];
            if (!token.IsEmpty && token[0] is (byte)'+' or (byte)'-') { negative = token[0] == '-'; token = token[1..]; }
        }
        uint value = 0;
        foreach (byte c in token)
        {
            uint d = c is >= (byte)'0' and <= (byte)'9' ? (uint)(c - '0') : c is >= (byte)'a' and <= (byte)'f' ? (uint)(c - 'a' + 10) : c is >= (byte)'A' and <= (byte)'F' ? (uint)(c - 'A' + 10) : 16U;
            if (d >= radix) break;
            value = unchecked(value * radix + d);
        }
        return unchecked((int)(negative ? 0U - value : value));
    }
    public static ushort ParseBinaryFlags(ReadOnlySpan<byte> token)
    {
        ushort value = 0;
        foreach (byte c in token) { if (c is not ((byte)'0' or (byte)'1')) break; value = unchecked((ushort)(value * 2 + c - '0')); }
        return value;
    }
}
