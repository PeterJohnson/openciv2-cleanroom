using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>W06 private victim payment decision and real nested lifecycle routing.</summary>
public sealed class BarbarianRansomOperation(BarbarianRansom.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private BarbarianRansomDecision? pending;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed ransom presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            int? answer = pending switch
            {
                BarbarianRansomDecision.Lifecycle life => LifecycleInteraction.Reply(life.Decision, input),
                BarbarianRansomDecision.Offer => input.Kind switch
                {
                    OperationInputKind.Choice => input.Choice,
                    OperationInputKind.Cancel => 0,
                    OperationInputKind.Timeout => input.Choice ?? 0,
                    _ => null
                },
                _ => null
            };
            pending = operation.Resume(answer);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    BarbarianRansomDecision.Offer offer => new InteractionSpec(
                        $"{offer.Message}\n\nPay {offer.Demand} gold to spare {offer.CityName}?", recipient(offer.Victim),
                        new CommandValue.City(offer.City), new([0, 1]))
                    { Options = new([new(0, "Refuse"), new(1, $"Pay {offer.Demand} gold")]) },
                    BarbarianRansomDecision.Lifecycle life => LifecycleInteraction.Prompt(context, life.Decision, recipient,
                        ref intelligenceWindow, intelligence),
                    _ => throw new InvalidOperationException("Unknown ransom decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (operation.Result is null)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("outcome", new CommandValue.Text(operation.Result.Value.ToString()))]);
            context.Effect("world.barbarian-ransom.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
