using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>V06 escape followed by the actual incident, suspended for the human victim's pretext.
/// The owning mission supplies the appropriate participant context; no public player command bypasses admission.</summary>
public sealed class SpyEscapeOperation(SpyEscape escape, EspionageIncidents incidents,
    int slot, int mode, int victim, string call) : IContinuation
{
    private SpyEscapeResult? fate;
    private EspionageIncidents.Operation? incident;
    private EspionagePretext? pending;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (fate is null)
        {
            fate = escape.Resolve(slot, mode, victim, call);
            if (fate.Incident is { } request)
            { incident = incidents.Begin(request.Actor, request.Victim, call + "/incident"); }
            context.Publish();
        }
        int? reply = pending is null ? null : input.Kind switch
        {
            OperationInputKind.Choice => input.Choice,
            OperationInputKind.Cancel or OperationInputKind.Timeout => 0,
            _ => null
        };
        pending = incident?.Resume(reply);
        if (pending is { } decision)
        {
            string response = decision.Allied ? "Cancel the alliance" : "Declare war";
            var prompt = new InteractionSpec($"Espionage incident\n{decision.Actor.Plural} agents were caught acting against the {decision.Victim.Plural}.\n{response}?",
                null, null, new([0, 1]))
            { Options = new([new(0, "Decline"), new(1, response)]) };
            return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
        }
        var result = new OwnedList<NamedValue>([new("lost", new CommandValue.Boolean(fate.Lost))]);
        context.Effect("espionage.escape.completed", result);
        return new(Result: result);
    }
}
