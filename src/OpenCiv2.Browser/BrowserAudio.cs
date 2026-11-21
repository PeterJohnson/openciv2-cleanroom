using System.Runtime.InteropServices.JavaScript;
using OpenCiv2.Base;

internal sealed partial class BrowserAudio : IPresentationMedia
{
    private int playing;
    [JSImport("play", "audio")] private static partial Task<string> Play(string id, string samples, int rate, int channels, bool loop);
    [JSImport("cancel", "audio")] private static partial void Cancel(string id);

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

        if (request.Audio is not { } audio)
        {
            return MediaResult.Skipped;
        }

        if (Interlocked.Exchange(ref playing, 1) != 0)
        {
            return MediaResult.Unavailable;
        }

        string id = Guid.NewGuid().ToString("N");
        try
        {
            // Install JS ownership before registering cancellation; a late cancel cannot target another sound.
            var pending = Play(id, Convert.ToBase64String(audio.EncodeFloat32()), audio.SampleRate, audio.Channels, request.Loop);
            using var registration = cancellation.Register(() => Cancel(id));
            return await pending switch
            {
                "completed" => MediaResult.Completed,
                "cancelled" => MediaResult.Cancelled,
                _ => MediaResult.Unavailable
            };
        }
        catch (JSException) { return MediaResult.Unavailable; }
        finally { Volatile.Write(ref playing, 0); }
    }
}
