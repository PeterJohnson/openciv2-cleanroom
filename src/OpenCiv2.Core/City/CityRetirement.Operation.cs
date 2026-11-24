using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed partial class CityRetirement
{
    public sealed class Operation : IDisposable
    {
        private readonly CityRetirement parent;
        private readonly CivilizationElimination elimination;
        private readonly EspionageIncidents incidents;
        private readonly string call;
        private readonly int candidateBudget;
        private readonly IEnumerator<Retirement> steps;
        private CivilizationElimination.Operation? child;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();

        internal Operation(CityRetirement parent, CitySlot slot, CivilizationElimination elimination,
            EspionageIncidents incidents, string call, int candidateBudget)
        {
            ArgumentNullException.ThrowIfNull(elimination);
            ArgumentNullException.ThrowIfNull(incidents);
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
            this.parent = parent;
            this.elimination = elimination;
            this.incidents = incidents;
            this.call = call;
            this.candidateBudget = candidateBudget;
            steps = parent.Steps(slot).GetEnumerator();
        }

        public LifecycleDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("A failed city retirement cannot resume");
            }

            if (Completed)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && reply is null)
                {
                    return Pending;
                }

                if (Pending is null)
                {
                    reply = null;
                }

                Pending = null;
                while (true)
                {
                    if (child is not null)
                    {
                        Pending = child.ResumeDecision(reply);
                        reply = null;
                        if (child.Result is null)
                        {
                            return Pending;
                        }

                        failures.AddRange(child.PresentationFailures);
                        child.Dispose();
                        child = null;
                    }
                    if (!steps.MoveNext())
                    { Completed = true; steps.Dispose(); return null; }
                    var action = steps.Current;
                    string childCall = call + "/retire/" + action.Unit;
                    child = action.Shortage
                        ? elimination.BeginDestroy(parent.Loss, action.Unit, childCall, incidents, candidateBudget)
                        : elimination.BeginDelete(action.Unit, true, childCall, incidents, candidateBudget);
                }
            }
            catch { faulted = true; Release(); throw; }
        }

        private void Release() { child?.Dispose(); child = null; steps.Dispose(); }
        public void Dispose() { disposed = true; Release(); }
    }
}
