using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Session;

/// <summary>V05 ally request with private emissary, intelligence and retained payment choices.</summary>
public sealed class AllyHelpOperation(AllyHelp.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private AllyHelpDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("A failed ally-help presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            pending = operation.Resume(pending is null ? null : AllyHelpInteraction.Reply(pending, input));
            if (pending is not null)
            {
                context.Publish();
                var prompt = AllyHelpInteraction.Prompt(pending, recipient, intelligence, ref window);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            var result = new OwnedList<NamedValue>([new("admitted", new CommandValue.Boolean(operation.Admitted)), new("helped", new CommandValue.Boolean(operation.Helped))]);
            context.Effect("diplomacy.ally-help.completed", result);
            return completed = new(Result: result);
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}

internal static class AllyHelpInteraction
{
    internal static int? Reply(AllyHelpDecision pending, OperationInput input) =>
        pending is AllyHelpDecision.Emissary emissary ? EmissaryInteraction.Reply(emissary.Decision, input)
        : input.Kind == OperationInputKind.Choice ? input.Choice
        : input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? 0 : null;
    internal static InteractionSpec Prompt(AllyHelpDecision pending, Func<int, ParticipantId> recipient,
        Func<EmissaryIntelligence, InteractionSpec> intelligence, ref InteractionSpec? window)
    {
        if (pending is AllyHelpDecision.Emissary receive)
        { return EmissaryInteraction.Prompt(receive.Decision, recipient, intelligence, ref window); }
        var request = pending is AllyHelpDecision.Demand demand ? demand.Request : ((AllyHelpDecision.Bonus)pending).Request;
        string payment = pending is AllyHelpDecision.Bonus ? $" They offer {request.Payment} gold." : string.Empty;
        return new($"The {request.Ally.Plural} request help against the {request.Enemy.Plural}.{payment}",
            recipient(request.Recipient), null, new([0, 1]))
        { Options = new([new(0, "Decline"), new(1, "Declare war")]) };
    }
}
