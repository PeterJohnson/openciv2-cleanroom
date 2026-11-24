using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U02/U04/U05/C07/W06 local movement information and selection focus.
/// Core owns activation, relocation, visibility and every retained decision. Cosmetic
/// animations/delays are omitted; native input flushing must not discard admitted commands.</summary>
public sealed partial class SinglePlayerMovementPresentation(Func<OperationContext> current, Func<int> viewer) :
    IMovementDispatcherEffects, ILandEntryEffects, ICaravanTradeEffects, IUnitExpulsionEffects,
    IForeignUnitEffects, ITransportMovementEffects, IForeignCityEffects, IMovementRelocationEffects,
    IMovementCompletionEffects, ISpecialMovementEffects, IHutEffects, IUnitSelectionEffects
{
    private OperationContext Current => current();
    public SinglePlayerMovementEffects Effects => new(this, this, this, this, this, this, this, this, this, this, this, TooManyCities);
    private void Tell(int recipient, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recipient);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(recipient, EntityLimits.Civilizations);
        (Current.Notify ?? throw new InvalidOperationException("Movement presentation requires a scoped notice publisher"))(
            new((byte)recipient), text);
    }
    public void Publish() => Current.Publish();
    public void Redraw(MapPoint point) => Publish();
    public void RedrawNeighbors(MapPoint point) => Publish();
    public void RedrawMaps() => Publish();
    public void RefreshStatus() => Publish();
    public void CityUnitsChanged(UnitTarget unit, MapPoint oldPosition) => Publish();
    private void Focus(MapPoint point) => new SinglePlayerEmissaryPresentation(current).EnsureVisible(point, viewer());
    public void Focus(UnitTarget unit, MapPoint position, int owner) => Focus(position);
    public void FocusSelectedUnit(UnitSlot unit, MapPoint position) => Focus(position);
    public void MoveCursor(MapPoint point) => Focus(point);
    public void EnsureOriginVisible(MapPoint point) => Focus(point);
    public void StopBlinking() => Publish();
    public void FlushInput() { }
    public void PauseEnemyMovement(int ticks) { }
    public void Sound(LandEntrySound sound) { }
    public void Sound(CaravanSound sound) { }
    public void Sound(MovementLossSound sound) { }
    public void PrepareEntry(LandEntryAnimation animation) => Publish();
    public void AnimateEntry(LandEntryAnimation animation) => Publish();
    public void Slide(MovementSlide movement) => Publish();
    public void Enter(ExpulsionEntry entry) => Publish();

    public void NotOnMap(int recipient, UnitTarget unit) => Tell(recipient, "This unit is not currently on the map.");
    public void TooManyCities() => Tell(viewer(), "No free city capacity remains. The new city could not be created.");
    public void ZoneTutorial(int recipient) => Tell(recipient, "An enemy zone of control blocks this move. Choose another route.");
    public void AlliedRepair(AlliedRepairNotice notice) => Tell(notice.Recipient,
        $"Your {notice.UnitName} stopped for allied repairs with the {notice.CivilizationName}.");
    public void NoLandfall(UnitSlot ship, string name) => Tell(Current.World.Entities.Units.At(ship).Owner.Value,
        $"The troops aboard {name} have insufficient movement left to make landfall.");
    public void CannotOccupy(OccupationDenied notice) => Tell(notice.Recipient, $"Your {notice.UnitName} cannot occupy this foreign city.");
    public void Landing(MovementLandingNotice notice) => Tell(notice.Recipient,
        $"Troops from the {Current.World.Names[notice.MoverCivilization].Tribe} are landing near {notice.CityName}.");
    public void Lost(MovementLossNotice notice) => Tell(notice.Viewer, notice.Sinking
        ? $"Your {notice.Name} was lost at sea." : $"Your {notice.Name} ran out of fuel and was lost.");
    public void Notice(ForeignUnitNotice notice) => Tell(notice.Recipient, notice.Kind switch
    {
        ForeignUnitNoticeKind.RequiresAmphibious => $"Your {notice.UnitName} needs amphibious attack capability to attack from the sea.",
        ForeignUnitNoticeKind.NoncombatUnit => $"Your {notice.UnitName} cannot attack another unit.",
        ForeignUnitNoticeKind.RequiresFighter => $"Your {notice.UnitName} cannot make this attack. A fighter is required.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
    public void Notice(ExpulsionNotice notice) => Tell(notice.Recipient, notice.Kind switch
    {
        ExpulsionNoticeKind.CannotExpel => $"The {notice.Offer.VictimAdjective} {notice.Offer.TargetName} cannot be expelled.",
        ExpulsionNoticeKind.VictimReturned => $"The {notice.Offer.ActorPlural} expelled the {notice.Offer.VictimAdjective} {notice.Offer.TargetName} to {notice.CityName}.",
        ExpulsionNoticeKind.AlliedReturn => $"The allied {notice.Offer.VictimAdjective} {notice.Offer.TargetName} returned to {notice.CityName}.",
        ExpulsionNoticeKind.Expelled => $"The {notice.Offer.VictimAdjective} {notice.Offer.TargetName} was expelled to {notice.CityName}.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
}
