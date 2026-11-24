using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>F04 private decisions and report acknowledgements around a retained real
/// global advance. Game-over presentation belongs to the subsequent turn-loop phase.</summary>
public sealed class GlobalTurnOperation(GlobalTurn.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private GlobalTurnDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted, progressPublished, arrivalPublished;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        {
            throw new InvalidOperationException("Failed global turn presentation cannot resume");
        }

        if (completed is not null)
        {
            return completed;
        }

        try
        {
            GlobalTurnReply? reply = pending switch
            {
                GlobalTurnDecision.Lifecycle choice when LifecycleInteraction.ResearchReply(choice.Choice, input) is { } value => new GlobalTurnReply.Lifecycle(value),
                GlobalTurnDecision.Throne choice when !choice.Upgrade.ActorCurrent || input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout => new GlobalTurnReply.Throne(null),
                GlobalTurnDecision.Throne when input.Kind == OperationInputKind.Choice && input.Choice is { } value => new GlobalTurnReply.Throne(ThroneInteraction.Selection(value)),
                GlobalTurnDecision.History or GlobalTurnDecision.Landing when OutcomeText.Acknowledged(input) => new GlobalTurnReply.Acknowledge(),
                _ => null
            };
            var next = operation.Resume(reply);
            if (pending is GlobalTurnDecision.Throne previous && previous.Upgrade.Accepted is { } accepted)
            {
                ThroneInteraction.Accepted(context, previous.Upgrade, accepted);
            }

            if (!ReferenceEquals(next, pending))
            {
                window = null;
            }

            pending = next;
            if (!progressPublished && operation.Progress is { Awarded: true } progress)
            {
                progressPublished = true;
                context.Effect("throne.progress", new([new("civilization", new CommandValue.Integer(progress.Civilization)), new("progress", new CommandValue.Integer(progress.Progress))]));
            }
            if (!arrivalPublished && operation.Arrival is { } arrival)
            {
                arrivalPublished = true;
                context.Effect("spaceship.arrived", new([new("civilization", new CommandValue.Integer(arrival.Civilization))]));
            }
            if (pending is not null)
            {
                var prompt = pending switch
                {
                    GlobalTurnDecision.Lifecycle choice => LifecycleInteraction.Prompt(context, choice.Choice, recipient, ref window, intelligence),
                    GlobalTurnDecision.Throne choice => ThroneInteraction.Prompt(context, choice.Upgrade, recipient),
                    GlobalTurnDecision.History history => window ??= new("Historian report — wealth\n" + string.Join("\n",
                        history.Report.Ranks.Select(r => $"{r.Rank}. {OutcomeText.Plural(context, r.Civilization)}")),
                        recipient(context.World.Multiplayer.LocalHuman), null, new([0]), Cancellable: false)
                    { Options = new([new(0, "Continue")]) },
                    GlobalTurnDecision.Landing landing => window ??= OutcomeText.Notice($"Spaceship landing\nThe {OutcomeText.Plural(context, landing.Arrival.Civilization)} spaceship has arrived."),
                    _ => throw new InvalidOperationException("Unknown global turn decision")
                };
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            context.Publish();
            if (!operation.Completed)
            {
                return new(CommandStatus.Pending) { ReadyToContinue = true };
            }

            context.Effect("turn.global.advanced", new([new("turn", new CommandValue.Integer(context.World.Globals.Turn))]));
            return completed = new(operation.PresentationFailures.Count == 0 ? CommandStatus.Completed : CommandStatus.Failed,
                Code: operation.PresentationFailures.Count == 0 ? "" : "global_turn_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
