using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenCiv2.Automation;

/// <summary>Length-prefixed frames on the private, redirected pipes of one desktop Lua task.</summary>
internal static class LuaWorker
{
    internal const int HeapLimitBytes = 128 * 1024 * 1024;
    internal const int MemoryExitCode = 42;
    private const int MaximumFrame = 512 * 1024;
    private const int Protocol = 1;
    private static readonly byte[] MemoryFailure = Frame(Result(new(ScriptMachineState.Failed, Error: "memory_budget")));

    internal static JsonObject Start(string script, ScriptLimits limits) => new()
    {
        ["protocol"] = Protocol,
        ["script"] = script,
        ["instructions"] = limits.InstructionsPerTick,
        ["ticks"] = limits.Ticks,
        ["commands"] = limits.Commands,
        ["outputBytes"] = limits.OutputBytes
    };

    private static JsonObject Result(ScriptMachineResult result) => new()
    {
        ["state"] = result.State.ToString(),
        ["operation"] = result.Operation,
        ["argument"] = result.Argument,
        ["error"] = result.Error
    };

    internal static ScriptMachineResult ReadResult(JsonObject response)
    {
        var state = response["state"]?.GetValue<string>() switch
        {
            nameof(ScriptMachineState.Request) => ScriptMachineState.Request,
            nameof(ScriptMachineState.Completed) => ScriptMachineState.Completed,
            nameof(ScriptMachineState.Failed) => ScriptMachineState.Failed,
            _ => throw new InvalidDataException("Unknown script worker state.")
        };
        string ReadText(string name) => response[name]?.GetValue<string>()
            ?? throw new InvalidDataException($"Missing script worker field {name}.");
        string operation = ReadText("operation"), argument = ReadText("argument"), error = ReadText("error");
        if (Encoding.UTF8.GetByteCount(operation) + Encoding.UTF8.GetByteCount(argument) > 65536)
        {
            throw new InvalidDataException("Script worker request exceeds limit.");
        }
        return new(state, operation, argument, error);
    }

    internal static async Task SendAsync(Stream stream, JsonObject value)
    {
        await stream.WriteAsync(Frame(value)).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static byte[] Frame(JsonObject value)
    {
        byte[] body = Encoding.UTF8.GetBytes(value.ToJsonString());
        if (body.Length > MaximumFrame)
        {
            throw new InvalidDataException("Script worker frame exceeds limit.");
        }
        byte[] frame = new byte[body.Length + sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length);
        body.CopyTo(frame, sizeof(int));
        return frame;
    }

    internal static async Task<JsonObject> ReceiveAsync(Stream stream)
    {
        var header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaximumFrame)
        {
            throw new InvalidDataException("Invalid script worker frame length.");
        }
        var body = new byte[length];
        await stream.ReadExactlyAsync(body).ConfigureAwait(false);
        return JsonNode.Parse(body, documentOptions: new() { MaxDepth = 24 }) as JsonObject
            ?? throw new InvalidDataException("Invalid script worker frame.");
    }

    internal static int Run(Stream input, Stream output)
    {
        try
        {
            if (GC.GetGCMemoryInfo().TotalAvailableMemoryBytes is <= 0 or > HeapLimitBytes)
            {
                SendAsync(output, Result(new(ScriptMachineState.Failed, Error: "script_worker_heap_limit_unavailable")))
                    .GetAwaiter().GetResult();
                return 2;
            }
            var start = ReceiveAsync(input).GetAwaiter().GetResult();
            if (start["protocol"]?.GetValue<int>() != Protocol)
            {
                throw new InvalidDataException("Unknown script worker protocol.");
            }
            T Required<T>(string name) => start[name] is { } field ? field.GetValue<T>()
                : throw new InvalidDataException($"Missing script worker field {name}.");
            ScriptLimits limits = new(Required<uint>("instructions"), Required<uint>("ticks"),
                Required<int>("commands"), Required<int>("outputBytes"));
            using var machine = new LuaMachine(Required<string>("script"), limits);
            JsonNode? reply = null;
            while (true)
            {
                var result = machine.Resume(reply);
                SendAsync(output, Result(result)).GetAwaiter().GetResult();
                if (result.State != ScriptMachineState.Request)
                {
                    return 0;
                }
                var response = ReceiveAsync(input).GetAwaiter().GetResult();
                if (!response.TryGetPropertyValue("reply", out reply))
                {
                    throw new InvalidDataException("Missing script worker reply.");
                }
            }
        }
        catch (OutOfMemoryException)
        {
            try
            {
                output.Write(MemoryFailure);
                output.Flush();
            }
            catch (Exception error) when (error is IOException or OutOfMemoryException) { }
            return MemoryExitCode;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException
            or ArgumentException or InvalidOperationException)
        {
            return 2;
        }
    }
}
