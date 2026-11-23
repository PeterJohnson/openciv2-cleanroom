using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

internal static class ThroneInteraction
{
    public static ThroneSelection Selection(int choice) => new(choice >= 100 ? ThroneUpgradeKind.Overlay : ThroneUpgradeKind.Piece,
        choice >= 100 ? choice - 100 : choice);
    public static void Accepted(OperationContext context, ThroneUpgrade upgrade, ThroneSelection selected) =>
        context.Effect("throne.upgraded", new([new("civilization", new CommandValue.Integer(upgrade.Civilization)),
            new("kind", new CommandValue.Text(selected.Kind.ToString())), new("index", new CommandValue.Integer(selected.Index))]));
    public static InteractionSpec Prompt(OperationContext context, ThroneUpgrade upgrade, Func<int, ParticipantId> recipient)
    {
        var options = upgrade.Choices().Select(c => new InteractionOption(c.Kind == ThroneUpgradeKind.Overlay ? c.Index + 100 : c.Index,
            c.Kind == ThroneUpgradeKind.Overlay ? $"Add decoration {c.Index + 1}"
                : $"Improve feature {c.Index + 1}: level {context.World.Civilizations[upgrade.Civilization].ThroneRoom[c.Index]} → {context.World.Civilizations[upgrade.Civilization].ThroneRoom[c.Index] + 1}")).ToArray();
        return new("Throne room\nChoose an upgrade.", recipient(upgrade.Civilization),
            new CommandValue.Integer(upgrade.Civilization), new(options.Select(o => o.Value)))
        { Options = new(options) };
    }
}
