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
        MapKnowledge.RequireLocal(world);
        if (advance == AdvanceIds.Monarchy)
        {
            if (Current(c) == Government.Despotism)
            {
                Ask(c, advance, Government.Monarchy, true);
            }
        }
        else if (advance is AdvanceIds.Republic or AdvanceIds.Democracy or AdvanceIds.Communism or AdvanceIds.Fundamentalism)
        {
            if (world.Globals.GameOptions.Tutorial && advance == AdvanceIds.Republic
                && unchecked((byte)world.Civilizations[c].Government) < (byte)Government.Communism)
            {
                effects.Tutorial(c, ResearchTutorial.Republic);
                return;
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
                return;
            }

            if (Current(c) is not (Government.Democracy or Government.Anarchy))
            {
                Ask(c, advance, proposed, false);
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

    private void Ask(int c, int advance, Government proposed, bool monarchy)
    {
        if (effects.ConfirmGovernment(new(c, advance, Current(c), proposed, monarchy)) == 1)
        {
            effects.RevolutionStarting(c);
            governments.Set(c, (int)Government.Anarchy);
        }
    }
}
