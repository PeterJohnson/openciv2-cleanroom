using System.Text;
using Lua;
using Lua.Platforms;
using Lua.Runtime;
using Lua.Standard;

namespace OpenCiv2.Automation;

// Lua-CSharp coroutines run inline until the game binding yields to the owner.
// Only the owner tick resumes execution; session dispatch happens outside Lua.
internal sealed class LuaMachine : IDisposable
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    private LuaState? state = LuaState.Create(LuaPlatform.DefaultByteStrings);
    private readonly ScriptLimits limits;
    private readonly LuaFunction hook;
    private readonly string source;
    private LuaState? coroutine;
    private readonly LuaStack exchange = new();
    private readonly CancellationTokenSource cancellation = new();
    private string operation = "", argument = "";
    private uint ticks, remaining;
    internal string Error { get; private set; } = "";

    internal LuaMachine(string script, ScriptLimits limits)
    {
        this.limits = limits;
        using var stream = typeof(LuaMachine).Assembly.GetManifestResourceStream("OpenCiv2.Automation.Prelude")!;
        using var reader = new StreamReader(stream);
        source = reader.ReadToEnd() + "\n" + script;
        state.OpenBasicLibrary();
        state.OpenTableLibrary();
        state.OpenStringLibrary();
        state.OpenMathLibrary();
        state.ModuleLoader = null;
        foreach (string name in new[] { "dofile", "loadfile", "load", "collectgarbage", "print", "getmetatable",
            "setmetatable", "xpcall", MathematicsLibrary.RandomInstanceKey })
        {
            state.Environment[name] = LuaValue.Nil;
        }
        var strings = state.Environment["string"].Read<LuaTable>();
        foreach (string name in new[] { "find", "match", "gmatch", "gsub", "dump" })
        {
            strings[name] = LuaValue.Nil;
        }
        strings["rep"] = new LuaFunction("string.rep", (context, ct) =>
        {
            double count = context.GetArgument<double>(1);
            if (!double.IsFinite(count) || count > 65536)
            {
                throw new LuaRuntimeException(context.State, "host_work_budget");
            }
            return StringLibrary.Instance.Rep(context, ct);
        });
        var tables = state.Environment["table"].Read<LuaTable>();
        tables["sort"] = tables["move"] = LuaValue.Nil;
        var math = state.Environment["math"].Read<LuaTable>();
        math["random"] = math["randomseed"] = LuaValue.Nil;
        state.Environment["_oc2_call"] = new LuaFunction("game.call", Call);
        hook = new LuaFunction("instruction_budget", (_, _) =>
        {
            if (remaining <= 1000)
            {
                Error = "instruction_budget";
                // Cancellation is propagated through Lua-CSharp's pcall, unlike a Lua error.
                throw new OperationCanceledException(Error);
            }
            remaining -= 1000;
            return new(0);
        });
        state.SetHook(hook, "", 1000);
    }

    private ValueTask<int> Call(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        string op = context.GetArgument<string>(0), arg = context.GetArgument<string>(1);
        if (op.Length > 65536 || arg.Length > 65536 - op.Length)
        {
            Error = "host_request_budget";
            throw new OperationCanceledException(Error);
        }
        try
        {
            operation = Utf8.GetString(LuaByteString.ToBytes(op));
            argument = Utf8.GetString(LuaByteString.ToBytes(arg));
        }
        catch (DecoderFallbackException)
        {
            Error = "invalid_utf8";
            throw new OperationCanceledException(Error);
        }
        return context.State.YieldAsync(context, cancellationToken);
    }

    internal int Resume(LuaValue reply)
    {
        var current = state ?? throw new ObjectDisposedException(nameof(LuaMachine));
        if (++ticks > limits.Ticks)
        {
            Error = "host_call_budget";
            return -1;
        }
        remaining = limits.InstructionsPerTick;
        // Restart the hook's counter for this tick, including ticks inside pcall.
        try
        {
            if (coroutine is null)
            {
                coroutine = current.CreateCoroutine(current.Load(source, "automation"));
            }
            else
            {
                exchange.Push(reply);
            }
            coroutine.SetHook(hook, "", 1000);
            var resume = coroutine.ResumeAsync(exchange, cancellation.Token);
            // All exposed bindings are synchronous or coroutine yields. Never block
            // a browser/UI thread waiting for unrelated asynchronous work.
            if (!resume.IsCompleted)
            {
                throw new InvalidOperationException("unexpected_async_operation");
            }

            resume.GetAwaiter().GetResult();
            exchange.Clear();
            return coroutine.GetStatus() == LuaThreadStatus.Dead ? 0 : 1;
        }
        catch (Exception exception) when (exception is LuaParseException or LuaCompileException)
        {
            Error = "syntax: " + LuaByteString.DecodeUtf8(exception.Message);
        }
        catch (LuaRuntimeException exception)
        {
            Error = exception.InnerException is null && exception.ErrorObject.Type != LuaValueType.String
                ? "lua_error" : LuaByteString.DecodeUtf8(exception.Message);
        }
        catch (OperationCanceledException)
        {
            if (Error.Length == 0)
            {
                Error = "cancelled";
            }
        }
        return -1;
    }

    internal string Operation => operation;
    internal string Argument => argument;

    public void Dispose()
    {
        if (state is null)
        {
            return;
        }

        cancellation.Cancel(); // Unwind a suspended yield before disposing its call stack.
        coroutine?.Dispose();
        coroutine = null;
        state.Dispose();
        state = null;
        exchange.Clear();
        cancellation.Dispose();
        operation = argument = "";
    }
}
