using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U01/V01/V06/V07 local lifecycle presentation. Required revelation/contact
/// children must come from the retained gameplay graph. Announcements without a recipient
/// use the host viewer; spy outcomes and incidents address their explicit owner/actor.</summary>
public sealed class SinglePlayerLifecyclePresentation(Func<OperationContext> current, Func<int> viewer) :
    IUnitCreationEffects, IUnitRevelationEffects, IUnitLossEffects, ISpyEscapeEffects,
    ICivilizationEliminationEffects, ICivilWarEffects
{
    private OperationContext Current => current();
    public SinglePlayerLifecycleEffects Effects => new(this, this, this, this, this, this,
        Incident, CityRemoved, RedrawTile, RedrawMaps);
    public SinglePlayerDeletionPresentation Deletion => new(UnitsChangedAt, _ => Publish(), SynchronizeDeletion);
    private void Publish() => Current.Publish();
    private void Tell(int recipient, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recipient);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(recipient, EntityLimits.Civilizations);
        (Current.Notify ?? throw new InvalidOperationException("Lifecycle presentation requires a scoped notice publisher"))(
            new((byte)recipient), text);
    }
    private void Tell(string text) => Tell(viewer(), text);
    public void UnitsChangedAt(MapPoint position) => Publish();
    public void RedrawTile(MapPoint position) => Publish();
    public void Redraw(MapPoint position) => Publish();
    public void RedrawMaps() => Publish();
    public void RevealOrigin(MapPoint position) => Publish();
    public void CityRemoved(CityTarget city) => Publish();
    public void RevealSurroundings(int slot, bool allowContact) =>
        throw new InvalidOperationException("Creation requires the retained revelation service");
    public void Meet(int actingCivilization, int otherCivilization, MapPoint position) =>
        throw new InvalidOperationException("Revelation requires the retained diplomatic-contact service");
    private static InvalidOperationException Network() => new("Unit synchronization requires the owning network session");
    public void SynchronizeCreation() => throw Network();
    public void SynchronizeLoss(MapPoint? redraw) => throw Network();
    private void SynchronizeDeletion(MapPoint position, CitySlot? home) => throw Network();

    public void Notice(UnitCreationNotice notice) => Tell(notice switch
    {
        UnitCreationNotice.TooManyUnits => "No free unit capacity remains. The new unit could not be created.",
        UnitCreationNotice.Ships => "A naval unit is ready. Select it to inspect its movement and orders.",
        UnitCreationNotice.Aircraft => "An aircraft is ready. Select it to inspect its movement and orders.",
        UnitCreationNotice.Caravan => "A trade unit is ready. It can travel to a city to deliver its cargo.",
        UnitCreationNotice.FirstMilitary => "Your first military unit is ready. Select it to inspect its movement and orders.",
        UnitCreationNotice.SecondMilitary => "Your second military unit is ready. Select a unit to give it orders.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });
    public void Notice(SpyNotice notice)
    {
        string unit = RuleText.Display(Current.Process.Rules.Definitions.Units[notice.Type.Value].Identity.Name);
        string fate = notice.Fate switch
        {
            SpyFate.Survived => "escaped and will become a veteran",
            SpyFate.VeteranSurvived => "survived another mission as a veteran",
            SpyFate.Died => "died during the escape attempt",
            SpyFate.Caught => "was caught and lost",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        };
        Tell(notice.Owner, $"Your {unit} {fate}." + (notice.Destination is { } destination ? $" Destination: {destination}." : ""));
    }
    public void Destroyed(CivilizationDestroyedNotice notice) =>
        Tell($"The {notice.Adjective} civilization has been destroyed by the {notice.KillerName}.");
    public void SpaceshipLost(SpaceshipLossNotice notice) => Tell(notice.Returning
        ? $"The {notice.Adjective} spaceship is returning home."
        : $"The {notice.Adjective} spaceship has been destroyed.");
    public void Schism(CivilWarNotice notice) =>
        Tell($"Civil war has divided the {notice.ParentAdjective} civilization. The {notice.RebelAdjective} rebels have emerged.");
    public void Incident(EspionageIncidentNotice notice)
    {
        string victim = notice.Victim.Plural;
        string text = notice.Kind switch
        {
            EspionageIncidentKind.Terror => $"The {victim} vow revenge over your espionage.",
            EspionageIncidentKind.War => $"Your espionage against the {victim} has become a cause for war.",
            EspionageIncidentKind.Allied => $"The {victim} are breaking their alliance over your espionage.",
            EspionageIncidentKind.Leniency => $"The {victim} show leniency over the stolen knowledge. Attitude: "
                + RuleText.Display(Current.Process.Rules.Definitions.DiplomaticAttitudes[notice.AttitudeCategory
                    ?? throw new InvalidOperationException("A leniency notice requires its attitude category")].Name) + ".",
            EspionageIncidentKind.SenateScandal => $"An espionage scandal involving the {victim} forces your government to resign.",
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        };
        Tell(notice.Actor.Civilization, text);
    }
}
