using System.Text;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
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
    public void Hall_records_use_native_replacement_and_preserve_short_input()
    {
        // V08 §4 / F06 HALLFAME.DAT: actual native bytes, case alias and failed output.
        byte[] initial = OpenCiv2.FoundationCases.FoundationSuite.HallReference();
        string path = Path.Combine(directory, "HallFame.dat");
        File.WriteAllBytes(path, [.. initial, 1, 2, 3]);
        var hall = new HallOfFame(new ClassicStorage(directory));
        Assert.Equal(HallLoadStatus.Loaded, hall.Load());
        var entry = hall.Record(0) with { Rating = 90, Score = 777 };
        Assert.Equal(0, hall.Insert(entry)); Assert.True(hall.Save());
        byte[] saved = File.ReadAllBytes(path); Assert.Equal(432, saved.Length);
        Assert.Equal((short)777, ClassicHallOfFame.Decode(saved.AsSpan(0, 72)).Score);
        Assert.Equal(3, new HallOfFame(new ClassicStorage(directory)).Show(-1).Count);
        File.WriteAllBytes(path, saved[..100]);
        Assert.Equal(HallLoadStatus.ShortRead, hall.Load());
        Assert.All(Enumerable.Range(0, 6), i => Assert.Equal((short)-1, hall.Record(i).Score));
        Assert.Equal(saved[..100], File.ReadAllBytes(path));
        File.Delete(path); Directory.CreateDirectory(path);
        Assert.False(hall.Save()); Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void Autosave_rotation_uses_case_aliases_without_overwriting_failed_backups()
    {
        // F04 §9: remove and rename are distinct failure-preserving operations.
        var disk = new ClassicStorage(directory);
        Text("Ca_Auto.sav", "current"); Text("Ca_Auto2.sav", "old");
        Assert.False(disk.Rename("CA_AUTO.SAV", "CA_AUTO2.SAV"));
        Assert.Equal("current", File.ReadAllText(Path.Combine(directory, "Ca_Auto.sav")));
        Assert.True(disk.Remove("CA_AUTO2.SAV")); Assert.True(disk.Rename("CA_AUTO.SAV", "Ca_Auto2.SAV"));
        Assert.False(disk.Exists(".", "Ca_Auto.SAV")); Assert.True(disk.Exists(".", "CA_AUTO2.SAV"));
        Assert.Equal("current", File.ReadAllText(Path.Combine(directory, "Ca_Auto2.SAV")));
        Assert.False(disk.Rename("missing", "new")); Assert.False(disk.Rename(".", "new"));
        Assert.False(disk.Rename("Ca_Auto2.SAV", "missing\\new"));
    }

    [Fact]
    public void Local_turns_save_and_rotate_real_classic_files_before_movement()
    {
        // F04 §9 / F06 §5: actual LocalGame command, storage and decoded saves.
        var disk = new ClassicStorage(directory);
        using var game = new LocalGame(null, autosave: new(directory,
            new(disk, disk.Rename, disk.ChangeDirectory, () => "Auto")));
        void Turn()
        {
            var submitted = game.Player.Submit(new(LocalPlayCommands.EndTurn, new([])));
            while (game.AdvanceOne())
            {
                var update = game.Player.Result(submitted.Correlation.Command)!;
                if (update.Interaction is { } interaction)
                    Assert.True(game.Player.Reply(interaction.Id, interaction.Spec.Prompt == "End the current player turn?" ? 1 : interaction.Spec.Choices[0]).Accepted);
            }
            Assert.Equal(OpenCiv2.Contracts.CommandStatus.Completed, game.Player.Result(submitted.Correlation.Command)!.Status);
        }
        Turn(); string current = Directory.GetFiles(directory, "*.SAV").Single();
        byte[] first = File.ReadAllBytes(current); Assert.Equal((short)2, ClassicFiles.DecodeStoredSave(first).Globals.Turn);
        Turn(); string backup = Path.Combine(directory, Path.GetFileNameWithoutExtension(current) + "2.SAV");
        Assert.Equal(first, File.ReadAllBytes(backup));
        byte[] second = File.ReadAllBytes(current); Assert.Equal((short)3, ClassicFiles.DecodeStoredSave(second).Globals.Turn);
        Assert.Equal(LocalLoadResult.Loaded, game.LoadClassic(second)); Assert.Equal(second, File.ReadAllBytes(current));
        Assert.Equal(first, File.ReadAllBytes(backup)); Assert.Equal(disk.Working, Path.GetFullPath(directory));
        File.Delete(current); Directory.CreateDirectory(current);
        Turn(); Assert.True(Directory.Exists(current)); Assert.Equal(first, File.ReadAllBytes(backup));
        Assert.Equal(4, game.Player.ObserveLocalPlay()!.Turn); // Failed replace does not roll back upkeep.
    }

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
        var process = new ProcessContext(); Assert.Equal(PreferenceLoadStatus.Defaults, PreferenceFiles.Load(process, new(1000, true), disk)); process.OrdinaryPreferences.Text = ClassicText.DecodeWindows1252((Enumerable.Repeat((byte)255, 32)).ToArray()); process.MultiplayerPreferences.GameName = ClassicText.DecodeWindows1252((Enumerable.Repeat((byte)254, 32)).ToArray()); File.WriteAllBytes(Path.Combine(directory, "cIv2.dAt"), new byte[999]);
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
