namespace OpenCiv2.Base;

// Semantic identity domains, never object addresses or implicit classic wire fields.
public readonly record struct RuntimeId(ulong Value);
public readonly record struct SessionId(ulong Value);
public readonly record struct ParticipantId(ulong Value);
public readonly record struct ControllerId(ulong Value);
public readonly record struct CommandId(ulong Value);
public readonly record struct InteractionId(ulong Value);
public readonly record struct ConnectionId(ushort Value);
