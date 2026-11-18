using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace OpenCiv2.App;

public sealed class SeamApp : Application
{
    private SeamSession? owner;
    private DispatcherTimer? timer;
    public static Action<string>? Observe { get; set; }
    public static SeamView? CurrentView { get; private set; }
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        owner = new(); var view = new SeamView(owner.Panel); CurrentView = view;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window { Title = "OpenCiv2 · integration", Width = 880, Height = 760, MinWidth = 640, MinHeight = 640, Content = view };
            desktop.Exit += (_, _) => Stop();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single) single.MainView = view;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        timer.Tick += (_, _) => { owner.Tick(); view.Refresh(); Observe?.Invoke(view.Snapshot()); }; timer.Start();
        base.OnFrameworkInitializationCompleted();
    }
    private void Stop() { timer?.Stop(); owner?.Dispose(); owner = null; CurrentView = null; }
}
