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
using OpenCiv2.App.Map;
using OpenCiv2.Base;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Graphics;
using OpenCiv2.MapRendering;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

/// <summary>P01/P04/P05 presentation over the scoped, owned model and production command lane.</summary>
public sealed class SeamView : UserControl
{
    private readonly SessionPanel model;
    private readonly MapControl map;
    private readonly MapOverview overview;
    private readonly TextBlock inspection = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private ulong noticeSequence;
    private readonly ListBox units = new() { Height = 122 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock prompt = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap, MinHeight = 42 };
    private readonly TextBox input = new() { Text = "unit.stack-bottom unit=0:100", PlaceholderText = "Enter a described command" };
    private readonly Button send = new() { Content = "Send to bottom of stack" }, run = new() { Content = "Run command" },
        yes = new() { Content = "Confirm" }, no = new() { Content = "Decline" }, cancel = new() { Content = "Cancel command" };
    private readonly Button mapOptions;
    private readonly CheckBox reducedMotion = new() { Content = "Reduced motion" };
    private readonly Dictionary<string, CheckBox> mapChoices = [];
    private readonly Border dialog;
    private SessionView? rendered;
    private InteractionId? shownInteraction;
    private UnitTarget? activeTarget;
    private MapPick? selection;
    private string parseError = "";
    private bool changingItems;

    internal SeamView(SessionPanel model, MapPainter painter, bool pinnedFonts, MapArtSelection? selection)
    {
        this.model = model;
        map = new(painter);
        overview = new(map, painter);
        AutomationProperties.SetAutomationId(units, "units");
        AutomationProperties.SetAutomationId(input, "command-input");
        foreach (var (button, id) in new[] { (send, "send"), (run, "run"), (yes, "confirm"), (no, "decline"), (cancel, "cancel") })
            AutomationProperties.SetAutomationId(button, id);

        var root = new Grid { RowDefinitions = new("Auto,*"), Background = Brush.Parse("#eee9de") };
        var header = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(22, 11) };
        var logo = new TextBlock { Text = "OpenCiv2", FontSize = 29, FontWeight = FontWeight.Bold };
        var chapter = new TextBlock
        {
            Text = pinnedFonts ? "Authored map  /  Riverbend" : "Riverbend  /  Limited graphics preview",
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
        root.Children.Add(header);

        var layout = new Grid { ColumnDefinitions = new("*,290"), Margin = new Thickness(14, 0, 14, 14), ColumnSpacing = 14 };
        Grid.SetRow(layout, 1);
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
            Text = "Drag or use arrows to pan · Scroll to zoom · Ctrl+arrows to inspect · Enter to order",
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
        side.Children.Add(send);
        side.Children.Add(new TextBlock { Text = "Command console", FontSize = 16, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 12, 0, 0) });
        side.Children.Add(input);
        side.Children.Add(run);
        side.Children.Add(status);
        var scroll = new ScrollViewer { Content = side, Background = Brush.Parse("#f9f6ed") };
        Grid.SetColumn(scroll, 1);
        layout.Children.Add(scroll);

