using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

public sealed class ClassicSaveMenuState { public bool SessionActive; public string Filename = ""; }
public readonly record struct LocalCalendar(int Month, int Day, int Year, int Hour, int Minute, int Second);
public sealed record SaveNotice(string Key, OwnedList<string> Arguments);
public interface ICountdownControl { void Stop(bool notify); void Start(bool notify); }
public sealed record SaveMenuServices(IFileStorage Output, ICountdownControl Countdown, Func<LocalCalendar> LocalTime,
    Func<string, string> Lowercase, Func<int, string> Label, Func<int, string> RulerName, Func<int, string> RulerTitle,
    Func<int, string> CivilizationPlural, Func<int, string> FormatYear, Func<string> ErrorText, Action<SaveNotice> Notice)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Output); ArgumentNullException.ThrowIfNull(Countdown); ArgumentNullException.ThrowIfNull(LocalTime);
        ArgumentNullException.ThrowIfNull(Lowercase); ArgumentNullException.ThrowIfNull(Label); ArgumentNullException.ThrowIfNull(RulerName);
        ArgumentNullException.ThrowIfNull(RulerTitle); ArgumentNullException.ThrowIfNull(CivilizationPlural); ArgumentNullException.ThrowIfNull(FormatYear);
        ArgumentNullException.ThrowIfNull(ErrorText); ArgumentNullException.ThrowIfNull(Notice);
    }
}
public enum SaveMenuResult { Inactive, Cancelled, Saved, Failed }
/// <summary>F06 §11 production save interaction. One phase per Advance, no UI dispatcher or participant binding.
/// Owns a chooser and explicit world pin until completion. Dispose abandons unfinished work best-effort;
/// explicit Advance/Cancel propagate failures. Required providers and runtime must outlive this operation.</summary>
public sealed class ClassicSaveOperation : IDisposable
{
    private readonly SessionRuntime runtime;
    private readonly ClassicFileState files;
    private readonly ClassicSaveMenuState menu;
    private readonly bool scenario;
    private readonly SaveMenuServices? services;
    private readonly IFileChooser? chooser;
    private SessionActivity? activity;
    private bool started, stopped, advancing, dismissed;
    public SaveMenuResult? Result { get; private set; }
    public ClassicSaveOperation(SessionRuntime runtime, ClassicFileState files, ClassicSaveMenuState menu, bool scenario, SaveMenuServices? services, IFileChooser? chooser)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(files); ArgumentNullException.ThrowIfNull(menu);
        this.runtime = runtime; this.files = files; this.menu = menu; this.scenario = scenario; this.services = services; this.chooser = chooser;
    }
    private static string Text(string value) { int end = value.IndexOf('\0'); return end < 0 ? value : value[..end]; }
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static SaveNotice Notice(string key, params string[] arguments) => new(key, new(arguments.Concat(Enumerable.Repeat("", 5 - arguments.Length))));
    private FileChoiceRequest PrepareRequest()
    {
        var s = services!; var world = runtime.World; var mode = world.Globals.Multiplayer; int year = world.Globals.Year, civ = world.Multiplayer.LocalHuman;
        bool monthly = (scenario || (world.Globals.GameFlags & 0x80) != 0) && world.Scenario.YearIncrement < 0;
        files.Extension = ClassicGameFiles.SelectExtension(scenario, mode, files.Extension);
        string name = Text(s.RulerName(civ)); var prefix = (name[..Math.Min(2, name.Length)] + "_").ToCharArray();
        for (int i = 0; i < prefix.Length; ++i) if (prefix[i] is >= ' ' and <= '@') prefix[i] = '_';
        string suggestion = new string(prefix) + (year < 0 ? "b" : "a") + Number(Math.Abs(year));
        if (mode == GameMode.Email && !scenario)
        {
            var date = s.LocalTime(); suggestion += " " + Number(date.Month) + "-" + Number(date.Day) + "-" + Number(date.Year) + " " + Number(date.Hour) + "-" + Number(date.Minute) + "-" + Number(date.Second);
        }
        if (monthly) { int month = year < 0 ? 11 - Math.Abs(year % 12) : Math.Abs(year % 12); suggestion = Number(Math.Abs(year / 12)) + Text(s.Label(420 + month)); }
        suggestion = Text(s.Lowercase(suggestion + files.Extension));
        if (!files.Extension.StartsWith('.')) throw new ArgumentException("Invalid save extension");
        return new(true, false, s.Label(scenario ? 416 : 239), files.Text.Search.Length == 0 ? suggestion : files.Text.Search + "\\" + suggestion,
            files.Extension[1..], ClassicFileChoice.Types(mode, scenario, false, true, s.Label));
    }
    private SaveNotice DescribeSave()
    {
        var world = runtime.World; var mode = world.Globals.Multiplayer; int civ = world.Multiplayer.LocalHuman, year = world.Globals.Year, difficulty = (int)world.Globals.Difficulty;
        if (scenario) return Notice("SCENOK", Encoding.Latin1.GetString(world.Scenario.Name.ToArray()));
        if (mode == GameMode.Email) return Notice("EMAILSAVED");
        var rules = runtime.Process.Rules; string difficultyName = Encoding.Latin1.GetString(rules.Text.Text(rules.Names.Difficulties[difficulty]).ToArray());
        var s = services!; return Notice("SAVEOK", Text(s.RulerTitle(civ)), Text(s.RulerName(civ)), Text(s.CivilizationPlural(civ)), Text(s.FormatYear(year)), difficultyName);
    }
    private SaveMenuResult Accept(FileChoiceResult choice)
    {
        if (choice.Status == FileChoiceStatus.Cancelled) return SaveMenuResult.Cancelled;
        var s = services!;
        if (choice.Status == FileChoiceStatus.Failed) { s.Notice(Notice("SAVEERROR", choice.Error)); return SaveMenuResult.Failed; }
        menu.Filename = choice.Path; var notice = DescribeSave(); if (notice.Key == "EMAILSAVED") notice = Notice("EMAILSAVED", choice.Path);
        if (ClassicGameFiles.Save(runtime, files, choice.Path, scenario, s.Output) != CoreSaveStatus.Success) { s.Notice(Notice("SAVEERROR", s.ErrorText())); return SaveMenuResult.Failed; }
        s.Notice(notice); return SaveMenuResult.Saved;
    }
    private void Finish(SaveMenuResult result)
    {
        Result = result; Exception? failure = null;
        // Mark before invoking external cleanup: even a failing provider is attempted once.
        if (!dismissed) { dismissed = true; try { chooser?.Cancel(); } catch (Exception error) { failure = error; } }
        bool restart = stopped; stopped = false;
        try { if (restart) services!.Countdown.Start(true); }
        catch (Exception error) { failure ??= error; }
        finally { activity?.Dispose(); activity = null; }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    public SaveMenuResult? Advance()
    {
        if (advancing) throw new InvalidOperationException("Cannot recursively advance a save workflow");
        if (Result.HasValue) return Result; advancing = true;
        try
        {
            if (!started)
            {
                if (!menu.SessionActive) { Finish(SaveMenuResult.Inactive); return Result; }
                ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(chooser); services.Validate();
                activity = runtime.BeginActivity(); services.Countdown.Stop(true); stopped = true; started = true; chooser.Begin(PrepareRequest()); return null;
            }
            var choice = chooser!.Poll(); if (choice is null) return null;
            Finish(Accept(ClassicFileChoice.Validate(choice))); return Result;
        }
        catch { if (!Result.HasValue) { try { Finish(SaveMenuResult.Failed); } catch { } } throw; }
        finally { advancing = false; }
    }
    public SaveMenuResult Cancel()
    {
        if (advancing) throw new InvalidOperationException("Cannot cancel a save workflow during a phase");
        if (!Result.HasValue) Finish(SaveMenuResult.Cancelled); return Result!.Value;
    }
    public void Dispose()
    {
        if (advancing) throw new InvalidOperationException("Cannot dispose a save workflow during a phase");
        if (!Result.HasValue) { try { Finish(SaveMenuResult.Cancelled); } catch { } }
    }
}
public static class ClassicSaveMenu
{
    private sealed class ImmediateChooser(Func<FileChoiceRequest, FileChoiceResult> choose) : IFileChooser
    {
        private FileChoiceResult? result;
        public void Begin(FileChoiceRequest request) => result = ClassicFileChoice.Choose(request, choose);
        public FileChoiceResult? Poll() { var selected = result; result = null; return selected; }
        public void Cancel() => result = null;
    }
    public static SaveMenuResult Save(SessionRuntime runtime, ClassicFileState files, ClassicSaveMenuState menu, bool scenario, SaveMenuServices? services, Func<FileChoiceRequest, FileChoiceResult> choose)
    {
        using var operation = new ClassicSaveOperation(runtime, files, menu, scenario, services, new ImmediateChooser(choose));
        return operation.Advance() ?? operation.Advance()!.Value;
    }
}
