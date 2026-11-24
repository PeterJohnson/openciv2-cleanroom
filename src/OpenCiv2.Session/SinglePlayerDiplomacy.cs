using OpenCiv2.Base.Randomness;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Semantic presentation ports for the local diplomacy graph. Gameplay choices are
/// returned by the retained operations and answered on the session lane.</summary>
public sealed record SinglePlayerDiplomacyEffects(
    IDiplomaticWarEffects War,
    IAllyHelpEffects AllyHelp,
    IDiplomaticTechnologyEffects Technology,
    IOpeningTreatyEffects OpeningTreaty,
    ITreatyNegotiationEffects TreatyNegotiation,
    IDiplomaticProposalEffects Proposal,
    IDiplomaticFavorEffects Favor,
    IDiplomaticGiftEffects Gift,
    IDiplomaticDemandEffects Demand,
    INegotiatedMapEffects Map,
    IDiplomaticAudienceEffects Audience,
    IResearchChoiceEffects ResearchChoice,
    Action<AttackNotice> Attack,
    Action<NegotiatedTreatyNotice> Treaty,
    Action<ConversationNotice> Conversation,
    Action<ContactNotice> Contact,
    Action<TakenTechnologyNotice> TakenTechnology);

/// <summary>World-owned V04/V05 diplomacy composition using actual A08 policy, V03 acquisition
/// and retained S02 permission actions. The scenario dispatcher factory can close the
/// contact/revelation/creation cycle after construction, before any operation is begun.</summary>
public sealed class SinglePlayerDiplomacy
{
    public DiplomaticWar War { get; }
    public AttackConfirmation Attacks { get; }
    public AllyHelp Allies { get; }
    public AiAdvanceExchange Exchange { get; }
    public CapturedTechnology CapturedTechnology { get; }
    public DiplomaticTechnology Technology { get; }
    public NegotiatedTreaties Treaties { get; }
    public DiplomaticConversation Conversation { get; }
    public DiplomaticAudience Audience { get; }
    public DiplomaticContact Contact { get; }

    public SinglePlayerDiplomacy(GameState world, RuleState rules, GameplayRandom random,
        StackOperations stacks, UnitCargo cargo, SinglePlayerResearch research,
        ProductionOrders production, ScenarioPermissionActions events, SinglePlayerDiplomacyEffects effects)
    {
        War = new(world, rules.Definitions, stacks, cargo, random, effects.War);
        Attacks = new(world, rules.Definitions, effects.Attack);
        Allies = new(world, research.Emissaries, War, effects.AllyHelp);
        Exchange = new(world, rules, random, research.Grants);
        CapturedTechnology = research.Captured(production, effects.ResearchChoice, effects.TakenTechnology);
        Technology = new(world, rules, random, research.Emissaries, research.Grants, CapturedTechnology, effects.Technology);
        Treaties = new(world, research.Stance, effects.Treaty);
        var treaties = new TreatyNegotiation(world, rules, random, research.Stance, research.Grants,
            Treaties, War, effects.TreatyNegotiation);
        var opening = new OpeningTreaties(world, rules.Definitions, random, research.Emissaries, Attacks,
            research.Grants, War, Treaties, effects.OpeningTreaty);
        var demands = new DiplomaticDemands(world, rules, random, research.Stance, Technology, research.Grants,
            stacks, War, effects.Demand);
        var proposals = new DiplomaticProposals(world, research.Stance, treaties, demands, War, effects.Proposal);
        var favors = new DiplomaticFavors(world, rules, random, research.Stance, Technology, research.Grants,
            new(world, effects.Map), War, effects.Favor);
        var gifts = new DiplomaticGifts(world, rules, random, Technology, research.Grants, stacks, effects.Gift);
        Conversation = new(world, rules.Definitions, research.Stance, research.Emissaries, Attacks,
            new(world, rules.Definitions, stacks), proposals, favors, gifts, effects.Conversation);
        Audience = new(world, rules, random, research.Stance, research.Emissaries, Technology, research.Grants,
            War, opening, Conversation, events, effects.Audience);
        Contact = new(world, rules, research.Stance, Audience, Exchange, War, Allies, events, effects.Contact);
    }
}
