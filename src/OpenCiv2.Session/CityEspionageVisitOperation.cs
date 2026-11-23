using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>Trusted V06 visit child. The required window adapter supplies the actual intelligence/city
/// presentation; its close decision resumes the mission. Recipient routing is enforced here.</summary>
public sealed class CityEspionageVisitOperation(CityEspionageVisits.Operation operation,
    Func<int, ParticipantId> recipient, Func<CityEspionageVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private CityEspionageDecision? pending;
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private InteractionSpec? visitPrompt;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed visit presentation cannot resume"); }
        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        { return completed; }
        int? reply = input.Kind switch
        {
            OperationInputKind.Choice => input.Choice,
            OperationInputKind.Cancel or OperationInputKind.Timeout => 0,
            _ => null
        };
        if (pending is CityEspionageDecision.Lifecycle lifecycle)
        { reply = LifecycleInteraction.Reply(lifecycle.Decision, input); }
        pending = operation.Resume(pending is null ? null : reply);
        if (pending is not null)
        {
            context.Publish();
            var prompt = pending switch
            {
                CityEspionageDecision.Visit visit => visitPrompt ??= window(visit.Request) with { Recipient = recipient(visit.Request.Actor) },
                CityEspionageDecision.Lifecycle incident => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
                _ => throw new InvalidOperationException("Unknown city visit decision")
            };
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending); }
        var values = new OwnedList<NamedValue>([new("fallback", new CommandValue.Integer(operation.Fallback is { } mission ? (int)mission : -1))]);
        context.Effect("espionage.visit.completed", values);
        return completed = new(Result: values);
    }
    public void Dispose() => operation.Dispose();
}
