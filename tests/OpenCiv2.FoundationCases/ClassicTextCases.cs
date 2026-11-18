using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Formats.Classic;
using ClassicReader = OpenCiv2.Formats.Classic.TextReader;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static byte[] ByteText(string text) => Encoding.Latin1.GetBytes(text);
    private static string ByteString(IEnumerable<byte> text) => Encoding.Latin1.GetString(text.ToArray());
    private sealed class TextTrace
    {
        public int Reads, Closes;
        public List<long> Seeks { get; } = [];
        public TextRead? Override;
        public bool FailSeek, FailClose;
        public Action? OnRead;
    }
    private sealed class TracedText(byte[] bytes, TextTrace trace) : ITextStream
    {
        private readonly MemoryTextStream memory = new(bytes);
        public TextRead Read() { ++trace.Reads; trace.OnRead?.Invoke(); return trace.Override ?? memory.Read(); }
        public bool Seek(long offset) { trace.Seeks.Add(offset); return !trace.FailSeek && memory.Seek(offset); }
        public void Close() { ++trace.Closes; memory.Close(); if (trace.FailClose) throw new InvalidOperationException("close failed"); }
    }
    private sealed class ClassicTextInput
    {
        public TextWorkspace Workspace { get; } = new();
        public TextDirectories Directories { get; } = new("caller", "scenario", "home", "txt");
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, TextTrace> Traces { get; } = new(StringComparer.Ordinal);
        public List<string> Opens { get; } = [];
        public TextFiles Services() => new((directory, path) =>
        {
            Equal(directory, Directories.Working); string key = directory + "/" + path; Opens.Add(key);
            if (!Files.TryGetValue(key, out string? bytes)) return new();
            if (!Traces.TryGetValue(key, out var trace)) Traces[key] = trace = new();
            return new(key, new TracedText(ByteText(bytes), trace));
        });
        public ClassicReader Reader() => new(Workspace, Directories, Services(), TextCase.Ascii);
    }
    private static void TextError(TextFault expected, Action action)
    {
        try { action(); } catch (TextException error) { Equal((int)expected, (int)error.Fault); return; }
        throw new InvalidOperationException("Expected classic text fault " + expected);
    }
    private static void ClassicReadWindows()
    {
        byte[] bytes = ByteText(new string('a', 253) + "\r\n" + new string('b', 254) + "Z\n\rX\r\n");
        var stream = new MemoryTextStream(bytes); bytes[0] = (byte)'z';
        Equal(new string('a', 253) + "\n", ByteString(stream.Read().Characters)); Equal(255, stream.Position);
        Equal(new string('b', 254), ByteString(stream.Read().Characters)); Equal(509, stream.Position);
        Equal("Z\n", ByteString(stream.Read().Characters)); Equal("\rX\n", ByteString(stream.Read().Characters)); Equal((int)TextReadStatus.End, (int)stream.Read().Status);
        Equal(true, stream.Seek(253)); Equal("\n", ByteString(stream.Read().Characters)); Equal(false, stream.Seek(1000)); Equal(false, stream.Seek(-1)); stream.Close(); Equal(false, stream.Seek(0));
        TextError(TextFault.UnavailableStream, () => stream.Read());
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = " \t a , b \t\r\n\n; comment\n@OTHER\n\vname\r\n\xef\xbb\xbf@A";
        using var reader = f.Reader(); Equal(true, reader.OpenLow("rules", null));
        foreach (string expected in new[] { "a , b", "", "; comment", "@OTHER", "\vname", "\xef\xbb\xbf@A" })
        { Equal(true, reader.NextLine()); Equal(expected, Encoding.Latin1.GetString(f.Workspace.Line)); Equal(0, f.Workspace.Cursor!.Value); }
        Equal(false, reader.NextLine()); Equal(0, f.Workspace.Line.Length); Equal(false, reader.IsOpen);
    }
    private static void ClassicTokensAndNumbers()
    {
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = " a , ,b;tail,\n\"a,b\",x\n,,Caf\xe9\n" + new string('n', 254);
        using var reader = f.Reader(); Equal(true, reader.OpenLow("rules", null)); Equal(true, reader.NextLine());
        foreach (string expected in new[] { "a", "", "b;tail", "", "" }) Equal(expected, ByteString(reader.NextToken()));
        Equal(true, reader.NextLine()); foreach (string expected in new[] { "\"a", "b\"", "x" }) Equal(expected, ByteString(reader.NextToken()));
        Equal(true, reader.NextLine()); Equal(" ", ByteString(reader.NextName(true))); Equal("", ByteString(reader.NextName(false))); Equal("Caf\xe9", ByteString(reader.NextName(true)));
        Equal(true, reader.NextLine()); Equal(new string('n', 254), ByteString(reader.NextName(true))); Equal(254, f.Workspace.Cursor!.Value); Equal(0, reader.NextToken().Length); Equal(254, f.Workspace.Cursor!.Value);
        (string Text, int Expected)[] cases = [("", 0), ("bad", 0), ("-0x10", 0), ("0xG", 0), ("0x", 0), ("0xFFFFFFFF", -1), ("0XfF; note", 255), ("0x100000001", 1), ("0x+10", 0), ("0x 10", 0),
            ("0b101x11", 5), ("0B11", 3), ("0b-1", 0), ("0b 1", 0), ("0d\v+009x", 9), ("0D-1", -1), ("0d0x10", 0), ("0012", 12), ("12 ; note", 12), ("\t\n\v\f\r -1", -1), ("-\t1", 0), ("\u00a01", 0),
            ("2147483648", int.MinValue), ("4294967295", -1), ("4294967296", 0), ("-4294967297", -1), ("-2147483649", int.MaxValue), ("18446744073709551617", 1), ("0b111111111111111111111111111111111", -1)];
        foreach (var c in cases) Equal(c.Expected, ClassicReader.ParseInteger(ByteText(c.Text)));
        (string Text, ushort Expected)[] flags = [("101x11", 5), ("0b101", 0), ("10000000000000001", 1), (new string('1', 100), 65535), ("+1", 0), (" 1", 0)];
        foreach (var c in flags) Equal(c.Expected, ClassicReader.ParseBinaryFlags(ByteText(c.Text)));
        f.Files["scenario/RULES.TXT"] = "bad,0b11,0d-3,1; comment\n 101x11 , 0b101";
        Equal(true, reader.OpenLow("rules", null)); Equal(true, reader.NextLine());
        foreach (int expected in new[] { 0, 3, -3, 1, 0 }) Equal(expected, reader.NextInteger());
        Equal(true, reader.NextLine()); Equal((ushort)5, reader.NextBinaryFlags()); Equal((ushort)0, reader.NextBinaryFlags());
    }
    private static void ClassicReadFailureState()
    {
        foreach (var status in new[] { TextReadStatus.End, TextReadStatus.Failure })
        {
            var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = "head,tail"; using var reader = f.Reader();
            Equal(true, reader.OpenLow("rules", null)); Equal(true, reader.NextLine()); Equal("head", ByteString(reader.NextToken()));
            var expected = (byte[])f.Workspace.Buffer.Clone(); expected[0] = 0; int backed = f.Workspace.BackedBytes;
            f.Traces["scenario/RULES.TXT"].Override = new(status, new([])); Equal(false, reader.NextLine()); BytesEqual(expected, f.Workspace.Buffer);
            Equal(backed, f.Workspace.BackedBytes); Equal(5, f.Workspace.Cursor!.Value); Equal(0, f.Workspace.Line.Length);
            Equal("tail", ByteString(reader.NextToken())); Equal(0, reader.NextToken().Length); Equal(1, f.Traces["scenario/RULES.TXT"].Closes);
            TextError(TextFault.UnavailableStream, () => reader.NextLine());
        }
    }
    private static void ClassicSourceFallback()
    {
        var f = new ClassicTextInput(); f.Files["home/RULES.TXT"] = "@UNITS\nrow"; using var reader = f.Reader();
        Equal(true, reader.OpenLow("rules", ByteText("units"))); Equal(new OwnedList<string>(["scenario/RULES.TXT", "home/RULES.TXT"]), new(f.Opens));
        Equal("caller", f.Directories.Working); Equal("home/RULES.TXT", reader.SourcePath); Equal("@UNITS", Encoding.Latin1.GetString(reader.Line)); Equal(6, f.Workspace.Cursor!.Value); Equal(0, reader.NextToken().Length);
        Equal(true, reader.NextLine()); f.Files["scenario/PACK.V2/RULES"] = "a"; Equal(true, reader.OpenLow("pack.v2/rules", null)); Equal("scenario/PACK.V2/RULES", f.Opens[^1]);
        Equal(1, f.Traces["home/RULES.TXT"].Closes); Equal("row", Encoding.Latin1.GetString(reader.Line)); Equal("scenario", f.Directories.Search);
        Equal(false, reader.OpenLow("missing", ByteText("A"))); Equal(false, reader.IsOpen); Equal("caller", f.Directories.Working);
        f.Files["scenario/RULES.TXT"] = "@OTHER\nscenario"; f.Files["home/RULES.TXT"] = "@UNITS\nhome"; f.Opens.Clear();
        Equal(true, reader.Open("rules", ByteText("units"))); Equal(new OwnedList<string>(["scenario/RULES.TXT", "home/RULES.TXT"]), new(f.Opens));
        Equal("caller", f.Directories.Working); Equal("scenario", f.Directories.Search); Equal(true, reader.NextLine()); Equal("home", ByteString(reader.NextToken()));
        Equal(1, f.Traces["scenario/RULES.TXT"].Closes); Equal(0, f.Traces["scenario/RULES.TXT"].Seeks.Count);
        reader.Close(); f.Opens.Clear(); Equal(false, reader.OpenSource("scenario", "CITY")); Equal(new OwnedList<string>(["scenario/CITY.TXT"]), new(f.Opens));
        f.Files["home/CITY.TXT"] = "@Tribe\nName\n@STOP\n"; Equal(true, reader.OpenSource("home", "CITY")); Equal("caller", f.Directories.Working); Equal("scenario", f.Directories.Search);
        var trace = f.Traces["home/CITY.TXT"]; trace.FailClose = true; Throws<InvalidOperationException>(() => reader.Close()); Equal(false, reader.IsOpen); reader.Close(); Equal(1, trace.Closes);
    }
    private static void ClassicSectionSearch()
    {
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = "@A\nfirst\n@B\nsecond"; f.Files["home/RULES.TXT"] = "@MISSING\nnot merged";
        using var reader = f.Reader(); Equal(true, reader.OpenLow("rules", ByteText("B"))); Equal(true, reader.OpenLow(null, ByteText("A")));
        Equal(new OwnedList<long>([0]), new(f.Traces["scenario/RULES.TXT"].Seeks)); Equal(true, reader.NextLine()); Equal("first", ByteString(reader.NextToken()));
        TextError(TextFault.UnavailableStream, () => reader.Open(null, ByteText("MISSING")));
        Equal(new OwnedList<long>([0, 0]), new(f.Traces["scenario/RULES.TXT"].Seeks)); Equal(1, f.Traces["scenario/RULES.TXT"].Closes); Equal(1, f.Opens.Count);
        Equal(false, reader.IsOpen); Equal("caller", f.Directories.Working); Equal("scenario", f.Directories.Search);
        f.Files["scenario/RULES.TXT"] = "\xef\xbb\xbf@UNITS\n@UNITS ; comment\n@UNITS2\n \t@uNiTs \t\r\nvalue";
        Equal(true, reader.OpenLow("rules", ByteText("UNITS"))); Equal("@uNiTs", Encoding.Latin1.GetString(reader.Line)); Equal(6, f.Workspace.Cursor!.Value);
        Equal(0, reader.NextToken().Length); Equal(true, reader.NextLine()); Equal("value", Encoding.Latin1.GetString(reader.Line));
    }
    private static void ClassicIndexedSearch()
    {
        var f = new ClassicTextInput(); f.Files["scenario/RULES.TXT"] = "@A\nfirst\n@A\nsecond\n"; using var reader = f.Reader();
        Equal(true, reader.OpenLow("rules", ByteText("A"))); Equal(true, reader.NextLine()); Equal("first", ByteString(reader.NextToken()));
        Equal(true, reader.OpenLow(null, ByteText("A"))); Equal(true, reader.NextLine()); Equal("second", ByteString(reader.NextToken())); reader.Close();
        long offset = 9; var files = f.Services() with { SectionOffset = (path, section) => { Equal("scenario/RULES.TXT", path); Equal(true, section.SequenceEqual("A"u8) || section.SequenceEqual("Missing"u8)); return offset; } };
        using var indexed = new ClassicReader(f.Workspace, f.Directories, files, TextCase.Ascii);
        Equal(true, indexed.OpenLow("rules", ByteText("A"))); Equal(true, indexed.NextLine()); Equal("second", ByteString(indexed.NextToken()));
        Equal(new OwnedList<long>([9]), new(f.Traces["scenario/RULES.TXT"].Seeks)); offset = 0;
        Equal(true, indexed.OpenLow("rules", ByteText("A"))); Equal(true, indexed.NextLine()); Equal("first", ByteString(indexed.NextToken())); Equal(1, f.Traces["scenario/RULES.TXT"].Seeks.Count);
        offset = 1000; TextError(TextFault.SeekFailed, () => indexed.OpenLow("rules", ByteText("A"))); Equal(true, indexed.IsOpen); Equal("first", Encoding.Latin1.GetString(indexed.Line));
        offset = 0; f.Traces["scenario/RULES.TXT"].FailSeek = true; TextError(TextFault.SeekFailed, () => indexed.OpenLow(null, ByteText("Missing"))); Equal(true, indexed.IsOpen);
    }
    private static void ClassicServiceAdmission()
    {
        var f = new ClassicTextInput(); using var reader = f.Reader(); TextError(TextFault.UnbackedCursor, () => reader.NextToken());
        f.Workspace.Cursor = 255; TextError(TextFault.UnbackedCursor, () => reader.NextToken()); f.Workspace.Cursor = 0; Array.Fill(f.Workspace.Buffer, (byte)'x'); TextError(TextFault.UnterminatedBuffer, () => reader.NextToken());
        f.Files["scenario/RULES.TXT"] = "ok\n"; Equal(true, reader.OpenLow("rules", null)); Equal(true, reader.NextLine()); Equal(3, f.Workspace.BackedBytes);
        f.Workspace.Cursor = 4; TextError(TextFault.UnbackedCursor, () => reader.NextToken()); var saved = (byte[])f.Workspace.Buffer.Clone();
        TextRead[] invalid = [new(TextReadStatus.Line, new([])), new(TextReadStatus.Line, new(ByteText(new string('x', 255)))), new(TextReadStatus.Line, new(ByteText("a\nb\n"))), new(TextReadStatus.Failure, new(ByteText("partial"))), new((TextReadStatus)99, new([]))];
        foreach (var value in invalid)
        {
            f.Traces["scenario/RULES.TXT"].Override = value; TextError(TextFault.InvalidServiceResult, () => reader.NextLine()); BytesEqual(saved, f.Workspace.Buffer); Equal(4, f.Workspace.Cursor!.Value);
        }
        f.Traces["scenario/RULES.TXT"].Override = new(TextReadStatus.Line, new([65, 0])); TextError(TextFault.UnsupportedControl, () => reader.NextLine()); BytesEqual(saved, f.Workspace.Buffer);
        foreach (byte control in new byte[] { 0, 26 })
        { var raw = new MemoryTextStream([.. "ok\n"u8, control, .. "tail"u8]); Equal("ok\n", ByteString(raw.Read().Characters)); TextError(TextFault.UnsupportedControl, () => raw.Read()); Equal(3, raw.Position); }
        f.Traces["scenario/RULES.TXT"].Override = new(TextReadStatus.Failure, new([])); Equal(false, reader.OpenLow(null, ByteText("B"))); Equal(0, f.Traces["scenario/RULES.TXT"].Seeks.Count);
        var services = f.Services() with { Open = (directory, _) => { Equal("scenario", directory); throw new InvalidOperationException("open failed"); } };
        using var failing = new ClassicReader(f.Workspace, f.Directories, services, TextCase.Ascii);
        Throws<InvalidOperationException>(() => failing.Open("rules", ByteText("A"))); Equal("caller", f.Directories.Working); Equal("scenario", f.Directories.Search);
    }
    private static void ClassicCaseAndRequestOwnership()
    {
        var comparison = TextCase.Ascii; Equal(true, comparison.Equal("@uNiTs"u8, "@UNITS"u8)); Equal(true, comparison.Equal(ByteText("@pRoBe\xc9"), ByteText("@Probe\xc9")));
        Equal(false, comparison.Equal(ByteText("name\xe9"), "@UNITS"u8)); Equal(false, comparison.Equal("@A"u8, "@AA"u8));
        TextError(TextFault.ComparisonUnavailable, () => comparison.Equal(ByteText("@Probe\xc9"), ByteText("@probe\xe9"))); TextError(TextFault.ComparisonUnavailable, () => comparison.UppercasePath("café.txt"));
        var f = new ClassicTextInput(); f.Files["scenario/CITY.TXT"] = "@probe\xe9\nName"; using var reader = f.Reader();
        TextError(TextFault.ComparisonUnavailable, () => reader.OpenLow("CITY", ByteText("Probe\xc9"))); Equal(true, reader.IsOpen); Equal("@probe\xe9", Encoding.Latin1.GetString(reader.Line)); Equal(0, f.Workspace.Cursor!.Value); Equal("caller", f.Directories.Working);
        byte[] section = ByteText("A"); string filename = "rules";
        var services = f.Services() with { Open = (directory, path) => { Equal("RULES.TXT", path); filename = "other"; section[0] = (byte)'B'; return new(directory + "/RULES.TXT", new MemoryTextStream(ByteText(directory == "home" ? "@A\nselected" : "@OTHER\nignored"))); } };
        using var owned = new ClassicReader(f.Workspace, f.Directories, services, comparison);
        Equal(true, owned.Open(filename, section)); Equal("home/RULES.TXT", owned.SourcePath); Equal("@A", Encoding.Latin1.GetString(owned.Line)); Equal(true, owned.NextLine()); Equal("selected", ByteString(owned.NextToken()));
    }
}
