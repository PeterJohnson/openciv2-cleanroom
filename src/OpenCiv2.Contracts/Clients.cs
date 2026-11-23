using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

public enum ControllerKind { Frontend, Automation, Scripted, EnginePolicy }
public enum AccessFailure { SessionEnded, Revoked }
public sealed class AccessException(AccessFailure failure)
    : Exception(failure == AccessFailure.SessionEnded ? "Client session has ended" : "Client binding was revoked")
{
    public AccessFailure Failure { get; } = failure;
}
/// <summary>
/// The complete scope of a client grant, including runtime, session, participant and observer identity.
/// </summary>
public sealed record ClientIdentity(RuntimeId Runtime, SessionId Session, ParticipantId Participant,
    ControllerId Controller, CivilizationSlot Observer, ControllerKind Kind = ControllerKind.Frontend,
    AccessClass Access = AccessClass.Player);

/// <summary>Scoped player capabilities. Only the trusted runtime issues a binding; every call checks its
/// lease.</summary>
public interface IPlayerClient
{
    ClientIdentity Identity { get; }

    /// <summary>
    /// Returns owned unit values visible to this observer at the current revision.
    /// </summary>
    LiveRecords<OwnUnit> OwnUnits();

    /// <summary>
    /// Returns owned city values visible to this observer at the current revision.
    /// </summary>
    LiveRecords<OwnCity> OwnCities();

    /// <summary>
    /// Returns an observer-filtered map snapshot, or null when no map is allocated.
    /// </summary>
    MapObservation? ObserveMap();

    /// <summary>Returns the observer's status and city-window data in a supported local game.</summary>
    LocalPlayObservation? ObserveLocalPlay() => null;

    /// <summary>Drains this automation binding's own-city production completions.</summary>
    CityProductionEvents TakeCityProductionEvents() => CityProductionEvents.Empty;

    /// <summary>
    /// Lists command descriptors permitted by this binding's access class.
    /// </summary>
    OwnedList<CommandDescriptor> Commands();

    /// <summary>
    /// Requests admission of a command; accepted execution proceeds on later runtime steps.
    /// </summary>
    CommandUpdate Submit(CommandRequest request);

    /// <summary>
    /// Takes this controller's queued updates, clearing only its delivery queue.
    /// </summary>
    OwnedList<CommandUpdate> Updates();

    /// <summary>
    /// Returns visible retained state for a command, or null when absent or outside this binding's scope.
    /// </summary>
    CommandUpdate? Result(CommandId command);

    /// <summary>
    /// Queues an allowed choice for a currently visible interaction.
    /// </summary>
    InputReceipt Reply(InteractionId interaction, int choice);

    /// <summary>
    /// Requests cancellation of an accessible command subject to its interaction and authority policy.
    /// </summary>
    InputReceipt Cancel(CommandId command);
}
