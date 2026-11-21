using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.ViewModels;

/// <summary>Identifies a dialog across command replacement and observer/controller handoff.</summary>
public sealed record DialogIdentity(ClientIdentity Client, CommandCorrelation Command, InteractionId Interaction);

/// <summary>Owned interaction content and the local input latch, independent of layout and rendering.</summary>
public sealed record DialogView(DialogIdentity Identity, InteractionSpec Spec, bool Submitted);

/// <summary>P04/P05 nonblocking dialog lifetime. A captured identity cannot answer a later interaction.</summary>
public sealed class InteractionDialogModel
{
    public DialogView? View { get; private set; }

    public void Refresh(SessionView session)
    {
        if (View is { Submitted: true } submitted && session.Identity == submitted.Identity.Client &&
            session.Command is { Status: CommandStatus.Pending } pending &&
            pending.Correlation == submitted.Identity.Command)
        {
            return;
        }
        if (session.Identity is not { } client ||
            session.Command is not { Status: CommandStatus.AwaitingInteraction, Interaction: { } interaction } command ||
            interaction.Recipient != client.Participant || interaction.Observer != client.Observer)
        {
            View = null;
            return;
        }

        var identity = new DialogIdentity(client, command.Correlation, interaction.Id);
        View = new(identity, interaction.Spec, View?.Identity == identity && View.Submitted);
    }

    /// <summary>Submits one allowed reply or cancellation; only accepted input latches this dialog.</summary>
    public bool Respond(SessionPanel panel, DialogIdentity identity, int? choice)
    {
        panel.Refresh();
        Refresh(panel.View);
        if (View is not { Submitted: false } current || current.Identity != identity ||
            (choice is int selected ? !current.Spec.Choices.Contains(selected) : !current.Spec.Cancellable))
        {
            return false;
        }

        bool accepted = choice is int value ? panel.Reply(value) : panel.Cancel();
        if (accepted)
        {
            View = current with { Submitted = true };
        }

        Refresh(panel.View);
        return accepted;
    }
}
