using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public abstract record UnitProductionDecision;
public sealed record SettlerAbandonment(int Owner, CityTarget City, string CityName, UnitType Type,
    string UnitName) : UnitProductionDecision;
public sealed record UnitProductionLifecycle(LifecycleDecision Decision) : UnitProductionDecision;

public sealed partial class CityUnitProduction
{
    public sealed class Operation : IDisposable
    {
        private readonly CityUnitProduction parent;
        private readonly CivilizationElimination lifecycle;
        private readonly EspionageIncidents incidents;
        private readonly string call;
        private readonly int candidateBudget;
        private readonly IEnumerator<ProductionWork> steps;
        private CivilizationElimination.Operation? child;
        private Action<int>? childResult;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        public UnitProductionResult? Result { get; private set; }
        public UnitProductionDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();

        internal Operation(CityUnitProduction parent, CitySlot slot, string call,
            CivilizationElimination lifecycle, EspionageIncidents incidents, int candidateBudget)
        {
            ArgumentNullException.ThrowIfNull(lifecycle);
            ArgumentNullException.ThrowIfNull(incidents);
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateBudget);
            this.parent = parent;
            this.lifecycle = lifecycle;
            this.incidents = incidents;
            this.call = call;
            this.candidateBudget = candidateBudget;
            steps = parent.Steps(slot, call, result => Result = result).GetEnumerator();
        }

        public UnitProductionDecision? ResumeDecision(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("A failed unit production cannot resume");
            }

            if (Result is not null)
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

                if (Pending is SettlerAbandonment)
                {
                    steps.Current.Reply!(reply!.Value.Selected);
                    reply = null;
                }
                Pending = null;
                while (true)
                {
                    if (child is not null)
                    {
                        var decision = child.ResumeDecision(reply);
                        reply = null;
                        if (decision is not null)
                        {
                            return Pending = new UnitProductionLifecycle(decision);
                        }

                        if (child.Result is null)
                        {
                            return null;
                        }

                        failures.AddRange(child.PresentationFailures);
                        childResult?.Invoke(child.Result.Value);
                        child.Dispose();
                        child = null;
                        childResult = null;
                    }
                    if (!steps.MoveNext())
                    { steps.Dispose(); return null; }
                    var action = steps.Current;
                    if (action.Kind == ProductionWorkKind.Abandon)
                    {
                        return Pending = action.Choice;
                    }

                    childResult = action.Reply;
                    child = action.Kind switch
                    {
                        ProductionWorkKind.DeleteUnit => lifecycle.BeginDelete(action.Target, true, call + "/cancel", incidents, candidateBudget),
                        ProductionWorkKind.RemoveCity => lifecycle.BeginRemoveCity(parent.Removal, action.Target, call + "/remove", incidents, candidateBudget),
                        ProductionWorkKind.CheckOwner => lifecycle.Begin(action.Target, -1, call + "/check", incidents, candidateBudget),
                        _ => throw new InvalidOperationException("Unknown unit production lifecycle work")
                    };
                }
            }
            catch { faulted = true; Release(); throw; }
        }
        private void Release() { child?.Dispose(); child = null; steps.Dispose(); }
        public void Dispose() { disposed = true; Release(); }
    }
}
