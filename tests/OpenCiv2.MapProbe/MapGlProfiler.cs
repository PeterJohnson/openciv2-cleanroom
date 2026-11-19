using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>Test-only GL submission counters and timer/fence observations. No game interop.</summary>
internal sealed class MapGlProfiler
{
    internal readonly record struct Counters(long Draws, long UploadCalls, long UploadBytes, long TextureBinds, long TextureCreates, long TextureDeletes);
    private readonly List<Delegate> callbacks = [];
    private uint unpackBuffer;
    public Counters Counts { get; private set; }

    public nint Resolve(string name)
    {
        nint address = eglGetProcAddress(name);
        if (address == 0) return 0;
        return name switch
        {
            "glDrawArrays" => Wrap<DrawArrays>(address, original => (mode, first, count) => { Draw(); original(mode, first, count); }),
            "glDrawElements" => Wrap<DrawElements>(address, original => (mode, count, type, indices) => { Draw(); original(mode, count, type, indices); }),
            "glDrawRangeElements" => Wrap<DrawRangeElements>(address, original => (mode, start, end, count, type, indices) => { Draw(); original(mode, start, end, count, type, indices); }),
            "glDrawArraysInstanced" => Wrap<DrawArraysInstanced>(address, original => (mode, first, count, instances) => { Draw(); original(mode, first, count, instances); }),
            "glDrawElementsInstanced" => Wrap<DrawElementsInstanced>(address, original => (mode, count, type, indices, instances) => { Draw(); original(mode, count, type, indices, instances); }),
            "glTexImage2D" => Wrap<TexImage>(address, original => (target, level, format, width, height, border, external, type, pixels) =>
            { if (pixels != 0 || unpackBuffer != 0) Upload(width, height, external, type); original(target, level, format, width, height, border, external, type, pixels); }),
            "glTexSubImage2D" => Wrap<TexSubImage>(address, original => (target, level, x, y, width, height, format, type, pixels) =>
            { Upload(width, height, format, type); original(target, level, x, y, width, height, format, type, pixels); }),
            "glBindBuffer" => Wrap<BindTexture>(address, original => (target, buffer) =>
            { if (target == 0x88ec) unpackBuffer = buffer; original(target, buffer); }),
            "glBindTexture" => Wrap<BindTexture>(address, original => (target, texture) =>
            { Counts = Counts with { TextureBinds = Counts.TextureBinds + 1 }; original(target, texture); }),
            "glGenTextures" => Wrap<Textures>(address, original => (count, textures) =>
            { Counts = Counts with { TextureCreates = Counts.TextureCreates + count }; original(count, textures); }),
            "glDeleteTextures" => Wrap<Textures>(address, original => (count, textures) =>
            { Counts = Counts with { TextureDeletes = Counts.TextureDeletes + count }; original(count, textures); }),
            _ => address
        };
    }

    private nint Wrap<T>(nint address, Func<T, T> wrap) where T : Delegate
    {
        T callback = wrap(Marshal.GetDelegateForFunctionPointer<T>(address));
        callbacks.Add(callback);
        return Marshal.GetFunctionPointerForDelegate(callback);
    }

    private void Draw() => Counts = Counts with { Draws = Counts.Draws + 1 };
    private void Upload(int width, int height, uint format, uint type)
    {
        // The exercised Skia path uses uncompressed byte/half/float pixels.
        // Byte estimates exclude row padding and driver-internal transfers.
        int channels = format switch { 0x1908 or 0x80e1 => 4, 0x1907 => 3, 0x8227 => 2, _ => 1 };
        int bytes = type switch { 0x1401 => channels, 0x140b or 0x1403 => channels * 2, 0x1406 => channels * 4, _ => 4 };
        Counts = Counts with { UploadCalls = Counts.UploadCalls + 1, UploadBytes = Counts.UploadBytes + (long)width * height * bytes };
    }

    public static Counters Delta(Counters a, Counters b) => new(b.Draws - a.Draws, b.UploadCalls - a.UploadCalls,
        b.UploadBytes - a.UploadBytes, b.TextureBinds - a.TextureBinds, b.TextureCreates - a.TextureCreates, b.TextureDeletes - a.TextureDeletes);

