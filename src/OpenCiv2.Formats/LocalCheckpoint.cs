using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;

namespace OpenCiv2.Formats;

/// <summary>M1-01 owned persistence data. World and continuation bytes have explicitly named owner codecs;
/// this is not a serialized CLR graph or an assertion that arbitrary gameplay state is supported.</summary>
public sealed record CheckpointCounters(
    ulong Session,
    ulong Participant,
    ulong Controller,
    ulong Command,
    ulong Interaction,
    ulong Revision);
public sealed record CheckpointCommand(
    CommandRequest Request,
    uint DescriptorVersion,
    ControllerId? Controller,
    AccessClass Access,
    CommandUpdate Update,
    string Codec,
    uint CodecVersion,
    OwnedList<byte> Locals);
public sealed record LocalCheckpoint(
    string Profile,
    string ContentHash,
    RuntimeId Origin,
    SessionId Session,
    ParticipantId Participant,
    CheckpointCounters Counters,
    OwnedList<byte> World,
    RandomCheckpoint Gameplay,
    RandomCheckpoint Presentation,
    CheckpointCommand? Command);
