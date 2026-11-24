using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V06 device confirmation and independently routed incidents from actual capture/nuclear children.</summary>
public sealed class PlantedNuclearDeviceOperation(PlantedNuclearDevice.Operation operation,
    Func<int, ParticipantId> recipient, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private CityEspionageDecision? pending;
    private OperationStep? completed;
    private InteractionSpec? intelligenceWindow;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        {
            throw new InvalidOperationException("A failed device continuation cannot resume");
        }

        try
        { return Advance(context, input); }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    private OperationStep Advance(OperationContext context, OperationInput input)
    {
        if (completed is not null)
        {
            return completed;
        }

        int? reply = input.Kind switch
        {
            OperationInputKind.Choice => input.Choice,
            OperationInputKind.Cancel or OperationInputKind.Timeout => 0,
            _ => null
        };
        ResearchReply? answer = LifecycleInteraction.Selection(reply);
        if (pending is CityEspionageDecision.Lifecycle lifecycle)
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
                CityEspionageDecision.Lifecycle incident => LifecycleInteraction.Prompt(context, incident.Decision, recipient, ref intelligenceWindow, intelligence),
                CityEspionageDecision.Question question => new InteractionSpec($"Risk a major incident by planting a nuclear device in {question.Name}?",
                    recipient(question.Actor), new CommandValue.City(question.City), new([0, 1]))
                { Options = new([new(0, "Cancel"), new(1, "Plant device")]) },
                _ => throw new InvalidOperationException("Unknown device mission decision")
            };
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        if (!operation.Completed)
        { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
        context.Effect("espionage.device.completed", new([]));
        return completed = new();
    }
    public void Dispose() => operation.Dispose();
}
