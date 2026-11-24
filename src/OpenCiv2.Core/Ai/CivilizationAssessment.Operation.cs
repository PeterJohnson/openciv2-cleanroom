using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed partial class CivilizationAssessment
{
    /// <summary>A01 assignment retains its census, captured human flag, reverse unit scan and
    /// goal weights while real V01/U01 lifecycle children finish. The caller serializes mutations.</summary>
    public sealed class Operation : IDisposable
    {
        private readonly CivilizationAssessment parent;
        private readonly int owner, candidateBudget;
        private readonly string call;
        private readonly EspionageIncidents incidents;
        private readonly IEnumerator<Work> steps;
        private CivilizationElimination.Operation? child;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();

        internal Operation(CivilizationAssessment parent, int owner, string call,
            EspionageIncidents incidents, int candidateBudget)
        {
            if (owner < 0 || owner >= EntityLimits.Civilizations)
            { throw new ArgumentOutOfRangeException(nameof(owner)); }
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentNullException.ThrowIfNull(incidents);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
            this.parent = parent;
            this.owner = owner;
            this.call = call;
            this.incidents = incidents;
            this.candidateBudget = candidateBudget;
            steps = new Assessment(parent, owner, call).Run().GetEnumerator();
        }

        public LifecycleDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed civilization assessment cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.world);
                if (Pending is not null && reply is null)
                { return Pending; }
                if (Pending is null)
                { reply = null; }
                Pending = null;
                while (true)
                {
                    if (child is not null)
                    {
                        Pending = child.ResumeDecision(reply);
                        reply = null;
                        if (child.Result is null)
                        { return Pending; }
                        failures.AddRange(child.PresentationFailures);
                        child.Dispose();
                        child = null;
                    }
                    if (!steps.MoveNext())
                    { Completed = true; steps.Dispose(); return null; }
                    child = steps.Current.Unit is { } unit
                        ? parent.elimination.BeginDestroy(parent.loss, unit, call + "/retire/" + unit, incidents, candidateBudget)
                        : parent.elimination.Begin(owner, 0, call + "/eliminate", incidents, candidateBudget);
                }
            }
            catch { faulted = true; Release(); throw; }
        }

        private void Release() { child?.Dispose(); child = null; steps.Dispose(); }
        public void Dispose() { disposed = true; Release(); }
    }
}
