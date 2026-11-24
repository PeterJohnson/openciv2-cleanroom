using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OpenCiv2.App.Map;
using OpenCiv2.Base;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.IO;
using OpenCiv2.MapRendering;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

/// <summary>P01/P04/P05 presentation over the scoped, owned model and production command lane.</summary>
public sealed partial class SeamView : UserControl
{
    private readonly SessionPanel model;
    private readonly bool contentLoaded;
    private readonly MapControl map;
    private readonly MapOverview overview;
    private readonly TextBlock inspection = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private ulong noticeSequence;
    private readonly ListBox units = new() { Height = 122 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock gameplayNotices = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Expander gameplayNoticePanel = new() { Header = "Recent notices", IsExpanded = true, IsVisible = false };
    private readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap, MinHeight = 42 };
    private readonly TextBox input = new()
    {
        Text = "unit.stack-bottom unit=0:100",
        PlaceholderText = "Enter a described command"
    };
    private readonly Button send = new()
    {
        Content = "Send to bottom of stack"
    }, run = new()
    {
        Content = "Run command"
    };
    private readonly Button mapOptions;
    private MapRendererSettingsView? rendererSettings;
    private ComboBox? colorChoice;
    private readonly CheckBox reducedMotion = new() { Content = "Reduced motion" };
    private readonly Dictionary<string, CheckBox> mapChoices = [];
    private readonly InteractionDialogView dialog;
    private readonly FileLibraryView? files;
    private string? lastMedia;
    private ulong serviceTicks;
    private readonly ShellMenu menu;
    private readonly Grid shell;
    private SessionView? rendered;
    private UnitTarget? activeTarget;
    private MapPick? selection;
    private string parseError = "";
    private bool changingItems;

