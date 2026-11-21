using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using OpenCiv2.Contracts;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

/// <summary>Nonblocking P04 dialog with custom drawn, accessible button hit regions.</summary>
internal sealed class InteractionDialogView : UserControl
{
    private readonly SessionPanel session;
    private readonly InteractionDialogModel model = new();
    private readonly StackPanel choices = new() { Spacing = 8 };
    private readonly TextBlock prompt = new() { TextWrapping = TextWrapping.Wrap, FontSize = 17 };
    private readonly TextBlock target = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private readonly Dictionary<int, DialogChoiceButton> buttons = [];
    private readonly StackPanel body;
    private Control? returnFocus;
    private DialogIdentity? displayed;
    public DialogView? Model => model.View;
    public string Prompt => prompt.Text ?? "";
    public string Target => target.Text ?? "";
    public IReadOnlyDictionary<int, DialogChoiceButton> Choices => buttons;
    public Button Cancel { get; private set; } = new() { Content = "Cancel command", IsEnabled = false };
    public event Action? Responded;

    public InteractionDialogView(SessionPanel session, bool pinnedFonts)
    {
        this.session = session;
        IsVisible = false;
        AutomationProperties.SetAutomationId(this, "interaction-dialog");
        AutomationProperties.SetName(this, "Command decision");
        AutomationProperties.SetAutomationId(Cancel, "dialog.cancel");
        var title = new TextBlock { Text = "Your decision", FontSize = 24, FontWeight = FontWeight.Bold };
        if (pinnedFonts)
        {
            title.FontFamily = GraphicsFonts.Serif;
        }

        body = new StackPanel { Spacing = 14, Children = { title, prompt, target, choices, Cancel } };
        KeyboardNavigation.SetTabNavigation(body, KeyboardNavigationMode.Cycle);
        var card = new Border
        {
            Padding = new Thickness(22),
            Margin = new Thickness(16),
            MaxWidth = 470,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush.Parse("#fff8e7"),
            BorderBrush = Brush.Parse("#a58557"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new ScrollViewer { Content = body }
        };
        Content = new Border { Background = Brush.Parse("#b0172b33"), Child = card };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public void RememberFocus(Control fallback)
    {
        returnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control current &&
            current is not MenuItem ? current : fallback;
    }

    public void Refresh(Control fallback)
    {
        model.Refresh(session.View);
        var view = model.View;
        bool wasVisible = IsVisible;
        IsVisible = view is not null;
        if (view is null)
        {
            displayed = null;
            prompt.Text = target.Text = "";
            foreach (var button in buttons.Values)
            {
                button.IsEnabled = false;
            }
            choices.Children.Clear();
            buttons.Clear();
            Cancel.IsEnabled = false;
            if ((wasVisible || returnFocus is not null) && fallback.IsEffectivelyEnabled)
            {
                var restore = returnFocus is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } saved
                    ? saved : fallback;
                restore.Focus();
                returnFocus = null;
            }
            return;
        }

        bool opening = displayed != view.Identity;
        if (opening)
        {
            displayed = view.Identity;
            choices.Children.Clear();
            buttons.Clear();
            // Capture this interaction's identity in every handler, including cancellation.
            foreach (int value in view.Spec.Choices.OrderBy(value => value == 1 ? 0 : 1))
            {
                string label = value switch { 1 => "Confirm", 0 => "Decline", _ => $"Choice {value}" };
                string detail = value switch
                {
                    1 => "Continue with this command",
                    0 => "Leave the stack unchanged",
                    _ => "Use this offered choice"
                };
                var button = new DialogChoiceButton(label, detail);
                AutomationProperties.SetAutomationId(button, $"dialog.choice.{value}");
                button.Click += (_, _) => Respond(view.Identity, value);
                buttons.Add(value, button);
                choices.Children.Add(button);
            }
            Cancel.IsEnabled = false;
            body.Children.Remove(Cancel);
            Cancel = new Button { Content = "Cancel command", HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetAutomationId(Cancel, "dialog.cancel");
            Cancel.Click += (_, _) => Respond(view.Identity, null);
            body.Children.Add(Cancel);
        }

        prompt.Text = view.Spec.Prompt;
        target.Text = view.Submitted ? "Waiting for the command to finish…" : DescribeTarget(view.Spec.Target);
        foreach (var button in buttons.Values)
        {
            button.IsEnabled = !view.Submitted;
        }
        Cancel.IsVisible = view.Spec.Cancellable;
        Cancel.IsEnabled = view.Spec.Cancellable && !view.Submitted;
        if (opening)
        {
            (buttons.GetValueOrDefault(1) ?? buttons.Values.FirstOrDefault() as Control ?? Cancel).Focus();
        }
    }

    private string DescribeTarget(CommandValue? value)
    {
        if (value is CommandValue.Unit unit)
        {
            var own = session.View.Units?.Records.FirstOrDefault(
                item => item.Slot == unit.Value.Slot && item.Token == unit.Value.Token);
            return own is null ? "This unit is no longer available. The command will be checked again." :
                $"Unit {own.Slot.Value} · tile {own.Position.X}, {own.Position.Y}";
        }
        return "";
    }

    private void CancelCurrent(object? sender, RoutedEventArgs e)
    {
        if (displayed is { } identity)
        {
            Respond(identity, null);
        }
    }

    private void Respond(DialogIdentity identity, int? choice)
    {
        model.Respond(session, identity, choice);
        Responded?.Invoke();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) != 0)
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            CancelCurrent(this, e);
            e.Handled = true;
        }
    }
}

/// <summary>A custom painted choice with Button's pointer, keyboard and automation behavior.</summary>
internal sealed class DialogChoiceButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    public DialogChoiceButton(string label, string detail)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(this, label);
        AutomationProperties.SetHelpText(this, detail);
        Template = new FuncControlTemplate<DialogChoiceButton>((owner, _) => new ChoiceSurface(owner, label, detail));
    }

    private sealed class ChoiceSurface : Decorator
    {
        private readonly DialogChoiceButton owner;

        public ChoiceSurface(DialogChoiceButton owner, string label, string detail)
        {
            this.owner = owner;
            MinHeight = 70;
            Child = new StackPanel
            {
                Margin = new Thickness(18, 12),
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = label, FontSize = 16, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock { Text = detail, FontSize = 12, TextWrapping = TextWrapping.Wrap }
                }
            };
            owner.PropertyChanged += (_, _) => InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            var fill = Brush.Parse(!owner.IsEnabled ? "#e8e3d7" : owner.IsPressed ? "#c5d4ce" :
                owner.IsPointerOver ? "#e0ebe3" : "#f8f1df");
            var outline = new Pen(Brush.Parse(owner.IsKeyboardFocusWithin ? "#225e67" : "#b8a47e"),
                owner.IsKeyboardFocusWithin ? 3 : 1);
            context.DrawRectangle(fill, outline, new Rect(Bounds.Size).Deflate(2), 5, 5);
            base.Render(context);
        }
    }
}
