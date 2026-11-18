using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

/// <summary>Presentation receives only the scoped, owned view model; it never accesses engine records.</summary>
public sealed class SeamView : UserControl
{
    private readonly SessionPanel model;
    private readonly ListBox units = new() { Height = 118 };
    private readonly TextBlock status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock prompt = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox input = new() { Text = "unit.stack-bottom unit=0:100", PlaceholderText = "Enter a described command" };
    private readonly Button send = new() { Content = "Move selected unit to bottom" }, run = new() { Content = "Run command" },
        yes = new() { Content = "Confirm" }, no = new() { Content = "Decline" }, cancel = new() { Content = "Cancel command" };
    private SessionView? rendered;
    private string parseError = "";
    public SeamView(SessionPanel model)
    {
        this.model = model;
        AutomationProperties.SetAutomationId(units, "units"); AutomationProperties.SetAutomationId(input, "command-input");
        foreach (var (button, id) in new[] { (send, "send"), (run, "run"), (yes, "confirm"), (no, "decline"), (cancel, "cancel") }) AutomationProperties.SetAutomationId(button, id);
        var body = new StackPanel { Spacing = 14, Margin = new Thickness(28), MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Stretch };
        body.Children.Add(new TextBlock { Text = "OpenCiv2", FontSize = 30, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        body.Children.Add(new TextBlock { Text = "Production session integration · authored world", FontSize = 16 });
        body.Children.Add(new TextBlock { Text = "Your units", FontSize = 20 }); body.Children.Add(units); body.Children.Add(send);
        body.Children.Add(new TextBlock { Text = "Command console", FontSize = 20 }); body.Children.Add(input); body.Children.Add(run);
        body.Children.Add(prompt);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }; actions.Children.Add(yes); actions.Children.Add(no); actions.Children.Add(cancel); body.Children.Add(actions);
        body.Children.Add(status);
        body.Children.Add(new TextBlock { Text = "This integration world exercises stack ordering. Playable turns and full game setup remain under development.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.7 });
        Content = new ScrollViewer { Content = body };
        send.Click += (_, _) =>
        {
            if (units.SelectedIndex is var i && i >= 0 && model.View.Units is { } list && i < list.Records.Count)
            {
                var unit = list.Records[i]; parseError = "";
                model.Submit(new("unit.stack-bottom", new([new("unit", new CommandValue.Unit(new(unit.Slot, unit.Token)))]))); Refresh();
            }
        };
        run.Click += (_, _) => Run();
        input.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { Run(); e.Handled = true; } };
        yes.Click += (_, _) => { model.Reply(1); Refresh(); }; no.Click += (_, _) => { model.Reply(0); Refresh(); }; cancel.Click += (_, _) => { model.Cancel(); Refresh(); };
        Refresh();
    }
    private void Run()
    {
        try { parseError = ""; model.Submit(CommandText.Parse(input.Text ?? "", model.View.Commands)); }
        catch (ArgumentException error) { parseError = error.Message; }
        rendered = null; Refresh();
    }
    public void Refresh()
    {
        var view = model.View; if (view == rendered) return;
        if (view.Units?.Records != rendered?.Units?.Records)
        {
            int selected = units.SelectedIndex; units.ItemsSource = view.Units?.Records.Select(u => $"Unit {u.Slot.Value} · token {u.Token.Value} · ({u.X}, {u.Y})").ToArray() ?? [];
            units.SelectedIndex = view.Units is { Records.Count: > 0 } list ? Math.Clamp(selected, 0, list.Records.Count - 1) : -1;
        }
        bool busy = view.Command?.Status is CommandStatus.Pending or CommandStatus.AwaitingInteraction;
        send.IsEnabled = view.Identity is not null && !busy && view.Units?.Records.Count > 0; run.IsEnabled = view.Identity is not null && !busy;
        yes.IsEnabled = no.IsEnabled = view.Command?.Interaction is not null; cancel.IsEnabled = busy;
        prompt.Text = view.Command?.Interaction?.Spec.Prompt ?? "No decision pending.";
        status.Text = parseError.Length > 0 ? parseError : view.Command is { } command ? $"{command.Status}{(command.Code.Length == 0 ? "" : ": " + command.Code)} · committed effects: {command.Committed.Count}" : "Ready. Select a unit or enter a command.";
        rendered = view;
    }
    /// <summary>Read-only browser smoke observations, restricted to the displayed client's permitted values.</summary>
    public string Snapshot()
    {
        var view = model.View;
        JsonObject ControlBounds(Control control)
        {
            var point = control.TranslatePoint(default, this) ?? default;
            return new() { ["x"] = point.X, ["y"] = point.Y, ["width"] = control.Bounds.Width, ["height"] = control.Bounds.Height, ["enabled"] = control.IsEnabled };
        }
        var command = view.Command;
        return new JsonObject
        {
            ["scaling"] = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1,
            ["width"] = Bounds.Width,
            ["height"] = Bounds.Height,
            ["selected"] = units.SelectedIndex,
            ["observer"] = view.Identity?.Observer.Value.ToString(CultureInfo.InvariantCulture),
            ["units"] = new JsonArray(view.Units?.Records.Select(u => (JsonNode)new JsonObject { ["slot"] = u.Slot.Value.ToString(CultureInfo.InvariantCulture), ["token"] = u.Token.Value.ToString(CultureInfo.InvariantCulture) }).ToArray() ?? []),
            ["status"] = command?.Status.ToString(),
            ["effects"] = command?.Committed.Count ?? 0,
            ["changed"] = command?.Result.FirstOrDefault(v => v.Name == "changed")?.Value is CommandValue.Boolean changed ? changed.Value : null,
            ["message"] = status.Text,
            ["prompt"] = prompt.Text,
            ["controls"] = new JsonObject { ["send"] = ControlBounds(send), ["run"] = ControlBounds(run), ["input"] = ControlBounds(input), ["confirm"] = ControlBounds(yes), ["decline"] = ControlBounds(no), ["cancel"] = ControlBounds(cancel) }
        }.ToJsonString();
    }
}
