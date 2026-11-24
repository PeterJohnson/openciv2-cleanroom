using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>V04/V05 informational presentation. Core owns disclosure gates and gameplay;
/// retained Session operations own choices. The viewer receives announcements with no
/// explicit recipient, independently of the participant who initiated the command.</summary>
public sealed partial class SinglePlayerDiplomacyPresentation(Func<OperationContext> current, Func<int> viewer) :
    IDiplomaticWarEffects, IAllyHelpEffects, IDiplomaticTechnologyEffects, IOpeningTreatyEffects,
    ITreatyNegotiationEffects, IDiplomaticProposalEffects, IDiplomaticFavorEffects, IDiplomaticGiftEffects,
    IDiplomaticDemandEffects, INegotiatedMapEffects, IDiplomaticAudienceEffects
{
    private OperationContext Current => current();
    public SinglePlayerDiplomacyEffects Effects(IResearchChoiceEffects research) => new(
        this, this, this, this, this, this, this, this, this, this, this, research,
        Notice, Notice, Notice, Notice, Notice);
    private void Tell(int recipient, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recipient);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(recipient, EntityLimits.Civilizations);
        (Current.Notify ?? throw new InvalidOperationException("Diplomacy presentation requires a scoped notice publisher"))(
            new((byte)recipient), text);
    }
    private string Wonder(int wonder) => RuleText.Display(Current.Process.Rules.Definitions.Improvements[39 + wonder].Identity.Name);
    private static string Technology(ResearchOption? advance) => advance is { } value ? RuleText.Display(value.Name)
        : throw new InvalidOperationException("A technology notice requires its advance");
    private static string Third(DiplomaticParty? party) => party?.Plural
        ?? throw new InvalidOperationException("A diplomatic notice requires its third party");
    public void Publish() => Current.Publish();
    public void RefreshStatus() => Publish();
    public void RefreshCity(CitySlot city) => Publish();
    public void Redraw(MapPoint position) => Publish();
    public void RedrawMaps(int localHuman) => Publish();
    // The desktop has no separate active-unit blink queue to drain.
    public void ClearActiveUnitBlink() => Publish();
    public void MoveCursor(MapPoint position) => new SinglePlayerEmissaryPresentation(current).EnsureVisible(position, viewer());

    public void Notice(DiplomaticWarNotice notice)
    {
        string a = notice.First.Plural, b = notice.Second.Plural;
        Tell(notice.Participant ?? viewer(), notice.Kind switch
        {
            DiplomaticWarNoticeKind.AllianceCancelled => $"The alliance between the {a} and the {b} has ended.",
            DiplomaticWarNoticeKind.AllyHelps => $"The {Third(notice.Third)} are joining the {a} against the {b}.",
            DiplomaticWarNoticeKind.AllyActivated => $"The {a} have called on the {Third(notice.Third)} to join the war against the {b}.",
            DiplomaticWarNoticeKind.JoinedWar => $"The {a} are joining their allies, the {Third(notice.Third)}, in war against the {b}.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        });
    }
    public void Notice(AttackNotice notice) => Tell(notice.Actor.Civilization, notice.Kind switch
    {
        AttackNoticeKind.Allied => $"You cannot attack your allies, the {notice.Victim.Plural}.",
        AttackNoticeKind.Overruled => $"The senate under {RuleText.Display(Current.Process.Rules.Definitions.Governments[(int)notice.Government].Identity.Name)} blocks an attack on the {notice.Victim.Plural}.",
        AttackNoticeKind.UnitedNations => $"An attack on the {notice.Victim.Plural} is permitted despite their {Wonder(WonderIds.UnitedNations)}.",
        AttackNoticeKind.Hawks => $"The senate permits an attack on the {notice.Victim.Plural}.",
        AttackNoticeKind.Aggressor => $"The senate permits an attack on the {notice.Victim.Plural} because of their aggression.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
    public void Notice(NegotiatedTreatyNotice notice)
    {
        string treaty = notice.Kind switch
        {
            NegotiatedTreatyKind.Alliance => "an alliance",
            NegotiatedTreatyKind.Peace => "a peace treaty",
            NegotiatedTreatyKind.Ceasefire => "a ceasefire",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        };
        Tell(notice.Actor.Civilization, $"The {notice.Actor.Plural} and the {notice.Other.Plural} have agreed to {treaty}.");
    }
    public void DidNotHelp(AllyHelpRequest request) => Tell(request.Recipient,
        $"The {request.Ally.Plural} are disappointed that you did not help against the {request.Enemy.Plural}.");
    public void CancelledAlliance(int actor, DiplomaticParty other) => Tell(actor, $"Your alliance with the {other.Plural} has ended.");
    public void Accepted(int requester, DiplomaticParty grantor) => Tell(requester, $"The {grantor.Plural} agree to exchange maps.");
    public void Notice(TakenTechnologyNotice notice) => Tell(notice.Victim,
        $"The {notice.CivilizationName} have taken your knowledge of {notice.AdvanceName}.");
    public void Notice(ContactNotice notice)
    {
        string a = notice.First.Plural, b = notice.Second.Plural;
        Tell(viewer(), notice.Kind switch
        {
            ContactNoticeKind.UNForce => $"The {a} must make peace with the {b} because of {Wonder(WonderIds.UnitedNations)}.",
            ContactNoticeKind.WallForce => $"The {a} must make peace with the {b} because of {Wonder(WonderIds.GreatWall)}.",
            ContactNoticeKind.AllyMakesPeace => $"Your allies, the {a}, are making peace with the {b}.",
            ContactNoticeKind.SignPeace => $"The {a} and the {b} are signing a peace treaty.",
            ContactNoticeKind.CancelAllied => $"The alliance between the {a} and the {b} is ending.",
            ContactNoticeKind.SignNato or ContactNoticeKind.SignAllied => $"The {a} and the {b} are forming an alliance against the {Third(notice.Target)}."
                + (notice.CityName is { } city ? $" City: {city}." : ""),
            ContactNoticeKind.AllyMakesWar => $"Your allies, the {a}, are going to war with the {b}.",
            ContactNoticeKind.DeclareWar => $"The {a} and the {b} are going to war.",
            ContactNoticeKind.CancelPeace => $"The peace treaty between the {a} and the {b} is ending in war.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        });
    }
}