    internal SeamView(SessionPanel model, MapPainter painter, bool pinnedFonts, MapArtSelection? selection,
        LocalGame? game = null, IDesktopAutomation? automation = null)
    {
        this.model = model;
        this.game = game;
        this.automation = automation;
        if (game is not null)
        {
            input.Clear();
        }
        contentLoaded = pinnedFonts;
        map = new(painter, clock: () => checked((long)SeamApp.Clock.Milliseconds));
        overview = new(map, painter);
        AutomationProperties.SetAutomationId(units, "units");
        AutomationProperties.SetAutomationId(input, "command-input");
        foreach (var (button, id) in new[] {
            (send, "send"),
            (run, "run")
            })
        {
            AutomationProperties.SetAutomationId(button, id);
        }

        var root = shell = new Grid { RowDefinitions = new("Auto,Auto,*"), Background = Brush.Parse("#eee9de") };
        menu = new ShellMenu(ShellActions.Create(model.View, null, new()).Concat(PlayMenuActions()), DispatchAction);
        root.Children.Add(menu.Control);
        var header = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(22, 11) };
        var logo = new TextBlock { Text = "OpenCiv2", FontSize = 29, FontWeight = FontWeight.Bold };
        var chapter = new TextBlock
        {
            Text = game is not null ? "Alder Valley · Local game" :
                pinnedFonts ? "Authored map  /  Riverbend" : "Riverbend  /  Limited graphics preview",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (pinnedFonts)
        {
            TextElement.SetFontFamily(root, GraphicsFonts.Sans);
            logo.FontFamily = GraphicsFonts.Serif;
            input.FontFamily = GraphicsFonts.Mono;
        }
        header.Children.Add(logo);
        Grid.SetColumn(chapter, 1);
        header.Children.Add(chapter);
        Grid.SetRow(header, 1);
        root.Children.Add(header);

        var layout = new Grid
        {
            ColumnDefinitions = new("*,290"),
            Margin = new Thickness(14, 0, 14, 14),
            ColumnSpacing = 14
        };
        Grid.SetRow(layout, 2);
        root.Children.Add(layout);
        var mapArea = new Grid { RowDefinitions = new("*,Auto"), Background = Brush.Parse("#172b33") };
        mapArea.Children.Add(map);
        var inset = new Border
        {
            Background = Brush.Parse("#22353b"),
            BorderBrush = Brush.Parse("#a98d65"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Width = 190,
            Height = 106,
            Margin = new Thickness(8),
            Child = overview
        };
        mapArea.Children.Add(inset);
        var mapHelp = new TextBlock
        {
            Text = game is not null ? "Select a unit to issue orders · Drag to pan · Scroll to zoom" :
                "Drag or use arrows to pan · Scroll to zoom · Ctrl+arrows to inspect · Enter to order",
            FontSize = 12,
            Margin = new Thickness(12, 7),
            Foreground = Brush.Parse("#e2d9c2"),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(mapHelp, 1);
        mapArea.Children.Add(mapHelp);
        layout.Children.Add(mapArea);

        var side = new StackPanel { Spacing = 9, Margin = new Thickness(14, 16) };
        side.Children.Add(new TextBlock { Text = "Selection", FontSize = 19, FontWeight = FontWeight.Bold });
        side.Children.Add(details);
        side.Children.Add(inspection);
        mapOptions = MapOptions(selection);
        side.Children.Add(mapOptions);
        side.Children.Add(new TextBlock { Text = "Your units", FontSize = 16, FontWeight = FontWeight.Bold });
        side.Children.Add(units);
        if (game is null)
        {
            side.Children.Add(send);
        }
        else
        {
            side.Children.Add(BuildPlayOrders());
        }
        side.Children.Add(
            new TextBlock
            {
                Text = "Command console",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 12, 0, 0)
            });
        side.Children.Add(input);
        side.Children.Add(run);
        side.Children.Add(status);
        AutomationProperties.SetName(gameplayNotices, "Recent gameplay notices");
        playControls["notices"] = gameplayNoticePanel;
        gameplayNoticePanel.Content = new ScrollViewer { Content = gameplayNotices, MaxHeight = 180 };
        side.Children.Add(gameplayNoticePanel);
        var scroll = new ScrollViewer { Content = side, Background = Brush.Parse("#f9f6ed") };
        Grid.SetColumn(scroll, 1);
        layout.Children.Add(scroll);

        dialog = new InteractionDialogView(model, pinnedFonts, consoleReplies: game is not null);
        dialog.Responded += () => Refresh(force: true);
        var overlays = new Grid { Children = { root } };
        if (game is not null)
        {
            BuildCityOverlay();
            overlays.Children.Add(cityOverlay);
        }

        overlays.Children.Add(dialog);
        if (SeamApp.Storage is { } storage)
        {
            files = new(new DocumentLibrary(storage, SeamApp.FileTransfer ?? new AvaloniaFileTransfer(() => TopLevel.GetTopLevel(this))),
                () => SeamApp.DisplayPreferences!.SaveAsync(map.Display), storage, SeamApp.Media,
                game is null ? null : OpenClassicGame);
            files.Closed += () => { Refresh(force: true); map.Focus(); };
            overlays.Children.Add(files);
        }
        Content = overlays;
        if (SeamApp.DisplayPreferences is { } preferences)
        {
            mapChoices["grid"].IsChecked = preferences.Saved.Grid;
            mapChoices["bare"].IsChecked = preferences.Saved.Bare;
            reducedMotion.IsChecked = preferences.Saved.ReducedMotion;
            mapChoices["cityRadius"].IsChecked = preferences.Saved.CityRadius;
            mapChoices["ownership"].IsChecked = preferences.Saved.Ownership;
            mapChoices["follow"].IsChecked = preferences.Saved.FollowActive;
            mapChoices["detailedSentry"].IsChecked = preferences.Saved.Sentry == SentryAppearance.Greyscale;
            colorChoice!.SelectedItem = preferences.Saved.Color;
        }
        if (pinnedFonts)
        {
            TextElement.SetFontFamily(dialog, GraphicsFonts.Sans);
            if (files is not null)
            {
                TextElement.SetFontFamily(files, GraphicsFonts.Sans);
            }
        }

        map.SelectionChanged += Pick;
        map.HoverChanged += pick =>
        {
            inspection.Text = pick is { } inspected && map.Scene is { } scene
                ? MapInspection.Describe(scene, inspected)
                : "";
            inspection.IsVisible = inspection.Text.Length != 0;
        };
        map.OrderRequested += () =>
        {
            if (game is null)
            {
                Send();
            }
            else
            {
                OpenOwnedCity();
            }
        };
        units.SelectionChanged += (_, _) => SelectListUnit();
        send.Click += (_, _) => Send();
        run.Click += (_, _) => Run();
        input.GotFocus += (_, _) => menu.SetTextEntry(true);
        input.LostFocus += (_, _) => menu.SetTextEntry(false);
        input.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter || (game is not null && ConsoleEditKey(e.Key)))
            {
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        input.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Run();
                e.Handled = true;
            }
        };
        AddHandler(KeyDownEvent, OnShellKey, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => menu.KeyUp(e), RoutingStrategies.Tunnel, handledEventsToo: true);
        LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (!IsKeyboardFocusWithin)
            {
                menu.ReleaseKeys();
            }
        });
        Refresh();
        if (SeamApp.DisplayPreferences?.Warning is { } startupWarning)
        { parseError = startupWarning; Refresh(force: true); }
    }

    private void SelectListUnit()
    {
        if (changingItems || model.View.Units is not { } records || units.SelectedIndex < 0 ||
            units.SelectedIndex >= records.Records.Count)
        {
            return;
        }

        var unit = records.Records[units.SelectedIndex];
        activeTarget = new(unit.Slot, unit.Token);
        selection = new(unit.Position, MapPickKind.Unit);
        map.Select(selection);
        DescribeSelection();
        if (game is not null && !Busy)
        {
            if (activeTarget != model.View.Map?.Annotations?.ActiveUnit)
            {
                UnitCommand(LocalPlayCommands.Select);
            }
            else
            {
                RefreshPlay(model.View, model.View, false);
            }
        }
    }

    private void Pick(MapPick? value)
    {
        selection = value;
        activeTarget = value is { Kind: MapPickKind.Unit } item ? map.Scene?.Objects(item.Position).Unit?.Target : null;
        SelectUnitRow();
        DescribeSelection();
        if (game is not null && !Busy)
        {
            if (value is { Kind: MapPickKind.City } && model.View.Cities?.Records.Any(c => c.Position == value.Value.Position) == true)
            {
                OpenOwnedCity();
            }
            else if (activeTarget is not null && activeTarget != model.View.Map?.Annotations?.ActiveUnit)
            {
                UnitCommand(LocalPlayCommands.Select);
            }
            else if (model.View.Play is { })
            {
                RefreshPlay(model.View, model.View, false);
            }
        }
    }

    private void SelectUnitRow()
    {
        changingItems = true;
        units.SelectedIndex = -1;
        if (activeTarget is { } target && model.View.Units is { } own)
        {
            for (int i = 0; i < own.Records.Count; i++)
            {
                var unit = own.Records[i];
                if (unit.Slot != target.Slot || unit.Token != target.Token)
                {
                    continue;
                }

                units.SelectedIndex = i;
                break;
            }
        }
        changingItems = false;
    }

    private void DescribeSelection()
    {
        details.Text = selection is { } pick && map.Scene is { } scene
            ? MapInspection.Describe(scene, pick, activeTarget) : "Choose a tile, city or unit.";
        RefreshActions();
    }

    private Button MapOptions(MapArtSelection? selectedArt)
    {
        var options = new StackPanel { Spacing = 4 };
        void Choice(string key, string title, Func<MapDisplay, bool, MapDisplay> update)
        {
            var box = new CheckBox { Content = title };
            mapChoices.Add(key, box);
            box.IsCheckedChanged += (_, _) =>
            {
                map.Display = update(map.Display, box.IsChecked == true);
                RefreshActions();
            };
            options.Children.Add(box);
        }
        Choice("grid", "Grid", (d, enabled) => d with { Grid = enabled });
        Choice("cityRadius", "City radius", (d, enabled) => d with { CityRadius = enabled });
        Choice("ownership", "Disclosed ownership", (d, enabled) => d with { Ownership = enabled });
        Choice("bare", "Bare terrain", (d, enabled) => d with { Bare = enabled });
        Choice("follow", "Follow active unit", (d, enabled) => d with { FollowActive = enabled });
        reducedMotion.IsCheckedChanged += (_, _) =>
        {
            map.Display = map.Display with { ReducedMotion = reducedMotion.IsChecked == true };
            RefreshActions();
        };
        options.Children.Add(reducedMotion);
        Choice(
            "detailedSentry",
            "Detailed sleeping units",
            (d, enabled) => d with { Sentry = enabled ? SentryAppearance.Greyscale : SentryAppearance.Silhouette });
        mapChoices["detailedSentry"].IsChecked = map.Display.Sentry == SentryAppearance.Greyscale;
        if (selectedArt is not null)
        {
            var suppliedArt = new CheckBox { Content = "Use supplied art", IsChecked = true };
            suppliedArt.IsCheckedChanged += (_, _) => map.SelectArt(suppliedArt.IsChecked == true ? selectedArt : null);
            options.Children.Add(suppliedArt);
        }
        var color = colorChoice = new ComboBox { ItemsSource = Enum.GetValues<MapColorCorrection>(), SelectedIndex = 0 };
        AutomationProperties.SetName(color, "Map color correction");
        color.SelectionChanged += (_, _) =>
        {
            if (color.SelectedItem is MapColorCorrection selected)
            {
                map.Display = map.Display with { Color = selected };
            }
        };
        options.Children.Add(color);
        var recover = new Button { Content = "Reload map textures" };
        recover.Click += (_, _) => map.RecoverResources();
        options.Children.Add(recover);
        rendererSettings = new MapRendererSettingsView(map, SeamApp.RendererPreferences);
        options.Children.Add(rendererSettings);
        return new Button
        {
            Content = "Map options",
            Flyout = new Flyout { Content = new ScrollViewer { Content = options, MaxHeight = 540 } }
        };
    }

    internal void AdvancePresentation()
    {
        serviceTicks++;
        map.AdvancePresentation(checked((long)SeamApp.Clock.Milliseconds));
    }
    internal void StopServices() { files?.StopPlayback(); files?.Model.Dispose(); }
    internal void RestoreModalFocus() { dialog.RestoreFocus(); files?.RestoreFocus(); }

    private bool Busy => model.View.Command?.Status is CommandStatus.Pending or CommandStatus.AwaitingInteraction;

    private void Send() => DispatchAction(ShellActions.StackBottom);

    private void Run()
    {
        if (Busy)
        {
            return;
        }

        try
        {
            parseError = "";
            dialog.RememberFocus(map);
            mapOptions.Flyout?.Hide();
            string line = input.Text?.Trim() ?? "";
            if (game is not null && line.Length > 0)
            {
                RememberLine(line);
                if (ConsoleQuery(line))
                {
                    Refresh(force: true);
                    return;
                }
            }

            model.Submit(CommandText.Parse(line, model.View.Commands));
        }
        catch (ArgumentException error)
        {
            parseError = error.Message;
        }
        Refresh(force: true);
    }

    public void Refresh(bool force = false)
    {
        var view = model.View;
        if (!force && ReferenceEquals(view, rendered))
        {
            return;
        }

        var previous = rendered;
        rendered = view;
        bool newBinding = view.Identity != previous?.Identity;
        if (newBinding)
        {
            noticeSequence = 0;
            activeTarget = null;
            selection = null;
            parseError = "";
        }
        map.Show(view.Map);
        if (view.Command is { Status: CommandStatus.Completed, Committed.Count: > 0 } completed && game is null &&
            (previous?.Command?.Status != CommandStatus.Completed
                || previous.Command.Correlation != completed.Correlation))
        {
            map.Queue(++noticeSequence, "Stack order updated", 2400);
            _ = PresentNoticeAsync();
        }

        if (selection is { } old && map.Scene?.Contains(old.Position) != true)
        {
            activeTarget = null;
            selection = null;
            map.Select(null);
        }
        if (newBinding || !Equals(view.Units?.Records, previous?.Units?.Records))
        {
            changingItems = true;
            units.ItemsSource = view.Units?.Records.Select(u =>
            {
                var name = view.Play?.Units.FirstOrDefault(p => p.Target == new UnitTarget(u.Slot, u.Token))?.Name
                    ?? ("Unit " + u.Slot.Value);
                return $"{name}   ({u.Position.X}, {u.Position.Y})";
            }).ToArray()
                ?? [];
            changingItems = false;
            if (newBinding && view.Units is { Records.Count: > 0 } starter)
            {
                var first = game is not null && view.Map?.Annotations?.ActiveUnit is { } target
                    ? starter.Records.FirstOrDefault(u => u.Slot == target.Slot && u.Token == target.Token) ?? starter.Records[0]
                    : starter.Records[0];
                activeTarget = new(first.Slot, first.Token);
                selection = new(first.Position, MapPickKind.Unit);
                map.Select(selection);
            }
            SelectUnitRow();
            if (activeTarget is not null)
            {
                if (units.SelectedIndex < 0)
                {
                    Pick(null);
                }
                else if (view.Units is { } current)
                {
                    var unit = current.Records[units.SelectedIndex];
                    selection = new(unit.Position, MapPickKind.Unit);
                }
                map.Select(selection);
            }
        }

        if (game is not null && view.Map?.Annotations?.ActiveUnit is { } selected
            && view.Units?.Records.FirstOrDefault(unit =>
                unit.Slot == selected.Slot && unit.Token == selected.Token) is { } selectedUnit
            && (previous?.Map?.Annotations?.ActiveUnit != selected
                || previous?.Units?.Records.Any(unit => unit.Slot == selected.Slot && unit.Token == selected.Token
                    && unit.Position != selectedUnit.Position) == true))
        {
            activeTarget = selected;
            selection = new(selectedUnit.Position, MapPickKind.Unit);
            SelectUnitRow();
            map.Select(selection);
        }

        RefreshPlay(view, previous, newBinding);
        if (!Equals(view.Notices, previous?.Notices))
        {
            gameplayNoticePanel.IsVisible = view.Notices?.Items.Count > 0;
            gameplayNotices.Text = view.Notices is { } notices
                ? string.Join("\n\n", notices.Items.Reverse().Select(n => $"Turn {n.Turn}: {n.Text}"))
                    + (notices.Omitted > 0 ? $"\n\n{notices.Omitted} earlier notices omitted." : "") : "";
        }
        shell.IsEnabled = !Busy && !transferring && !cityOverlay.IsVisible && files?.IsVisible != true;
        bool gameActive = game is null || view.Play?.GameActive == true;
        map.IsVisible = overview.IsVisible = units.IsVisible = gameActive;
        map.IsEnabled = overview.IsEnabled = units.IsEnabled = !Busy && gameActive;
        input.IsEnabled = !Busy;
        dialog.Refresh(map);

        run.IsEnabled = view.Identity is not null && !Busy;
        status.Text = parseError.Length > 0 ? parseError : view.Command is { } command
            ? game is not null ? CommandText.Result(command)
                : $"{command.Status}{(command.Code.Length == 0 ? "" : " · " + command.Code)} · {command.Committed.Count} committed"
            : game is null ? "Stack order is available for your units." : "Type help for commands.";
        DescribeSelection();
    }

    private async Task PresentNoticeAsync()
    {
        try
        { lastMedia = (await SeamApp.Media.PlayAsync(new("stack-order-updated", "Stack order updated"))).ToString(); }
        catch (Exception error) when (error is IOException or InvalidOperationException) { lastMedia = MediaResult.Unavailable.ToString(); }
    }

    /// <summary>Read-only observations for application input tests, limited to the displayed client.</summary>
    public string Snapshot()
    {
        var view = model.View;
        JsonObject ControlBounds(Control? control)
        {
            if (control is null)
            {
                return new() { ["x"] = 0, ["y"] = 0, ["width"] = 0, ["height"] = 0, ["enabled"] = false };
            }
            var point = control.TranslatePoint(default, this) ?? default;
            return new()
            {
                ["x"] = point.X,
                ["y"] = point.Y,
                ["width"] = control.Bounds.Width,
                ["height"] = control.Bounds.Height,
                ["enabled"] = control.IsEffectivelyEnabled,
                ["focused"] = control.IsKeyboardFocusWithin
            };
        }

        JsonObject? FlyoutControlBounds(Control control, double scaling)
        {
            if (mapOptions.Flyout?.IsOpen != true || TopLevel.GetTopLevel(control) is null || control.Bounds.Width <= 0)
            {
                return null;
            }

            return PopupBounds(control, scaling);
        }

        var command = view.Command;
        var stack = map.Scene is { } scene && scene.Contains(new(2, 2)) ? scene.Objects(new(2, 2)) : (MapObjects?)null;
        var camera = map.Camera;
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        return new JsonObject
        {
            ["scaling"] = scaling,
            ["contentLoaded"] = contentLoaded,
            ["width"] = Bounds.Width,
            ["height"] = Bounds.Height,
            ["selected"] = units.SelectedIndex,
            ["inputFocused"] = input.IsFocused,
            ["observer"] = view.Identity?.Observer.Value.ToString(CultureInfo.InvariantCulture),
            ["units"] = new JsonArray(
                view.Units?.Records.Select(
                    u => (JsonNode)new JsonObject
                    {
                        ["slot"] = u.Slot.Value.ToString(CultureInfo.InvariantCulture),
                        ["token"] = u.Token.Value.ToString(CultureInfo.InvariantCulture)
                    })
                    .ToArray()
                    ?? []),
            ["status"] = command?.Status.ToString(),
            ["effects"] = command?.Committed.Count ?? 0,
            ["changed"] = command?.Result.FirstOrDefault(
                v => v.Name == "changed")?.Value is CommandValue.Boolean changed
                ? changed.Value
                : null,
            ["message"] = status.Text,
            ["prompt"] = dialog.Prompt,
            ["dialog"] = DialogSnapshot(ControlBounds),
            ["play"] = PlaySnapshot(view, ControlBounds),
            ["files"] = FilesSnapshot(ControlBounds),
            ["media"] = lastMedia,
            ["serviceTicks"] = serviceTicks,
            ["rendererPreferences"] = new JsonObject
            {
                ["saved"] = SeamApp.RendererPreferences.Saved.Mode.ToString(),
                ["message"] = rendererSettings?.Message,
                ["choice"] = rendererSettings is null ? null : PopupBounds(rendererSettings.Choice, scaling),
                ["focused"] = rendererSettings?.Choice.IsKeyboardFocusWithin
            },
            ["menu"] = MenuSnapshot(scaling),
            ["map"] = new JsonObject
            {
                ["x"] = camera.X,
                ["y"] = camera.Y,
                ["zoom"] = camera.Zoom,
                ["selection"] = selection?.Kind.ToString(),
                ["tileX"] = selection?.Position.X,
                ["tileY"] = selection?.Position.Y,
                ["stackVisible"] = stack?.Count,
                ["stackTop"] = stack?.Unit?.Target?.Slot.Value,
                ["revision"] = view.Map?.Revision,
                ["grid"] = map.Display.Grid,
                ["cityRadius"] = map.Display.CityRadius,
                ["ownership"] = map.Display.Ownership,
                ["bare"] = map.Display.Bare,
                ["reducedMotion"] = map.Display.ReducedMotion,
                ["notices"] = map.LastDraw?.Stats.Notices,
                ["composition"] = map.LastDraw?.Stats.Composition,
                ["requestedRenderer"] = map.LastDraw?.Stats.RequestedRenderer,
                ["rendererFallback"] = map.LastDraw?.Stats.RendererFallback,
                ["detail"] = details.Text
            },
            ["controls"] = new JsonObject
            {
                ["map"] = ControlBounds(map),
                ["overview"] = ControlBounds(overview),
                ["send"] = ControlBounds(send),
                ["run"] = ControlBounds(run),
                ["input"] = ControlBounds(input),
                ["confirm"] = ControlBounds(dialog.Choices.GetValueOrDefault(1)),
                ["decline"] = ControlBounds(dialog.Choices.GetValueOrDefault(0)),
                ["cancel"] = ControlBounds(dialog.Cancel),
                ["mapOptions"] = ControlBounds(mapOptions),
                ["motionToggle"] = FlyoutControlBounds(reducedMotion, scaling),
                ["modeToggles"] = new JsonObject(mapChoices.Select(entry =>
                    new KeyValuePair<string, JsonNode?>(entry.Key, FlyoutControlBounds(entry.Value, scaling))))
            }
        }.ToJsonString();
    }
}
