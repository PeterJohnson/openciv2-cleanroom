using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OpenCiv2.Base;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.App;

internal sealed class FileLibraryView : Border
{
    internal DocumentLibrary Model { get; }
    internal ListBox Files { get; } = new() { MinHeight = 100, MaxHeight = 220 };
    internal Dictionary<string, Button> Buttons { get; } = [];
    private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Func<Task> savePreferences;
    private readonly Func<StoredDocument, (bool Opened, string Message)>? openGame;
    private bool savingPreferences;
    private readonly IByteStore storage;
    private readonly IPresentationMedia media;
    private CancellationTokenSource? playback;
    internal bool Playing => playback is not null;
    internal string? PlaybackResult { get; private set; }
    internal event Action? Closed;
    internal string Message => message.Text ?? "";
    internal FileLibraryView(DocumentLibrary model, Func<Task> savePreferences, IByteStore storage,
        IPresentationMedia media, Func<StoredDocument, (bool Opened, string Message)>? openGame = null)
    {
        Model = model;
        this.savePreferences = savePreferences;
        this.openGame = openGame;
        this.storage = storage;
        this.media = media;
        IsVisible = false;
        Background = Brush.Parse("#99000000");
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Files and preferences", FontSize = 24, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock
        {
            Text = openGame is null
                ? "Keep imported files here and export an unchanged copy."
                : "Open a stored Alder Valley save or export an unchanged copy.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(Files);
        var actions = new WrapPanel();
        Add(actions, "import", "Import file…", () => Model.ImportAsync());
        if (openGame is not null)
        {
            Add(actions, "open", "Open selected game", OpenGameAsync);
        }

        Add(actions, "export", "Export selected…", () => Selected is { } entry ? Model.ExportAsync(entry.Key) : Task.CompletedTask);
        Add(actions, "remove", "Remove stored copy", () => Selected is { } entry ? Model.RemoveAsync(entry.Key) : Task.CompletedTask);
        panel.Children.Add(actions);
        var audio = new WrapPanel();
        Add(audio, "play", "Play selected WAV", PlayAsync);
        Add(audio, "stop", "Stop sound", () => { StopPlayback(); return Task.CompletedTask; });
        panel.Children.Add(audio);
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
                if (Playing)
                {
                    StopPlayback();
                }
                else if (Model.CanCancel)
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
        if (Model.Busy || savingPreferences || Playing)
        {
            return;
        }

        IsVisible = false;
        Closed?.Invoke();
    }

    private async Task OpenGameAsync()
    {
        if (openGame is not null && !Playing && !savingPreferences && Selected is { } entry
            && await Model.OpenAsync(entry.Key, openGame))
        {
            Close();
        }
    }
    internal void StopPlayback() => playback?.Cancel();
    internal void RestoreFocus()
    {
        if (IsVisible && !IsKeyboardFocusWithin)
        {
            Buttons[Playing ? "stop" : Model.CanCancel ? "cancel" : "import"].Focus();
        }
    }
    private async Task PlayAsync()
    {
        if (Playing || Model.Busy || savingPreferences || Selected is not { } entry)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        playback = cancellation;
        PlaybackResult = null;
        message.Text = "Playing “" + entry.Name + "”…";
        Refresh(preserveMessage: true);
        Dispatcher.UIThread.Post(() => { if (Playing) { Buttons["stop"].Focus(); } }, DispatcherPriority.Input);
        try
        {
            byte[] bytes = await storage.ReadAsync(entry.Key) ?? throw new IOException("Stored file is unavailable.");
            cancellation.Token.ThrowIfCancellationRequested();
            var document = StoredDocument.Decode(bytes);
            var audio = WaveAudio.Decode(document.Bytes.ToArray());
            var result = await media.PlayAsync(new(entry.Key, document.Name, audio), cancellation.Token);
            PlaybackResult = result.ToString();
            message.Text = result switch
            {
                MediaResult.Completed => "Playback finished: " + document.Name,
                MediaResult.Cancelled => "Playback stopped: " + document.Name,
                MediaResult.Skipped => "Sound is disabled: " + document.Name,
                _ => "Audio output unavailable or permission denied: " + document.Name
            };
        }
        catch (OperationCanceledException)
        { PlaybackResult = MediaResult.Cancelled.ToString(); message.Text = "Playback stopped."; }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException)
        { PlaybackResult = MediaResult.Unavailable.ToString(); message.Text = "Cannot play this file: " + error.Message; }
        finally
        {
            playback = null;
            Refresh(preserveMessage: true);
            if (IsVisible)
            {
                Buttons["play"].Focus();
            }
        }
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
        bool idle = !Model.Busy && !savingPreferences && !Playing;
        Files.IsEnabled = idle;
        Buttons["import"].IsEnabled = Buttons["preferences"].IsEnabled = Buttons["close"].IsEnabled = idle;
        Buttons["export"].IsEnabled = Buttons["remove"].IsEnabled = idle && Selected is not null;
        Buttons["play"].IsEnabled = idle && Selected is not null;
        if (Buttons.TryGetValue("open", out var open))
        {
            open.IsEnabled = idle && Selected is not null;
        }
        Buttons["stop"].IsEnabled = Playing;
        Buttons["cancel"].IsEnabled = Model.CanCancel;
        Buttons["cancel"].IsVisible = Model.CanCancel;
        if (!preserveMessage && !Playing)
        {
            message.Text = Model.Message;
        }
    }
}
