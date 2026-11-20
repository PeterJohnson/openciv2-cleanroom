using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenCiv2.Automation;

internal sealed partial class LuaMachine : SafeHandleZeroOrMinusOneIsInvalid
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);

    private LuaMachine() : base(true) { }

    internal static LuaMachine Create(string script, ScriptLimits limits)
    {
        if (Abi() != 1 || Marshal.PtrToStringUTF8(Version()) != "Lua 5.5.1")
        {
            throw new InvalidOperationException("Unsupported Lua runtime/ABI.");
        }

        using var stream = typeof(LuaMachine).Assembly.GetManifestResourceStream("OpenCiv2.Automation.Prelude")!;
        using var reader = new StreamReader(stream);
        byte[] bytes = Utf8.GetBytes(reader.ReadToEnd() + "\n" + script);
        var machine = new LuaMachine();
        machine.SetHandle(
            CreateNative(bytes, (uint)bytes.Length, limits.MemoryBytes, limits.InstructionsPerTick, limits.Ticks));
        if (machine.IsInvalid)
        {
            machine.Dispose();
            throw new InvalidOperationException("Lua allocation failed.");
        }
        return machine;
    }

    internal int Resume(byte[] reply) => Step(this, reply, (uint)reply.Length);

    internal string Argument(uint index)
    {
        nint pointer = GetArgument(this, index, out uint size);
        if (pointer == 0 || size > 65536)
        {
            throw new InvalidOperationException("Invalid Lua host request.");
        }

        var data = new byte[size];
        Marshal.Copy(pointer, data, 0, (int)size);
        GC.KeepAlive(this);
        return Utf8.GetString(data);
    }

    internal string Error
    {
        get
        {
            string error = Marshal.PtrToStringUTF8(GetError(this)) ?? "lua_error";
            GC.KeepAlive(this);
            return error;
        }
    }

    internal uint MemoryBytes => Memory(this);

    protected override bool ReleaseHandle()
    {
        Destroy(handle);
        return true;
    }

    [LibraryImport("oc2_lua", EntryPoint = "oc2_lua_abi")] private static partial uint Abi();

    [LibraryImport("oc2_lua", EntryPoint = "oc2_lua_version")] private static partial nint Version();

    [LibraryImport("oc2_lua", EntryPoint = "oc2_lua_create")]
    private static partial nint CreateNative(
        byte[] script,
        uint length,
        uint memory,
        uint instructions,
        uint resumes);

    [LibraryImport("oc2_lua", EntryPoint = "oc2_lua_step")]
    private static partial int Step(
        LuaMachine machine,
        byte[] reply,
        uint length);

    [LibraryImport("oc2_lua", EntryPoint = "oc2_lua_argument")]
    private static partial nint GetArgument(
        LuaMachine machine,
        uint index,
        out uint length);

    [LibraryImport("oc2_lua", EntryPoint = "oc2_lua_error")] private static partial nint GetError(LuaMachine machine);

    [LibraryImport("oc2_lua", EntryPoint = "oc2_lua_memory")] private static partial uint Memory(LuaMachine machine);

    [LibraryImport("oc2_lua", EntryPoint = "oc2_lua_destroy")] private static partial void Destroy(nint machine);
}
