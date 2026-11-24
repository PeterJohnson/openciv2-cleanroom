using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed partial class CityFood
{
    public sealed class Operation : IDisposable
    {
        private readonly CityFood parent;
        private readonly CivilizationElimination elimination;
        private readonly EspionageIncidents incidents;
        private readonly string call;
        private readonly int candidateBudget;
        private readonly IEnumerator<WorkItem> steps;
        private CivilizationElimination.Operation? child;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        public bool? Removed { get; private set; }
        public bool Completed => Removed is not null;
        public LifecycleDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();

        internal Operation(CityFood parent, CitySlot slot, CivilizationElimination elimination,
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
            steps = parent.Steps(slot, value => Removed = value).GetEnumerator();
        }

        public LifecycleDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("A failed city food operation cannot resume");
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
                    { steps.Dispose(); return null; }
                    var action = steps.Current;
                    child = action.Kind switch
                    {
                        FoodWork.RemoveCity => elimination.BeginRemoveCity(parent.Removal, action.Target, call + "/remove", incidents, candidateBudget),
                        FoodWork.CheckOwner => elimination.Begin(action.Target, -1, call + "/check", incidents, candidateBudget),
                        FoodWork.LoseSettler => elimination.BeginEraseCargo(parent.Loss, action.Target, call + "/settler/" + action.Target, incidents, candidateBudget),
                        _ => throw new InvalidOperationException("Unknown city food lifecycle work")
                    };
                }
            }
            catch { faulted = true; Release(); throw; }
        }

        private void Release() { child?.Dispose(); child = null; steps.Dispose(); }
        public void Dispose() { disposed = true; Release(); }
    }
}
