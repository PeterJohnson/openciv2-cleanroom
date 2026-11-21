using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCiv2.Base;

namespace OpenCiv2.Platform.Native;

/// <summary>P10 miniaudio output adapter. Device work never blocks the application dispatcher.</summary>
public sealed partial class NativeAudio : IPresentationMedia
{
    private readonly SemaphoreSlim deviceGate = new(1);
    public string? LastError { get; private set; }
    public async Task<MediaResult> PlayAsync(MediaRequest request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellation.IsCancellationRequested)
        {
            return MediaResult.Cancelled;
        }

        if (request.Kind != MediaKind.Sound)
        {
            return MediaResult.Unavailable;
        }

        if (request.Audio is null)
        {
            return MediaResult.Skipped;
        }

        if (!deviceGate.Wait(0))
        {
            return MediaResult.Unavailable;
        }

        LastError = null;
        try
        { return await Task.Run(() => Play(request.Audio, request.Loop, cancellation), CancellationToken.None); }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { LastError = error.Message; return MediaResult.Unavailable; }
        finally { deviceGate.Release(); }
    }
    private MediaResult Play(PcmAudio audio, bool loop, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return MediaResult.Cancelled;
        }

        if (Version() != 11026)
        { LastError = "miniaudio 0.11.26 is required."; return MediaResult.Unavailable; }
        int result = Open(audio.Samples.ToArray(), audio.Frames, audio.SampleRate, audio.Channels, loop ? 1 : 0, out nint device);
        if (result != 0)
        { LastError = "Audio device initialization failed: " + result; return MediaResult.Unavailable; }
        try
        {
            long progress = Stopwatch.GetTimestamp();
            ulong prior = Clock(device);
            while (true)
            {
                if (cancellation.WaitHandle.WaitOne(10))
                {
                    return MediaResult.Cancelled;
                }

                int state = State(device);
                if (state < 0)
                { LastError = "Audio device stopped."; return MediaResult.Unavailable; }
                if (state == 1)
                {
                    // Provider completion is not a physical speaker timestamp. Drain the backend tail.
                    return cancellation.WaitHandle.WaitOne(250) ? MediaResult.Cancelled : MediaResult.Completed;
                }
                ulong current = Clock(device);
                if (current != prior)
                { prior = current; progress = Stopwatch.GetTimestamp(); }
                if (Stopwatch.GetElapsedTime(progress).TotalSeconds > 3)
                { LastError = "Audio device stopped consuming samples."; return MediaResult.Unavailable; }
            }
        }
        finally { Close(device); }
    }
    [LibraryImport("oc2_audio", EntryPoint = "oc2_audio_version")] private static partial int Version();
    [LibraryImport("oc2_audio", EntryPoint = "oc2_audio_open")] private static partial int Open(float[] samples, int frames, int rate, int channels, int loop, out nint device);
    [LibraryImport("oc2_audio", EntryPoint = "oc2_audio_state")] private static partial int State(nint device);
    [LibraryImport("oc2_audio", EntryPoint = "oc2_audio_clock")] private static partial ulong Clock(nint device);
    [LibraryImport("oc2_audio", EntryPoint = "oc2_audio_close")] private static partial void Close(nint device);
}
