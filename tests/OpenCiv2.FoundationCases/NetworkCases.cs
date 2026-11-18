using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.IO;
using OpenCiv2.Network;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void MessageEnvelopeBytes()
    {
        var bytes = Messages.Encode(new(new(0x12345678, 19, 0xfedcba98), new([0, 128, 255])));
        BytesEqual([0x60, 0x66, 0x60, 0x66, 0x78, 0x56, 0x34, 0x12, 19, 0, 0, 0, 0x98, 0xba, 0xdc, 0xfe, 0, 128, 255], bytes);
        var decoded = Messages.Decode(bytes); BytesEqual(bytes, Messages.Encode(decoded)); bytes[16] = 99;
        Equal((byte)0, decoded.Body[0]); Equal(new MessageHeader(0x12345678, 19, 0xfedcba98), decoded.Header);
        Messages.InitializeHeader(bytes, 7, 16); Equal(new MessageHeader(7, 16, 0xfedcba98), Messages.DecodeHeader(bytes)); Equal((byte)99, bytes[16]);
        var empty = Messages.Encode(new(new(5), new([]))); Equal(16, empty.Length); Equal(0U, Messages.DecodeHeader(empty).Sequence);
    }
    private static void MessageEnvelopeAdmission()
    {
        var bytes = Messages.Encode(new(new(1), new([])));
        for (int i = 0; i < 16; ++i) { var cut = bytes[..i]; Throws<DecodeException>(() => Messages.DecodeHeader(cut)); }
        foreach (int offset in new[] { 0, 8 }) { var bad = bytes.ToArray(); bad[offset] = 0; Throws<DecodeException>(() => Messages.Decode(bad)); }
        Throws<DecodeException>(() => Messages.Decode([.. bytes, 0]));
        Throws<ArgumentException>(() => Messages.Encode(new(new(1, 15), new([]))));
        Throws<ArgumentException>(() => Messages.Encode(new(new(1, 17), new([]))));
        var prior = bytes.ToArray(); Throws<ArgumentException>(() => Messages.InitializeHeader(bytes, 9, 15)); BytesEqual(prior, bytes);
        Throws<ArgumentException>(() => Messages.InitializeHeader(new byte[15], 9, 16));
    }
    private static void ArgumentEnvelopeBytes()
    {
        int[] args = [int.MinValue, int.MaxValue, -1, 0, 1, 0x12345678];
        var bytes = Messages.EncodeArguments(0x42, args, [255, 0, 2], uint.MaxValue);
        Equal(47, bytes.Length); Equal(3, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40)));
        for (int i = 0; i < 6; ++i) Equal(args[i], BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16 + 4 * i)));
        var decoded = Messages.DecodeArguments(bytes); Equal(new OwnedList<int>(args), decoded.Arguments); Equal(new OwnedList<byte>([255, 0, 2]), decoded.Payload);
        Equal(new MessageHeader(0x42, 47, uint.MaxValue), decoded.Header); bytes[44] = 7; Equal((byte)255, decoded.Payload[0]);
        var empty = Messages.EncodeArguments(9, new int[6], [], 2); Equal(44, empty.Length); Equal(0, Messages.DecodeArguments(empty).Payload.Count);
        Throws<ArgumentException>(() => Messages.EncodeArguments(1, new int[5], [], 0));
        Throws<DecodeException>(() => Messages.DecodeArguments(Messages.Encode(new(new(1, 43), new(new byte[27])))));
        foreach (int length in new[] { -1, 0, 2, 4, int.MaxValue })
        { var bad = bytes.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(40), length); Throws<DecodeException>(() => Messages.DecodeArguments(bad)); }
    }
    private static void MessageSendSequencing()
    {
        var packet = Messages.Encode(new(new(4), new([]))); var state = new MessageSendState { NextSequence = uint.MaxValue, LastResult = 88 };
        Equal(false, Messages.SendDirected(packet, 255, state, (recipient, live, size) =>
        { Equal((ushort)255, recipient); Equal(16U, size); Equal(uint.MaxValue, Messages.DecodeHeader(live).Sequence); Equal(0U, state.NextSequence); return -9; }));
        Equal(-9, state.LastResult); Equal(true, Messages.SendDirected(packet, 0, state, (_, _, _) => 0)); Equal(1U, state.NextSequence);
        foreach (ushort recipient in new ushort[] { 8, 254, 256, ushort.MaxValue }) Throws<ArgumentException>(() => Messages.SendDirected(packet, recipient, state, (_, _, _) => 0));
        Equal(1U, state.NextSequence); Equal(0, state.LastResult);
        Throws<IOException>(() => Messages.SendDirected(packet, 7, state, (_, _, _) => throw new IOException("send")));
        Equal(2U, state.NextSequence); Equal(1U, Messages.DecodeHeader(packet).Sequence); Equal(0, state.LastResult);
        Throws<ArgumentException>(() => Messages.SendDirected(packet, 1, state, null!)); Equal(2U, state.NextSequence);
        packet[8] = 15; Throws<DecodeException>(() => Messages.SendDirected(packet, 1, state, (_, _, _) => 0)); Equal(2U, state.NextSequence);
    }
    private static void MessageBroadcastLiveSize()
    {
        var packet = Messages.Encode(new(new(0), new([]))); var state = new MessageSendState();
        Equal(false, Messages.SendBroadcast(packet, state, (_, _) => 15)); Equal(15, state.LastResult);
        Equal(true, Messages.SendBroadcast(packet, state, (live, size) => { Equal(16U, size); live[0] = 0; BinaryPrimitives.WriteInt32LittleEndian(live[8..], -5); return -4; }));
        Equal(2U, state.NextSequence); Equal(-4, state.LastResult);
        Messages.InitializeHeader(packet, 0, 16);
        Equal(false, Messages.SendBroadcast(packet, state, (live, _) => { BinaryPrimitives.WriteInt32LittleEndian(live[8..], 50); return 49; }));
        Messages.InitializeHeader(packet, 0, 16);
        Throws<IOException>(() => Messages.SendBroadcast(packet, state, (_, _) => throw new IOException("send")));
        Equal(4U, state.NextSequence); Equal(49, state.LastResult);
        Throws<ArgumentNullException>(() => Messages.SendBroadcast(packet, state, null!)); Equal(4U, state.NextSequence);
    }
    private static void StreamFrameBytes()
    {
        Equal(0xff345678U, Wire.RouteTag(-1, 0x1234, 0xabcd5678));
        Equal(0x00000000U, Wire.RouteTag(256, 256, 0x10000));
        foreach (var (length, total) in new[] { (0, 64), (2, 64), (52, 64), (53, 128), (100, 128), (308, 320), (3002, 3072) })
        {
            var payload = Enumerable.Range(0, length).Select(i => unchecked((byte)i)).ToArray(); var bytes = Wire.EncodeFrame(0x12345678, payload);
            Equal(total, bytes.Length); Equal((uint)total, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
            Equal(0x12345678U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))); Equal((uint)length, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
            BytesEqual(payload, bytes.AsSpan(12, length).ToArray()); Equal(true, bytes.Skip(12 + length).All(b => b == 0));
        }
        var padding = Enumerable.Range(0, 50).Select(i => (byte)(i + 100)).ToArray(); var exact = Wire.EncodeFrame(Wire.IdTag, [1, 2], padding);
        BytesEqual(padding, exact[14..]); Throws<ArgumentException>(() => Wire.EncodeFrame(0, [1, 2], [0]));
    }
    private static void StreamFragmentation()
    {
        var first = Wire.EncodeFrame(Wire.PingRequestTag, [1, 2], Enumerable.Repeat((byte)0xcd, 50).ToArray());
        var second = Wire.EncodeFrame(Wire.PingReplyTag, new byte[100]); byte[] stream = [.. first, .. second];
        for (int split = 0; split <= stream.Length; ++split)
        {
            var receiver = new FrameReceiver(); var frames = new List<byte[]>();
            void Frame(FrameView frame) { frames.Add(frame.Wire.ToArray()); Equal(frame.Wire.Length - frame.Payload.Length - 12, frame.Padding.Length); }
            var a = receiver.Feed(stream.AsSpan(0, split), Frame); var b = receiver.Feed(stream.AsSpan(split), Frame);
            Equal(2, a.Delivered + b.Delivered); Equal(2, frames.Count); BytesEqual(first, frames[0]); BytesEqual(second, frames[1]); Equal(false, receiver.Incomplete); Equal(0U, receiver.PendingTotal);
        }
        var one = new FrameReceiver(); int calls = 0;
        foreach (var value in stream) one.Feed([value], frame => { ++calls; Equal(true, frame.Tag == Wire.PingRequestTag || frame.Tag == Wire.PingReplyTag); });
        Equal(2, calls); Equal(false, one.Incomplete);
        var unrounded = new byte[15]; BinaryPrimitives.WriteUInt32LittleEndian(unrounded, 15); BinaryPrimitives.WriteUInt32LittleEndian(unrounded.AsSpan(8), 3);
        Equal(1, one.Feed(unrounded, frame => Equal(3, frame.Payload.Length)).Delivered);
        Equal(0, one.Feed(first).Delivered); Equal(false, one.Incomplete);
    }
    private static void StreamReceiveAdmission()
    {
        var receiver = new FrameReceiver(); var valid = Wire.EncodeFrame(1, [2]);
        foreach (uint size in new uint[] { 0, 1, 4, 11, 8 * 1024 * 1024 + 1, uint.MaxValue })
        {
            var input = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(input, size);
            var result = receiver.Feed([.. input, .. valid], _ => throw new Exception("must discard feed"));
            var expected = size == 0 ? ReceiveStop.ZeroTotal : size < 12 ? ReceiveStop.Malformed : ReceiveStop.BackingLimit;
            Equal((int)expected, (int)result.Stop); Equal(false, receiver.Incomplete); Equal(1, receiver.Feed(valid, _ => { }).Delivered);
        }
        var malformed = valid.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(8), 53);
        Equal((int)ReceiveStop.Malformed, (int)receiver.Feed([.. malformed, .. valid]).Stop); Equal(false, receiver.Incomplete);
        receiver.BackingLimit = 63; Equal((int)ReceiveStop.BackingLimit, (int)receiver.Feed(valid).Stop);
    }
    private static void StreamOversizedCallbacks()
    {
        var bytes = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, 50000); var receiver = new FrameReceiver(); var values = new List<uint>();
        Equal((int)ReceiveStop.Oversized, (int)receiver.Feed(bytes, onOversized: values.Add).Stop); Equal(49988U, values[^1]);
        for (int split = 1; split < 4; ++split)
        {
            receiver.Feed(bytes.AsSpan(0, split), onOversized: values.Add); Equal(split, receiver.Received); Equal(0U, receiver.PendingTotal);
            Equal((int)ReceiveStop.Oversized, (int)receiver.Feed(bytes.AsSpan(split), onOversized: values.Add).Stop); Equal(50000U, values[^1]); Equal(false, receiver.Incomplete);
        }
        receiver.Feed(bytes); Equal(4, receiver.Received); Equal(50000U, receiver.PendingTotal); receiver.Reset();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 49988); Equal((int)ReceiveStop.Exhausted, (int)receiver.Feed(bytes, onOversized: values.Add).Stop); Equal(4, receiver.Received); receiver.Reset();
        receiver.OversizedLimit = 11; BinaryPrimitives.WriteUInt32LittleEndian(bytes, 64); receiver.Feed(bytes, onOversized: _ => throw new Exception("unsigned threshold")); Equal(64U, receiver.PendingTotal);
    }
    private static void StreamCallbackBoundaries()
    {
        var receiver = new FrameReceiver(); var bytes = Wire.EncodeFrame(1, []);
        var reset = receiver.Feed([.. bytes, .. bytes], _ => receiver.Reset()); Equal(1, reset.Delivered); Equal((int)ReceiveStop.ResetInCallback, (int)reset.Stop); Equal(false, receiver.Incomplete);
        var close = receiver.Feed(bytes, _ => receiver.Close()); Equal((int)ReceiveStop.ResetInCallback, (int)close.Stop); Equal((int)ReceiveStop.Closed, (int)receiver.Feed(bytes).Stop);
        receiver.Reset(); receiver.Feed(bytes, _ => Throws<InvalidOperationException>(() => receiver.Feed(bytes))); Equal(false, receiver.Incomplete);
        Throws<IOException>(() => receiver.Feed(bytes, _ => throw new IOException("callback"))); receiver.Reset(); Equal(1, receiver.Feed(bytes, _ => { }).Delivered);
        receiver.OversizedLimit = 13;
        Equal((int)ReceiveStop.ResetInCallback, (int)receiver.Feed(bytes, onOversized: _ => receiver.Close()).Stop); Equal((int)ReceiveStop.Closed, (int)receiver.Feed(bytes).Stop);
        receiver.Reset(); Throws<IOException>(() => receiver.Feed(bytes, onOversized: _ => throw new IOException("oversized"))); receiver.Reset(); Equal(false, receiver.Incomplete);
    }

    private sealed class ScriptedSocket : ISocketTransport
    {
        public List<byte[]> Attempts { get; } = [];
        public Queue<SocketResult> Results { get; } = new();
        public bool Throw { get; set; }
        public SocketResult Transmit(ReadOnlySpan<byte> bytes)
        {
            Attempts.Add(bytes.ToArray());
            if (Throw) throw new IOException("socket");
            return Results.TryDequeue(out var result) ? result : new(bytes.Length);
        }
    }
    private static void StreamSendQueueProgress()
    {
        var socket = new ScriptedSocket(); var queue = new SendQueue(); var limits = new SendLimits { ChunkCapacity = 10 }; int posts = 0;
        var services = new SendDependencies(socket, limits, () => { ++posts; Equal(true, queue.Count > 0); });
        byte[] payload = [1, 2]; var expected = Wire.EncodeFrame(99, payload); socket.Results.Enqueue(new(3));
        Equal((int)SendResult.Queued, (int)queue.Send(99, payload, services)); Equal(3, queue.FrontOffset); Equal(1, posts); payload[0] = 9;
        Equal((int)SendResult.Queued, (int)queue.Send(88, [], services)); Equal(2, queue.Count); Equal(1, socket.Attempts.Count);
        limits.ChunkCapacity = 5; Equal((int)DrainResult.Pending, (int)queue.DrainOne(services)); BytesEqual(expected.AsSpan(3, 5).ToArray(), socket.Attempts[^1]); Equal(8, queue.FrontOffset);
        limits.ChunkCapacity = 100; Equal((int)DrainResult.Complete, (int)queue.WriteEvent(services)); Equal(0, queue.Count); Equal(0, queue.FrontOffset);
        BytesEqual(expected[8..], socket.Attempts[^2]); BytesEqual(Wire.EncodeFrame(88, []), socket.Attempts[^1]);
        Equal((int)DrainResult.Empty, (int)queue.WriteEvent(services)); Equal((int)SendResult.Immediate, (int)queue.Send(1, [], services));
        limits.ChunkCapacity = 0; Equal((int)SendResult.Queued, (int)queue.Send(1, [], services)); Equal(0, queue.FrontOffset); Equal(0, socket.Attempts[^1].Length);
        Equal((int)DrainResult.Pending, (int)queue.DrainOne(services)); queue.Clear(); Equal(0, queue.Count);
    }
    private static void StreamSendErrorDomains()
    {
        foreach (var error in Enum.GetValues<SocketError>().Where(e => e != SocketError.None))
        {
            var socket = new ScriptedSocket(); var queue = new SendQueue(); var services = new SendDependencies(socket, new()); socket.Results.Enqueue(new(999, error));
            var expected = error == SocketError.IsConnected ? SendResult.IsConnected : error == SocketError.Fatal ? SendResult.Fatal : SendResult.Queued;
            Equal((int)expected, (int)queue.Send(1, [], services)); Equal(expected == SendResult.Queued ? 1 : 0, queue.Count); Equal(0, queue.FrontOffset);
            if (queue.Count == 0) { socket.Results.Enqueue(new(0)); queue.Send(1, [], services); }
            socket.Results.Enqueue(new(999, error)); int closed = 0; var drain = queue.WriteEvent(services, () => ++closed);
            var drainExpected = error == SocketError.WouldBlock ? DrainResult.Pending : error == SocketError.IsConnected ? DrainResult.ExcludedRetiredDescriptor : DrainResult.Fatal;
            Equal((int)drainExpected, (int)drain); Equal(drainExpected == DrainResult.Fatal ? 1 : 0, closed); Equal(error == SocketError.WouldBlock ? 1 : 0, queue.Count);
        }
    }
    private static void StreamSendFailureRetention()
    {
        var socket = new ScriptedSocket { Throw = true }; var queue = new SendQueue(); var limits = new SendLimits(); var services = new SendDependencies(socket, limits);
        Throws<IOException>(() => queue.Send(0, [], services)); Equal(0, queue.Count); socket.Throw = false;
        foreach (int accepted in new[] { -1, 65 }) { socket.Results.Enqueue(new(accepted)); Throws<ArgumentException>(() => queue.Send(0, [], services)); Equal(0, queue.Count); }
        socket.Results.Enqueue(new(7)); queue.Send(0, [], services); socket.Throw = true; Throws<IOException>(() => queue.DrainOne(services)); Equal(1, queue.Count); Equal(7, queue.FrontOffset);
        socket.Throw = false; socket.Results.Enqueue(new(58)); Throws<ArgumentException>(() => queue.DrainOne(services)); Equal(7, queue.FrontOffset);
        socket.Results.Enqueue(new(1)); Throws<IOException>(() => queue.DrainOne(services with { PostWrite = () => throw new IOException("post") })); Equal(8, queue.FrontOffset);
        queue.Clear(); socket.Results.Enqueue(new(0)); Throws<IOException>(() => queue.Send(0, [], services with { PostWrite = () => throw new IOException("post") })); Equal(1, queue.Count);
        queue.Clear(); limits.OversizedLimit = 12; int calls = socket.Attempts.Count;
        Equal((int)SendResult.Oversized, (int)queue.Send(0, [], services)); Equal(calls, socket.Attempts.Count);
        limits.OversizedLimit = 14; Equal((int)SendResult.Oversized, (int)queue.Send(0, [1, 2], services)); Equal((int)SendResult.Immediate, (int)queue.Send(0, [1], services));
    }
    private static void StreamSendBacklog()
    {
        var socket = new ScriptedSocket(); var queue = new SendQueue(); var limits = new SendLimits { ChunkCapacity = 0 }; var reports = new List<int>();
        var services = new SendDependencies(socket, limits, Backlog: reports.Add);
        for (int i = 0; i < 51; ++i) queue.Send(0, [], services);
        Equal(1, socket.Attempts.Count); queue.DrainOne(services); Equal(51, reports.Single()); Equal(101, queue.BacklogThreshold);
        queue.DrainOne(services); Equal(1, reports.Count);
        for (int i = 51; i < 102; ++i) queue.Send(0, [], services);
        Throws<IOException>(() => queue.DrainOne(services with { Backlog = _ => throw new IOException("backlog") })); Equal(101, queue.BacklogThreshold); Equal(102, queue.Count);
        queue.DrainOne(services); Equal(102, reports[^1]); Equal(152, queue.BacklogThreshold);
        queue.Clear(); Equal(152, queue.BacklogThreshold); queue.Send(0, [], services); queue.DrainOne(services); Equal(50, queue.BacklogThreshold);
        limits.ChunkCapacity = 64; Equal((int)DrainResult.Complete, (int)queue.WriteEvent(services));
    }
    private static void DatagramBytesAndRetention()
    {
        var backing = Enumerable.Repeat((byte)0xaa, 516).ToArray();
        BytesEqual([4, 3, 2, 1], Wire.EncodeDatagram(0x01020304, 65536, null, backing)); Equal((byte)0xaa, backing[4]);
        BytesEqual([255, 255, 255, 255, 0xaa], Wire.EncodeDatagram(uint.MaxValue, 65537, null, backing));
        byte[] payload = [1, 2, 3]; BytesEqual([0, 0, 0, 0, 1, 2, 3], Wire.EncodeDatagram(0, 3, payload, backing));
        BytesEqual([5, 0, 0, 0, 1, 2, 3, 0xaa], Wire.EncodeDatagram(5, 4, null, backing));
        Equal(516, Wire.EncodeDatagram(1, 65535, null, backing).Length);
        var prior = backing.ToArray(); Throws<ArgumentException>(() => Wire.EncodeDatagram(99, 4, payload, backing)); BytesEqual(prior, backing);
        Throws<ArgumentException>(() => Wire.EncodeDatagram(0, 0, null, new byte[3]));
        for (int i = 0; i < 4; ++i) { var shortBytes = new byte[i]; Throws<DecodeException>(() => Wire.DecodeDatagram(shortBytes)); }
        var bytes = Wire.EncodeDatagram(0x80000000, 3, payload, backing); var decoded = Wire.DecodeDatagram(bytes);
        Equal(0x80000000U, decoded.Header); Equal(new OwnedList<byte>(payload), decoded.Payload); bytes[4] = 99; Equal((byte)1, decoded.Payload[0]);
        Equal(0, Wire.DecodeDatagram(new byte[4]).Payload.Count);
    }
}
