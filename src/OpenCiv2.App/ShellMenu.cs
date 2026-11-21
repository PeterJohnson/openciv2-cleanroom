using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

/// <summary>Materializes one action model as an in-window or macOS native menu.</summary>
internal sealed class ShellMenu
{
    private readonly Dictionary<string, (MenuItem Drawn, NativeMenuItem Native, ActionCommand Command)> items = [];
    private readonly Dictionary<string, MenuItem> groups = [];
    private readonly Dictionary<string, NativeMenuItem> nativeHeaders = [];
    private readonly Dictionary<string, KeyGesture> shortcuts = [];
    private readonly HashSet<Key> pressed = [];
    private readonly Action<string> dispatch;
    public Menu Control { get; } = new();
    public NativeMenu Native { get; } = new();
    public bool UsesNative { get; private set; }
    public MenuItem Item(string id) => items[id].Drawn;
    public IReadOnlyDictionary<string, MenuItem> Groups => groups;

    public ShellMenu(IEnumerable<ShellAction> actions, Action<string> dispatch)
    {
        this.dispatch = dispatch;
        var definitions = actions.ToArray();
        var nativeGroups = new Dictionary<string, NativeMenu>();
        foreach (var action in definitions)
        {
            if (!groups.TryGetValue(action.Group, out var group))
            {
                group = new MenuItem { Header = "_" + action.Group };
                AutomationProperties.SetAutomationId(group, "menu." + action.Group.ToLowerInvariant());
                groups.Add(action.Group, group);
                Control.Items.Add(group);
                var children = new NativeMenu();
                nativeGroups.Add(action.Group, children);
                var nativeHeader = new NativeMenuItem { Header = action.Group, Menu = children };
                nativeHeaders.Add(action.Group, nativeHeader);
                Native.Items.Add(nativeHeader);
            }

            var command = new ActionCommand(() => this.dispatch(action.Id));
            var drawn = new MenuItem { Header = action.Label, Command = command };
            var native = new NativeMenuItem { Header = action.Label, Command = command };
            if (action.Shortcut is { } shortcut)
            {
                var primary = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers
                    ?? (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
                string modifier = primary.HasFlag(KeyModifiers.Meta) ? "Meta" : "Control";
                var gesture = KeyGesture.Parse(shortcut.Replace("Primary", modifier));
                drawn.InputGesture = gesture;
                native.Gesture = gesture;
                shortcuts.Add(action.Id, gesture);
            }

            // Checked state belongs to the model; clicking never independently toggles a second value.
            drawn.ToggleType = native.ToggleType = action.Checked is null
                ? MenuItemToggleType.None : MenuItemToggleType.CheckBox;
            AutomationProperties.SetAutomationId(drawn, "action." + action.Id);
            group.Items.Add(drawn);
            nativeGroups[action.Group].Items.Add(native);
            items.Add(action.Id, (drawn, native, command));
        }
        Refresh(definitions);
    }

    public void Attach(Window window)
    {
        window.Deactivated += (_, _) => ReleaseKeys();
        if (OperatingSystem.IsMacOS())
        {
            NativeMenu.SetMenu(window, Native);
            UsesNative = true;
            Control.IsVisible = false;
        }
    }

    public void Refresh(IEnumerable<ShellAction> actions)
    {
        var snapshot = actions.ToArray();
        foreach (var action in snapshot)
        {
            var item = items[action.Id];
            item.Command.Enabled = action.Enabled && !action.Hidden;
            item.Drawn.IsVisible = item.Native.IsVisible = !action.Hidden;
            item.Drawn.IsChecked = item.Native.IsChecked = action.Checked == true;
            item.Command.Notify();
        }
        foreach (var (group, drawn) in groups)
        {
            bool visible = snapshot.Any(action => action.Group == group && !action.Hidden);
            drawn.IsVisible = nativeHeaders[group].IsVisible = visible;
        }
    }

    public void KeyDown(KeyEventArgs e)
    {
        // Native tracking owns its gestures on macOS. Other hosts have one central key route.
        if (UsesNative || e.Handled)
        {
            return;
        }

        foreach (var (id, gesture) in shortcuts)
        {
            if (!gesture.Matches(e))
            {
                continue;
            }

            e.Handled = true;
            if (pressed.Add(e.Key) && items[id].Command.CanExecute(null))
            {
                items[id].Command.Execute(null);
            }
            return;
        }
    }

    public void KeyUp(KeyEventArgs e) => pressed.Remove(e.Key);
    public void ReleaseKeys() => pressed.Clear();

    public void SetTextEntry(bool active)
    {
        // Native accelerators bypass the visual key route. Suppress them during text entry too.
        foreach (var (id, gesture) in shortcuts)
        {
            items[id].Native.Gesture = active ? null : gesture;
        }
    }

    private sealed class ActionCommand(Action execute) : ICommand
    {
        public bool Enabled { get; set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter)
        {
            if (Enabled)
            {
                execute();
            }
        }
        public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
