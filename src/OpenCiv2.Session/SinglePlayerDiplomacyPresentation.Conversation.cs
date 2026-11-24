using OpenCiv2.Core;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Session;

public sealed partial class SinglePlayerDiplomacyPresentation
{
    public void Notice(ConversationNotice notice)
    {
        string partner = notice.Partner.Plural;
        string text = notice.Kind switch
        {
            ConversationNoticeKind.ContinueUN => $"Your {Wonder(WonderIds.UnitedNations)} permits continued war with the {partner}.",
            ConversationNoticeKind.ContinueHawks => $"The senate permits continued war with the {partner}.",
            ConversationNoticeKind.SenateCease => $"The senate requires a ceasefire with the {partner}.",
            ConversationNoticeKind.WallPeace => $"The {partner}' {Wonder(WonderIds.GreatWall)} requires peace.",
            ConversationNoticeKind.UNPeace => $"The {partner}' {Wonder(WonderIds.UnitedNations)} requires peace.",
            ConversationNoticeKind.WallCease => $"The {partner}' {Wonder(WonderIds.GreatWall)} requires a ceasefire.",
            ConversationNoticeKind.UNCease => $"The {partner}' {Wonder(WonderIds.UnitedNations)} requires a ceasefire.",
            ConversationNoticeKind.NotoriousBig or ConversationNoticeKind.NotoriousSmall => $"The {partner} denounce the conduct of the {Third(notice.Third)}.",
            ConversationNoticeKind.NotoriousHate => $"The {partner} denounce their enemies, the {Third(notice.Third)}.",
            ConversationNoticeKind.AttitudeAlly => $"Your allies, the {partner}, are displeased with you.",
            ConversationNoticeKind.AttitudePeace => $"The {partner} express displeasure despite your treaty.",
            ConversationNoticeKind.Attitude => $"The {partner} express their displeasure.",
            ConversationNoticeKind.WelcomeAlly or ConversationNoticeKind.HowdyAlly => $"Your allies, the {partner}, welcome you.",
            ConversationNoticeKind.WelcomePeace or ConversationNoticeKind.HowdyPeace => $"The {partner} welcome you in peace.",
            ConversationNoticeKind.Welcome or ConversationNoticeKind.Howdy => $"The {partner} welcome you.",
            ConversationNoticeKind.DoodyAlly => $"Your allies, the {partner}, receive you again.",
            ConversationNoticeKind.Doody => $"The {partner} receive you again.",
            ConversationNoticeKind.PleaseCity or ConversationNoticeKind.PleaseCities => $"The {partner} ask you to withdraw forces near {notice.CityName ?? throw new InvalidOperationException("A border notice requires its city name")}.",
            ConversationNoticeKind.AllyPlea => $"Your allies, the {partner}, need help against their enemies.",
            ConversationNoticeKind.AllyBrag => $"Your allies, the {partner}, report an advantage over their enemies.",
            ConversationNoticeKind.PatienceAlly or ConversationNoticeKind.Patience => $"The {partner} have no patience for further requests.",
            ConversationNoticeKind.CeaseExpire => $"Your ceasefire with the {partner} has expired.",
            ConversationNoticeKind.Ambassadors => $"You are exchanging ambassadors with the {partner}. "
                + $"{notice.UnitName ?? throw new InvalidOperationException("An embassy guide requires its unit name")} units can establish embassies.",
            ConversationNoticeKind.SchismTutorial => "Capturing a rival's capital may split their civilization in a civil war.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        };
        if (notice.Kind is ConversationNoticeKind.AttitudeAlly or ConversationNoticeKind.AttitudePeace or ConversationNoticeKind.Attitude
            or ConversationNoticeKind.WelcomeAlly or ConversationNoticeKind.WelcomePeace or ConversationNoticeKind.Welcome
            or ConversationNoticeKind.HowdyAlly or ConversationNoticeKind.HowdyPeace or ConversationNoticeKind.Howdy
            or ConversationNoticeKind.DoodyAlly or ConversationNoticeKind.Doody)
        {
            int category = DiplomaticAttitudes.Category(Current.World.Diplomacy.Attitude);
            text += $" Attitude: {RuleText.Display(Current.Process.Rules.Definitions.DiplomaticAttitudes[category].Name)}.";
        }
        Tell(notice.Recipient, text);
    }
}
