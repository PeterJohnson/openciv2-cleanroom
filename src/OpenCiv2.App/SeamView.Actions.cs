using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

public sealed partial class SeamView
{
    private ulong actionDispatches;
    private string? lastAction;

    internal void AttachMenu(Window window) => menu.Attach(window);

    private IEnumerable<ShellAction> Actions() => ShellActions.Create(model.View, activeTarget,
        new(map.Display.Grid, map.Display.Bare, map.Display.ReducedMotion)).Concat(PlayMenuActions())
        .Select(action => action with
        {
            Enabled = action.Enabled && !cityOverlay.IsVisible && !transferring && files?.IsVisible != true &&
            (action.Id != ShellActions.Files || files is not null)
        });

    private void RefreshActions()
    {
        var actions = Actions().ToArray();
        menu.Refresh(actions);
        send.IsEnabled = actions.Single(action => action.Id == ShellActions.StackBottom).Enabled;
    }

    private void OnShellKey(object? sender, KeyEventArgs e)
    {
        // Text editing and dialog choices own their keys; the menu's decorative gestures do not dispatch.
        if (!input.IsKeyboardFocusWithin && !cityName.IsKeyboardFocusWithin && !dialog.IsVisible
            && !cityOverlay.IsVisible && !transferring && files?.IsVisible != true)
        {
            menu.KeyDown(e);
        }
    }

    private void DispatchAction(string id)
    {
        model.Refresh();
        if (!Actions().Any(action => action.Id == id && action.Enabled && !action.Hidden))
        {
            Refresh(force: true);
            return;
        }

        lastAction = id;
        actionDispatches++;
        if (id.StartsWith(LocalPlayCommands.Work + ".", StringComparison.Ordinal)
            && int.TryParse(id.AsSpan(LocalPlayCommands.Work.Length + 1), out int job))
        {
            UnitCommand(LocalPlayCommands.Work, new("job", new OpenCiv2.Contracts.CommandValue.Integer(job)));
            return;
        }
        switch (id)
        {
            case PlaySave:
                _ = Transfer(false);
                break;
            case PlayReload:
                _ = Transfer(true);
                break;
            case PlayCity:
                OpenOwnedCity();
                break;
            case LocalPlayCommands.Fortify:
                UnitCommand(LocalPlayCommands.Fortify);
                break;
            case LocalPlayCommands.EndTurn:
                PlayCommand(LocalPlayCommands.EndTurn);
                break;
            case LocalPlayCommands.EditRates:
                PlayCommand(LocalPlayCommands.EditRates);
                break;
            case ShellActions.Files:
                mapOptions.Flyout?.Hide();
                if (files is not null)
                {
                    _ = files.OpenAsync();
                }

                break;
            case ShellActions.StackBottom:
                parseError = "";
                dialog.RememberFocus(map);
                mapOptions.Flyout?.Hide();
                ShellActions.Submit(model, id, activeTarget);
                break;
            case ShellActions.Grid:
                mapChoices["grid"].IsChecked = !map.Display.Grid;
                break;
            case ShellActions.Bare:
                mapChoices["bare"].IsChecked = !map.Display.Bare;
                break;
            case ShellActions.ReducedMotion:
                reducedMotion.IsChecked = !map.Display.ReducedMotion;
                break;
            case ShellActions.MapOptions:
                mapOptions.Flyout?.ShowAt(mapOptions);
                break;
            case ShellActions.Console:
                input.Focus();
                break;
        }
        Refresh(force: true);
    }

    private JsonObject? FilesSnapshot(Func<Control?, JsonObject> bounds) => files is null ? null : new()
    {
        ["visible"] = files.IsVisible,
        ["busy"] = files.Model.Busy,
        ["canCancel"] = files.Model.CanCancel,
        ["message"] = files.Message,
        ["playing"] = files.Playing,
        ["playbackResult"] = files.PlaybackResult,
        ["entries"] = new JsonArray(files.Model.Entries.Select(entry => (JsonNode)new JsonObject
        { ["key"] = entry.Key, ["name"] = entry.Name, ["size"] = entry.Size }).ToArray()),
        ["rows"] = new JsonArray(files.Model.Entries.Select((_, index) =>
            (JsonNode)bounds(files.Files.ContainerFromIndex(index))).ToArray()),
        ["controls"] = new JsonObject(files.Buttons.Select(entry => new KeyValuePair<string, JsonNode?>(entry.Key, bounds(entry.Value))))
    };

    private JsonObject DialogSnapshot(Func<Control?, JsonObject> bounds) => new()
    {
        ["visible"] = dialog.IsVisible,
        ["interaction"] = dialog.Model?.Identity.Interaction.ToString(),
        ["submitted"] = dialog.Model?.Submitted,
        ["target"] = dialog.Target,
        ["console"] = bounds(dialog.ReplyInput),
        ["regions"] = new JsonObject(dialog.Choices.Select(entry =>
            new KeyValuePair<string, JsonNode?>("choice." + entry.Key, bounds(entry.Value))))
        {
            ["cancel"] = bounds(dialog.Cancel)
        }
    };

    private JsonObject? PopupBounds(Control control, double scaling)
    {
        if (!control.IsEffectivelyVisible || control.Bounds.Width <= 0 || TopLevel.GetTopLevel(control) is null)
        {
            return null;
        }
        var local = control.TranslatePoint(default, this);
        if (local is null)
        {
            var origin = this.PointToScreen(default);
            var point = control.PointToScreen(default);
            // Browser screen coordinates are CSS pixels; desktop popup roots use physical pixels.
            double divisor = OperatingSystem.IsBrowser() ? 1 : scaling;
            local = new Point((point.X - origin.X) / divisor, (point.Y - origin.Y) / divisor);
        }
        return new()
        {
            ["x"] = local.Value.X,
            ["y"] = local.Value.Y,
            ["width"] = control.Bounds.Width,
            ["height"] = control.Bounds.Height,
            ["enabled"] = control.IsEffectivelyEnabled
        };
    }

    private JsonObject MenuSnapshot(double scaling)
    {
        return new()
        {
            ["native"] = menu.UsesNative,
            ["dispatches"] = actionDispatches,
            ["lastAction"] = lastAction,
            ["groups"] = new JsonObject(menu.Groups.Select(entry => new KeyValuePair<string, JsonNode?>(entry.Key,
                new JsonObject
                {
                    ["open"] = entry.Value.IsSubMenuOpen,
                    ["bounds"] = PopupBounds(entry.Value, scaling)
                }))),
            ["actions"] = new JsonObject(Actions().Select(action => new KeyValuePair<string, JsonNode?>(action.Id,
                new JsonObject
                {
                    ["label"] = action.Label,
                    ["enabled"] = action.Enabled,
                    ["hidden"] = action.Hidden,
                    ["checked"] = action.Checked,
                    ["shortcut"] = action.Shortcut,
                    ["bounds"] = menu.Groups[action.Group].IsSubMenuOpen
                        ? PopupBounds(menu.Item(action.Id), scaling) : null
                })))
        };
    }
}
