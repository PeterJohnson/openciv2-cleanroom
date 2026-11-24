using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>Owns the F04 human phase across command segments, including idle and active input.
/// The trusted host supplies its current cursor/city view and runs real player commands between
/// segments. The world stays pinned until completion or disposal; this does not admit a new game profile.</summary>
public sealed class HumanMovementPhaseHost : IDisposable
{
    public static OwnedList<ValueSchema> ResultSchema { get; } = new([
        new("input_required", ValueKind.Boolean), new("phase_complete", ValueKind.Boolean),
        new("waited_for_input", ValueKind.Boolean), new("automatically_ended", ValueKind.Boolean)]);
    private readonly HumanMovementPhase.Operation operation;
    private readonly Func<HumanMovementView> view;
    private readonly Func<int, ParticipantId> recipient;
    private readonly Func<CityEspionageVisit, InteractionSpec> visit;
    private readonly Func<EmissaryIntelligence, InteractionSpec> intelligence;
    private readonly Func<CaptureCityVisit, InteractionSpec> capture;
    private readonly Func<HutCityVisit, InteractionSpec> hut;
    private SessionActivity? pin;
    private bool segmentActive, disposed;
    public bool NeedsInput => operation.Pending is HumanMovementDecision.IdleInput
        or HumanMovementDecision.Active { Value: ActiveUnitDecision.PlayerInput };
    public bool Completed => operation.Completed;
    public HumanMovementPhaseHost(SessionRuntime runtime, HumanMovementPhase.Operation operation, Func<HumanMovementView> view,
        Func<int, ParticipantId> recipient, Func<CityEspionageVisit, InteractionSpec> visit,
        Func<EmissaryIntelligence, InteractionSpec> intelligence, Func<CaptureCityVisit, InteractionSpec> capture,
        Func<HutCityVisit, InteractionSpec> hut)
    {
        this.operation = operation;
        this.view = view;
        this.recipient = recipient;
        this.visit = visit;
        this.intelligence = intelligence;
        this.capture = capture;
        this.hut = hut;
        try
        { pin = runtime.PinWorld(); }
        catch { operation.Dispose(); throw; }
    }
    public IContinuation BeginSegment(bool inputHandled)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (segmentActive)
        { throw new InvalidOperationException("A human movement segment already owns the phase"); }
        if (operation.Completed)
        { throw new InvalidOperationException("The human movement phase is complete"); }
        segmentActive = true;
        return new Segment(this, inputHandled);
    }
    public void RequestStop() => operation.RequestStop();
    private void ReleasePin() { pin?.Dispose(); pin = null; }
    public void Dispose() { if (!disposed) { disposed = true; operation.Dispose(); ReleasePin(); } }

    private sealed class Segment : IContinuation
    {
        private readonly HumanMovementPhaseHost owner;
        private readonly bool inputHandled;
        private readonly MovementInteraction movement;
        private readonly AutomatedOrderInteraction automated;
        private HumanMovementDecision? pending;
        private InteractionSpec? contactWindow;
        private OperationStep? completed;
        private bool started, disposed;
        internal Segment(HumanMovementPhaseHost owner, bool inputHandled)
        {
            this.owner = owner;
            this.inputHandled = inputHandled;
            movement = new(owner.recipient, owner.visit, owner.intelligence, owner.capture, owner.hut);
            automated = new(owner.recipient, owner.visit, owner.intelligence, owner.capture, owner.hut);
        }
        public OperationStep Resume(OperationContext context, OperationInput input)
        {
            ObjectDisposedException.ThrowIf(disposed || owner.disposed, this);
            if (completed is not null)
            { return completed; }
            try
            {
                if (input.Kind is OperationInputKind.Cancel or OperationInputKind.Timeout)
                { owner.operation.RequestStop(); }
                HumanMovementReply? reply = null;
                if (!started && inputHandled && owner.NeedsInput)
                {
                    reply = owner.operation.Pending is HumanMovementDecision.IdleInput
                        ? new HumanMovementReply.InputHandled() : new HumanMovementReply.Active(new ActiveUnitReply.InputHandled());
                }
                else if (pending is HumanMovementDecision.Contact contact
                    && DiplomaticContactInteraction.Reply(contact.Value, input) is { } contactReply)
                { reply = new HumanMovementReply.Contact(contactReply); }
                else if (pending is HumanMovementDecision.Active { Value: ActiveUnitDecision.Movement move }
                    && movement.Reply(move.Value, input) is { } answer)
                { reply = new HumanMovementReply.Active(new ActiveUnitReply.Movement(answer)); }
                else if (pending is HumanMovementDecision.Active { Value: ActiveUnitDecision.Automated auto }
                    && automated.Reply(auto.Value, input) is { } choice)
                { reply = new HumanMovementReply.Active(new ActiveUnitReply.Automated(choice)); }
                started = true;
                var previous = pending;
                pending = owner.operation.Resume(owner.view(), reply);
                if (!ReferenceEquals(previous, pending))
                { contactWindow = null; }
                if (pending is HumanMovementDecision.Contact nextContact)
                {
                    context.Publish();
                    return new(CommandStatus.AwaitingInteraction, Interaction: DiplomaticContactInteraction.Prompt(
                        context, nextContact.Value, owner.recipient, owner.intelligence, ref contactWindow));
                }
                if (pending is HumanMovementDecision.Active { Value: ActiveUnitDecision.Movement next })
                { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: movement.Prompt(context, next.Value)); }
                if (pending is HumanMovementDecision.Active { Value: ActiveUnitDecision.Automated thinking })
                { context.Publish(); return new(CommandStatus.AwaitingInteraction, Interaction: automated.Prompt(context, thinking.Value)); }
                if (owner.NeedsInput || owner.operation.Completed)
                {
                    var values = new OwnedList<NamedValue>([
                        new("input_required", new CommandValue.Boolean(owner.NeedsInput)),
                        new("phase_complete", new CommandValue.Boolean(owner.operation.Completed)),
                        new("waited_for_input", new CommandValue.Boolean(owner.operation.WaitedForInput)),
                        new("automatically_ended", new CommandValue.Boolean(owner.operation.AutomaticallyEnded))]);
                    context.Effect(owner.NeedsInput ? "phase.input.required" : "phase.movement.completed", values);
                    if (owner.operation.Completed)
                    { owner.ReleasePin(); }
                    return completed = owner.operation.PresentationFailures.Count == 0 ? new(Result: values)
                        : new(CommandStatus.Failed, Result: values, Code: "human_movement_presentation_failed");
                }
                context.Publish();
                return new(CommandStatus.Pending) { ReadyToContinue = true };
            }
            catch { owner.Dispose(); context.Publish(); throw; }
        }
        public void Dispose()
        {
            if (disposed)
            { return; }
            disposed = true;
            owner.segmentActive = false;
            if (completed is null)
            { owner.Dispose(); }
        }
    }
}
