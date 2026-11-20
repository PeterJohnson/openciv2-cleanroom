using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using OpenCiv2.MapRendering;

namespace OpenCiv2.App.Map;

internal sealed class MapRendererSettingsView : StackPanel
{
    internal ComboBox Choice { get; }
    internal string? Message => message.Text;
    private readonly TextBlock active = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap };

    internal MapRendererSettingsView(MapControl map, MapRendererPreferences preferences)
    {
        Width = 260;
        Spacing = 4;
        Children.Add(active);
        Children.Add(new TextBlock { Text = "Renderer after restart" });
        Choice = new ComboBox
        {
            ItemsSource = new[] { "CPU", "GPU (experimental)" },
            SelectedIndex = preferences.Saved.Mode == MapRendererMode.Gpu ? 1 : 0,
            IsEnabled = preferences.CanSave
        };
        AutomationProperties.SetName(Choice, "Map renderer after restart");
        Children.Add(Choice);
        Children.Add(message);
        message.Text = preferences.Warning ?? "CPU is the default. GPU uses more memory.";
        bool refreshing = false;
        AttachedToVisualTree += (_, _) =>
        {
            refreshing = true;
            Choice.SelectedIndex = preferences.Saved.Mode == MapRendererMode.Gpu ? 1 : 0;
            refreshing = false;
        };
        Choice.SelectionChanged += (_, _) =>
        {
            if (refreshing) return;
            try
            {
                preferences.Save(Choice.SelectedIndex == 1 ? MapRendererMode.Gpu : MapRendererMode.Cpu);
                message.Text = "Saved. Restart to apply; a command-line override takes precedence.";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                message.Text = "Could not save renderer preference: " + error.Message;
            }
        };
        map.RendererChanged += Refresh;
        Refresh();

        void Refresh()
        {
            var stats = map.LastDraw?.Stats;
            active.Text = stats is null ? "Renderer: waiting for first frame" :
                "Active renderer: " + (stats.Value.Composition == "gpu" ? "GPU (experimental)" : "CPU") +
                (stats.Value.RendererFallback is { } reason ? ". GPU fallback: " + reason + ". Reload textures to retry." : "");
        }
    }
}
