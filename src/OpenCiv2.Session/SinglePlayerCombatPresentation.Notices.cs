using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed partial class SinglePlayerCombatPresentation
{
    public void Notice(CaptureNotice notice)
    {
        string ownership = notice.Restored ? "recaptured" : "captured";
        string outcome = $"The {notice.WinnerName} have {ownership} {notice.CityName} from the {notice.LoserName} civilization.";
        Tell(notice.Viewer, notice.Kind switch
        {
            CaptureNoticeKind.Incited => $"{notice.CityName} has revolted against the {notice.LoserName} and joined the {notice.WinnerName} civilization.",
            CaptureNoticeKind.PalaceEscape => $"The {notice.LoserName} government has escaped to {notice.CityName}, spending {notice.NumericArgument} gold to establish its new capital.",
            CaptureNoticeKind.AbandonedWonder => $"The {notice.LoserName} have abandoned work on {notice.ImprovementName} after losing {notice.CityName}.",
            CaptureNoticeKind.StillOneWonder => $"The {notice.LoserName} continue building {notice.ImprovementName} in one other city after losing {notice.CityName}.",
            CaptureNoticeKind.StillWonders => $"The {notice.LoserName} continue building {notice.ImprovementName} in {notice.NumericArgument} other cities after losing {notice.CityName}.",
            CaptureNoticeKind.CapturedWonder => $"You captured {notice.ImprovementName} in {notice.CityName}.",
            CaptureNoticeKind.LostWonder => $"You lost {notice.ImprovementName} in {notice.CityName} to the {notice.WinnerName}.",
            CaptureNoticeKind.Capture => outcome + $" Plunder: {notice.NumericArgument} gold.",
            CaptureNoticeKind.WinnerAlly => $"Your allies, the {notice.WinnerName}, have {ownership} {notice.CityName} from the {notice.LoserName} civilization.",
            CaptureNoticeKind.LoserAlly => $"Your allies, the {notice.LoserName}, have lost {notice.CityName} to the {notice.WinnerName}.",
            CaptureNoticeKind.General => outcome,
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        });
    }

    public void Opening(CombatOpeningPresentation presentation)
    {
        Focus(presentation.Target, presentation.Recipient);
        foreach (var notice in presentation.Notices)
        {
            var arguments = notice.Arguments;
            string text = notice.Kind switch
            {
                CombatOpeningNoticeKind.MissileAttack => $"A missile attack is approaching {arguments[0]}.",
                CombatOpeningNoticeKind.PearlHarbor => $"A {arguments[0]} {arguments[1]} is attacking {arguments[2]} ships.",
                CombatOpeningNoticeKind.Battery => $"{arguments[1]} defends {arguments[2]} against this attack.",
                CombatOpeningNoticeKind.BatteryWithSdi => $"{arguments[1]} and {arguments[3]} defend {arguments[2]} against this attack.",
                CombatOpeningNoticeKind.Scramble => $"A {arguments[0]} {arguments[1]} scrambles to defend {arguments[2]}.",
                CombatOpeningNoticeKind.AmphibiousAttack => $"A {arguments[1]} {arguments[2]} is making an amphibious attack.",
                _ => throw new ArgumentOutOfRangeException(nameof(presentation))
            };
            Tell(presentation.Recipient, text);
        }
    }
}
