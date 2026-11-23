using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>C01 private naming, nested lifecycle decisions and actual final city visit.</summary>
public sealed class CityFoundingOperation(CityFounding.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private FoundingDecision? pending;
    private InteractionSpec? visit, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        {
            throw new InvalidOperationException("Failed founding presentation cannot resume");
        }

        if (completed is not null)
        {
            return completed;
        }

        try
        {
            FoundingReply? answer = pending switch
            {
                FoundingDecision.Lifecycle life => LifecycleInteraction.Reply(life.Decision, input) is { } choice ? new(choice) : null,
                FoundingDecision.Name => input.Kind switch
                {
                    OperationInputKind.Choice => new(input.Choice ?? 1, input.Text),
                    OperationInputKind.Cancel => new(1),
                    OperationInputKind.Timeout => new(input.Choice ?? 1, input.Text),
                    _ => null
                },
                FoundingDecision.Window when input.Kind is OperationInputKind.Choice or OperationInputKind.Cancel or OperationInputKind.Timeout => new(0),
                _ => null
            };
            pending = operation.Resume(answer);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    FoundingDecision.Name name => new InteractionSpec("Name the new city", recipient(name.Owner), new CommandValue.City(name.City), new([0]))
                    { Options = new([new(0, "Found city")]), TextInput = new("City name", name.InitialName, name.MaximumLength, 0) },
                    FoundingDecision.Lifecycle life => LifecycleInteraction.Prompt(context, life.Decision, recipient, ref intelligenceWindow, intelligence),
                    FoundingDecision.Window city => (visit ??= window(city.Request)) with { Recipient = recipient(city.Request.Owner) },
                    _ => throw new InvalidOperationException("Unknown founding decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("outcome", new CommandValue.Text(operation.Result!.Value.ToString()))]);
            context.Effect("city.founding-completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
