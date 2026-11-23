using OpenCiv2.Base;

namespace OpenCiv2.App;

/// <summary>An optional desktop task driven on the same lane as the game session.</summary>
public interface IDesktopAutomation : IDisposable
{
    DesktopAutomationState State { get; }
    void Start();
    void TogglePause();
    void Cancel();
    void Tick();
    bool Retains(CommandId command);
}

public sealed record DesktopAutomationState(string Status, string Detail,
    bool CanStart = false, bool CanPause = false, bool CanCancel = false, bool Paused = false);
