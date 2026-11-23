using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.ViewModels;

public sealed record SessionView(ClientIdentity? Identity, LiveRecords<OwnUnit>? Units, LiveRecords<OwnCity>? Cities,
    OwnedList<CommandDescriptor> Commands, CommandUpdate? Command, string Message, MapObservation? Map = null,
    LocalPlayObservation? Play = null)
{
    public static SessionView Empty { get; } = new(
        null,
        null,
        null,
        OwnedList<CommandDescriptor>.Empty,
        null,
        "No active session.");
}

/// <summary>Single-command presentation model over owned player observations. Binding changes discard
/// retained observations/results; the host owns client revocation and the mutation-lane pump.</summary>
public sealed class SessionPanel(bool observeMap = false)
{
    private IPlayerClient? client;
    private CommandId? command;
    public SessionView View { get; private set; } = SessionView.Empty;

    public void Bind(IPlayerClient? value)
    {
        client = value;
        command = null;
        View = SessionView.Empty;
        Refresh();
    }

    public void Refresh()
    {
        if (client is null)
        {
            return;
        }

        try
        {
            var identity = client.Identity;
            var units = client.OwnUnits();
            var cities = client.OwnCities();
            var descriptors = client.Commands();
            var play = client.ObserveLocalPlay();
            var current = command is { } id ? client.Result(id) : null;
            // This model displays the complete current result, not an unbounded event history.
            client.Updates();
            var map = observeMap && (View.Map?.Identity != identity || View.Map.Revision != units.Observation.Revision)
                ? client.ObserveMap() : View.Map;
            View = new(identity, units, cities, descriptors, current ?? View.Command, View.Message, map, play);
        }
        catch (AccessException)
        {
            ClearRevoked();
        }
    }

    public bool Submit(CommandRequest request)
    {
        if (client is null || View.Command is { Status: CommandStatus.Pending or CommandStatus.AwaitingInteraction })
        {
            return false;
        }

        try
        {
            var result = client.Submit(request);
            command = result.Status == CommandStatus.Rejected ? null : result.Correlation.Command;
            View = View with { Command = result, Message = result.Code };
            Refresh();
            return result.Status != CommandStatus.Rejected;
        }
        catch (AccessException)
        {
            ClearRevoked();
            return false;
        }
    }

    /// <summary>Displays a command submitted by another visible controller on this seat.</summary>
    public void Follow(CommandId id)
    {
        if (command == id || View.Command is { Status: CommandStatus.Pending or CommandStatus.AwaitingInteraction })
        {
            return;
        }
        try
        {
            if (client?.Result(id) is { } update)
            {
                command = id;
                View = View with { Command = update, Message = update.Code };
                Refresh();
            }
        }
        catch (AccessException)
        {
            ClearRevoked();
        }
    }

    public bool Reply(int choice)
    {
        if (client is null || View.Command?.Interaction is not { } interaction)
        {
            return false;
        }

        return Receive(() => client.Reply(interaction.Id, choice));
    }

    public bool Cancel()
    {
        if (client is null || command is not { } id)
        {
            return false;
        }

        return Receive(() => client.Cancel(id));
    }

    private bool Receive(Func<InputReceipt> call)
    {
        try
        {
            var receipt = call();
            View = View with { Message = receipt.Code };
            Refresh();
            return receipt.Accepted;
        }
        catch (AccessException)
        {
            ClearRevoked();
            return false;
        }
    }

    private void ClearRevoked()
    {
        client = null;
        command = null;
        View = SessionView.Empty with { Message = "Session access ended." };
    }
}
