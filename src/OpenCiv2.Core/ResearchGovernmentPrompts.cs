using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum ResearchTutorial { Republic, Railroad, Refrigeration, Trade, Seafaring, Writing }
public sealed record ResearchGovernmentChoice(int Civilization, int Advance, Government Current, Government Proposed, bool Monarchy);
public interface IResearchGovernmentEffects
{
    int ConfirmGovernment(ResearchGovernmentChoice choice);
    void RevolutionStarting(int civilization);
    void Tutorial(int civilization, ResearchTutorial tutorial);
}

/// <summary>V03 §11 local government/tutorial prompts. The caller owns human/local/autoplay gates;
/// accepting reply1 invokes the actual V02 direct Anarchy setter, not its announced-change wrapper.</summary>
public sealed class ResearchGovernmentPrompts(GameState world, GovernmentChanges governments, IResearchGovernmentEffects effects)
{
    private Government Current(int c) => (Government)unchecked((byte)world.Civilizations[c].Government);

    public void Run(int c, int advance)
    {
        using var operation = Begin(c, advance);
        int? reply = null;
        while (!operation.Completed)
        {
            var question = operation.Resume(reply);
            reply = question is null ? null : effects.ConfirmGovernment(question);
        }
    }

    public Operation Begin(int civilization, int advance) => new(this, civilization, advance);
    public sealed class Operation : IDisposable
    {
        private readonly ResearchGovernmentPrompts parent;
        private readonly IEnumerator<ResearchGovernmentChoice> steps;
        private int? reply;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public ResearchGovernmentChoice? Pending { get; private set; }
        internal Operation(ResearchGovernmentPrompts parent, int civilization, int advance)
        { this.parent = parent; steps = parent.Execute(this, civilization, advance).GetEnumerator(); }
        internal int TakeReply() { int answer = reply!.Value; reply = null; return answer; }
        public ResearchGovernmentChoice? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed research government prompt cannot resume"); }
            if (Completed)
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
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private GameState World => world;
    private IEnumerable<ResearchGovernmentChoice> Execute(Operation operation, int c, int advance)
    {
        if (advance == AdvanceIds.Monarchy)
        {
            if (Current(c) == Government.Despotism)
            {
                yield return new(c, advance, Current(c), Government.Monarchy, true);
                Accept(operation, c);
            }
        }
        else if (advance is AdvanceIds.Republic or AdvanceIds.Democracy or AdvanceIds.Communism or AdvanceIds.Fundamentalism)
        {
            if (world.Globals.GameOptions.Tutorial && advance == AdvanceIds.Republic
                && unchecked((byte)world.Civilizations[c].Government) < (byte)Government.Communism)
            {
                effects.Tutorial(c, ResearchTutorial.Republic);
                yield break;
            }
            var proposed = advance switch
            {
                AdvanceIds.Republic => Government.Republic,
                AdvanceIds.Democracy => Government.Democracy,
                AdvanceIds.Communism => Government.Communism,
                _ => Government.Fundamentalism
            };
            if (advance != AdvanceIds.Democracy && Current(c) == proposed)
            {
                yield break;
            }

            if (Current(c) is not (Government.Democracy or Government.Anarchy))
            {
                yield return new(c, advance, Current(c), proposed, false);
                Accept(operation, c);
            }
        }
        if (world.Globals.GameOptions.Tutorial)
        {
            ResearchTutorial? tutorial = advance switch
            {
                AdvanceIds.Railroad => ResearchTutorial.Railroad,
                AdvanceIds.Refrigeration => ResearchTutorial.Refrigeration,
                AdvanceIds.Trade => ResearchTutorial.Trade,
                AdvanceIds.Seafaring => ResearchTutorial.Seafaring,
                AdvanceIds.Writing => ResearchTutorial.Writing,
                _ => null
            };
            if (tutorial is { } value)
            {
                effects.Tutorial(c, value);
            }
        }
    }

    private void Accept(Operation operation, int c)
    {
        if (operation.TakeReply() == 1)
        {
            effects.RevolutionStarting(c);
            governments.Set(c, (int)Government.Anarchy);
        }
    }
}
