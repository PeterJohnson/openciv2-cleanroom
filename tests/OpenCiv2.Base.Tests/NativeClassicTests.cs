using System.Text;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Classic;
using OpenCiv2.IO.Native;
using OpenCiv2.Platform.Native;
using OpenCiv2.Session;
using Xunit;
using ClassicReader = OpenCiv2.Formats.Classic.TextReader;

namespace OpenCiv2.Base.Tests;

public sealed class NativeClassicTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "oc2-classic-" + Guid.NewGuid().ToString("N"));
    public NativeClassicTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private void Text(string name, string text) => File.WriteAllBytes(Path.Combine(directory, name), Encoding.Latin1.GetBytes(text));
    [Fact]
    public void Binary_controls_short_prefixes_and_independent_cursors()
    {
        byte[] bytes = [0, 26, 255, 13, 10]; File.WriteAllBytes(Path.Combine(directory, "MiXeD.sAv"), bytes); var files = new ClassicStorage(directory);
        using var a = files.Read("MIXED.SAV"); using var b = files.Read("mixed.sav"); Assert.NotNull(a); Assert.NotNull(b);
        Assert.Equal((byte)0, a.Byte()); var tail = a.Item(9); Assert.False(tail.Complete); Assert.Equal(bytes[1..], tail.Prefix); Assert.Null(a.Byte()); Assert.True(a.Close()); Assert.Throws<ObjectDisposedException>(() => a.Byte());
        Assert.Equal(bytes, b.Item(5).Prefix); Assert.True(b.Close()); Assert.Null(files.Read("missing")); Assert.Null(files.Read(".")); Assert.Null(files.Replace("missing\\output.sav"));
        Assert.Throws<ArgumentException>(() => files.Read("bad\0")); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(directory, "MiXeD.sAv")));
    }
    [Fact]
    public void Replacement_truncates_case_alias_and_keeps_partial_output()
    {
        string path = Path.Combine(directory, "Save.sav"); File.WriteAllBytes(path, new byte[300]); var files = new ClassicStorage(directory);
        using (var output = files.Replace("SAVE.SAV")) { Assert.NotNull(output); Assert.True(output.Item([26])); Assert.True(output.Item([0, 255, 13, 10])); Assert.True(output.Close()); }
        Assert.Equal(new byte[] { 26, 0, 255, 13, 10 }, File.ReadAllBytes(path)); Assert.Single(Directory.EnumerateFiles(directory));
        using (var partial = files.Replace("save.sav")) Assert.True(partial!.Item([1, 2, 3])); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.Null(files.Replace(".")); Assert.False(files.Remove(".")); Assert.False(files.Remove("missing")); Assert.True(files.Remove("SAVE.SAV")); Assert.False(File.Exists(path));
    }
    [Fact]
    public void Explicit_directories_unicode_paths_and_physical_text_offsets()
    {
        Directory.CreateDirectory(Path.Combine(directory, "Scenario 世界")); Text("Scenario 世界/rules.txt", "@ONE\r\nfirst\r\n@TWO\r\nsecond\n"); string cwd = Environment.CurrentDirectory; var files = new ClassicStorage(directory);
        files.ChangeDirectory("sCeNaRiO 世界"); Assert.True(files.Exists(".", "RULES.TXT")); Assert.Equal(cwd, Environment.CurrentDirectory);
        var opened = new ClassicTextFiles(files).Open(".", "RULES.TXT"); Assert.NotNull(opened.Stream);
        try
        {
            Assert.Equal("@ONE\n", Encoding.Latin1.GetString(opened.Stream.Read().Characters.ToArray())); Assert.Equal("first\n", Encoding.Latin1.GetString(opened.Stream.Read().Characters.ToArray()));
            Assert.True(opened.Stream.Seek(13)); Assert.Equal("@TWO\n", Encoding.Latin1.GetString(opened.Stream.Read().Characters.ToArray())); Assert.True(opened.Stream.Seek(0)); Assert.Equal("@ONE\n", Encoding.Latin1.GetString(opened.Stream.Read().Characters.ToArray()));
            Assert.False(opened.Stream.Seek(-1)); Assert.True(opened.Stream.Seek(1000)); Assert.Equal(TextReadStatus.End, opened.Stream.Read().Status);
        }
        finally { opened.Stream.Close(); }
        Assert.Throws<ObjectDisposedException>(() => opened.Stream.Read()); files.ChangeDirectory(".."); Assert.True(files.Exists("Scenario 世界", "RULES.TXT")); Assert.Equal(cwd, Environment.CurrentDirectory);
        files.ChangeDirectory(Path.GetPathRoot(directory)!); files.ChangeDirectory(".."); Assert.True(Directory.Exists(files.Working));
    }
    [Fact]
    public void Text_windows_and_controls_remain_byte_oriented()
    {
        Text("long.txt", new string('x', 253) + "\r\nY\rZ\n"); var text = new ClassicTextFiles(new(directory)); var opened = text.Open(".", "long.txt"); Assert.NotNull(opened.Stream);
        try { Assert.Equal(new string('x', 253) + "\n", Encoding.Latin1.GetString(opened.Stream.Read().Characters.ToArray())); Assert.Equal("Y\rZ\n", Encoding.Latin1.GetString(opened.Stream.Read().Characters.ToArray())); Assert.Equal(TextReadStatus.End, opened.Stream.Read().Status); }
        finally { opened.Stream.Close(); }
        foreach (byte control in new byte[] { 0, 26 })
        {
            File.WriteAllBytes(Path.Combine(directory, "control.txt"), [65, control, 66]); var bad = text.Open(".", "control.txt"); Assert.NotNull(bad.Stream);
            try { var error = Assert.Throws<TextException>(() => bad.Stream.Read()); Assert.Equal(TextFault.UnsupportedControl, error.Fault); Assert.Throws<TextException>(() => bad.Stream.Read()); }
            finally { bad.Stream.Close(); }
        }
    }
    [Fact]
    public void Section_fallback_and_preference_roundtrip_use_real_native_files()
    {
        Directory.CreateDirectory(Path.Combine(directory, "Scenario")); Text("Scenario/rules.txt", "@FIRST\r\nscenario\r\n"); Text("RuLeS.TxT", "@FIRST\nhome\n@SECOND\nfallback\n");
        var disk = new ClassicStorage(directory); var text = new ClassicTextFiles(disk); var dirs = new TextDirectories(directory, Path.Combine(directory, "Scenario"), directory); using var reader = new ClassicReader(new(), dirs, new(text.Open), TextCase.Ascii);
        Assert.True(reader.Open("rules", "FIRST"u8.ToArray())); Assert.True(reader.NextLine()); Assert.Equal("scenario", Encoding.Latin1.GetString(reader.Line)); Assert.True(reader.Open("rules", "SECOND"u8.ToArray())); Assert.True(reader.NextLine()); Assert.Equal("fallback", Encoding.Latin1.GetString(reader.Line)); reader.Close(); Assert.Equal(Path.Combine(directory, "Scenario"), dirs.Search);
        var process = new ProcessContext(); Assert.Equal(PreferenceLoadStatus.Defaults, PreferenceFiles.Load(process, new(1000, true), disk)); process.OrdinaryPreferences.Text = new(Enumerable.Repeat((byte)255, 32)); process.MultiplayerPreferences.GameName = new(Enumerable.Repeat((byte)254, 32)); File.WriteAllBytes(Path.Combine(directory, "cIv2.dAt"), new byte[999]);
        Assert.Equal(PreferenceSaveStatus.Success, PreferenceFiles.Save(process, disk)); Assert.Equal(516, File.ReadAllBytes(Path.Combine(directory, "cIv2.dAt")).Length); var ordinary = process.OrdinaryPreferences; var multiplayer = process.MultiplayerPreferences; process.OrdinaryPreferences = new(); process.MultiplayerPreferences = new();
        Assert.Equal(PreferenceLoadStatus.Success, PreferenceFiles.Load(process, new(1, false), disk)); Assert.Equal(ordinary, process.OrdinaryPreferences); Assert.Equal(multiplayer, process.MultiplayerPreferences);
    }
    [Fact]
    public void Symlink_parent_traversal_and_non_ascii_aliases_are_not_lexically_collapsed()
    {
        if (OperatingSystem.IsWindows()) return; // Native symlink privileges are separately provisioned on Windows.
        Directory.CreateDirectory(Path.Combine(directory, "target", "nested")); File.WriteAllBytes(Path.Combine(directory, "target", "Data"), [7]); File.WriteAllBytes(Path.Combine(directory, "Data"), [9]); Directory.CreateSymbolicLink(Path.Combine(directory, "link"), Path.Combine(directory, "target", "nested"));
        var files = new ClassicStorage(directory); using var input = files.Read("link\\..\\DATA"); Assert.NotNull(input); Assert.Equal((byte)7, input.Byte()); input.Close();
        File.WriteAllBytes(Path.Combine(directory, "é.txt"), [1]); Assert.True(files.Exists(".", "é.TXT")); Assert.False(files.Exists(".", "É.txt"));
        File.CreateSymbolicLink(Path.Combine(directory, "alias"), Path.Combine(directory, "target", "Data")); Assert.True(files.Remove("ALIAS")); Assert.True(File.Exists(Path.Combine(directory, "target", "Data")));
    }
    [Fact]
    public void Ambiguous_case_aliases_are_rejected_before_open_or_replace()
    {
        if (!OperatingSystem.IsLinux()) return; // This authored directory needs a case-sensitive filesystem.
        File.WriteAllBytes(Path.Combine(directory, "a"), [1]); File.WriteAllBytes(Path.Combine(directory, "A"), [2]); var files = new ClassicStorage(directory);
        Assert.Throws<ArgumentException>(() => files.Read("a")); Assert.Throws<ArgumentException>(() => files.Replace("A")); Assert.Throws<ArgumentException>(() => files.Remove("a")); Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(Path.Combine(directory, "a"))); Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(Path.Combine(directory, "A")));
    }
}
