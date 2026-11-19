using System.Runtime.InteropServices;
using SkiaSharp;

/// <summary>Independent Linux EGL test context. Never linked into the game host.</summary>
internal sealed class MapProbeGlContext : IDisposable
{
    private const int None = 0x3038;
    private nint display, surface, native;
    private GRGlInterface? api;
    public GRContext Context { get; private set; } = null!;
    public string Renderer { get; private set; } = "";
    public string Version { get; private set; } = "";
    public MapGlProfiler? Profiler { get; }

    public MapProbeGlContext(bool profile = false)
    {
        if (profile) Profiler = new();
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("The GPU probe uses Linux EGL");
        try
        {
            display = eglGetDisplay(0);
            Check(display != 0 && eglInitialize(display, out _, out _), "initialize EGL on the selected display");
            Check(eglBindAPI(0x30a2), "bind desktop OpenGL");
            nint[] configs = new nint[1];
            Check(eglChooseConfig(display, [0x3033, 1, 0x3040, 8, 0x3024, 8, 0x3023, 8, 0x3022, 8, None],
                configs, 1, out int count) && count == 1, "choose RGBA pbuffer config");
            surface = eglCreatePbufferSurface(display, configs[0], [0x3057, 1, 0x3056, 1, None]);
            native = eglCreateContext(display, configs[0], 0, [None]);
            Check(surface != 0 && native != 0 && eglMakeCurrent(display, surface, surface, native), "make OpenGL current");
            Renderer = Marshal.PtrToStringAnsi(glGetString(0x1f01)) ?? "unknown";
            Version = Marshal.PtrToStringAnsi(glGetString(0x1f02)) ?? "unknown";
            if (Renderer.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase) || Renderer.Contains("softpipe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GPU probe requires hardware rendering: " + Renderer);
            api = GRGlInterface.CreateOpenGl(Profiler is null ? eglGetProcAddress : Profiler.Resolve) ?? throw new InvalidOperationException("No Skia GL interface");
            // Match Avalonia's default SkiaOptions stencil policy so this
            // independent context uses the same vector antialiasing strategy.
            Context = GRContext.CreateGl(api, new GRContextOptions { AvoidStencilBuffers = true }) ??
                throw new InvalidOperationException("No Skia GPU context");
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        Context?.Dispose();
        api?.Dispose();
        if (display == 0) return;
        eglMakeCurrent(display, 0, 0, 0);
        if (native != 0) eglDestroyContext(display, native);
        if (surface != 0) eglDestroySurface(display, surface);
        eglTerminate(display);
        display = 0;
    }

    private static void Check(bool success, string operation)
    {
        if (!success) throw new InvalidOperationException($"Could not {operation}: EGL 0x{eglGetError():x}");
    }

    [DllImport("libEGL.so.1")] private static extern nint eglGetDisplay(nint nativeDisplay);
    [DllImport("libEGL.so.1")] private static extern bool eglInitialize(nint display, out int major, out int minor);
    [DllImport("libEGL.so.1")] private static extern bool eglBindAPI(uint api);
    [DllImport("libEGL.so.1")] private static extern bool eglChooseConfig(nint display, int[] attributes, [Out] nint[] configs, int size, out int count);
    [DllImport("libEGL.so.1")] private static extern nint eglCreatePbufferSurface(nint display, nint config, int[] attributes);
    [DllImport("libEGL.so.1")] private static extern nint eglCreateContext(nint display, nint config, nint share, int[] attributes);
    [DllImport("libEGL.so.1")] private static extern bool eglMakeCurrent(nint display, nint draw, nint read, nint context);
    [DllImport("libEGL.so.1")] private static extern bool eglDestroyContext(nint display, nint context);
    [DllImport("libEGL.so.1")] private static extern bool eglDestroySurface(nint display, nint surface);
    [DllImport("libEGL.so.1")] private static extern bool eglTerminate(nint display);
    [DllImport("libEGL.so.1")] private static extern int eglGetError();
    [DllImport("libEGL.so.1")] private static extern nint eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport("libGL.so.1")] private static extern nint glGetString(uint name);
}
