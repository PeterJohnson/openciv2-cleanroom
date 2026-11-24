using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A08 §6 ordered AI technology exchange with actual advance valuation,
/// bilateral tribute and retained V03 grants. The caller owns meeting admission.</summary>
public sealed class AiAdvanceExchange(GameState world, RuleState rules, GameplayRandom random, ResearchGrants grants)
{
    private GameState World => world;
    private ResearchGrants Grants => grants;
    public Operation Begin(int first, int second, string call) => new(this, first, second, call);

    private (int Owner, int Advance, int Source)[] Select(int first, int second, string call)
    {
        // A08 Appendix A and S01 use the same scenario option for conquest and AI exchange.
        if (world.Globals.Status.Scenario && world.Scenario.Flags.NoConquestTechnology)
        { return []; }
        int leader = world.Globals.CivByRank[7];
        bool leaderFarAhead = world.Globals.HumanPlayers.Contains(leader)
            && world.Globals.Difficulty != Difficulty.Chieftain
            && world.Civilizations[leader].CityCount >= 5 && world.Globals.Turn >= 201
            && world.Civilizations[leader].NumAdvances > world.Civilizations[first].NumAdvances
            && world.Civilizations[leader].NumAdvances > world.Civilizations[second].NumAdvances;
        int forward = -1, reverse = -1, forwardScore = 0, reverseScore = 0;
        for (int advance = 0; advance < RuleLimits.Advances; ++advance)
        {
            var definition = rules.Definitions.Advances[advance];
            if (definition.Prerequisites[0] == AdvanceReference.Never && definition.Prerequisites[1] == AdvanceReference.Never)
            { continue; }
            if (!Knows(first, advance) && Knows(second, advance))
            {
                int draw = random.PrimaryModulo(new ChoiceKey("A08.exchange.forward", call, advance), 3);
                int score = unchecked(draw + AdvanceValuation.Value(world, rules, first, advance));
                if (forward < 0 || score > forwardScore)
                { forward = advance; forwardScore = score; }
            }
            else if (!Knows(second, advance) && Knows(first, advance))
            {
                int draw = random.PrimaryModulo(new ChoiceKey("A08.exchange.reverse", call, advance), 3);
                int score = unchecked(draw + AdvanceValuation.Value(world, rules, second, advance));
                if (reverse < 0 || score > reverseScore)
                { reverse = advance; reverseScore = score; }
            }
        }
        if (forward >= 0 && reverse >= 0)
        { return [(first, forward, second), (second, reverse, first)]; }
        if (forward >= 0 && Gift(first, second, leaderFarAhead))
        {
            DiplomaticRelations.Set(world, first, second, new() { Tribute = true });
            return [(first, forward, second)];
        }
        if (reverse >= 0 && Gift(second, first, leaderFarAhead))
        {
            DiplomaticRelations.Set(world, first, second, new() { Tribute = true });
            return [(second, reverse, first)];
        }
        return [];
    }

    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
    private bool Gift(int recipient, int source, bool leaderFarAhead) =>
        world.Civilizations[recipient].Treaties[source].Alliance
        && (world.Civilizations[recipient].NumAdvances + 2 * (6 - (int)world.Globals.Difficulty)
            < world.Civilizations[source].NumAdvances || leaderFarAhead)
        && !world.Civilizations[recipient].Treaties[source].Tribute;

    public sealed class Operation(AiAdvanceExchange parent, int first, int second, string call) : IDisposable
    {
        private (int Owner, int Advance, int Source)[]? selections;
        private int next;
        private ResearchGrants.Operation? grant;
        private bool faulted, disposed;
        private readonly List<Exception> failures = [];
        public bool Completed { get; private set; }
        public bool? Result { get; private set; }
        public ResearchGrantDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();

        public ResearchGrantDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed advance exchange cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (selections is null)
                {
                    ArgumentException.ThrowIfNullOrEmpty(call);
                    selections = parent.Select(first, second, call);
                }
                while (next < selections.Length)
                {
                    var selected = selections[next];
                    grant ??= parent.Grants.Begin(selected.Owner, selected.Advance, selected.Source, false, $"{call}/grant/{next}");
                    Pending = grant.Resume(answer);
                    answer = null;
                    if (!grant.Completed)
                    { return Pending; }
                    failures.AddRange(grant.PresentationFailures);
                    grant.Dispose();
                    grant = null;
                    ++next;
                }
                Result = selections.Length != 0;
                Completed = true;
                return null;
            }
            catch { faulted = true; grant?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; grant?.Dispose(); }
    }
}
