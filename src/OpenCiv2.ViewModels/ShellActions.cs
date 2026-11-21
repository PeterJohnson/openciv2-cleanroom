using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.ViewModels;

/// <summary>A semantic menu leaf. Shortcuts use Primary for the host's command modifier.</summary>
public sealed record ShellAction(
    string Id, string Group, string Label, string? Shortcut, bool Enabled, bool Hidden = false, bool? Checked = null);

/// <summary>Local presentation preferences; these values never select simulation work.</summary>
public sealed record ShellPresentation(bool Grid = false, bool Bare = false, bool ReducedMotion = false);

/// <summary>P05 action availability over owned observations. Execution still uses command admission.</summary>
public static class ShellActions
{
    public const string Files = "file.library";
    public const string StackBottom = "unit.stack-bottom";
    public const string Grid = "view.grid";
    public const string Bare = "view.bare";
    public const string ReducedMotion = "view.reduced-motion";
    public const string Console = "view.console";
    public const string MapOptions = "view.map-options";

    public static OwnedList<ShellAction> Create(SessionView view, UnitTarget? selected, ShellPresentation display)
    {
        bool idle = view.Identity is not null &&
            view.Command?.Status is not (CommandStatus.Pending or CommandStatus.AwaitingInteraction);
        bool registered = view.Commands.Any(command => command.Name == StackBottom);
        bool ownTarget = selected is { } target && view.Units?.Records.Any(
            unit => unit.Slot == target.Slot && unit.Token == target.Token) == true;
        return new([
            new(Files, "File", "Files and preferences…", null, idle),
            new(StackBottom, "Orders", "Send to bottom of stack", "Primary+Enter", idle && registered && ownTarget,
                Hidden: !registered),
            new(Grid, "View", "Map grid", "Primary+G", idle, Checked: display.Grid),
            new(Bare, "View", "Bare terrain", null, idle, Checked: display.Bare),
            new(ReducedMotion, "View", "Reduced motion", null, idle, Checked: display.ReducedMotion),
            new(MapOptions, "View", "Map options…", null, idle),
            new(Console, "View", "Command console", null, idle)
        ]);
    }

    /// <summary>Resolves the same stable target for menu, shortcut and drawn-control routes.</summary>
    public static bool Submit(SessionPanel panel, string action, UnitTarget? selected)
    {
        panel.Refresh();
        if (action != StackBottom || selected is not { } target ||
            !Create(panel.View, selected, new()).Any(item => item.Id == action && item.Enabled && !item.Hidden))
        {
            return false;
        }

        return panel.Submit(new(StackBottom, new([new("unit", new CommandValue.Unit(target))])));
    }
}
