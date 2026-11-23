using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>P09 private progress/upgrade boundary for F04. Progress survives cancellation
/// of the optional upgrade; neither viewing nor repeated replies award progress.</summary>
public sealed class ThroneProgressOperation(Func<int, ParticipantId> recipient) : IContinuation
{
    private ThroneProgress? throne;
    private ThroneUpgrade? upgrade;
    private OperationStep? completed;
    private bool started, faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        {
            throw new InvalidOperationException("Failed throne progress cannot resume");
        }

        if (completed is not null)
        {
            return completed;
        }

        try
        {
            if (!started)
            {
                started = true;
                throne = new(context.World, CivilizationScoreOperation.CreateScore(context));
                var progress = throne.Check(context.Process.OrdinaryPreferences.AcceleratedStart);
                upgrade = progress.Upgrade;
                if (progress.Awarded)
                { context.Effect("throne.progress", new([new("civilization", new CommandValue.Integer(progress.Civilization)), new("progress", new CommandValue.Integer(progress.Progress))])); }
                if (upgrade is null)
                {
                    return completed = new();
                }
            }
            if (!upgrade!.ActorCurrent)
            { upgrade.Cancel(); return completed = new(CommandStatus.Failed, Code: "throne_actor_changed"); }
            if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
            { upgrade.Cancel(); return completed = new(CommandStatus.Cancelled, Code: "cancelled"); }
            if (input.Kind == OperationInputKind.Choice && input.Choice is int choice)
            {
                var selected = ThroneInteraction.Selection(choice);
                if (upgrade.Select(selected))
                {
                    ThroneInteraction.Accepted(context, upgrade, selected);
                    return completed = new();
                }
            }
            var choices = upgrade.Choices();
            if (choices.Count == 0)
            { upgrade.Cancel(); return completed = new(CommandStatus.Cancelled, Code: "no_throne_upgrade"); }
            context.Publish();
            return new(CommandStatus.AwaitingInteraction, Interaction: ThroneInteraction.Prompt(context, upgrade, recipient));
        }
        catch { faulted = true; upgrade?.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => upgrade?.Dispose();
}
