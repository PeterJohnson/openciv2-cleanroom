using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

internal static class CargoInteraction
{
    internal static ResearchReply? Reply(CargoDecision pending, OperationInput input)
    {
        if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
        {
            // No default cargo is defined for dismissal. Confirmation/report dismissal returns to the menu.
            return new(pending is CargoMenu ? -1 : 1);
        }
        if (input.Kind != OperationInputKind.Choice || input.Choice is not { } choice)
        {
            return null;
        }

        return pending is CargoMenu && choice >= 4 ? new(choice - 4, 1) : new(choice);
    }

    internal static InteractionSpec Prompt(CargoDecision pending, Func<int, ParticipantId> recipient)
    {
        if (pending is CargoMenu menu)
        {
            var options = new OwnedList<InteractionOption>(menu.Options.Select(o => new InteractionOption(o.Slot, "Load " + o.Name))
                .Concat(menu.Options.Select(o => new InteractionOption(o.Slot + 4, "Report: " + o.Name))));
            return new($"Choose cargo in {menu.CityName}.", recipient(menu.Owner), new CommandValue.City(menu.City), new(options.Select(o => o.Value)))
            { Options = options };
        }
        if (pending is CargoConfirmation confirmation)
        {
            return new($"Load {confirmation.Name}?", recipient(confirmation.Owner), null, new([0, 1]))
            { Options = new([new(0, "Load cargo"), new(1, "Choose again")]) };
        }
        if (pending is CargoReport report)
        {
            string text = report.Food ? "No commodity-city report is available for Food."
                : report.Cities.Count == 0 ? $"No known cities supply or demand {report.Name}."
                : string.Join("\n", report.Cities.Select(row => $"{row.Name} ({(row.Adjective is null ? "" : row.Adjective + ", ")}{row.Size}): "
                    + (row.Demand ? "Demands " : "Supplies ") + report.Name));
            return new(text, recipient(report.Owner), null, new([0]))
            { Options = new([new(0, "Back to cargo")]) };
        }
        throw new InvalidOperationException("Unknown cargo decision");
    }
}
