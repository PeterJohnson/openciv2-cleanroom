using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Session;

public sealed partial class SinglePlayerMovementPresentation
{
    public void Delivery(CaravanDeliveryNotice notice) => Tell(notice.Recipient,
        $"{notice.CargoName} delivery from {notice.HomeName} to {notice.DestinationName}."
        + (notice.Delivery.Cargo < 0 ? " Food shipment." : $" Payment: {notice.Delivery.Payment} gold and {notice.Delivery.Payment} research."));
    public void Reciprocal(CaravanReciprocalNotice notice) => Tell(notice.Recipient,
        $"{notice.Adjective} trade from {notice.HomeName} delivered {notice.CargoName} to {notice.DestinationName}. "
        + $"Return cargo from {notice.DestinationName} to {notice.HomeName}: {notice.ReturnName}.");
    public void Donation(CaravanDonationNotice notice) => Tell(notice.Recipient,
        $"{notice.CityName} received {notice.Shields} shields toward its wonder. {notice.Remaining} shields remain to be produced.");
    public void Notice(HutNotice notice)
    {
        string text = notice.Kind switch
        {
            HutNoticeKind.Tribe => "This village offers to join your civilization.",
            HutNoticeKind.Mercenaries => "Mercenaries offer to join your civilization.",
            HutNoticeKind.Metals => $"You found treasure worth {notice.Amount} gold.",
            HutNoticeKind.Nothing => "The village is deserted.",
            HutNoticeKind.Barbarians => "You disturbed a hostile village.",
            HutNoticeKind.Scrolls => "Ancient scrolls describe " + RuleText.Display(Current.Process.Rules.Definitions.Advances[
                notice.Advance ?? throw new InvalidOperationException("Scrolls require their captured advance")].Identity.Name) + ".",
            HutNoticeKind.Nomads => "A band of nomads offers to join your civilization.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        };
        Tell(notice.Actor, $"Village at ({notice.Position.X}, {notice.Position.Y}): {text}");
    }
}
