using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Session;

/// <summary>V06 mission information. Actor/victim notices are private independently of
/// command ownership. Local desertion/device announcements use the host viewer.
/// Required P06/P07 visits and mission choices remain in the retained operations.
/// Cosmetic audio, explosion animation and delays are omitted under X01/P10.</summary>
public sealed class SinglePlayerMissionPresentation(Func<OperationContext> current, Func<int> viewer) :
    ICityEspionageVisitEffects, ICityEspionageDamageEffects, ITechnologyTheftEffects,
    ICityIncitementEffects, IUnitEspionageEffects
{
    private OperationContext Current => current();
    public SinglePlayerMissionEffects Effects => new(this, this, this, this, this, Device, StopBlinking);
    private void Tell(int recipient, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recipient);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(recipient, EntityLimits.Civilizations);
        (Current.Notify ?? throw new InvalidOperationException("Mission presentation requires a scoped notice publisher"))(
            new((byte)recipient), text);
    }
    public void Publish() => Current.Publish();
    public void Redraw(MapPoint position) => Publish();
    public void RefreshCity(CitySlot city) => Publish();
    public void RefreshStatus(int civilization) => Publish();
    public void Explosion(MapPoint position) => Publish();
    public void EnsureVisible(MapPoint position) => new SinglePlayerEmissaryPresentation(current).EnsureVisible(position, viewer());
    // The current desktop has no separate active-unit blink queue.
    public void StopBlinking() => Publish();
    public void Sound() { }
    public void Sound(CityEspionageSound sound) { }
    public void Sound(UnitEspionageSound sound) { }
    public void SpySound() { }
    public void Delay(int ticks) { }

    public void Notice(CityEspionageVisitNotice notice)
    {
        string unit = RuleText.Display(Current.Process.Rules.Definitions.Units[notice.InitialType.Value].Identity.Name);
        Tell(notice.Victim, notice.Kind switch
        {
            CityEspionageVisitKind.Intelligence => $"The {notice.ActorPlural} established an embassy with your civilization using a {unit}.",
            CityEspionageVisitKind.City => $"The {notice.ActorPlural} investigated {notice.CityName} using a {unit}.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        });
    }
    public void Notice(CityEspionageNotice notice) => Tell(notice.Actor, notice.Kind switch
    {
        CityEspionageNoticeKind.Immune => $"{RuleText.Display(Current.Process.Rules.Definitions.Improvements[notice.Building].Identity.Name)} in {notice.CityName} cannot be sabotaged.",
        CityEspionageNoticeKind.ProductionSabotaged => $"Your agents sabotaged production of {notice.ItemName} in {notice.CityName}.",
        CityEspionageNoticeKind.BuildingSabotaged => $"Your agents sabotaged {notice.ItemName} in {notice.CityName}.",
        CityEspionageNoticeKind.WaterPoisoned => $"Your agents poisoned the water supply of {notice.CityName}.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
    public void Notice(TechnologyTheftNotice notice) => Tell(notice.Actor, notice.Kind switch
    {
        TechnologyTheftNoticeKind.Unavailable => $"There is no technology available to steal in {notice.CityName}.",
        TechnologyTheftNoticeKind.Stolen => $"Technology theft in {notice.CityName}: {notice.AdvanceName}.",
        TechnologyTheftNoticeKind.Detected => $"A {notice.DetectorName} detected your agent in {notice.CityName}. The theft failed.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
    private string Democracy => RuleText.Display(Current.Process.Rules.Definitions.Governments[(int)Government.Democracy].Identity.Name);
    public void NoRevolt(CityTarget city, string name, int actor) => Tell(actor, $"{name} cannot be incited to revolt under {Democracy}.");
    public void Incorruptible(UnitMissionSubject target, int actor) => Tell(actor,
        $"The {target.Adjective} {target.Name} cannot be bribed under {Democracy}.");
    public void Deserted(UnitMissionSubject target, string newOwnerPlural) => Tell(viewer(),
        $"Your {target.Name} deserted to the {newOwnerPlural}.");
    public void Sabotaged(UnitMissionSubject actor, UnitMissionSubject target) => Tell(actor.Owner,
        $"Your {actor.Name} is sabotaging the {target.Adjective} {target.Name}.");
    public void Device(PlantedNuclearNotice notice) => Tell(viewer(),
        $"A nuclear device exploded in {notice.CityName}. " + (notice.Identified
            ? $"The {notice.ActorPlural} were identified as responsible."
            : "Those responsible were not identified."));
}
