using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Presentation binding for the complete local gameplay service graph.
/// The host supplies the active operation scope, viewer, content colors, targeting and
/// scenario wonder-presentation request. Decisions stay in retained operations.</summary>
public sealed class SinglePlayerGameplayPresentation
{
    private readonly Func<OperationContext> current;
    private readonly Func<int> viewer;
    public SinglePlayerResearchPresentation Research { get; }
    public SinglePlayerGameplayEffects Effects { get; }

    public SinglePlayerGameplayPresentation(Func<OperationContext> current, Func<int> viewer,
        Func<int, int> secondaryColor, Action<UnitSlot> targetParadrop, Action suppressWonders,
        Func<CitySlot?>? selectedCity = null, int researchNoticeMode = 0)
    {
        this.current = current;
        this.viewer = viewer;
        Research = new(current, viewer, selectedCity, researchNoticeMode);
        var emissary = new SinglePlayerEmissaryPresentation(current);
        var diplomacy = new SinglePlayerDiplomacyPresentation(current, viewer);
        var lifecycle = new SinglePlayerLifecyclePresentation(current, viewer);
        var combat = new SinglePlayerCombatPresentation(current, viewer, secondaryColor, targetParadrop);
        var missions = new SinglePlayerMissionPresentation(current, viewer);
        var movement = new SinglePlayerMovementPresentation(current, viewer);
        Effects = new(Research, Research, emissary, diplomacy.Effects(Research), lifecycle.Effects,
            combat.Effects, missions.Effects, movement.Effects, movement, lifecycle.Deletion,
            Government, Upgrade, Production, Airlift, suppressWonders);
    }

    private void Tell(int recipient, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recipient);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(recipient, EntityLimits.Civilizations);
        (current().Notify ?? throw new InvalidOperationException("Gameplay presentation requires a scoped notice publisher"))(
            new((byte)recipient), text);
    }

    // V02: Core emits before changing government; retain the current leader's old title.
    private void Government(GovernmentChangeNotice notice)
    {
        var context = current();
        var names = context.World.Names[notice.Civilization];
        string title = names.Titles[(int)context.World.Civilizations[notice.Civilization].Government];
        string government = RuleText.Display(context.Process.Rules.Definitions.Governments[notice.NewGovernment].Identity.Name);
        Tell(viewer(), notice.Overthrown
            ? $"{title} {names.Leader} of the {names.Adjective} civilization has been overthrown. New government: {government}."
            : $"The {names.Adjective} civilization under {title} {names.Leader} is changing its government to {government}.");
    }

    // C08 announces each old type before assigning replacements in its ascending pass.
    private void Upgrade(LeonardoUpgradeNotice notice) => Tell(notice.Owner,
        $"{notice.WonderName} is upgrading your {notice.OldName} units to {notice.NewName}.");

    // C06 supplies civilization-wide information, not a disclosure of a hidden city name.
    private void Production(ProductionWonderNotice notice) => Tell(viewer(), notice.Kind switch
    {
        ProductionWonderNoticeKind.Start => $"The {notice.Civilization} have started building {notice.Name}.",
        ProductionWonderNoticeKind.Switch => $"The {notice.Civilization} have switched from building "
            + $"{notice.PreviousName ?? throw new InvalidOperationException("A wonder switch requires its previous name")} to {notice.Name}.",
        ProductionWonderNoticeKind.Abandon => $"The {notice.Civilization} have abandoned building {notice.Name}.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice))
    });

    // U05's AI donor continuation emits these after relocation and possible deletion.
    private void Airlift(AiAirliftNotice notice) => Tell(viewer(), notice.ShotDown
        ? $"An airlift to {notice.Name} was shot down."
        : $"An airlift to {notice.Name} was spotted.");
}
