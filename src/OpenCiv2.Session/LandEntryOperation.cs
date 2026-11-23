using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Private U02 land-entry decisions using the actual C07/V06 interaction contracts.</summary>
public sealed class LandEntryOperation(LandEntry.Operation operation, Func<int, ParticipantId> recipient,
    Func<CityEspionageVisit, InteractionSpec> visit, Func<CaptureCityVisit, InteractionSpec> captureWindow) : IContinuation
{
    private LandEntryDecision? pending;
    private InteractionSpec? visitPrompt, capturePrompt;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed land-entry presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            ResearchReply? reply = pending switch
            {
                LandEntryDecision.City city => CityMissionInteraction.Reply(city.Decision, input),
                LandEntryDecision.Unit unit => UnitMissionInteraction.Reply(unit.Decision, input) is { } value ? new(value) : null,
                LandEntryDecision.Caravan => AttackConfirmationInteraction.Reply(input) is { } value ? new(value) : null,
                _ => null
            };
            pending = operation.Resume(reply);
            if (pending is not null)
            {
                context.Publish();
                var prompt = pending switch
                {
                    LandEntryDecision.Caravan caravan => CaravanInteraction.Prompt(caravan.Decision, recipient),
                    LandEntryDecision.Unit unit => UnitMissionInteraction.Prompt(unit.Decision, recipient),
                    LandEntryDecision.City city => CityMissionInteraction.Prompt(context, city.Decision, recipient, visit, captureWindow, ref visitPrompt, ref capturePrompt),
                    _ => throw new InvalidOperationException("Unknown land-entry decision")
                };
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending); }
            var result = new OwnedList<NamedValue>([new("stopped", new CommandValue.Boolean(operation.Result!.Stopped)),
                new("destinationOwner", new CommandValue.Integer(operation.Result.DestinationOwner ?? -1))]);
            context.Effect("movement.land-entry", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
