using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]

namespace OpenCiv2.Export.Sqlite;

// General-purpose SQLite C API only; no gameplay interop. The optional native
// artifact is built from the archive pinned in dependencies.json.
internal static unsafe partial class NativeSqlite
{
    private const string Library = "oc2_sqlite";
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_libversion")] internal static partial nint Version();
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_compileoption_used", StringMarshalling = StringMarshalling.Utf8)] internal static partial int CompileOption(string name);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_open_v2", StringMarshalling = StringMarshalling.Utf8)] internal static partial int Open(string name, out nint database, int flags, nint vfs);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_close_v2")] internal static partial int Close(nint database);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_errmsg")] internal static partial nint Error(DatabaseHandle database);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_exec", StringMarshalling = StringMarshalling.Utf8)] internal static partial int Execute(DatabaseHandle database, string sql, nint callback, nint context, nint error);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_prepare_v2", StringMarshalling = StringMarshalling.Utf8)] internal static partial int Prepare(DatabaseHandle database, string sql, int length, out nint statement, nint tail);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_finalize")] internal static partial int Finalize(nint statement);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_bind_text64", StringMarshalling = StringMarshalling.Utf8)] internal static partial int Text(StatementHandle statement, int index, string text, ulong length, nint destructor, byte encoding);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_bind_blob64")] internal static partial int Blob(StatementHandle statement, int index, byte* data, ulong length, nint destructor);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_bind_int64")] internal static partial int Integer(StatementHandle statement, int index, long value);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_step")] internal static partial int Step(StatementHandle statement);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_reset")] internal static partial int Reset(StatementHandle statement);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_clear_bindings")] internal static partial int Clear(StatementHandle statement);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_serialize", StringMarshalling = StringMarshalling.Utf8)] internal static partial nint Serialize(DatabaseHandle database, string schema, out long size, uint flags);
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport(Library, EntryPoint = "sqlite3_free")] internal static partial void Free(nint bytes);
}
internal sealed class DatabaseHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal DatabaseHandle(nint value) : base(true) => SetHandle(value);
    protected override bool ReleaseHandle() => NativeSqlite.Close(handle) == 0;
}
internal sealed class StatementHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal StatementHandle(nint value) : base(true) => SetHandle(value);
    protected override bool ReleaseHandle() { NativeSqlite.Finalize(handle); return true; }
}
