using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>S02 §9 DestroyACivilization, with one-level recipient resolution and actual V01 removal.</summary>
public sealed class ScenarioDestruction(GameState world, CivilizationRemoval removal)
{
    public Operation Begin(Event entry, string call) => new(world, removal, entry, call);
    public sealed class Operation(GameState world, CivilizationRemoval removal, Event entry, string call) : IDisposable
    {
        private CivilizationRemoval.Operation? child;
        private bool started, disposed, faulted;
        public int? Result { get; private set; }
        public LifecycleDecision? Pending => child?.Pending;
        public IReadOnlyList<Exception> PresentationFailures => child?.PresentationFailures ?? [];
        public LifecycleDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed scenario destruction cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(world);
                if (!started)
                {
                    ArgumentException.ThrowIfNullOrEmpty(call);
                    started = true;
                    int recipient = ScenarioActions.ResolveCivilization(entry, entry.Values.DestroyCiv);
                    if (recipient is < 1 or >= EntityLimits.Civilizations || !world.Globals.CivsAlive.Contains(recipient))
                    { Result = 0; return null; }
                    if (world.Globals.HumanPlayers.Contains(recipient) && recipient == world.Multiplayer.LocalHuman)
                    { world.PendingEndReason = EndReason.Defeat; }
                    child = removal.Begin(recipient, 1, 1, call + "/remove");
                    reply = null;
                }
                var pending = child!.Resume(reply);
                if (child.Completed)
                { Result = 1; }
                return pending;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
