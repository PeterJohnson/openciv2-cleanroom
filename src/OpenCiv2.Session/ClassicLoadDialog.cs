using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed class ClassicLoadDialogState
{
    public string ChosenPath = "";
    public int LoadedFileKind;
}
public sealed record LoadDialogServices(
    CoreLoadServices Input,
    Func<string, string> UppercasePath,
    Action<int> CloseMapWindow,
    Func<string, string, OwnedList<string>> Enumerate,
    Func<OwnedList<EventInput>?> LoadEvents,
    Action ScenarioLoaded,
    Action SynchronizeMusic,
    Action? PresentTitle,
    Action<bool> ConfigureSound,
    Action<CoreLoadStatus> LoadNotice,
    IUnitDeletionEffects Deletion,
    List<StackDiagnostic>? Diagnostics = null,
    Action? Placement = null)
{
    internal void Validate(bool selected, bool showTitle, bool retained = false)
    {
        ArgumentNullException.ThrowIfNull(Input);
        ArgumentNullException.ThrowIfNull(Input.ChangeDirectory);
        if (!selected)
        {
            return;
        }

        Input.Validate();
        ArgumentNullException.ThrowIfNull(UppercasePath);
        ArgumentNullException.ThrowIfNull(CloseMapWindow);
        ArgumentNullException.ThrowIfNull(Enumerate);
        ArgumentNullException.ThrowIfNull(LoadEvents);
        if (!retained)
        { ArgumentNullException.ThrowIfNull(ScenarioLoaded); }
        ArgumentNullException.ThrowIfNull(SynchronizeMusic);
        if (showTitle)
        {
            ArgumentNullException.ThrowIfNull(PresentTitle);
        }

        ArgumentNullException.ThrowIfNull(ConfigureSound);
        ArgumentNullException.ThrowIfNull(LoadNotice);
        if (!retained)
        { ArgumentNullException.ThrowIfNull(Deletion); }
    }
}
public static class ClassicLoadDialog
{
    private static void Publish(SessionRuntime runtime)
    {
        if (runtime.Active)
        {
            runtime.Publish();
        }
    }

    private sealed class PublishedDeletion(SessionRuntime runtime, IUnitDeletionEffects effects) : IUnitDeletionEffects
    {
        public void UnitRemoved(MapPoint position)
        {
            Publish(runtime);
            effects.UnitRemoved(position);
        }

        public void RefreshHomeCity(int city)
        {
            Publish(runtime);
            effects.RefreshHomeCity(city);
        }

        public void CheckCivilization(int owner, int killer)
        {
            Publish(runtime);
            effects.CheckCivilization(owner, killer);
        }

        public void SynchronizeDeletion(MapPoint position, CitySlot? home)
        {
            Publish(runtime);
            effects.SynchronizeDeletion(position, home);
        }
    }

    private static string Alternative(SessionRuntime runtime, string path, LoadDialogServices services, string call)
    {
        Publish(runtime);
        if (!services.UppercasePath(path).Contains(".SCN", StringComparison.Ordinal))
        {
            return path;
        }

        string directory = path[..path.LastIndexOf('\\')];
        Publish(runtime);
        var entries = services.Enumerate(directory, "*.ALT");
        if (entries.Count == 0)
        {
            return path;
        }

        if (entries.Count > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(services),
                "Scenario alternatives exceed admitted enumeration backing");
        }

        int draw = runtime.GameplayRandom.PrimaryCode(new ChoiceKey("F06.scenario-alternative", call));
        int choice = draw * (entries.Count + 1) / 32768;
        if (choice == 0)
        {
            return path;
        }

