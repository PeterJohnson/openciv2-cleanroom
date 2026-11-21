using System.Diagnostics;

namespace OpenCiv2.Base;

/// <summary>Explicit environmental time. Monotonic milliseconds drive deadlines; calendar time is separate.</summary>
public interface IPlatformClock
{
    ulong Milliseconds { get; }
    DateTimeOffset LocalNow { get; }
}

public sealed class SystemPlatformClock : IPlatformClock
{
    private readonly long origin = Stopwatch.GetTimestamp();
    public ulong Milliseconds => checked((ulong)Stopwatch.GetElapsedTime(origin).TotalMilliseconds);
    public DateTimeOffset LocalNow => DateTimeOffset.Now;
}

/// <summary>A deterministic clock for authored service tests; never advances on a read.</summary>
public sealed class ManualPlatformClock(DateTimeOffset localNow) : IPlatformClock
{
    public ulong Milliseconds { get; private set; }
    public DateTimeOffset LocalNow { get; private set; } = localNow;
    public void Advance(uint milliseconds)
    {
        Milliseconds = checked(Milliseconds + milliseconds);
        LocalNow = LocalNow.AddMilliseconds(milliseconds);
    }
}

public sealed record MediaRequest(string Identity, string Caption);
public enum MediaResult { Completed, Skipped, Cancelled, Unavailable }

/// <summary>P10 presentation only. Semantic effects/notices are committed independently of playback.</summary>
public interface IPresentationMedia
{
    Task<MediaResult> PlayAsync(MediaRequest request, CancellationToken cancellation = default);
}

/// <summary>No decoder/device. Explicitly reports skipped or cancelled rather than claiming playback.</summary>
public sealed class SilentMedia : IPresentationMedia
{
    public Task<MediaResult> PlayAsync(MediaRequest request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(cancellation.IsCancellationRequested ? MediaResult.Cancelled : MediaResult.Skipped);
    }
}
