using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U05/U06/U07/C09 local combat information and observation publication.
/// The host supplies casualty-history colors from selected content and actual paradrop
/// targeting. Cosmetic sound/animation are omitted; Core still executes every gameplay step.
/// Retained combat, capture and contact decisions belong to their session operations.</summary>
public sealed partial class SinglePlayerCombatPresentation(
    Func<OperationContext> current, Func<int> viewer, Func<int, int> secondaryColor, Action<UnitSlot> targetParadrop) :
    ICityCaptureEffects, ICaptureProductionEffects, ICapturePartisansEffects, IParadropEffects,
    INuclearEffects, ICombatHistoryEffects, ICombatRoundEffects, ICombatResolutionEffects,
    ICombatAftermathEffects, ICombatContinuationEffects
{
    private OperationContext Current => current();
    public SinglePlayerCombatEffects Effects => new(this, this, this, this, this, this, this, this, this, this,
        Treaty, Promotion, Opening, Publish);
    public void Publish() => Current.Publish();
    private void Tell(int recipient, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recipient);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(recipient, EntityLimits.Civilizations);
        (Current.Notify ?? throw new InvalidOperationException("Combat presentation requires a scoped notice publisher"))(
            new((byte)recipient), text);
    }
    public void Redraw(MapPoint point) => Publish();
    public void Redraw(MapPoint origin, MapPoint target) => Publish();
    public void RedrawNeighbors(MapPoint point) => Publish();
    public void RedrawStatus() => Publish();
    public void RefreshInfo() => Publish();
    public void RefreshMilitaryAdvisor() => Publish();
    public void RefreshHistory(int civilization) => Publish();
    // This is the local resolution publication boundary, also reached without network play.
    public void Synchronize() => Publish();
    public void CityUnitChanged(UnitSlot unit, MapPoint origin) => Publish();
    public int SecondaryColor(int civilization) => secondaryColor(civilization);
    public string PluralName(int civilization) => Current.World.Names[civilization].Tribe;
    public void EnterTargeting(UnitSlot unit) => targetParadrop(unit);

    // Core already gated these calls for the local viewer. The actor/owner operand is
    // not the observer: an enemy paradrop or capture must not change the enemy's view.
    public void EnsureVisible(MapPoint point, int owner) => Focus(point, viewer());
    public void MoveCursor(MapPoint target) => Focus(target, viewer());
    private void Focus(MapPoint point, int observer) => new SinglePlayerEmissaryPresentation(current).EnsureVisible(point, observer);
    public void Explosion(MapPoint point, int kind) => Publish();
    public void Animate(MapPoint target) => Publish();
    public void Animate(UnitSlot unit, MapPoint approach, MapPoint target, int direction) => Publish();
    public void Sound(CombatSound sound, int argument) { }
    public void BarracksSound() { }
    public void InfantrySound() { }
    public void SpaceshipLost(SpaceshipLossNotice notice) => new SinglePlayerLifecyclePresentation(current, viewer).SpaceshipLost(notice);

    public void Rejected(UnitSlot unit, ParadropRejection reason, int range) => Tell(Current.World.Entities.Units.At(unit).Owner.Value,
        reason switch
        {
            ParadropRejection.AlreadyMoved => "This unit has already moved or paradropped this turn.",
            ParadropRejection.RequiresCityOrAirbase => "A paradrop must begin in a city or at an airbase.",
            ParadropRejection.Ocean => "A paradrop cannot land in the ocean.",
            ParadropRejection.ForeignUnit => "A foreign unit occupies the paradrop destination.",
            ParadropRejection.Range => $"The destination is beyond this unit's paradrop range of {range}.",
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        });
    public void Announce(ParadropAnnouncement announcement) => Tell(viewer(),
        $"A {announcement.Adjective} {announcement.UnitName} is paradropping to ({announcement.Target.X}, {announcement.Target.Y})"
        + (announcement.CityName is { } city ? $" near {city}." : "."));
    public void Notice(CapturePartisansNotice notice) => Tell(viewer(),
        $"{notice.Adjective} partisans have gathered near {notice.CityName}.");
    public void Notice(NuclearNotice notice) => Tell(notice.Recipient, notice.Kind switch
    {
        NuclearNoticeKind.UseWeapons => $"The {notice.Name} have used nuclear weapons.",
        NuclearNoticeKind.Intercepted => $"The nuclear attack was intercepted by the defenses of {notice.Name}.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
    public void Treaty(CombatTreatyNotice notice) => Tell(notice.Recipient, notice.Kind switch
    {
        CombatTreatyNoticeKind.SneakAttack => $"The {notice.AttackerName} have launched a sneak attack.",
        CombatTreatyNoticeKind.BreakCeasefire => $"The {notice.AttackerName} have broken the ceasefire by attacking.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
    public void Promotion(CombatPromotionNotice notice) => Tell(notice.Owner, $"Your {notice.Name} has become a veteran.");
    public void Notice(CombatFinalNotice notice) => Tell(notice.Recipient, notice.Kind switch
    {
        CombatFinalNoticeKind.MultipleLoss => $"You lost {notice.Count} units in the battle between the {notice.AttackerName} and the {notice.DefenderName}.",
        CombatFinalNoticeKind.MultipleWin => $"You defeated {notice.Count} units in the battle between the {notice.AttackerName} and the {notice.DefenderName}.",
        CombatFinalNoticeKind.Ransom => $"You received {notice.Gold} gold in ransom for the captured barbarian leader.",
        CombatFinalNoticeKind.CancelPeace => $"The battle between the {notice.AttackerName} and the {notice.DefenderName} has ended their peace agreement.",
        CombatFinalNoticeKind.AllyUnderAttack => $"Your allies, the {notice.DefenderName}, are under attack by the {notice.AttackerName}.",
        CombatFinalNoticeKind.AllyAttacking => $"Your allies, the {notice.AttackerName}, are attacking the {notice.DefenderName}.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
}
