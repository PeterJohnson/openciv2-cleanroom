using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>World-owned V03/V05 research composition for local gameplay callers. Grants always
/// retain the actual government and emissary operations; callers route their decisions through
/// the session lane. Rebuild this bundle when replacing the world or installed rule tables.</summary>
public sealed class SinglePlayerResearch
{
    private readonly GameState world;
    private readonly RuleState rules;
    private readonly GameplayRandom random;
    private readonly StackOperations stacks;

    public GovernmentPolicy GovernmentPolicy { get; }
    public DiplomaticStance Stance { get; }
    public EmissaryReception Emissaries { get; }
    public ResearchGovernmentPrompts GovernmentPrompts { get; }
    public ResearchGrants Grants { get; }

    public SinglePlayerResearch(GameState world, RuleState rules, GameplayRandom random, StackOperations stacks,
        GovernmentChanges governments, WonderEffects wonders, IResearchGovernmentEffects governmentEffects,
        IResearchGrantEffects grantEffects, IEmissaryEffects emissaryEffects)
    {
        this.world = world;
        this.rules = rules;
        this.random = random;
        this.stacks = stacks;
        GovernmentPolicy = new(world, rules, governments, random);
        Stance = new(world, rules, GovernmentPolicy);
        Emissaries = new(world, rules.Definitions, random, Stance, emissaryEffects);
        GovernmentPrompts = new(world, governments, governmentEffects);
        Grants = new(world, rules.Definitions, random, wonders, GovernmentPrompts, Emissaries, grantEffects);
    }

    /// <summary>City science and free advances use the same grants, RNG and real C03 recalculation.</summary>
    public ResearchFlow Flow(ProductionOrders production, IResearchChoiceEffects choices, IResearchCompletionEffects completion) =>
        new(world, rules, random, Grants, GovernmentPrompts, production, new(world, rules, stacks), choices, completion);

    /// <summary>Capture/theft retains grants before C06 production reset. Goal help remains an
    /// informational presentation input; it cannot substitute for acquisition or selection.</summary>
    public CapturedTechnology Captured(ProductionOrders production, IResearchChoiceEffects choices, Action<TakenTechnologyNotice> notice) =>
        new(world, rules, Grants, production, new(world, rules.Definitions, choices), notice);
}
