using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed class ClassicLoadDialogState { public string ChosenPath = ""; public int LoadedFileKind; }
public sealed record LoadDialogServices(CoreLoadServices Input, Func<string, string> UppercasePath, Action<int> CloseMapWindow,
    Func<string, string, OwnedList<string>> Enumerate, Func<OwnedList<EventInput>?> LoadEvents, Action ScenarioLoaded,
    Action SynchronizeMusic, Action? PresentTitle, Action<bool> ConfigureSound, Action<CoreLoadStatus> LoadNotice,
    IUnitDeletionEffects Deletion, List<StackDiagnostic>? Diagnostics = null, Action? Placement = null)
{
    internal void Validate(bool selected, bool showTitle)
    {
        ArgumentNullException.ThrowIfNull(Input); ArgumentNullException.ThrowIfNull(Input.ChangeDirectory);
        if (!selected) return;
        Input.Validate(); ArgumentNullException.ThrowIfNull(UppercasePath); ArgumentNullException.ThrowIfNull(CloseMapWindow); ArgumentNullException.ThrowIfNull(Enumerate);
        ArgumentNullException.ThrowIfNull(LoadEvents); ArgumentNullException.ThrowIfNull(ScenarioLoaded); ArgumentNullException.ThrowIfNull(SynchronizeMusic);
        if (showTitle) ArgumentNullException.ThrowIfNull(PresentTitle);
        ArgumentNullException.ThrowIfNull(ConfigureSound); ArgumentNullException.ThrowIfNull(LoadNotice); ArgumentNullException.ThrowIfNull(Deletion);
    }
}
public static class ClassicLoadDialog
{
    private static void Publish(SessionRuntime runtime) { if (runtime.Active) runtime.Publish(); }
    private sealed class PublishedDeletion(SessionRuntime runtime, IUnitDeletionEffects effects) : IUnitDeletionEffects
    {
        public void UnitRemoved(Position position) { Publish(runtime); effects.UnitRemoved(position); }
        public void RefreshHomeCity(int city) { Publish(runtime); effects.RefreshHomeCity(city); }
        public void CheckCivilization(int owner, int killer) { Publish(runtime); effects.CheckCivilization(owner, killer); }
        public void SynchronizeDeletion(Position position, CitySlot? home) { Publish(runtime); effects.SynchronizeDeletion(position, home); }
    }
    private static string Alternative(SessionRuntime runtime, string path, LoadDialogServices services, string call)
    {
        Publish(runtime); if (!services.UppercasePath(path).Contains(".SCN", StringComparison.Ordinal)) return path;
        string directory = path[..path.LastIndexOf('\\')]; Publish(runtime); var entries = services.Enumerate(directory, "*.ALT");
        if (entries.Count == 0) return path;
        if (entries.Count > 65535) throw new ArgumentOutOfRangeException(nameof(services), "Scenario alternatives exceed admitted enumeration backing");
        int draw = runtime.GameplayRandom.PrimaryCode(new ChoiceKey("F06.scenario-alternative", call)); int choice = draw * (entries.Count + 1) / 32768;
        if (choice == 0) return path;
        Publish(runtime); string name = entries[choice - 1];
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(['\\', '/', ':', '\0']) >= 0 || name is "." or "..") throw new ArgumentException("Scenario alternative is not an unqualified filename");
        return directory + "\\" + name;
    }
    /// <summary>F06 §11 accepted-file wrapper over the real core loader and U01 cleanup. Null selection cancels.
    /// Semantic services retain completed effects on failure. No chooser, S02 parser/executor or UI is invented.</summary>
    public static CoreLoadStatus? Load(SessionRuntime runtime, ClassicFileState files, ClassicLoadDialogState dialog, string? selectedPath, bool showTitle, LoadDialogServices services, StackBudget budget, string call)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(files); ArgumentNullException.ThrowIfNull(dialog); ArgumentNullException.ThrowIfNull(services);
        services.Validate(selectedPath is not null, showTitle);
        if (selectedPath is not null) { ArgumentNullException.ThrowIfNull(budget); ArgumentException.ThrowIfNullOrEmpty(call); }
        using var operation = selectedPath is null ? null : new ClassicLoadOperation(runtime, files, services.Input);
        void Home() { files.Text.Working = files.Text.Home; Publish(runtime); services.Input.ChangeDirectory(files.Text.Working); }
        try
        {
            if (selectedPath is null) { Home(); Publish(runtime); return null; }
            if (selectedPath.Contains('\0') || !selectedPath.Contains('\\')) throw new ArgumentException("Load dialog requires a qualified path without NUL", nameof(selectedPath));
            for (int index = 1; index < 8; ++index) { Publish(runtime); services.CloseMapWindow(index); }
            selectedPath = Alternative(runtime, selectedPath, services, call); var status = operation!.Load(selectedPath, call);
            if (status != CoreLoadStatus.Success) { Publish(runtime); services.LoadNotice(status); dialog.LoadedFileKind = 0; Publish(runtime); return status; }
            Publish(runtime); services.Input.Flush(); var world = runtime.World; var events = world.Events;
            if (!events.Loaded && (world.Globals.GameFlags & 0x80) != 0)
            {
                Publish(runtime);
                if (services.Input.Exists(files.ScenarioDirectory, "EVENTS." + files.Text.Extension))
                {
                    Publish(runtime); if (services.LoadEvents() is { } catalog) { events.Collection.Install(catalog); events.Loaded = true; }
                }
            }
            if (!events.Loaded) events.Collection.Reset();
            world.Globals.GameOptions = runtime.Process.OrdinaryPreferences.GameOptions;
            Publish(runtime); services.ConfigureSound((world.Globals.GameOptions & 0x10) != 0);
            if ((world.Globals.GameOptions & 8) != 0 && events.Loaded) { Publish(runtime); services.ScenarioLoaded(); }
            Publish(runtime); services.SynchronizeMusic(); if (showTitle) { Publish(runtime); services.PresentTitle!(); }
            Home(); Action? placement = services.Placement is null ? null : () => { Publish(runtime); services.Placement(); };
            ClassicLoadedUnits.Cleanup(world, runtime.Process.Rules.Definitions, budget, new PublishedDeletion(runtime, services.Deletion), services.Diagnostics, placement);
            dialog.ChosenPath = selectedPath; Publish(runtime); return status;
        }
        catch { try { Publish(runtime); } catch { } throw; }
    }
}
