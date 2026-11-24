using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>F06 §11 local loaded-unit repair with actual retained U01/V01 deletion.
/// The owner establishes the mutation lane; stack validation owns placement/diagnostic policy.</summary>
public sealed class ClassicLoadedUnitCleanup(GameState world, StackOperations stacks,
    CivilizationElimination lifecycle, EspionageIncidents incidents)
{
    private GameState World => world;
    public Operation Begin(string call) => new(this, call);
    public sealed class Operation : IDisposable
    {
        private readonly ClassicLoadedUnitCleanup parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(ClassicLoadedUnitCleanup parent, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public LifecycleDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed loaded-unit cleanup cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                if (parent.World.Globals.Multiplayer >= OpenCiv2.Contracts.GameMode.Tcp)
                { throw new InvalidOperationException("Retained loaded-unit cleanup requires local gameplay"); }
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<LifecycleDecision?> Execute(Operation op, string call)
    {
        world.Multiplayer.NetworkHost = true;
        for (int slot = 0; slot < world.Entities.Units.End; ++slot)
        {
            if (!world.Entities.Units.Storage[slot].Token.Occupied)
            { world.Entities.Units.Storage[slot].Previous = world.Entities.Units.Storage[slot].Next = null; }
            else
            {
                stacks.Validate(slot);
                var position = world.Entities.Units.Storage[slot].Position;
                if (position.X < 0 || position.Y < 0)
                {
                    using var child = lifecycle.BeginDelete(slot, false, call + "/unit/" + slot, incidents);
                    while (child.Result is null)
                    {
                        var pending = child.ResumeDecision(op.TakeReply());
                        if (child.Result is null)
                        { yield return pending; }
                    }
                    op.Report(child.PresentationFailures);
                }
            }
        }
    }
}
