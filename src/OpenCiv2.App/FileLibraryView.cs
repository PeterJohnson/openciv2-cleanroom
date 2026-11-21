using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OpenCiv2.IO;

namespace OpenCiv2.App;

internal sealed class FileLibraryView : Border
{
    internal DocumentLibrary Model { get; }
    internal ListBox Files { get; } = new() { MinHeight = 100, MaxHeight = 220 };
    internal Dictionary<string, Button> Buttons { get; } = [];
    private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Func<Task> savePreferences;
    private bool savingPreferences;
    internal event Action? Closed;
    internal string Message => message.Text ?? "";
    internal FileLibraryView(DocumentLibrary model, Func<Task> savePreferences)
    {
        Model = model;
        this.savePreferences = savePreferences;
        IsVisible = false;
        Background = Brush.Parse("#99000000");
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Files and preferences", FontSize = 24, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock { Text = "Keep imported files here and export an unchanged copy. Opening a playable game is not available yet.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(Files);
        var actions = new WrapPanel();
        Add(actions, "import", "Import file…", () => Model.ImportAsync());
        Add(actions, "export", "Export selected…", () => Selected is { } entry ? Model.ExportAsync(entry.Key) : Task.CompletedTask);
        Add(actions, "remove", "Remove stored copy", () => Selected is { } entry ? Model.RemoveAsync(entry.Key) : Task.CompletedTask);
        panel.Children.Add(actions);
        Add(panel, "cancel", "Cancel file selection", () => { Model.CancelSelection(); return Task.CompletedTask; });
        Add(panel, "preferences", "Save display preferences", SavePreferences);
        panel.Children.Add(message);
        Add(panel, "close", "Close", () => { Close(); return Task.CompletedTask; });
        Child = new Border
        {
            Background = Brush.Parse("#f9f6ed"),
            MaxWidth = 570,
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new ScrollViewer { Content = panel, MaxHeight = 560 }
        };
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (Model.CanCancel)
                {
                    Model.CancelSelection();
                }
                else if (!Model.Busy && !savingPreferences)
                {
                    Close();
                }

                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        Files.SelectionChanged += (_, _) => Refresh();
        Model.Changed += Refresh;
    }
    private DocumentEntry? Selected => Files.SelectedItem as DocumentEntry;
    private void Add(Panel panel, string id, string label, Func<Task> action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0) };
        AutomationProperties.SetAutomationId(button, "files." + id);
        button.Click += async (_, _) => await action();
        Buttons.Add(id, button);
        panel.Children.Add(button);
    }
    internal async Task OpenAsync()
    {
        IsVisible = true;
        await Model.RefreshAsync();
        // Menu dismissal restores its own focus after dispatch; focus the realized modal on the next turn.
        Dispatcher.UIThread.Post(() => { if (IsVisible && !Model.Busy) { Buttons["import"].Focus(); } }, DispatcherPriority.Input);
    }
    private void Close()
    {
        if (Model.Busy || savingPreferences)
        {
            return;
        }

        IsVisible = false;
        Closed?.Invoke();
    }
    private async Task SavePreferences()
    {
        if (savingPreferences || Model.Busy)
        {
            return;
        }

        savingPreferences = true;
        Refresh();
        try
        { await savePreferences(); message.Text = "Display preferences saved."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { message.Text = "Could not save display preferences: " + error.Message; }
        finally { savingPreferences = false; Refresh(preserveMessage: true); }
    }
    private void Refresh() => Refresh(false);
    private void Refresh(bool preserveMessage)
    {
        if (!ReferenceEquals(Files.ItemsSource, Model.Entries))
        {
            string? key = Selected?.Key;
            Files.ItemsSource = Model.Entries;
            Files.SelectedItem = Model.Entries.FirstOrDefault(entry => entry.Key == key) ?? Model.Entries.LastOrDefault();
        }
        bool idle = !Model.Busy && !savingPreferences;
        Files.IsEnabled = idle;
        Buttons["import"].IsEnabled = Buttons["preferences"].IsEnabled = Buttons["close"].IsEnabled = idle;
        Buttons["export"].IsEnabled = Buttons["remove"].IsEnabled = idle && Selected is not null;
        Buttons["cancel"].IsEnabled = Model.CanCancel;
        Buttons["cancel"].IsVisible = Model.CanCancel;
        if (!preserveMessage)
        {
            message.Text = Model.Message;
        }
    }
}
