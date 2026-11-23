using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenCiv2.Automation;

/// <summary>One heap-limited interpreter process. Its I/O never waits on the presentation thread.</summary>
internal sealed class IsolatedLuaMachine : IScriptMachine
{
    private static readonly TimeSpan ReplyDeadline = TimeSpan.FromSeconds(5);
    private readonly Process process;
    private readonly Stream input, output;
    private JsonObject? start;
    private Task<ScriptMachineResult>? pending;
    private int stopped;

    public IsolatedLuaMachine(string script, ScriptLimits limits)
    {
        start = LuaWorker.Start(script, limits);
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("script_worker_missing_host");
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        if (Path.GetFileNameWithoutExtension(executable) == "dotnet")
        {
            info.ArgumentList.Add(typeof(IsolatedLuaMachine).Assembly.Location);
        }
        info.ArgumentList.Add("--lua-worker");
        foreach (string name in info.Environment.Keys.Where(name =>
            name.StartsWith("DOTNET_GCHeapHardLimit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("COMPlus_GCHeapHardLimit", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            info.Environment.Remove(name);
        }
        info.Environment["DOTNET_GCHeapHardLimit"] = LuaWorker.HeapLimitBytes.ToString("X", CultureInfo.InvariantCulture);
        process = Process.Start(info) ?? throw new InvalidOperationException("script_worker_start_failed");
        input = process.StandardInput.BaseStream;
        output = process.StandardOutput.BaseStream;
    }

    public ScriptMachineResult Resume(JsonNode? reply)
    {
        if (pending is null)
        {
            var message = start ?? new JsonObject { ["reply"] = reply };
            start = null;
            pending = Task.Run(() => ExchangeWithDeadline(message));
        }
        if (!pending.IsCompleted)
        {
            return new(ScriptMachineState.Waiting);
        }
        var result = pending.GetAwaiter().GetResult();
        pending = null;
        return result;
    }

    private async Task<ScriptMachineResult> ExchangeWithDeadline(JsonObject message)
    {
        try
        {
            return await Exchange(message).WaitAsync(ReplyDeadline).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            StopWorker();
            return new(ScriptMachineState.Failed, Error: "script_worker_timeout");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException
            or ArgumentException or InvalidOperationException)
        {
            return new(ScriptMachineState.Failed, Error: "script_worker_protocol");
        }
    }

    private async Task<ScriptMachineResult> Exchange(JsonObject message)
    {
        try
        {
            await LuaWorker.SendAsync(input, message).ConfigureAwait(false);
            return LuaWorker.ReadResult(await LuaWorker.ReceiveAsync(output).ConfigureAwait(false));
        }
        catch (EndOfStreamException)
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            return new(ScriptMachineState.Failed, Error: process.ExitCode == LuaWorker.MemoryExitCode
                ? "memory_budget" : "script_worker_exited");
        }
    }

    private void StopWorker()
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0)
        {
            return;
        }
        try
        {
            process.Kill();
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) when (process.HasExited) { }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose() => StopWorker();
}
