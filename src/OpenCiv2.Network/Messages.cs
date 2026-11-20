using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.IO;

namespace OpenCiv2.Network;

public readonly record struct MessageHeader(uint Kind, uint Size = 16, uint Sequence = 0);
public sealed record Message(MessageHeader Header, OwnedList<byte> Body);
public sealed record ArgumentMessage(MessageHeader Header, OwnedList<int> Arguments, OwnedList<byte> Payload);
public sealed class MessageSendState
{
    public uint NextSequence { get; set; }
    public int LastResult { get; set; }
}
public delegate int SecureSend(ushort recipient, Span<byte> packet, uint size);
public delegate int BroadcastSend(Span<byte> packet, uint size);

/// <summary>N02 section 2 / Appendix A. Owned wire values, independent of command authority.</summary>
public static class Messages
{
    public const uint Magic = 0x66606660;
    public const int HeaderSize = 16, ArgumentEnvelopeSize = 44;

    public static void InitializeHeader(Span<byte> backing, uint kind, uint size)
    {
        if (backing.Length < HeaderSize || size < HeaderSize)
        {
            throw new ArgumentException("Message header requires sixteen bytes");
        }

        BinaryPrimitives.WriteUInt32LittleEndian(backing[8..], size);
        BinaryPrimitives.WriteUInt32LittleEndian(backing, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(backing[4..], kind);
    }

    public static MessageHeader DecodeHeader(ReadOnlySpan<byte> bytes)
    {
        var reader = new ByteReader(bytes);
        if (reader.U32() != Magic)
        {
            throw new DecodeException(0, "Incorrect application message magic");
        }

        var header = new MessageHeader(reader.U32(), reader.U32(), reader.U32());
        if (header.Size < HeaderSize)
        {
            throw new DecodeException(8, "Application message shorter than its header");
        }

        return header;
    }

    public static Message Decode(ReadOnlySpan<byte> bytes)
    {
        var header = DecodeHeader(bytes);
        if (header.Size != bytes.Length)
        {
            throw new DecodeException(8, "Captured application size differs from declared size");
        }

        return new(header, new(bytes[HeaderSize..].ToArray()));
    }

    public static byte[] Encode(Message message)
    {
        if (message.Header.Size < HeaderSize || message.Body.Count != message.Header.Size - HeaderSize)
        {
            throw new ArgumentException("Message body differs from declared size", nameof(message));
        }

        var writer = HeaderWriter(message.Header);
        writer.Append(message.Body.ToArray());
        return writer.ToArray();
    }

    public static ArgumentMessage DecodeArguments(ReadOnlySpan<byte> bytes)
    {
        var message = Decode(bytes);
        if (message.Body.Count < 28)
        {
            throw new DecodeException(bytes.Length, "Truncated argument envelope");
        }

        var reader = new ByteReader(bytes[HeaderSize..]);
        var arguments = new int[6];
        for (var i = 0; i < arguments.Length; ++i)
        {
            arguments[i] = reader.I32();
        }

        var size = reader.I32();
        if (size < 0 || size != reader.Remaining.Length)
        {
            throw new DecodeException(40, "Application payload length differs from captured extent");
        }

        return new(message.Header, new(arguments), new(reader.Remaining.ToArray()));
    }

    public static byte[] EncodeArguments(
        uint kind,
        ReadOnlySpan<int> arguments,
        ReadOnlySpan<byte> payload,
        uint sequence)
    {
        if (arguments.Length != 6)
        {
            throw new ArgumentException("Message requires six arguments", nameof(arguments));
        }

        if (payload.Length > int.MaxValue - ArgumentEnvelopeSize)
        {
            throw new ArgumentException("Application payload exceeds admitted size domain", nameof(payload));
        }

        var writer = HeaderWriter(new(kind, (uint)(ArgumentEnvelopeSize + payload.Length), sequence));
        foreach (var argument in arguments)
        {
            writer.I32(argument);
        }

        writer.I32(payload.Length);
        writer.Append(payload);
        return writer.ToArray();
    }

    public static bool SendDirected(Span<byte> packet, ushort recipient, MessageSendState state, SecureSend send)
    {
        if ((recipient > 7 && recipient != 255) || send is null)
        {
            throw new ArgumentException("Invalid directed message service or recipient");
        }

        var size = AdmitSend(packet);
        AllocateSequence(packet, state);
        state.LastResult = send(recipient, packet, size);
        return state.LastResult >= 0;
    }

    public static bool SendBroadcast(Span<byte> packet, MessageSendState state, BroadcastSend send)
    {
        ArgumentNullException.ThrowIfNull(send);
        var size = AdmitSend(packet);
        AllocateSequence(packet, state);
        state.LastResult = send(packet, size);
        // Callback mutation is observable. Only the signed size word is read here.
        return state.LastResult >= BinaryPrimitives.ReadInt32LittleEndian(packet[8..]);
    }

    private static ByteWriter HeaderWriter(MessageHeader header)
    {
        var writer = new ByteWriter();
        writer.U32(Magic);
        writer.U32(header.Kind);
        writer.U32(header.Size);
        writer.U32(header.Sequence);
        return writer;
    }

    private static uint AdmitSend(ReadOnlySpan<byte> packet)
    {
        var header = DecodeHeader(packet);
        if (header.Size != packet.Length || header.Size > int.MaxValue)
        {
            throw new ArgumentException("Message send requires exact positive declared backing");
        }

        return header.Size;
    }

    private static void AllocateSequence(Span<byte> packet, MessageSendState state)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(packet[12..], state.NextSequence);
        state.NextSequence = unchecked(state.NextSequence + 1);
    }
}
