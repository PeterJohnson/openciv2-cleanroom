namespace OpenCiv2.Network;

public enum SocketError { None, WouldBlock, InProgress, Interrupted, NoBuffers, IsConnected, Fatal }
public readonly record struct SocketResult(int Accepted = 0, SocketError Error = SocketError.None);
public interface ISocketTransport
{
    /// <summary>Exceptions mean no bytes were accepted. Accepted bytes require a result.</summary>
    SocketResult Transmit(ReadOnlySpan<byte> bytes);
}
public sealed class SendLimits
{
    public uint ChunkCapacity { get; set; } = 8192;
    public uint OversizedLimit { get; set; } = 50000;
}
public sealed record SendDependencies(ISocketTransport Transport, SendLimits Limits, Action? PostWrite = null, Action<int>? Backlog = null);
public enum SendResult { Immediate, Queued, Oversized, IsConnected, Fatal }
public enum DrainResult { Empty, Complete, Pending, Fatal, ExcludedRetiredDescriptor }

/// <summary>Owned FIFO partial sends. Transport/backlog callbacks do not reenter this queue;
/// event posting observes published state. Limits are sampled for each attempt.</summary>
public sealed class SendQueue
{
    private sealed class Pending(byte[] bytes, int sent)
    {
        public byte[] Bytes { get; } = bytes;
        public int Sent { get; set; } = sent;
    }
    private readonly Queue<Pending> queue = new();
    public int Count => queue.Count;
    public int FrontOffset => queue.TryPeek(out var pending) ? pending.Sent : 0;
    public int BacklogThreshold { get; private set; } = 50;
    public void Clear() => queue.Clear();

    public SendResult Send(uint tag, ReadOnlySpan<byte> payload, SendDependencies services, ReadOnlySpan<byte> padding = default)
    {
        if (payload.Length >= unchecked(services.Limits.OversizedLimit - 12)) return SendResult.Oversized;
        var record = Wire.EncodeFrame(tag, payload, padding);
        if (queue.Count != 0)
        {
            queue.Enqueue(new(record, 0));
            services.PostWrite?.Invoke();
            return SendResult.Queued;
        }
        var sent = Attempt(record, services);
        switch (sent.Error)
        {
            case SocketError.None: break;
            case SocketError.WouldBlock:
            case SocketError.InProgress:
            case SocketError.Interrupted:
            case SocketError.NoBuffers: sent = sent with { Accepted = 0 }; break;
            case SocketError.IsConnected: return SendResult.IsConnected;
            default: return SendResult.Fatal;
        }
        if (sent.Accepted == record.Length) return SendResult.Immediate;
        queue.Enqueue(new(record, sent.Accepted));
        services.PostWrite?.Invoke();
        return SendResult.Queued;
    }

    public DrainResult DrainOne(SendDependencies services)
    {
        if (queue.Count == 0) return DrainResult.Empty;
        var count = queue.Count;
        if (count > BacklogThreshold)
        {
            services.Backlog?.Invoke(count);
            BacklogThreshold = count + 50;
        }
        else if (count < 50) BacklogThreshold = 50;
        var pending = queue.Peek();
        var sent = Attempt(pending.Bytes.AsSpan(pending.Sent), services);
        if (sent.Error == SocketError.WouldBlock) sent = sent with { Accepted = 0 };
        else if (sent.Error == SocketError.IsConnected)
        {
            queue.Dequeue(); return DrainResult.ExcludedRetiredDescriptor;
        }
        else if (sent.Error != SocketError.None)
        {
            queue.Dequeue(); return DrainResult.Fatal;
        }
        pending.Sent += sent.Accepted;
        if (pending.Sent == pending.Bytes.Length)
        {
            queue.Dequeue(); return DrainResult.Complete;
        }
        services.PostWrite?.Invoke();
        return DrainResult.Pending;
    }

    public DrainResult WriteEvent(SendDependencies services, Action? closeHandler = null)
    {
        var result = DrainResult.Empty;
        while (queue.Count != 0)
        {
            result = DrainOne(services);
            if (result == DrainResult.Fatal) closeHandler?.Invoke();
            if (result != DrainResult.Complete) break;
        }
        return result;
    }
    private static SocketResult Attempt(ReadOnlySpan<byte> bytes, SendDependencies services)
    {
        var part = bytes[..(int)Math.Min((uint)bytes.Length, services.Limits.ChunkCapacity)];
        var result = services.Transport.Transmit(part);
        if (result.Error == SocketError.None && (result.Accepted < 0 || result.Accepted > part.Length))
            throw new ArgumentException("Socket accepted count outside its supplied span");
        return result;
    }
}
