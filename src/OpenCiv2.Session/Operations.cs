using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

public sealed record CommandActor(ParticipantId Participant, CivilizationSlot Civilization, ControllerId? Controller = null,
    AccessClass Access = AccessClass.Player, ConnectionId? Source = null);
public enum OperationInputKind { Begin, Proceed, Choice, Cancel, Timeout }
public sealed record OperationInput(OperationInputKind Kind = OperationInputKind.Begin, int? Choice = null, CommandActor? Source = null);
public sealed record OperationStep(CommandStatus Status = CommandStatus.Completed, OwnedList<NamedValue>? Result = null,
    string Code = "", InteractionSpec? Interaction = null);
public sealed record OperationContext(GameState World, ProcessContext Process, GameplayRandom Gameplay, PresentationRandom Presentation,
    CommandActor Actor, CommandCorrelation Correlation, Action<string, OwnedList<NamedValue>> Effect, Action Publish);
public interface IContinuation : IDisposable
{
    OperationStep Resume(OperationContext context, OperationInput input);
    void IDisposable.Dispose() { }
}
public enum CommandRoute { Execute, AwaitPeer }
public sealed record Admission(string? Rejection = null, CommandRoute Route = CommandRoute.Execute, ConnectionId? Executor = null);
/// <summary>Trusted engine callbacks. Admission must observe the world without mutating it;
/// binding changes are rechecked. Execution factories capture semantic inputs.</summary>
public sealed record CommandDefinition(CommandDescriptor Descriptor,
    Func<CommandActor, CommandRequest, GameState, Admission> Admit,
    Func<CommandRequest, IContinuation> Execute,
    Func<CommandRequest, OwnedList<NamedValue>, IContinuation>? ApplyResult = null,
    ContinuationCodec? Checkpoint = null);
public sealed record RoutedRequest(ConnectionId Destination, CommandCorrelation Correlation, CommandRequest Request);
public sealed record RoutedReply(ConnectionId Destination, CommandCorrelation Correlation, CommandStatus Status,
    OwnedList<NamedValue> Result, string Code = "");
