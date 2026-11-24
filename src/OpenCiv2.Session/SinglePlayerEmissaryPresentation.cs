using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Session;

/// <summary>V05 §2 emissary information in the receiving civilization's notice history.
/// Reception and intelligence decisions remain in their retained operations. The desktop
/// uses textual leader context; there is no separate portrait window to open or close.</summary>
public sealed class SinglePlayerEmissaryPresentation(Func<OperationContext> current) : IEmissaryEffects
{
    private OperationContext Current => current();

    public void Notice(EmissaryNotice notice)
    {
        var context = Current;
        int recipient = notice.Actor.Civilization;
        ArgumentOutOfRangeException.ThrowIfNegative(recipient);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(recipient, EntityLimits.Civilizations);
        string leader = $"{notice.Counterpart.Title} {notice.Counterpart.Leader} of the {notice.Counterpart.Plural}";
        string text = notice.Kind switch
        {
            EmissaryNoticeKind.ForcedReception => $"{leader} insists that you receive an emissary because their civilization has nuclear weapons.",
            EmissaryNoticeKind.Greeting => $"An emissary from {leader} greets the {notice.Actor.Plural}. "
                + $"Attitude: {RuleText.Display(context.Process.Rules.Definitions.DiplomaticAttitudes[notice.AttitudeCategory].Name)}."
                + (notice.Vendetta ? " Their civilization holds a vendetta against you." : "")
                + (notice.NuclearWeapons ? " Their civilization has nuclear weapons." : ""),
            EmissaryNoticeKind.YourNuclearWeapons => $"{leader} acknowledges your nuclear weapons."
                + (notice.Variant == 2 ? " Their civilization also has nuclear weapons." : ""),
            _ => throw new ArgumentOutOfRangeException(nameof(notice))
        };
        (context.Notify ?? throw new InvalidOperationException("Emissary presentation requires a scoped notice publisher"))(
            new((byte)recipient), text);
    }

    // The greeting itself supplies leader context in the persistent notice panel.
    public void OpenLeader(int counterpart) => Current.Publish();
    public void CloseLeader() => Current.Publish();

    public void EnsureVisible(MapPoint point, int observer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(observer);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(observer, EntityLimits.Civilizations);
        var context = Current;
        context.World.Views[observer] = context.World.Views[observer] with { Center = point };
        context.Publish();
    }
}
