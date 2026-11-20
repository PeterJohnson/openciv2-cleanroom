using OpenCiv2.Base;

namespace OpenCiv2.IO;

/// <summary>An owned accepted prefix; a short transfer can retain its effects.</summary>
public sealed record ReadResult(OwnedList<byte> Prefix, bool Complete = false);

/// <summary>
/// Explicit close reports finalization once. Dispose attempts cleanup without masking an earlier
/// failure. Call Close before Dispose when the caller needs the finalization result.
/// </summary>
public abstract class CoreFile : IDisposable
{
    private bool closed;
    private bool finalized;

    public bool Close()
    {
        if (!closed)
        {
            closed = true;
            finalized = Finish();
        }
        return finalized;
    }

    protected void RequireOpen()
    {
        if (closed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    protected abstract bool Finish();

    public void Dispose()
    {
        try
        {
            Close();
        }
        catch { /* Cleanup must preserve an earlier failure, including a failed explicit close. */ }
        GC.SuppressFinalize(this);
    }
}

public abstract class CoreInput : CoreFile
{
    public ReadResult Item(int size)
    {
        RequireOpen();
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        return ReadItem(size);
    }

    public byte? Byte()
    {
        RequireOpen();
        return ReadByte();
    }

    protected abstract ReadResult ReadItem(int size);

    protected abstract byte? ReadByte();
}

public abstract class CoreOutput : CoreFile
{
    public bool Item(ReadOnlySpan<byte> bytes)
    {
        RequireOpen();
        return WriteItem(bytes);
    }

    protected abstract bool WriteItem(ReadOnlySpan<byte> bytes);
}

/// <summary>Storage paths are provider-defined. A portable provider need not expose a filesystem.</summary>
public interface IFileStorage
{
    CoreInput? Read(string path);

    CoreOutput? Replace(string path);

    bool Remove(string path);
}
