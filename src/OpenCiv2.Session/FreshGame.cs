using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

public sealed partial class SessionRuntime
{
    /// <summary>X01/F02 fresh-game isolation, before chosen content or setup. This is separate
    /// from load/resume and client-session changes. Preferences, generators and random contexts survive.</summary>
    public void PrepareNewGame()
    {
        state.RequireQuiescent();
        var world = new GameState(state.Process); var rules = new RuleState(); rules.Text.Initialize();
        foreach (ref var civilization in world.Civilizations.AsSpan())
        {
            foreach (ref var goal in (Span<CivilizationGoal>)civilization.Goals) goal = new(0, 0, -1, 0);
            foreach (ref var goal in (Span<CivilizationGoal>)civilization.StandingGoals) goal = new(0, 0, -1, 0);
        }
        // All preparation precedes revocation. No service callbacks interrupt replacement.
        EndClientSession(); state.Requests.Clear(); state.Replies.Clear(); state.World = world;
        state.Process.Rules.SwapContents(rules); state.Process.TextParameters.Clear(); state.Process.Fallback.Value = new(); state.Revision = 0;
    }
}
