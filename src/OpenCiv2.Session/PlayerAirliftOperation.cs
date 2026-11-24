using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U05 destination/warning choices and actual nested deletion decisions. P05 owns command availability.</summary>
public sealed class PlayerAirliftOperation(PlayerAirlift.Operation operation, Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private AirliftDecision? pending;
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed airlift presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            int? reply = input.Kind switch
            {
                OperationInputKind.Choice => input.Choice,
                OperationInputKind.Cancel or OperationInputKind.Timeout => pending is AirliftDecision.Destination ? -1 : 0,
                _ => null
            };
            ResearchReply? answer = LifecycleInteraction.Selection(reply);
            if (pending is AirliftDecision.Lifecycle lifecycle)
            { answer = LifecycleInteraction.ResearchReply(lifecycle.Decision, input); }
            var previous = pending;
            pending = operation.ResumeDecision(pending is null ? null : answer);
            if (!ReferenceEquals(previous, pending))
            { intelligenceWindow = null; }
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    AirliftDecision.Destination d => Destination(d),
                    AirliftDecision.Fighters f => new InteractionSpec("Enemy fighters threaten this airlift. Proceed?", recipient(f.Actor),
                        new CommandValue.Unit(f.Unit), new([0, 1]))
                    { Options = new([new(0, "Cancel"), new(1, "Airlift")]) },
                    AirliftDecision.Lifecycle i => LifecycleInteraction.Prompt(context, i.Decision, recipient, ref intelligenceWindow, intelligence),
                    _ => throw new InvalidOperationException("Unknown airlift decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("transferred", new CommandValue.Boolean(operation.Transferred)), new("shotDown", new CommandValue.Boolean(operation.ShotDown))]);
            context.Effect("movement.airlift.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private InteractionSpec Destination(AirliftDecision.Destination request)
    {
        var options = request.Cities.Select(c => new InteractionOption(c.City.Value, c.Name)).Prepend(new(-1, "Cancel")).ToArray();
        return new("Select an airlift destination", recipient(request.Actor), new CommandValue.Unit(request.Unit), new(options.Select(o => o.Value)))
        { Options = new(options) };
    }
    public void Dispose() => operation.Dispose();
}
