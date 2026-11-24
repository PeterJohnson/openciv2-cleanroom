using OpenCiv2.Core;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Session;

public sealed partial class SinglePlayerDiplomacyPresentation
{
    public void Notice(TechnologyNegotiationNotice notice) => Tell(notice.Recipient,
        $"The {notice.Counterpart.Plural}: " + (notice.Kind switch
        {
            TechnologyNegotiationNoticeKind.RefusedMedium => "We decline to exchange knowledge.",
            TechnologyNegotiationNoticeKind.RefusedMad => "We will not exchange knowledge with you!",
            TechnologyNegotiationNoticeKind.WonderBlocked => $"We will not exchange technology while competing for {notice.Wonder ?? throw new InvalidOperationException("A wonder notice requires its name")}.",
            TechnologyNegotiationNoticeKind.NotNow => "We are not ready for another technology exchange.",
            TechnologyNegotiationNoticeKind.Unavailable => "We have no technology available for this exchange.",
            TechnologyNegotiationNoticeKind.Gift => $"We offer you {Technology(notice.Technology)} as a gift.",
            TechnologyNegotiationNoticeKind.Petty => "Your proposed technology exchange is insufficient.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        }));
    public void Notice(OpeningTreatyNotice notice) => Tell(notice.Recipient, notice.Kind switch
    {
        OpeningTreatyNoticeKind.DidNotProve => $"The {notice.Partner.Plural} are not convinced of your solidarity.",
        OpeningTreatyNoticeKind.ContinueUN => $"Your {Wonder(WonderIds.UnitedNations)} permits continued war with the {notice.Partner.Plural}.",
        OpeningTreatyNoticeKind.ContinueHawks => $"The senate permits continued war with the {notice.Partner.Plural}.",
        OpeningTreatyNoticeKind.OverruleCease => $"The senate requires acceptance of a ceasefire with the {notice.Partner.Plural}.",
        OpeningTreatyNoticeKind.WallOverCease => $"The {notice.Partner.Plural}' {Wonder(WonderIds.GreatWall)} requires a ceasefire.",
        OpeningTreatyNoticeKind.UNOverCease => $"The {notice.Partner.Plural}' {Wonder(WonderIds.UnitedNations)} requires a ceasefire.",
        OpeningTreatyNoticeKind.ContinueWar => $"War with the {notice.Partner.Plural} continues.",
        OpeningTreatyNoticeKind.Worthless => $"The {notice.Partner.Plural} doubt that you will honor the ceasefire.",
        OpeningTreatyNoticeKind.OverrulePeace => $"The senate requires peace with the {notice.Partner.Plural}.",
        OpeningTreatyNoticeKind.WallOverPeace => $"The {notice.Partner.Plural}' {Wonder(WonderIds.GreatWall)} requires peace.",
        OpeningTreatyNoticeKind.UNOverPeace => $"The {notice.Partner.Plural}' {Wonder(WonderIds.UnitedNations)} requires peace.",
        OpeningTreatyNoticeKind.NoPeace => $"No peace agreement was reached with the {notice.Partner.Plural}.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
    public void Notice(TreatyNegotiationNotice notice)
    {
        string treaty = notice.Alliance ? "an alliance" : "peace";
        Tell(notice.Recipient, $"The {notice.Partner.Plural}: " + (notice.Kind switch
        {
            TreatyNegotiationNoticeKind.NoSmall => $"Your civilization is too weak for {treaty}.",
            TreatyNegotiationNoticeKind.NoThanks => $"We are not interested in {treaty}.",
            TreatyNegotiationNoticeKind.NoPatience => $"We have no patience for further proposals of {treaty}.",
            TreatyNegotiationNoticeKind.NoDislike => $"Our relations do not favor {treaty}.",
            TreatyNegotiationNoticeKind.NoWinning => $"We will not strengthen your position by agreeing to {treaty}.",
            TreatyNegotiationNoticeKind.NoBetrayWeak or TreatyNegotiationNoticeKind.NoBetray => $"We cannot trust you to honor {treaty}.",
            TreatyNegotiationNoticeKind.ThanksAnyway => $"We appreciate your gift, but decline {treaty}.",
            TreatyNegotiationNoticeKind.Goodbye => "These negotiations are over.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        }));
    }
    public void Notice(DiplomaticFavorNotice notice) => Tell(notice.Recipient,
        $"The {notice.Partner.Plural}: " + (notice.Kind switch
        {
            DiplomaticFavorNoticeKind.MapNo => "We decline to exchange maps.",
            DiplomaticFavorNoticeKind.NoContact => $"We have no contact with the {Third(notice.Target)}.",
            DiplomaticFavorNoticeKind.AtWar => $"We are already at war with the {Third(notice.Target)}.",
            DiplomaticFavorNoticeKind.RefusedWar => $"We will not go to war with the {Third(notice.Target)}.",
            DiplomaticFavorNoticeKind.EternalAllies => $"We will not betray our allies, the {Third(notice.Target)}.",
            DiplomaticFavorNoticeKind.Unaffordable => "You cannot afford the agreed payment.",
            DiplomaticFavorNoticeKind.Declare => $"We have declared war on the {Third(notice.Target)}.",
            DiplomaticFavorNoticeKind.BetrayAlly => $"The {Third(notice.Target)} have learned of your betrayal of their alliance.",
            DiplomaticFavorNoticeKind.BetrayPeace => $"The {Third(notice.Target)} have learned of your betrayal of the peace treaty.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        }));
    public void Notice(DiplomaticGiftNotice notice) => Tell(notice.Recipient,
        $"The {notice.Counterpart.Plural}: " + (notice.Kind switch
        {
            DiplomaticGiftNoticeKind.KnowNo => "You have no new knowledge to offer us.",
            DiplomaticGiftNoticeKind.Wasting => "We regret that you chose not to offer a gift.",
            DiplomaticGiftNoticeKind.Accept => "We accept your gift.",
            DiplomaticGiftNoticeKind.MilitaryNone => "There are no eligible military units to give us.",
            DiplomaticGiftNoticeKind.MilitaryNo => "We cannot accept that military unit.",
            DiplomaticGiftNoticeKind.Breakthrough => $"Your gift has made a breakthrough in {Technology(notice.Advance)} possible.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        }));
    public void Notice(DiplomaticDemandNotice notice) => Tell(notice.Recipient,
        $"The {notice.Other.Plural}: " + (notice.Kind switch
        {
            DiplomaticDemandNoticeKind.Provoke => "Your demands provoke us. We are at war!",
            DiplomaticDemandNoticeKind.Apologize => "We apologize and have withdrawn our intruding forces.",
            DiplomaticDemandNoticeKind.NoViolators => "We have no forces violating your territory.",
            DiplomaticDemandNoticeKind.Feeble => "Your threats do not impress us.",
            DiplomaticDemandNoticeKind.Sympathy => "We sympathize, but cannot meet your request.",
            DiplomaticDemandNoticeKind.FeebleAlly => "Even as allies, we cannot meet your demand.",
            DiplomaticDemandNoticeKind.TauntAlly => "You demand too much of your allies.",
            DiplomaticDemandNoticeKind.GiveGold => $"We will give you {notice.Gold} gold.",
            DiplomaticDemandNoticeKind.GiveTechnology => $"We will share {Technology(notice.Advance)} with you.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        }));
    public void Notice(AudienceNotice notice) => Tell(notice.Recipient, notice.Kind switch
    {
        AudienceNoticeKind.OutaHere or AudienceNoticeKind.OutaHereAlly => $"The {notice.Partner.Plural} have ended this audience.",
        AudienceNoticeKind.Begone => $"The {notice.Partner.Plural} dismiss your emissary.",
        AudienceNoticeKind.Provoke or AudienceNoticeKind.Reject or AudienceNoticeKind.Rid => $"The {notice.Partner.Plural} have declared war on you.",
        AudienceNoticeKind.AccursedWall => $"The {notice.Partner.Plural} resent the protection of your {Wonder(WonderIds.GreatWall)}.",
        AudienceNoticeKind.AccursedUN => $"The {notice.Partner.Plural} resent the protection of your {Wonder(WonderIds.UnitedNations)}.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
}
