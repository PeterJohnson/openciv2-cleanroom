using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.IO;

namespace OpenCiv2.Network;

public readonly ref struct FrameView(uint tag, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> padding, ReadOnlySpan<byte> wire)
{
    public uint Tag { get; } = tag;
    public ReadOnlySpan<byte> Payload { get; } = payload;
    public ReadOnlySpan<byte> Padding { get; } = padding;
    public ReadOnlySpan<byte> Wire { get; } = wire;
}
public delegate void FrameHandler(FrameView frame);
public enum ReceiveStop { Exhausted, Oversized, ZeroTotal, Malformed, BackingLimit, ResetInCallback, Closed }
public readonly record struct ReceiveResult(int Delivered = 0, ReceiveStop Stop = ReceiveStop.Exhausted);
public sealed record Datagram(uint Header, OwnedList<byte> Payload);

/// <summary>N05 sections 4–6. Explicit framing and bounded, owned datagrams.</summary>
public static class Wire
{
    public const uint IdTag = 0xfa000000, PingRequestTag = 0xfc000000, PingReplyTag = 0xfd000000;
    public static uint RouteTag(int source, ushort destination, uint flags) => unchecked((uint)source << 24) | ((uint)destination & 255) << 16 | (flags & 65535);
    public static byte[] EncodeFrame(uint tag, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> padding = default)
    {
        if (payload.Length > int.MaxValue - 75) throw new ArgumentException("Frame length exceeds managed backing domain", nameof(payload));
        var total = (payload.Length + 75) & ~63;
        var tail = total - payload.Length - 12;
        if (!padding.IsEmpty && padding.Length != tail) throw new ArgumentException("Exact frame padding must cover the whole tail", nameof(padding));
        var bytes = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), tag);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)payload.Length);
        payload.CopyTo(bytes.AsSpan(12));
        if (!padding.IsEmpty) padding.CopyTo(bytes.AsSpan(12 + payload.Length));
        return bytes;
    }
    /// <summary>Null payload retains bytes. Requested length narrows to u16 before the capacity cap.</summary>
    public static byte[] EncodeDatagram(uint header, uint requested, ReadOnlyMemory<byte>? payload, Span<byte> retainedBacking)
    {
        if (retainedBacking.Length < 4) throw new ArgumentException("Datagram backing lacks its header");
        var count = Math.Min(unchecked((ushort)requested), retainedBacking.Length - 4);
        if (payload.HasValue && payload.Value.Length < count) throw new ArgumentException("Datagram payload lacks requested backing");
        BinaryPrimitives.WriteUInt32LittleEndian(retainedBacking, header);
        if (payload.HasValue) payload.Value.Span[..count].CopyTo(retainedBacking[4..]);
        return retainedBacking[..(4 + count)].ToArray();
    }
    public static Datagram DecodeDatagram(ReadOnlySpan<byte> bytes)
    {
        var reader = new ByteReader(bytes);
        return new(reader.U32(), new(reader.Remaining.ToArray()));
    }
}

/// <summary>Incremental N05 parser. Rejection discards this feed, not inferred future bytes.
/// Callbacks borrow views synchronously and may Reset/Close; recursive Feed is excluded.</summary>
public sealed class FrameReceiver
{
    private readonly byte[] header = new byte[4];
    private int headerUsed, received;
    private uint total;
    private byte[] frame = [];
    private ulong epoch;
    private bool closed, feeding;
    public uint OversizedLimit { get; set; } = 50000;
    public uint BackingLimit { get; set; } = 8 * 1024 * 1024;
    public int Received => frame.Length == 0 ? headerUsed : received;
    public uint PendingTotal => total;
    public bool Incomplete => Received != 0;
    public void Reset() { ClearPending(); closed = false; epoch = unchecked(epoch + 1); }
    public void Close() { Reset(); closed = true; }
    private void ClearPending() { headerUsed = received = 0; total = 0; frame = []; }
    public ReceiveResult Feed(ReadOnlySpan<byte> input, FrameHandler? onFrame = null, Action<uint>? onOversized = null)
    {
        if (feeding) throw new InvalidOperationException("Recursive stream feed is outside the admitted dispatch domain");
        if (closed) return new(0, ReceiveStop.Closed);
        feeding = true;
        var delivered = 0;
        ReceiveResult Discard(ReceiveStop stop) { ClearPending(); return new(delivered, stop); }
        try
        {
            while (!input.IsEmpty)
            {
                if (frame.Length == 0)
                {
                    var split = headerUsed != 0;
                    var count = Math.Min(4 - headerUsed, input.Length);
                    input[..count].CopyTo(header.AsSpan(headerUsed));
                    headerUsed += count; input = input[count..];
                    if (headerUsed < 4) return new(delivered);
                    total = BinaryPrimitives.ReadUInt32LittleEndian(header);
                    if (onOversized is not null && total > unchecked(OversizedLimit - 12))
                    {
                        var before = epoch;
                        onOversized(split ? total : unchecked(total - 12));
                        if (before != epoch) return new(delivered, ReceiveStop.ResetInCallback);
                        return Discard(ReceiveStop.Oversized);
                    }
                    if (total == 0) return Discard(ReceiveStop.ZeroTotal);
                    if (total < 12) return Discard(ReceiveStop.Malformed);
                    if (total > BackingLimit || total > int.MaxValue) return Discard(ReceiveStop.BackingLimit);
                    frame = new byte[(int)total];
                    header.CopyTo(frame, 0);
                    headerUsed = 0; received = 4;
                }
                var needed = Math.Min((int)total - received, input.Length);
                input[..needed].CopyTo(frame.AsSpan(received));
                received += needed; input = input[needed..];
                if (received != total) return new(delivered);
                var tag = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4));
                var length = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8));
                if (length > total - 12) return Discard(ReceiveStop.Malformed);
                if (onFrame is not null)
                {
                    var before = epoch;
                    ++delivered;
                    onFrame(new(tag, frame.AsSpan(12, (int)length), frame.AsSpan(12 + (int)length), frame));
                    if (before != epoch) return new(delivered, ReceiveStop.ResetInCallback);
                }
                ClearPending();
            }
            return new(delivered);
        }
        finally { feeding = false; }
    }
}
