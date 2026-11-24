using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>S02 §7 retained negotiation/schism predicates. The factory closes the real
/// event/contact/creation graph without substituting synchronous gameplay callbacks.</summary>
public sealed class ScenarioPermissionActions(GameState world, Func<ScenarioDispatcher> dispatcher)
{
    private GameState World => world;
    public Operation BeginNegotiation(int talker, int listener, string call) => new(this, talker, listener, false, call);
    public Operation BeginSchism(int civilization, string call) => new(this, civilization, 0, true, call);
    public sealed class Operation : IDisposable
    {
        private readonly ScenarioPermissionActions parent;
        private readonly IEnumerator<LifecycleDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool? Allowed { get; internal set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(ScenarioPermissionActions parent, int a, int b, bool schism, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, a, b, schism, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public LifecycleDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed scenario permission cannot resume"); }
            if (Allowed is not null)
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
    private IEnumerable<LifecycleDecision?> Execute(Operation op, int a, int b, bool schism, string call)
    {
        bool allowed = true;
        var events = world.Events.Collection;
        for (int i = 0; i < events.Count; ++i)
        {
            var entry = events.At(i);
            if (schism)
            {
                if (entry.Values.Trigger != EventTrigger.NoSchism || entry.Values.WhomCiv != a && entry.Values.WhomCiv != -2)
                { continue; }
                allowed = false;
            }
            else
            {
                if (entry.Values.Trigger != EventTrigger.Negotiation
                    || !ScenarioPermissions.Matches(world, entry.Values.WhoCiv, entry.Values.TalkerType, a)
                    || !ScenarioPermissions.Matches(world, entry.Values.WhomCiv, entry.Values.ListenerType, b))
                { continue; }
                allowed = !entry.Values.DenyNegotiation;
            }
            using var child = dispatcher().Begin(entry, call + "/event/" + entry.Identity);
            while (child.Result is null)
            {
                var pending = child.Resume(op.TakeReply());
                if (child.Result is null)
                { yield return pending; }
            }
            op.Report(child.PresentationFailures);
            if (!schism)
            { break; }
            if (!events.Contains(entry))
            { throw new InvalidOperationException("Scenario action invalidated the current event"); }
        }
        op.Allowed = allowed;
    }
}