    internal readonly record struct ClockCalibration(ulong GpuNs, double HostTicks, double UncertaintyMs)
    {
        public double HostTime(ulong gpuNs) => HostTicks + ((double)gpuNs - GpuNs) * Stopwatch.Frequency / 1e9;
    }

    public static ClockCalibration Calibrate()
    {
        ClockCalibration best = new(0, 0, double.MaxValue);
        for (int i = 0; i < 12; i++)
        {
            long before = Stopwatch.GetTimestamp();
            glGetInteger64v(0x8e28, out ulong timestamp);
            long after = Stopwatch.GetTimestamp();
            double uncertainty = Stopwatch.GetElapsedTime(before, after).TotalMilliseconds / 2;
            if (uncertainty < best.UncertaintyMs) best = new(timestamp, (before / 2d + after / 2d), uncertainty);
        }
        return best;
    }

    internal sealed class Frame(ClockCalibration clock) : IDisposable
    {
        private readonly uint[] queries = new uint[2];
        private nint fence;
        public long StartedAt { get; } = Stopwatch.GetTimestamp();
        public void Begin()
        {
            glGenQueries(2, queries);
            glQueryCounter(queries[0], 0x8e28);
        }
        public void Submit()
        {
            glQueryCounter(queries[1], 0x8e28);
            fence = glFenceSync(0x9117, 0);
            glFlush();
        }
        public bool Ready => glClientWaitSync(fence, 0, 0) is 0x911a or 0x911c;
        public (double StreamMs, double CompletionObservedMs, double GpuCompletionMs) Complete()
        {
            uint result = glClientWaitSync(fence, 1, 30_000_000_000);
            if (result is not (0x911a or 0x911c)) throw new InvalidOperationException($"GPU fence failed: {result:x}");
            double observed = Stopwatch.GetElapsedTime(StartedAt).TotalMilliseconds;
            glGetQueryObjectui64v(queries[0], 0x8866, out ulong start);
            glGetQueryObjectui64v(queries[1], 0x8866, out ulong end);
            double completion = (clock.HostTime(end) - StartedAt) * 1000 / Stopwatch.Frequency;
            if (completion < -clock.UncertaintyMs || completion > observed + clock.UncertaintyMs + 1)
                throw new InvalidOperationException($"GPU clock correlation invalid: completion {completion:F3}, observed {observed:F3} ms");
            return ((end - start) / 1e6, observed, completion);
        }
        public void Dispose()
        {
            if (fence != 0) glDeleteSync(fence);
            glDeleteQueries(2, queries);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DrawArrays(uint mode, int first, int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DrawElements(uint mode, int count, uint type, nint indices);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DrawRangeElements(uint mode, uint start, uint end, int count, uint type, nint indices);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DrawArraysInstanced(uint mode, int first, int count, int instances);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DrawElementsInstanced(uint mode, int count, uint type, nint indices, int instances);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void TexImage(uint target, int level, int format, int width, int height, int border, uint external, uint type, nint pixels);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void TexSubImage(uint target, int level, int x, int y, int width, int height, uint format, uint type, nint pixels);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BindTexture(uint target, uint texture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Textures(int count, nint textures);
    [DllImport("libEGL.so.1")] private static extern nint eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport("libGL.so.1")] private static extern void glGenQueries(int count, uint[] ids);
    [DllImport("libGL.so.1")] private static extern void glDeleteQueries(int count, uint[] ids);
    [DllImport("libGL.so.1")] private static extern void glQueryCounter(uint id, uint target);
    [DllImport("libGL.so.1")] private static extern void glGetInteger64v(uint parameter, out ulong value);
    [DllImport("libGL.so.1")] private static extern void glGetQueryObjectui64v(uint id, uint parameter, out ulong value);
    [DllImport("libGL.so.1")] private static extern nint glFenceSync(uint condition, uint flags);
    [DllImport("libGL.so.1")] private static extern uint glClientWaitSync(nint sync, uint flags, ulong timeout);
    [DllImport("libGL.so.1")] private static extern void glDeleteSync(nint sync);
    [DllImport("libGL.so.1")] private static extern void glFlush();
}
