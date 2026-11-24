using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>S02 §8 retained action dispatch with actual gameplay children. No early consumption
/// or recursion guard: each reached action and the final JustOnce flag are read live.</summary>
public sealed class ScenarioDispatcher(GameState world, ScenarioUnitActions units, ScenarioTerrain terrain,
    ScenarioCivilizationActions civilizations, ScenarioDestruction destruction, ScenarioPresentationActions presentation)
{
    private GameState World => world;
    public Operation Begin(Event entry, string call) => new(this, entry, call);
    public sealed class Operation : IDisposable
    {
        private readonly ScenarioDispatcher parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public int? Result { get; internal set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(ScenarioDispatcher parent, Event entry, string call)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, entry, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public LifecycleDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed scenario event cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<LifecycleDecision?> Execute(Operation op, Event entry, string call)
    {
        if (entry.Values.Actions.JustOnce && entry.Values.Actions.Consumed)
        { op.Result = 0; yield break; }
        foreach (var action in ScenarioActions.Order)
        {
            if (!ScenarioActions.Enabled(entry.Values.Actions, action))
            { continue; }
            switch (action)
            {
                case ScenarioAction.PlayWaveFile:
                    _ = presentation.Wave(entry);
                    break;
                case ScenarioAction.PlayCdTrack:
                    _ = presentation.Cd(entry);
                    break;
                case ScenarioAction.CreateUnit:
                    using (var child = units.BeginCreate(entry, call + "/create"))
                    {
                        while (!child.Completed)
                        {
                            var pending = child.Resume(op.TakeReply());
                            if (!child.Completed)
                            { yield return pending is null ? null : new LifecycleContact(pending); }
                        }
                        op.Report(child.PresentationFailures);
                    }
                    break;
                case ScenarioAction.MoveUnit:
                    _ = units.Move(entry);
                    break;
                case ScenarioAction.ChangeTerrain:
                    using (var child = terrain.Begin(entry, call + "/terrain"))
                    {
                        while (child.Result is null)
                        {
                            var pending = child.Resume(op.TakeReply());
                            if (child.Result is null)
                            { yield return pending; }
                        }
                        op.Report(child.PresentationFailures);
                    }
                    break;
                case ScenarioAction.MakeAggression:
                    _ = civilizations.MakeAggression(entry);
                    break;
                case ScenarioAction.ChangeMoney:
                    _ = ScenarioActions.ChangeMoney(world, entry);
                    break;
                case ScenarioAction.DestroyCivilization:
                    using (var child = destruction.Begin(entry, call + "/destroy"))
                    {
                        while (child.Result is null)
                        {
                            var pending = child.Resume(op.TakeReply());
                            if (child.Result is null)
                            { yield return pending; }
                        }
                        op.Report(child.PresentationFailures);
                    }
                    break;
                case ScenarioAction.GiveTechnology:
                    using (var child = civilizations.BeginGrant(entry, call + "/technology"))
                    {
                        while (child.Result is null)
                        {
                            var pending = child.Resume(op.TakeReply()?.Selected);
                            if (child.Result is null)
                            { yield return pending; }
                        }
                        op.Report(child.PresentationFailures);
                    }
                    break;
                case ScenarioAction.Text:
                    if (presentation.Text(entry) is { } text)
                    { yield return text; _ = op.TakeReply(); }
                    break;
                case ScenarioAction.SuppressWonderMovies:
                    presentation.SuppressWonders();
                    break;
                default:
                    throw new InvalidOperationException("Unknown scenario action");
            }
        }
        if (entry.Values.Actions.JustOnce)
        { entry.Values.Actions.Consumed = true; }
        op.Result = 1;
    }
}
