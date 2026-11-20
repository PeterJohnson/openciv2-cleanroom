using System.Globalization;
using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class SaveTimer : ICountdownControl
    {
        public int Stops, Starts; public Action? OnStop, OnStart;
        public void Stop(bool notify) { Equal(true, notify); ++Stops; OnStop?.Invoke(); }
        public void Start(bool notify) { Equal(true, notify); ++Starts; OnStart?.Invoke(); }
    }
    private sealed class SaveChooser : IFileChooser
    {
        public FileChoiceRequest? Request; public FileChoiceResult? Response; public Action? OnBegin, OnPoll, OnCancel; public int Begins, Polls, Cancels;
        public void Begin(FileChoiceRequest request) { Request = request; ++Begins; OnBegin?.Invoke(); }
        public FileChoiceResult? Poll() { ++Polls; OnPoll?.Invoke(); var value = Response; Response = null; return value; }
        public void Cancel() { ++Cancels; OnCancel?.Invoke(); }
    }
    private sealed class SaveMenuFixture : IDisposable
    {
        public GameFileFixture File = new(); public SaveTimer Timer = new(); public SaveChooser Chooser = new();
        public ClassicSaveMenuState Menu = new() { SessionActive = true, Filename = "previous.sav" };
        public SaveMenuServices Services; public List<SaveNotice> Notices = []; public int CalendarCalls;
        public SaveMenuFixture()
        {
            File.ReadySave(); File.Files.Text.Search = "saves"; File.Runtime.World.Globals.Year = -3980; File.Runtime.World.Globals.Difficulty = Difficulty.King;
            var rules = File.Runtime.Process.Rules; for (int i = 0; i < 6; ++i) rules.Definitions.Difficulties[i] = new("difficulty." + i, NameText("Difficulty" + i));
            Services = new(File.Storage, Timer, () => { ++CalendarCalls; return new(11, 14, 2026, 1, 2, 3); }, s => s.ToLowerInvariant(), i => "Label" + i,
                i => "Ruler" + i, i => "Title" + i, i => "Tribe" + i, i => i.ToString(CultureInfo.InvariantCulture), () => "Output failed", notice => Notices.Add(notice));
        }
        public ClassicSaveOperation Operation(bool scenario = false) => new(File.Runtime, File.Files, Menu, scenario, Services, Chooser);
        public SaveMenuResult Save(bool scenario = false, FileChoiceResult? choice = null) => ClassicSaveMenu.Save(File.Runtime, File.Files, Menu, scenario, Services, request => { Chooser.Request = request; return choice ?? new(FileChoiceStatus.Accepted, "chosen.sav"); });
        public void Dispose() => File.Dispose();
    }
    private static void FileChoiceTypesAndAdmission()
    {
        string[][] expected = [["sav", "net", "hot"], ["hot", "sav", "net"], ["eml", "sav", "net", "hot"], ["net", "sav", "hot"]];
        for (int mode = 0; mode < 7; ++mode)
        {
            var types = ClassicFileChoice.Types((GameMode)mode, false, false, false, i => "L" + i);
            Equal(new OwnedList<string>(expected[Math.Min(mode, 3)].Select(s => "*." + s)), new(types.Select(t => t.Pattern)));
            var only = ClassicFileChoice.Types((GameMode)mode, false, false, true, i => "L" + i); Equal(1, only.Count); Equal(types[0], only[0]);
            Equal("L" + (mode == 0 ? 238 : mode == 1 ? 855 : mode == 2 ? 858 : 861), only[0].Description);
        }
        Equal(new FileType("L415", "*.scn"), ClassicFileChoice.Types((GameMode)99, true, true, false, i => "L" + i).Single());
        Equal(new FileType("L261", "*.mp"), ClassicFileChoice.Types((GameMode)99, false, true, false, i => "L" + i).Single());
        Throws<ArgumentOutOfRangeException>(() => ClassicFileChoice.Types((GameMode)99, false, false, true, _ => "")); Throws<ArgumentNullException>(() => ClassicFileChoice.Types(GameMode.SinglePlayer, false, false, true, null!));
        foreach (var status in new[] { FileChoiceStatus.Accepted, FileChoiceStatus.Cancelled, FileChoiceStatus.Failed })
        {
            var request = new FileChoiceRequest(false, true, "open", "suggested", "SAV", new([])); string path = "世界\\" + new string('a', 700);
            var result = ClassicFileChoice.Choose(request, actual => { Equal(request, actual); return new(status, path, "diagnostic"); });
            Equal((int)status, (int)result.Status); Equal(status == FileChoiceStatus.Accepted ? path : "", result.Path); Equal("diagnostic", result.Error);
        }
        foreach (string path in new[] { "", "bad\0name" }) { var result = ClassicFileChoice.Validate(new(FileChoiceStatus.Accepted, path)); Equal((int)FileChoiceStatus.Failed, (int)result.Status); Equal("", result.Path); }
        Throws<ArgumentOutOfRangeException>(() => ClassicFileChoice.Validate(new((FileChoiceStatus)99))); Throws<ArgumentNullException>(() => ClassicFileChoice.Validate(null!));
    }
    private static void SaveMenuOwnedNoticesAndFormatting()
    {
        using var f = new SaveMenuFixture(); var random = f.File.Runtime.GameplayRandom.Capture(); var presentation = f.File.Runtime.PresentationRandom.Capture();
        Equal((int)SaveMenuResult.Saved, (int)f.Save()); Equal("SAVEOK", f.Notices.Single().Key); Equal(new OwnedList<string>(["Title2", "Ruler2", "Tribe2", "-3980", "Difficulty3"]), f.Notices[0].Arguments);
        Equal("chosen.sav", f.Menu.Filename); Equal("chosen.sav", f.File.Files.OpenedPath); Equal(1, f.Timer.Stops); Equal(1, f.Timer.Starts); Equal(0, f.CalendarCalls);
        Equal((short)-3980, ClassicFiles.DecodeStoredSave(f.File.Output.ToArray()).Globals.Year); Equal(random, f.File.Runtime.GameplayRandom.Capture()); Equal(presentation, f.File.Runtime.PresentationRandom.Capture());
        Equal("saves\\ru_b3980.sav", f.Chooser.Request!.InitialPath); Equal("Label239", f.Chooser.Request.Title); Equal("SAV", f.Chooser.Request.DefaultExtension); Equal(true, f.Chooser.Request.Save); Equal(false, f.Chooser.Request.RequireExisting);
        using var other = new SaveMenuFixture(); other.Services = other.Services with { RulerTitle = _ => "Other title", FormatYear = _ => "3980 BC" }; other.Save(); BytesEqual(f.File.Output, other.File.Output);
        using var nul = new SaveMenuFixture(); nul.Services = nul.Services with { RulerName = _ => "A\0Tail", RulerTitle = _ => "T\0x", CivilizationPlural = _ => "P\0x", FormatYear = _ => "Y\0x" }; nul.Save(); Equal("saves\\a_b3980.sav", nul.Chooser.Request!.InitialPath); Equal(new OwnedList<string>(["T", "A", "P", "Y", "Difficulty3"]), nul.Notices.Single().Arguments);
    }
    private static void SaveMenuNamesAndModeNotices()
    {
        foreach (short year in new short[] { short.MinValue, -13, -12, -1, 0, 1, 12, short.MaxValue })
        {
            using var f = new SaveMenuFixture(); f.File.Runtime.World.Globals.Year = year; f.File.Runtime.World.Scenario.YearIncrement = -1;
            f.File.Runtime.World.Scenario.Name = "\u00e9"; Equal((int)SaveMenuResult.Saved, (int)f.Save(true));
            int month = year < 0 ? 11 - Math.Abs(year % 12) : Math.Abs(year % 12); Equal("saves\\" + Math.Abs(year / 12) + "label" + (420 + month) + ".scn", f.Chooser.Request!.InitialPath);
            Equal("SCENOK", f.Notices.Single().Key); Equal("é", f.Notices[0].Arguments[0]); Equal(0, f.CalendarCalls);
        }
        using var email = new SaveMenuFixture(); email.File.Runtime.World.Globals.Multiplayer = GameMode.Email; email.Save(); Equal("saves\\ru_b3980 11-14-2026 1-2-3.eml", email.Chooser.Request!.InitialPath); Equal(1, email.CalendarCalls); Equal("EMAILSAVED", email.Notices.Single().Key); Equal("chosen.sav", email.Notices[0].Arguments[0]);
        using var special = new SaveMenuFixture(); special.Services = special.Services with { RulerName = _ => "1@Name" }; special.File.Files.Text.Search = ""; special.Save(); Equal("___b3980.sav", special.Chooser.Request!.InitialPath);
        using var culture = new SaveMenuFixture(); var saved = CultureInfo.CurrentCulture;
        try { var changed = (CultureInfo)CultureInfo.InvariantCulture.Clone(); changed.NumberFormat.NegativeSign = "~"; CultureInfo.CurrentCulture = changed; culture.File.Runtime.World.Globals.Year = short.MinValue; culture.Save(); Equal("saves\\ru_b32768.sav", culture.Chooser.Request!.InitialPath); } finally { CultureInfo.CurrentCulture = saved; }
    }
    private static void SaveMenuCancellationAndOutputFailures()
    {
        for (int mode = 0; mode < 7; ++mode) foreach (bool failure in new[] { false, true })
        {
            using var f = new SaveMenuFixture(); f.File.Runtime.World.Globals.Multiplayer = (GameMode)mode; var before = f.File.Runtime.World.Globals;
            Equal((int)(failure ? SaveMenuResult.Failed : SaveMenuResult.Cancelled), (int)f.Save(choice: new(failure ? FileChoiceStatus.Failed : FileChoiceStatus.Cancelled, "draft", "Chooser failed")));
            Equal("previous.sav", f.Menu.Filename); Equal(0, f.File.Opens); Equal(1, f.Timer.Starts); Equal(before, f.File.Runtime.World.Globals);
            Equal(failure ? 1 : 0, f.Notices.Count); if (failure) { Equal("SAVEERROR", f.Notices[0].Key); Equal("Chooser failed", f.Notices[0].Arguments[0]); }
        }
        for (int failure = 0; failure < 3; ++failure)
        {
            using var f = new SaveMenuFixture(); f.File.OpenOk = failure != 0; f.File.WriteOk = failure != 1; f.File.CloseOk = failure != 2;
            Equal((int)SaveMenuResult.Failed, (int)f.Save()); Equal("chosen.sav", f.Menu.Filename); Equal(1, f.File.Opens); Equal(failure == 0 ? 0 : 1, f.File.Closes); Equal(1, f.Timer.Starts); Equal("Output failed", f.Notices.Single().Arguments[0]);
        }
    }
    private static void SaveMenuDeferredAcceptanceAndPin()
    {
        using var f = new SaveMenuFixture(); using var operation = f.Operation(); var random = f.File.Runtime.GameplayRandom.Capture();
        Equal(true, operation.Advance() is null); Equal(1, f.Chooser.Begins); Equal(0, f.Chooser.Polls); Equal(0, f.Timer.Starts);
        for (int i = 0; i < 5; ++i) Equal(true, operation.Advance() is null); Equal(0, f.File.Opens);
        Throws<InvalidOperationException>(() => f.File.Runtime.PrepareNewGame()); Throws<InvalidOperationException>(() => f.File.Runtime.EndClientSession());
        f.File.Runtime.World.Globals.Year = -3960; f.File.Runtime.Publish(); f.Chooser.Response = new(FileChoiceStatus.Accepted, "later.sav");
        Equal((int)SaveMenuResult.Saved, (int)operation.Advance()!.Value); Equal((int)SaveMenuResult.Saved, (int)operation.Advance()!.Value); Equal((int)SaveMenuResult.Saved, (int)operation.Cancel()); operation.Dispose();
        Equal(1, f.File.Opens); Equal(1, f.Timer.Starts); Equal(1, f.Chooser.Cancels); Equal("-3960", f.Notices.Single().Arguments[3]); Equal((short)-3960, ClassicFiles.DecodeStoredSave(f.File.Output.ToArray()).Globals.Year); Equal(random, f.File.Runtime.GameplayRandom.Capture()); f.File.Runtime.EndClientSession();
    }
    private static void SaveMenuDeferredCleanupAndFailurePrecedence()
    {
        for (int failure = 0; failure < 8; ++failure)
        {
            using var f = new SaveMenuFixture(); using var operation = f.Operation();
            if (failure == 1) f.Chooser.OnBegin = () => throw new IOException("primary");
            if (failure == 6) f.Timer.OnStop = () => throw new IOException("primary");
            if (failure == 7) f.Timer.OnStart = () => throw new IOException("dispose cleanup");
            if (failure is 1 or 6) Throws<IOException>(() => operation.Advance());
            else
            {
                Equal(true, operation.Advance() is null);
                if (failure == 2) { f.Chooser.OnPoll = () => throw new IOException("primary"); f.Timer.OnStart = () => throw new IOException("cleanup"); try { operation.Advance(); throw new Exception("Expected failure"); } catch (IOException e) { Equal("primary", e.Message); } }
                else if (failure == 3) { Equal((int)SaveMenuResult.Cancelled, (int)operation.Cancel()); operation.Cancel(); }
                else if (failure == 4) { f.Chooser.Response = new(FileChoiceStatus.Accepted, ""); Equal((int)SaveMenuResult.Failed, (int)operation.Advance()!.Value); }
                else if (failure == 5) { f.Chooser.OnCancel = () => throw new IOException("dismiss"); f.Timer.OnStart = () => throw new IOException("restart"); try { operation.Cancel(); throw new Exception("Expected failure"); } catch (IOException e) { Equal("dismiss", e.Message); } }
            }
            operation.Dispose(); operation.Dispose(); Equal(0, f.File.Opens); Equal("previous.sav", f.Menu.Filename); Equal(1, f.Timer.Stops); Equal(failure == 6 ? 0 : 1, f.Timer.Starts); Equal(1, f.Chooser.Cancels); f.File.Runtime.PrepareNewGame();
        }
        using var before = new SaveMenuFixture(); using var untouched = before.Operation(); untouched.Dispose(); Equal(0, before.Timer.Stops); Equal(0, before.Timer.Starts); Equal((int)SaveMenuResult.Cancelled, (int)untouched.Advance()!.Value);
        using var saved = new SaveMenuFixture(); using var accepted = saved.Operation(); accepted.Advance(); saved.Chooser.Response = new(FileChoiceStatus.Accepted, "done.sav"); saved.Timer.OnStart = () => throw new IOException("restart");
        Throws<IOException>(() => accepted.Advance()); Equal((int)SaveMenuResult.Saved, (int)accepted.Result!.Value); Equal((int)SaveMenuResult.Saved, (int)accepted.Advance()!.Value); accepted.Dispose(); Equal(1, saved.File.Opens); Equal(1, saved.File.Closes); Equal(1, saved.Timer.Starts); saved.File.Runtime.PrepareNewGame();
    }
    private static void SaveMenuAdmissionAndProviderFailures()
    {
        using var inactive = new SaveMenuFixture(); inactive.Menu.SessionActive = false;
        Equal((int)SaveMenuResult.Inactive, (int)ClassicSaveMenu.Save(inactive.File.Runtime, inactive.File.Files, inactive.Menu, false, null, null!)); Equal(0, inactive.File.Opens); Equal(0, inactive.Timer.Stops);
        for (int failure = 0; failure < 5; ++failure)
        {
            using var f = new SaveMenuFixture();
            f.Services = failure switch { 0 => f.Services with { Label = null! }, 1 => f.Services with { Label = _ => throw new IOException("label") }, 2 => f.Services with { RulerTitle = _ => throw new IOException("title") }, 3 => f.Services with { Notice = _ => throw new IOException("notice") }, _ => f.Services with { ErrorText = () => throw new IOException("error") } };
            if (failure == 4) f.File.OpenOk = false;
            if (failure == 0) Throws<ArgumentNullException>(() => f.Save()); else Throws<IOException>(() => f.Save());
            Equal(failure == 0 ? 0 : 1, f.Timer.Stops); Equal(failure == 0 ? 0 : 1, f.Timer.Starts); Equal(failure >= 3 ? 1 : 0, f.File.Opens); f.File.Runtime.PrepareNewGame();
        }
        using var reentry = new SaveMenuFixture(); using var operation = reentry.Operation();
        reentry.Chooser.OnBegin = () => { Throws<InvalidOperationException>(() => operation.Advance()); Throws<InvalidOperationException>(() => operation.Cancel()); Throws<InvalidOperationException>(() => operation.Dispose()); }; operation.Advance(); operation.Cancel();
        using var pending = new SaveMenuFixture(); pending.File.Runtime.EndClientSession(); pending.File.Runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); pending.File.Runtime.BeginClientSession(new()); SessionClient(pending.File.Runtime).Submit(CommandRequestFor()); Throws<InvalidOperationException>(() => pending.Save()); Equal(0, pending.Timer.Stops);
    }
    private static void SaveMenuPendingCommandsRemainResponsive()
    {
        using var f = new SaveMenuFixture(); var runtime = f.File.Runtime; runtime.EndClientSession(); runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); runtime.BeginClientSession(new());
        using var operation = f.Operation(); operation.Advance(); var seat = runtime.AddParticipant(new(1), new(7)); var origin = new CommandCorrelation(new(2), new(1), new(1));
        Equal((int)CommandStatus.Pending, (int)runtime.ReceiveRequest(new(7), seat, origin, CommandRequestFor()).Status); Equal(true, runtime.AdvanceOne()); Equal(0, runtime.ExpireInteractions(49)); Equal(1, runtime.ExpireInteractions(50)); Equal(true, runtime.AdvanceOne());
        var reply = runtime.TakeReplies().Single(); Equal(origin, reply.Correlation); Equal((int)CommandStatus.Completed, (int)reply.Status); Equal(true, operation.Advance() is null);
        f.Chooser.Response = new(FileChoiceStatus.Cancelled, "draft"); Equal((int)SaveMenuResult.Cancelled, (int)operation.Advance()!.Value); Equal(0, f.File.Opens); Equal(1, f.Timer.Starts);
        using var busy = new SaveMenuFixture(); busy.File.Runtime.EndClientSession(); busy.File.Runtime.RegisterCommand(CommandDefinitionFor(_ => DecisionContinuation())); busy.File.Runtime.BeginClientSession(new());
        using var waiting = busy.Operation(); waiting.Advance(); SessionClient(busy.File.Runtime).Submit(CommandRequestFor()); busy.Chooser.Response = new(FileChoiceStatus.Accepted, "busy.sav");
        Throws<InvalidOperationException>(() => waiting.Advance()); Equal(0, busy.File.Opens); Equal("busy.sav", busy.Menu.Filename); Equal((int)SaveMenuResult.Failed, (int)waiting.Result!.Value); Equal(1, busy.Timer.Starts);
    }
}
