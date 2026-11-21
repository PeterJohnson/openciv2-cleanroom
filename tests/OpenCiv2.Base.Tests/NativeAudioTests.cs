using OpenCiv2.Base;
using OpenCiv2.Platform.Native;
using Xunit;

namespace OpenCiv2.Base.Tests;

public sealed class NativeAudioTests
{
    public static bool AudioEnabled => Environment.GetEnvironmentVariable("OC2_TEST_NATIVE_AUDIO") == "1";
    [Fact]
    public async Task Silent_missing_and_unsupported_requests_do_not_open_a_device()
    {
        var output = new NativeAudio();
        Assert.Equal(MediaResult.Skipped, await output.PlayAsync(new("missing", "Caption"), TestContext.Current.CancellationToken));
        Assert.Equal(MediaResult.Unavailable, await output.PlayAsync(new("video", "Keep caption", Kind: MediaKind.Video), TestContext.Current.CancellationToken));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Equal(MediaResult.Cancelled, await output.PlayAsync(new("cancelled", "Caption"), cancel.Token));
    }
    // Explicit opt-in: root/headless tests neither require an audio device nor emit sound.
    [Fact(Skip = "Run with OC2_TEST_NATIVE_AUDIO=1 against an isolated output sink.", SkipUnless = nameof(AudioEnabled))]
    public async Task Native_output_completion_loop_cancellation_and_replacement()
    {
        var output = new NativeAudio();
        var clip = new PcmAudio(22050, 2, Enumerable.Range(0, 4410).Select(i => (float)(.03 * Math.Sin((i / 2) * 440d * Math.PI * 2 / 22050))).ToArray());
        for (int iteration = 0; iteration < 5; iteration++)
        {
            Assert.Equal(MediaResult.Completed, await output.PlayAsync(new("authored", "Owned caption", clip), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var pending = output.PlayAsync(new("loop", "Loop caption", clip, Loop: true), cancellation.Token);
            Assert.Equal(MediaResult.Unavailable, await output.PlayAsync(new("overlap", "Ignored", clip), TestContext.Current.CancellationToken));
            await Task.Delay(300, TestContext.Current.CancellationToken);
            Assert.False(pending.IsCompleted);
            cancellation.Cancel();
            Assert.Equal(MediaResult.Cancelled, await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }
    }
}
