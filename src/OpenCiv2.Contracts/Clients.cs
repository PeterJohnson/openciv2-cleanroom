using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

public enum ControllerKind { Frontend, Automation, Scripted, EnginePolicy }
public enum AccessFailure { SessionEnded, Revoked }
public sealed class AccessException(AccessFailure failure)
    : Exception(failure == AccessFailure.SessionEnded ? "Client session has ended" : "Client binding was revoked")
{
    public AccessFailure Failure { get; } = failure;
}
public sealed record ClientIdentity(RuntimeId Runtime, SessionId Session, ParticipantId Participant,
    ControllerId Controller, CivilizationSlot Observer, ControllerKind Kind = ControllerKind.Frontend,
    AccessClass Access = AccessClass.Player);

/// <summary>Scoped player capabilities. Only the trusted runtime issues a binding; every call checks its lease.</summary>
public interface IPlayerClient
{
    ClientIdentity Identity { get; }
    LiveRecords<OwnUnit> OwnUnits();
    LiveRecords<OwnCity> OwnCities();
    MapObservation? ObserveMap();
    OwnedList<CommandDescriptor> Commands();
    CommandUpdate Submit(CommandRequest request);
    OwnedList<CommandUpdate> Updates();
    CommandUpdate? Result(CommandId command);
    InputReceipt Reply(InteractionId interaction, int choice);
    InputReceipt Cancel(CommandId command);
}