        var actions = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 6 };
        actions.Children.Add(yes);
        actions.Children.Add(no);
        actions.Children.Add(cancel);
        var decision = new StackPanel { Spacing = 14, Children = { prompt, actions } };
        var card = new Border
        {
            Child = decision,
            Padding = new Thickness(20),
            MaxWidth = 410,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush.Parse("#fff8e7"),
            BorderBrush = Brush.Parse("#a58557"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        dialog = new Border { Child = card, Background = Brush.Parse("#8e172b33"), IsVisible = false };
        AutomationProperties.SetName(card, "Stack order confirmation");
        Grid.SetRowSpan(dialog, 2);
        root.Children.Add(dialog);
        Content = root;

        map.SelectionChanged += Pick;
        map.HoverChanged += pick =>
        {
            inspection.Text = pick is { } inspected && map.Scene is { } scene ? MapInspection.Describe(scene, inspected) : "";
            inspection.IsVisible = inspection.Text.Length != 0;
        };
        map.OrderRequested += Send;
        units.SelectionChanged += (_, _) => SelectListUnit();
        send.Click += (_, _) => Send();
        run.Click += (_, _) => Run();
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) e.Handled = true; };
        input.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Enter) { Run(); e.Handled = true; }
        };
        yes.Click += (_, _) => Decide(1);
        no.Click += (_, _) => Decide(0);
        cancel.Click += (_, _) => Decide(null);
        AddHandler(KeyDownEvent, OnDialogKey, RoutingStrategies.Tunnel);
        Refresh();
    }

    private void OnDialogKey(object? sender, KeyEventArgs e)
    {
        if (!dialog.IsVisible || e.Key != Key.Escape) return;
        Decide(null);
        e.Handled = true;
    }

    private void Decide(int? choice)
    {
        if (shownInteraction is not { } id || model.View.Command?.Interaction?.Id != id || !yes.IsEnabled) return;
        bool accepted = choice is int selected ? model.Reply(selected) : model.Cancel();
        if (accepted) yes.IsEnabled = no.IsEnabled = cancel.IsEnabled = false;
        Refresh(force: true);
    }

    private void SelectListUnit()
    {
        if (changingItems || model.View.Units is not { } records || units.SelectedIndex < 0 ||
            units.SelectedIndex >= records.Records.Count) return;
        var unit = records.Records[units.SelectedIndex];
        activeTarget = new(unit.Slot, unit.Token);
        selection = new(new(unit.X, unit.Y), MapPickKind.Unit);
        map.Select(selection);
        DescribeSelection();
    }

    private void Pick(MapPick? value)
    {
        selection = value;
        activeTarget = value is { Kind: MapPickKind.Unit } item ? map.Scene?.Objects(item.Position).Unit?.Target : null;
        SelectUnitRow();
        DescribeSelection();
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
                if (unit.Slot != target.Slot || unit.Token != target.Token) continue;
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
        send.IsEnabled = activeTarget is not null && model.View.Identity is not null && !Busy;
    }

    private Button MapOptions(MapArtSelection? selectedArt)
    {
        var options = new StackPanel { Spacing = 4 };
        void Choice(string key, string title, Func<MapDisplay, bool, MapDisplay> update)
        {
            var box = new CheckBox { Content = title };
            mapChoices.Add(key, box);
            box.IsCheckedChanged += (_, _) => map.Display = update(map.Display, box.IsChecked == true);
            options.Children.Add(box);
        }
        Choice("grid", "Grid", (d, enabled) => d with { Grid = enabled });
        Choice("cityRadius", "City radius", (d, enabled) => d with { CityRadius = enabled });
        Choice("ownership", "Disclosed ownership", (d, enabled) => d with { Ownership = enabled });
        Choice("bare", "Bare terrain", (d, enabled) => d with { Bare = enabled });
        Choice("follow", "Follow active unit", (d, enabled) => d with { FollowActive = enabled });
        reducedMotion.IsCheckedChanged += (_, _) => map.Display = map.Display with { ReducedMotion = reducedMotion.IsChecked == true };
        options.Children.Add(reducedMotion);
        Choice("detailedSentry", "Detailed sleeping units", (d, enabled) => d with { Sentry = enabled ? SentryAppearance.Greyscale : SentryAppearance.Silhouette });
        if (selectedArt is not null)
        {
            var suppliedArt = new CheckBox { Content = "Use supplied art", IsChecked = true };
            suppliedArt.IsCheckedChanged += (_, _) => map.SelectArt(suppliedArt.IsChecked == true ? selectedArt : null);
            options.Children.Add(suppliedArt);
        }
        var color = new ComboBox { ItemsSource = Enum.GetValues<MapColorCorrection>(), SelectedIndex = 0 };
        AutomationProperties.SetName(color, "Map color correction");
        color.SelectionChanged += (_, _) =>
        {
            if (color.SelectedItem is MapColorCorrection selected) map.Display = map.Display with { Color = selected };
        };
        options.Children.Add(color);
        var recover = new Button { Content = "Reload map textures" };
        recover.Click += (_, _) => map.RecoverResources();
        options.Children.Add(recover);
        options.Children.Add(new MapRendererSettingsView(map, SeamApp.RendererPreferences));
        return new Button { Content = "Map options", Flyout = new Flyout { Content = new ScrollViewer { Content = options, MaxHeight = 540 } } };
    }

    internal void AdvancePresentation() => map.AdvancePresentation(Environment.TickCount64);

    private bool Busy => model.View.Command?.Status is CommandStatus.Pending or CommandStatus.AwaitingInteraction;

    private void Send()
    {
        if (Busy || activeTarget is not { } target) return;
        parseError = "";
        model.Submit(new(LocalUnitCommands.StackBottomName, new([new("unit", new CommandValue.Unit(target))])));
        Refresh();
    }

    private void Run()
    {
        if (Busy) return;
        try { parseError = ""; model.Submit(CommandText.Parse(input.Text ?? "", model.View.Commands)); }
        catch (ArgumentException error) { parseError = error.Message; }
        Refresh(force: true);
    }

    public void Refresh(bool force = false)
    {
        var view = model.View;
        if (!force && ReferenceEquals(view, rendered)) return;
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
        if (view.Command is { Status: CommandStatus.Completed, Committed.Count: > 0 } completed &&
            (previous?.Command?.Status != CommandStatus.Completed || previous.Command.Correlation != completed.Correlation))
            map.Queue(++noticeSequence, "Stack order updated", 2400);
        if (selection is { } old && map.Scene?.Contains(old.Position) != true)
        {
            activeTarget = null;
            selection = null;
            map.Select(null);
        }
        if (newBinding || !Equals(view.Units?.Records, previous?.Units?.Records))
        {
            changingItems = true;
            units.ItemsSource = view.Units?.Records.Select(u => $"Unit {u.Slot.Value}   ({u.X}, {u.Y})").ToArray() ?? [];
            changingItems = false;
            if (newBinding && view.Units is { Records.Count: > 0 } starter)
            {
                var first = starter.Records[0];
                activeTarget = new(first.Slot, first.Token);
                selection = new(new(first.X, first.Y), MapPickKind.Unit);
                map.Select(selection);
            }
            SelectUnitRow();
            if (activeTarget is not null)
            {
                if (units.SelectedIndex < 0) Pick(null);
                else if (view.Units is { } current)
                {
                    var unit = current.Records[units.SelectedIndex];
                    selection = new(new(unit.X, unit.Y), MapPickKind.Unit);
                }
                map.Select(selection);
            }
        }

        var interaction = view.Command?.Interaction;
        bool hadDialog = dialog.IsVisible;
        bool firstDisplay = interaction is not null && shownInteraction != interaction.Id;
        shownInteraction = interaction?.Id;
        dialog.IsVisible = interaction is not null;
        // The shell holds input until the session consumes the prior reply.
        if (firstDisplay) yes.IsEnabled = no.IsEnabled = cancel.IsEnabled = true;
        else if (interaction is null) yes.IsEnabled = no.IsEnabled = cancel.IsEnabled = false;
        map.IsEnabled = overview.IsEnabled = units.IsEnabled = input.IsEnabled = !Busy;
        if (firstDisplay) yes.Focus();
        else if (hadDialog && !dialog.IsVisible) map.Focus();
        run.IsEnabled = view.Identity is not null && !Busy;
        prompt.Text = interaction?.Spec.Prompt ?? "No decision pending.";
        status.Text = parseError.Length > 0 ? parseError : view.Command is { } command
            ? $"{command.Status}{(command.Code.Length == 0 ? "" : " · " + command.Code)} · {command.Committed.Count} committed"
            : "Stack order is available for your units.";
        DescribeSelection();
    }

    /// <summary>Read-only observations for application input tests, limited to the displayed client.</summary>
    public string Snapshot()
    {
        var view = model.View;
        JsonObject ControlBounds(Control control)
        {
            var point = control.TranslatePoint(default, this) ?? default;
            return new() { ["x"] = point.X, ["y"] = point.Y, ["width"] = control.Bounds.Width, ["height"] = control.Bounds.Height, ["enabled"] = control.IsEnabled };
        }

        JsonObject? FlyoutControlBounds(Control control, double scaling)
        {
            if (mapOptions.Flyout?.IsOpen != true || TopLevel.GetTopLevel(control) is null || control.Bounds.Width <= 0)
                return null;
            var origin = this.PointToScreen(default);
            var point = control.PointToScreen(default);
            return new()
            {
                ["x"] = (point.X - origin.X) / scaling,
                ["y"] = (point.Y - origin.Y) / scaling,
                ["width"] = control.Bounds.Width,
                ["height"] = control.Bounds.Height
            };
        }

        var command = view.Command;
        var stack = map.Scene is { } scene && scene.Contains(new(2, 2)) ? scene.Objects(new(2, 2)) : (MapObjects?)null;
        var camera = map.Camera;
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        return new JsonObject
        {
            ["scaling"] = scaling,
            ["width"] = Bounds.Width,
            ["height"] = Bounds.Height,
            ["selected"] = units.SelectedIndex,
            ["inputFocused"] = input.IsFocused,
            ["observer"] = view.Identity?.Observer.Value.ToString(CultureInfo.InvariantCulture),
            ["units"] = new JsonArray(view.Units?.Records.Select(u => (JsonNode)new JsonObject { ["slot"] = u.Slot.Value.ToString(CultureInfo.InvariantCulture), ["token"] = u.Token.Value.ToString(CultureInfo.InvariantCulture) }).ToArray() ?? []),
            ["status"] = command?.Status.ToString(),
            ["effects"] = command?.Committed.Count ?? 0,
            ["changed"] = command?.Result.FirstOrDefault(v => v.Name == "changed")?.Value is CommandValue.Boolean changed ? changed.Value : null,
            ["message"] = status.Text,
            ["prompt"] = prompt.Text,
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
                ["confirm"] = ControlBounds(yes),
                ["decline"] = ControlBounds(no),
                ["cancel"] = ControlBounds(cancel),
                ["mapOptions"] = ControlBounds(mapOptions),
                ["motionToggle"] = FlyoutControlBounds(reducedMotion, scaling),
                ["modeToggles"] = new JsonObject(mapChoices.Select(entry =>
                    new KeyValuePair<string, JsonNode?>(entry.Key, FlyoutControlBounds(entry.Value, scaling))))
            }
        }.ToJsonString();
    }
}
