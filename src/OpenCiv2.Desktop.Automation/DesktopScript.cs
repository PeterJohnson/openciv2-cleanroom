using System.ComponentModel;
using OpenCiv2.App;
using OpenCiv2.Automation;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

internal sealed class DesktopScript(LocalGame game, SessionPanel panel, string source, string name) : IDesktopAutomation
{
    private ScriptTask? task;
    private ClientIdentity? binding;
    private CommandId? followed;
    private string status = "Ready";
    private string? detail;

    public DesktopAutomationState State
    {
        get
        {
            bool paused = task?.Status == ScriptStatus.Paused;
            bool alive = paused || task?.Status == ScriptStatus.Running;
            return new(status, detail ?? name, task is null && !game.Busy, alive, alive, paused);
        }
    }

    public void Start()
    {
        if (task is not null || game.Busy)
        {
            return;
        }
        var client = game.Bind(ControllerKind.Automation);
        binding = client.Identity;
        try
        {
            task = new(client, source, new(Ticks: 10000), (script, limits) => new IsolatedLuaMachine(script, limits));
            status = "Running";
            detail = name;
        }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidOperationException or Win32Exception)
        {
            Release();
            status = "Failed";
            detail = error.Message;
        }
    }

    public void TogglePause()
    {
        if (task?.Status == ScriptStatus.Paused)
        {
            task.Resume();
        }
        else
        {
            task?.Pause();
        }
        UpdateStatus();
    }

    public void Cancel()
    {
        task?.Cancel();
        UpdateStatus();
    }

    public void Tick()
    {
        if (task is null)
        {
            return;
        }
        if (binding?.Runtime != game.Player.Identity.Runtime)
        {
            Release();
            status = "Stopped";
            detail = "Game reloaded";
            return;
        }
        task.Tick();
        var commands = task.SubmittedCommands;
        if (commands.Count > 0 && followed != commands[^1])
        {
            followed = commands[^1];
            panel.Follow(commands[^1]);
        }
        UpdateStatus();
        if (task.Status is not (ScriptStatus.Running or ScriptStatus.Paused) && !game.Busy)
        {
            Release();
        }
    }

    public bool Retains(CommandId command) => task?.SubmittedCommands.Contains(command) == true;

    private void UpdateStatus()
    {
        if (task is not null)
        {
            status = task.Status.ToString();
            detail = task.Error.Length > 0 ? task.Error : task.Output.Length > 0 ? task.Output.TrimEnd() : name;
        }
    }

    private void Release()
    {
        task?.Dispose();
        if (binding is { } identity && identity.Runtime == game.Player.Identity.Runtime)
        {
            foreach (var command in task?.SubmittedCommands ?? OwnedList<CommandId>.Empty)
            {
                game.RetireCommand(command);
            }
            game.Revoke(identity);
        }
        task = null;
        binding = null;
        followed = null;
    }

    public void Dispose() => Release();
}
