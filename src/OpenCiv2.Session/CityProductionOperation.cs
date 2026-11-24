using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;

namespace OpenCiv2.Session;

/// <summary>C06 production retains research, settler abandonment and lifecycle decisions.</summary>
public sealed class CityProductionOperation(CityProduction.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec>? intelligence = null) : IContinuation
{
    private ResearchDecision? research;
    private UnitProductionDecision? unit;
    private InteractionSpec? intelligenceWindow;
    private OperationStep? completed;
    private bool faulted, disposed;
    public CityProductionResult? Result => operation.Result;
    public bool ZoomRequested => operation.ZoomRequested;

    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        {
            throw new InvalidOperationException("A failed production presentation cannot resume");
        }

        if (completed is not null)
        {
            return completed;
        }

        try
        {
            ResearchReply? reply = unit switch
            {
                SettlerAbandonment => input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? new(0)
                    : input.Kind == OperationInputKind.Choice && input.Choice is { } choice ? new(choice) : null,
                UnitProductionLifecycle lifecycle => LifecycleInteraction.ResearchReply(lifecycle.Decision, input),
                UnitProductionCargo cargo => CargoInteraction.Reply(cargo.Decision, input),
                _ => ResearchFlowInteraction.Reply(research, input)
            };
            var previousResearch = research;
            var previousUnit = unit;
            research = operation.Resume(reply);
            unit = operation.UnitDecision;
            if (!ReferenceEquals(previousResearch, research) || !ReferenceEquals(previousUnit, unit))
            {
                intelligenceWindow = null;
            }

            InteractionSpec? prompt = unit switch
            {
                SettlerAbandonment abandon => new($"Abandon {abandon.CityName} to keep the new {abandon.UnitName}? "
                    + "Keeping the city removes the new unit without refunding its shields.",
                    recipient(abandon.Owner), null, new([0, 1]))
                { Options = new([new(0, "Keep city"), new(1, "Abandon city")]) },
                UnitProductionLifecycle lifecycle => LifecycleInteraction.Prompt(context, lifecycle.Decision, recipient,
                    ref intelligenceWindow, intelligence),
                UnitProductionCargo cargo => CargoInteraction.Prompt(cargo.Decision, recipient),
                _ => research is null ? null : ResearchFlowInteraction.Prompt(context, research, recipient, ref intelligenceWindow, intelligence)
            };
            if (prompt is not null)
            { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: prompt); }
            if (!operation.Finished)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("city.production.completed", OwnedList<NamedValue>.Empty);
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "city_production_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() { disposed = true; operation.Dispose(); }
}
