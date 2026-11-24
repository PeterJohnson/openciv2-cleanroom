using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>U04 private stack chooser and actual contact continuation. Separate wake/activate actions
/// preserve the classic immediate wake followed by optional cancellation.</summary>
public sealed class UnitStackSelectionOperation(UnitStackSelection.Operation operation, Func<int, ParticipantId> recipient,
    Func<EmissaryIntelligence, InteractionSpec> intelligence) : IContinuation
{
    private UnitStackDecision? pending;
    private InteractionSpec? window;
    private OperationStep? completed;
    private bool faulted;
    public OperationStep Resume(OperationContext context, OperationInput input)
    {
        if (faulted)
        { throw new InvalidOperationException("Failed stack selection presentation cannot resume"); }
        if (completed is not null)
        { return completed; }
        try
        {
            UnitStackReply? reply = null;
            if (pending is UnitStackDecision.Choose)
            {
                int? choice = input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout ? -1
                    : input.Kind == OperationInputKind.Choice ? input.Choice : null;
                if (choice is { } value)
                { reply = new UnitStackReply.Choose(value >= 9 ? value - 9 : value, value >= 9); }
            }
            else if (pending is UnitStackDecision.Contact contact)
            {
                var answer = DiplomaticContactInteraction.Reply(contact.Value, input);
                if (answer is not null)
                { reply = new UnitStackReply.Contact(answer.Value); }
            }
            var previous = pending;
            pending = operation.Resume(reply);
            if (!ReferenceEquals(previous, pending))
            { window = null; }
            if (pending is UnitStackDecision.Choose menu)
            {
                if (window is null)
                {
                    List<InteractionOption> options = [];
                    for (int i = 0; i < menu.Rows.Count; i++)
                    {
                        var row = menu.Rows[i];
                        string label = $"{row.Adjective} {(row.Veteran ? "Veteran " : "")}{row.Name} ({row.Home ?? "No home"})"
                            + (row.Cargo is null ? "" : " — " + row.Cargo);
                        options.Add(new(i, "Activate " + label));
                        options.Add(new(9 + i, "Wake " + label));
                    }
                    options.Add(new(-1, "Cancel"));
                    window = new("Select a unit. Waking a unit takes effect even if you cancel.", recipient(menu.Observer), null,
                        new(options.Select(o => o.Value)))
                    { Options = new(options) };
                }
                context.Publish();
                return new(CommandStatus.AwaitingInteraction, Interaction: window);
            }
            if (pending is UnitStackDecision.Contact meeting)
            {
                context.Publish();
                return new(CommandStatus.AwaitingInteraction,
                    Interaction: DiplomaticContactInteraction.Prompt(context, meeting.Value, recipient, intelligence, ref window));
            }
            if (!operation.Completed)
            { context.Publish(); return new(CommandStatus.Pending) { ReadyToContinue = true }; }
            context.Effect("unit.stack-selection.completed", new([]));
            return completed = operation.PresentationFailures.Count == 0 ? new()
                : new(CommandStatus.Failed, Code: "unit_stack_selection_presentation_failed");
        }
        catch { faulted = true; operation.Dispose(); context.Publish(); throw; }
    }
    public void Dispose() => operation.Dispose();
}
