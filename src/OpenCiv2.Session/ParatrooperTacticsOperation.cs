using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>A07 proactive paradrop with the actual child's private attack/capture decisions.</summary>
public sealed class ParatrooperTacticsOperation(ParatrooperTactics.Operation operation, Func<int, ParticipantId> recipient,
    Func<CaptureCityVisit, InteractionSpec> window, Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private ParadropDecision? pending;
    private InteractionSpec? windowPrompt, intelligenceWindow;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed paratrooper policy presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            var previous = pending;
            pending = operation.Resume(ParadropInteraction.Reply(pending, input));
            if (!ReferenceEquals(previous, pending))
            { intelligenceWindow = null; }
            if (pending is not null)
            {
                context.Publish();
                var prompt = ParadropInteraction.Prompt(context, pending, recipient, window,
                    ref windowPrompt, ref intelligenceWindow, intelligence);
                return new(CommandStatus.AwaitingInteraction, Interaction: prompt);
            }
            if (operation.Result is not { } choice)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            var result = new OwnedList<NamedValue>([new("route", new CommandValue.Integer((int)choice.Route)), new("landed", new CommandValue.Boolean(operation.Landed))]);
            context.Effect("ai.paratrooper.completed", result);
            return completed = operation.PresentationFailures.Count == 0 ? new(Result: result)
                : new(CommandStatus.Failed, Result: result, Code: "paratrooper_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
