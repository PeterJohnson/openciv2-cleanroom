using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>S02 §9 MakeAggression and GiveTechnology with actual V04/V03 children.
/// Indirection is resolved once; the child owns treaty and advance semantics.</summary>
public sealed class ScenarioCivilizationActions(GameState world, DiplomaticWar war, ResearchGrants grants)
{
    public int MakeAggression(Event entry)
    {
        MapKnowledge.RequireLocal(world);
        int who = ScenarioActions.ResolveCivilization(entry, entry.Values.AggressionWhoCiv);
        int whom = ScenarioActions.ResolveCivilization(entry, entry.Values.AggressionWhomCiv);
        if (whom is < 1 or > 7 || !world.Globals.CivsAlive.Contains(whom)
            || who is < 1 or > 7 || !world.Globals.CivsAlive.Contains(who))
        { return 0; }
        return war.Force(who, whom) ? 1 : 0;
    }

    public GrantOperation BeginGrant(Event entry, string call) => new(world, grants, entry, call);
    public sealed class GrantOperation : IDisposable
    {
        private readonly GameState world;
        private readonly ResearchGrants grants;
        private readonly Event entry;
        private readonly string call;
        private ResearchGrants.Operation? child;
        private bool started, disposed, faulted;
        public int? Result { get; private set; }
        public ResearchGrantDecision? Pending => child?.Pending;
        public IReadOnlyList<Exception> PresentationFailures => child?.PresentationFailures ?? [];
        internal GrantOperation(GameState world, ResearchGrants grants, Event entry, string call)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.world = world;
            this.grants = grants;
            this.entry = entry;
            this.call = call;
        }
        public ResearchGrantDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed scenario technology grant cannot resume"); }
            if (Result is not null)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(world);
                if (!started)
                {
                    started = true;
                    int recipient = ScenarioActions.ResolveCivilization(entry, entry.Values.GiveTechCiv);
                    int advance = entry.Values.GiveTech;
                    if (recipient is < 1 or > 7 || !world.Globals.CivsAlive.Contains(recipient))
                    { Result = 0; return null; }
                    child = grants.Begin(recipient, advance, 0, false, call + "/grant");
                    answer = null;
                }
                var pending = child!.Resume(answer);
                if (child.Completed)
                { Result = 1; }
                return pending;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
}