        Publish(runtime);
        string name = entries[choice - 1];
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(['\\', '/', ':', '\0']) >= 0 || name is "." or "..")
        {
            throw new ArgumentException("Scenario alternative is not an unqualified filename");
        }

        return directory + "\\" + name;
    }

    /// <summary>Actual post-load children are constructed after content/world installation.
    /// Their ports must refer to the newly loaded map, rules and compatibility state.</summary>
    public sealed record RetainedChildren(ScenarioTriggers Events, ClassicLoadedUnitCleanup Units);

    public static Operation Begin(SessionRuntime runtime, ClassicFileState files, ClassicLoadDialogState dialog,
        string? selectedPath, bool showTitle, LoadDialogServices services, StackBudget budget, string call,
        Func<RetainedChildren> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        return new(runtime, files, dialog, selectedPath, showTitle, services, budget, call, children);
    }

    /// <summary>F06 §11 synchronous compatibility path. Retained gameplay callers use Begin;
    /// no reply or gameplay child is invented by this wrapper.</summary>
    public static CoreLoadStatus? Load(SessionRuntime runtime, ClassicFileState files, ClassicLoadDialogState dialog,
        string? selectedPath, bool showTitle, LoadDialogServices services, StackBudget budget, string call)
    {
        using var operation = new Operation(runtime, files, dialog, selectedPath, showTitle, services, budget, call, null);
        while (!operation.Completed)
        {
            if (operation.Resume() is not null)
            { throw new InvalidOperationException("Synchronous loading cannot answer a gameplay decision"); }
        }
        return operation.Result;
    }

    /// <summary>Trusted host load workflow. The owner services Pending through the appropriate
    /// participant UI and does not advance unrelated commands while this standalone workflow runs.</summary>
    public sealed class Operation : IDisposable
    {
        private readonly SessionRuntime runtime;
        private readonly ClassicLoadOperation? loader;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public CoreLoadStatus? Result { get; internal set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(SessionRuntime runtime, ClassicFileState files, ClassicLoadDialogState dialog,
            string? selectedPath, bool showTitle, LoadDialogServices services, StackBudget budget, string call,
            Func<RetainedChildren>? children)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(files);
            ArgumentNullException.ThrowIfNull(dialog);
            ArgumentNullException.ThrowIfNull(services);
            services.Validate(selectedPath is not null, showTitle, children is not null);
            if (selectedPath is not null)
            {
                ArgumentNullException.ThrowIfNull(budget);
                ArgumentException.ThrowIfNullOrEmpty(call);
            }
            this.runtime = runtime;
            loader = selectedPath is null ? null : new ClassicLoadOperation(runtime, files, services.Input);
            steps = Execute(this, runtime, files, dialog, selectedPath, showTitle, services, budget, call, children).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal CoreLoadStatus Load(string path, string call) => loader!.Load(path, call);
        public LifecycleDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed load dialog cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { Publish(runtime); return Pending = steps.Current; }
                Completed = true;
                Release();
                return null;
            }
            catch
            {
                faulted = true;
                Release();
                try
                { Publish(runtime); }
                catch { }
                throw;
            }
        }
        private void Release() { try { steps.Dispose(); } finally { loader?.Dispose(); } }
        public void Dispose() { disposed = true; Release(); }
    }
    private static IEnumerable<LifecycleDecision?> Execute(Operation op, SessionRuntime runtime,
        ClassicFileState files, ClassicLoadDialogState dialog, string? selectedPath, bool showTitle,
        LoadDialogServices services, StackBudget budget, string call, Func<RetainedChildren>? children)
    {
        void Home()
        {
            files.Text.Working = files.Text.Home;
            Publish(runtime);
            services.Input.ChangeDirectory(files.Text.Working);
        }
        if (selectedPath is null)
        {
            Home();
            Publish(runtime);
            yield break;
        }
        if (selectedPath.Contains('\0') || !selectedPath.Contains('\\'))
        { throw new ArgumentException("Load dialog requires a qualified path without NUL", nameof(selectedPath)); }
        for (int index = 1; index < EntityLimits.Civilizations; ++index)
        { Publish(runtime); services.CloseMapWindow(index); }
        selectedPath = Alternative(runtime, selectedPath, services, call);
        var status = op.Load(selectedPath, call);
        if (status != CoreLoadStatus.Success)
        {
            Publish(runtime);
            services.LoadNotice(status);
            dialog.LoadedFileKind = 0;
            Publish(runtime);
            op.Result = status;
            yield break;
        }
        Publish(runtime);
        services.Input.Flush();
        var world = runtime.World;
        var events = world.Events;
        if (!events.Loaded && world.Globals.Status.Scenario)
        {
            Publish(runtime);
            if (services.Input.Exists(files.ScenarioDirectory, "EVENTS." + files.Text.Extension))
            {
                Publish(runtime);
                if (services.LoadEvents() is { } catalog)
                { events.Collection.Install(catalog); events.Loaded = true; }
            }
        }
        if (!events.Loaded)
        { events.Collection.Reset(); }
        ClassicConfigurationMetadata.CopyOptions(runtime.Process, world, initialize: false);
        world.Globals.GameOptions = runtime.Process.OrdinaryPreferences.GameOptions;
        Publish(runtime);
        services.ConfigureSound(world.Globals.GameOptions.Sound);
        var retained = children?.Invoke();
        if (children is not null)
        {
            ArgumentNullException.ThrowIfNull(retained);
            ArgumentNullException.ThrowIfNull(retained.Events);
            ArgumentNullException.ThrowIfNull(retained.Units);
        }
        if (world.Globals.GameOptions.Music && events.Loaded)
        {
            Publish(runtime);
            if (retained is null)
            { services.ScenarioLoaded(); }
            else
            {
                using var trigger = retained.Events.BeginLoaded(call + "/scenario-loaded");
                while (trigger.Result is null)
                {
                    var pending = trigger.Resume(op.TakeReply());
                    if (trigger.Result is null)
                    { yield return pending; }
                }
                op.Report(trigger.PresentationFailures);
            }
        }
        Publish(runtime);
        services.SynchronizeMusic();
        if (showTitle)
        { Publish(runtime); services.PresentTitle!(); }
        Home();
        if (retained is null)
        {
            Action? placement = services.Placement is null ? null : () =>
            { Publish(runtime); services.Placement(); };
            ClassicLoadedUnits.Cleanup(world, runtime.Process.Rules.Definitions, budget,
                new PublishedDeletion(runtime, services.Deletion), services.Diagnostics, placement);
        }
        else
        {
            using var cleanup = retained.Units.Begin(call + "/cleanup");
            while (!cleanup.Completed)
            {
                var pending = cleanup.Resume(op.TakeReply());
                if (!cleanup.Completed)
                { yield return pending; }
            }
            op.Report(cleanup.PresentationFailures);
        }
        dialog.ChosenPath = selectedPath;
        Publish(runtime);
        op.Result = status;
    }
}
