using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

/// <summary>
/// The admitted participant, civilization, controller grant and optional peer source of an operation.
/// </summary>
public sealed record CommandActor(
    ParticipantId Participant,
    CivilizationSlot Civilization,
    ControllerId? Controller = null,
    AccessClass Access = AccessClass.Player,
    ConnectionId? Source = null);
/// <summary>
/// The reason a continuation is being resumed on the cooperative session lane.
/// </summary>
public enum OperationInputKind { Begin, Proceed, Choice, Cancel, Timeout }
/// <summary>
/// A resume event with an optional choice and the actor that supplied it.
/// </summary>
public sealed record OperationInput(
    OperationInputKind Kind = OperationInputKind.Begin,
    int? Choice = null,
    CommandActor? Source = null,
    string? Text = null);
/// <summary>
/// A continuation's next lifecycle state, terminal result or request for participant interaction.
/// </summary>
public sealed record OperationStep(CommandStatus Status = CommandStatus.Completed, OwnedList<NamedValue>? Result = null,
    string Code = "", InteractionSpec? Interaction = null)
{
    /// <summary>A bounded local step has more work and needs no external input. Valid only with
    /// Pending and no interaction. Queues one Proceed after already admitted work.</summary>
    public bool ReadyToContinue { get; init; }
}
/// <summary>
/// Trusted services borrowed for a single continuation step.
/// </summary>
/// <remarks>
/// Effect records an already committed effect and publishes a revision. Publish advances the revision without
/// recording an effect. Failure does not roll back either action; retain explicit operation locals rather than
/// this context between steps.
/// </remarks>
public sealed record OperationContext(
    GameState World,
    ProcessContext Process,
    GameplayRandom Gameplay,
    PresentationRandom Presentation,
    CommandActor Actor,
    CommandCorrelation Correlation,
    Action<string, OwnedList<NamedValue>> Effect,
    Action Publish,
    Action<CityTarget, int>? CityProductionCompleted = null,
    Action<CivilizationSlot, string>? Notify = null);
/// <summary>
/// Owns the explicit state needed to suspend and resume one admitted command.
/// </summary>
/// <remarks>
/// Resume runs synchronously on the session lane. Dispose releases resources without advancing gameplay or
/// repeating accepted effects.
/// </remarks>
public interface IContinuation : IDisposable
{
    /// <summary>
    /// Runs one operation step using the current world and one queued input, then yields its lifecycle state.
    /// </summary>
    OperationStep Resume(OperationContext context, OperationInput input);

    void IDisposable.Dispose() { }
}
/// <summary>
/// Whether the local session executes an admitted command or waits for an authoritative peer result.
/// </summary>
public enum CommandRoute { Execute, AwaitPeer }
/// <summary>
/// An authority decision: reject with a code, execute locally, or route to a designated peer.
/// </summary>
public sealed record Admission(
    string? Rejection = null,
    CommandRoute Route = CommandRoute.Execute,
    ConnectionId? Executor = null);
/// <summary>Trusted engine callbacks. Admission must observe the world without mutating it;
/// binding changes are rechecked. Execution factories capture semantic inputs.</summary>
public sealed record CommandDefinition(CommandDescriptor Descriptor,
    Func<CommandActor, CommandRequest, GameState, Admission> Admit,
    Func<CommandRequest, IContinuation> Execute,
    Func<CommandRequest, OwnedList<NamedValue>, IContinuation>? ApplyResult = null,
    ContinuationCodec? Checkpoint = null);
/// <summary>
/// An owned request awaiting transfer to the indicated peer by the mode adapter.
/// </summary>
public sealed record RoutedRequest(ConnectionId Destination, CommandCorrelation Correlation, CommandRequest Request);
/// <summary>
/// A terminal peer reply awaiting transfer, preserving its original command correlation.
/// </summary>
public sealed record RoutedReply(ConnectionId Destination, CommandCorrelation Correlation, CommandStatus Status,
    OwnedList<NamedValue> Result, string Code = "");
