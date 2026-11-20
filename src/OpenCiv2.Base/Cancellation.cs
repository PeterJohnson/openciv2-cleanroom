namespace OpenCiv2.Base;

/// <summary>Poll-only cancellation; the flag does not publish unrelated operation state.</summary>
public sealed class CancellationSource
{
    private int requested;
    public StopToken Token => new(this);

    public bool RequestStop() => Interlocked.Exchange(ref requested, 1) == 0;

    internal bool Requested => Volatile.Read(ref requested) != 0;
}

/// <summary>A default token never stops; a retained token keeps its flag alive.</summary>
public readonly struct StopToken
{
    private readonly CancellationSource? source;

    internal StopToken(CancellationSource source) => this.source = source;

    public bool StopRequested => source?.Requested ?? false;

    public void CheckStop()
    {
        if (StopRequested)
        {
            throw new CancelledException();
        }
    }
}

public sealed class CancelledException() : Exception("Operation cancelled before publication");
