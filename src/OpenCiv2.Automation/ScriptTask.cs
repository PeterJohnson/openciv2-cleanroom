using System.Globalization;
using System.Text;
using Lua;
using OpenCiv2.Base;
using OpenCiv2.Console;
using OpenCiv2.Contracts;

namespace OpenCiv2.Automation;

public sealed record ScriptLimits(uint InstructionsPerTick = 100000, uint Ticks = 200,
    int Commands = 64, int OutputBytes = 4096)
{
    internal void Validate()
    {
        if (InstructionsPerTick is < 1000 or > 1000000 ||
            Ticks is < 1 or > 10000 || Commands is < 1 or > 128 || OutputBytes is < 0 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(ScriptLimits));
        }
    }
}
public enum ScriptStatus { Running, Completed, Cancelled, Failed }

/// <summary>Single mutation-lane task over an exclusive automation binding. Each Tick resumes
/// Lua once and handles at most one request; the owner pumps/retire session commands separately.
/// Host bindings suspend Lua before accessing the session. Cancellation never rolls back completed effects.</summary>
public sealed class ScriptTask : IDisposable
{
    private readonly IPlayerClient client;
    private readonly ScriptLimits limits;
    private readonly LuaMachine machine;
    private readonly List<CommandId> commands = [];
    private readonly StringBuilder output = new();
    private int outputBytes, attempts;
    private LuaValue reply;
    private bool disposed;
    public ScriptStatus Status { get; private set; }
    public string Error { get; private set; } = "";
    public string Output => output.ToString();
    public OwnedList<CommandId> SubmittedCommands => new(commands);

    /// <summary>Current command outcomes/effects through the original lease. Read after the
    /// owner pumps cancellation and before retiring commands; revoked leases still fail.</summary>
    public OwnedList<CommandUpdate> Results => new(commands.Select(id => client.Result(id) ??
        throw new InvalidOperationException("command_unavailable")));

    public ScriptTask(IPlayerClient client, string script, ScriptLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(script);
        this.limits = limits ?? new();
        this.limits.Validate();
        if (LuaMachine.Utf8.GetByteCount(script) > 65536)
        {
            throw new ArgumentException("script_budget", nameof(script));
        }

        if (client.Identity.Kind != ControllerKind.Automation)
        {
            throw new ArgumentException("An exclusive automation binding is required.", nameof(client));
        }

        this.client = client;
        machine = new LuaMachine(script, this.limits);
    }

    public void Tick()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Status != ScriptStatus.Running)
        {
            return;
        }

        try
        {
            _ = client.Identity; // Even pure waits must stop on revoked/session-ended authority.
            client.Updates(); // Dedicated binding; authoritative results remain in the session.
            int state = machine.Resume(reply);
            if (state < 0)
            {
                Stop(ScriptStatus.Failed, machine.Error);
                return;
            }
            if (state == 0)
            {
                Stop(ScriptStatus.Completed, "");
                return;
            }
            reply = Dispatch(machine.Operation, machine.Argument);
        }
        catch (Exception exception) when (exception is DecoderFallbackException or EncoderFallbackException)
        {
            Stop(ScriptStatus.Failed, "invalid_utf8");
        }
        catch (Exception exception) when (exception is AccessException or ArgumentException
            or InvalidOperationException)
        {
            Stop(ScriptStatus.Failed, exception.Message);
        }
    }

    private LuaValue Dispatch(string operation, string argument)
    {
        var w = new ReplyBuilder();
        switch (operation)
        {
            case "units":
                Empty(argument);
                return w.Array(client.OwnUnits().Records.Select(unit => w.Object(
                    ("slot", w.Integer(unit.Slot.Value)), ("token", w.Text(Decimal(unit.Token.Value))),
                    ("x", w.Integer(unit.Position.X)), ("y", w.Integer(unit.Position.Y)),
                    ("type", w.Integer(unit.Type.Value)))));
            case "commands":
                Empty(argument);
                return w.Array(client.Commands().Select(descriptor => w.Text(CommandText.Help(descriptor))));
            case "command":
                if (attempts++ >= limits.Commands)
                {
                    throw new InvalidOperationException("command_budget");
                }

                var update = client.Submit(CommandText.Parse(argument, client.Commands()));
                if (update.Status != CommandStatus.Rejected)
                {
                    commands.Add(update.Correlation.Command);
                }

                return WriteUpdate(w, update);
            case "result":
                return WriteUpdate(w, Result(argument));
            case "reply":
                string[] parts = argument.Split(':');
                if (parts.Length != 2
                    || !int.TryParse(
                        parts[1],
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out int choice)
                    ||
                    choice.ToString(CultureInfo.InvariantCulture) != parts[1])
                {
                    throw new ArgumentException("invalid_reply");
                }

                var interaction = new InteractionId(Unsigned(parts[0]));
                if (!commands.Any(id => client.Result(id)?.Interaction?.Id == interaction))
                {
                    throw new ArgumentException("interaction_not_owned");
                }

                return WriteReceipt(w, client.Reply(interaction, choice));
            case "cancel":
                return WriteReceipt(w, client.Cancel(Result(argument).Correlation.Command));
            case "wait":
                Empty(argument);
                return w.Nil();
            case "log":
                int size = LuaMachine.Utf8.GetByteCount(argument) + 1;
                if (size > limits.OutputBytes - outputBytes)
                {
                    throw new InvalidOperationException("output_budget");
                }

                output.Append(argument).Append('\n');
                outputBytes += size;
                return w.Nil();
            default:
                throw new ArgumentException("unknown_host_operation");
        }
    }

    private static void Empty(string value)
    {
        if (value.Length != 0)
        {
            throw new ArgumentException("unexpected_host_argument");
        }
    }

    private static string Decimal(ulong value) => value.ToString(CultureInfo.InvariantCulture);

    private static ulong Unsigned(string value)
    {
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id)
            || Decimal(id) != value)
        {
            throw new ArgumentException("invalid_identity");
        }

        return id;
    }

    private CommandUpdate Result(string value)
    {
        var id = new CommandId(Unsigned(value));
        if (!commands.Contains(id))
        {
            throw new ArgumentException("command_not_owned");
        }

        return client.Result(id) ?? throw new InvalidOperationException("command_unavailable");
    }

    private static LuaValue WriteReceipt(ReplyBuilder w, InputReceipt receipt) => w.Object(
        ("accepted", w.Boolean(receipt.Accepted)), ("code", w.Text(receipt.Code)));

    private static LuaValue WriteUpdate(ReplyBuilder w, CommandUpdate update) => w.Object(
        ("id", w.Text(Decimal(update.Correlation.Command.Value))),
        ("status", w.Text(update.Status.ToString())), ("code", w.Text(update.Code)),
        ("effects", w.Integer(update.Committed.Count)),
        ("interaction", update.Interaction is { } interaction
            ? w.Object(("id", w.Text(Decimal(interaction.Id.Value))),
                ("prompt", w.Text(interaction.Spec.Prompt)),
                ("choices", w.Array(interaction.Spec.Choices.Select(w.Integer))))
            : w.Nil()),
        ("result", w.Object(update.Result.Select(item => (item.Name, WriteValue(w, item.Value))).ToArray())));

    private static LuaValue WriteValue(ReplyBuilder w, CommandValue value) => value switch
    {
        CommandValue.Integer number => w.Integer(number.Value),
        CommandValue.Boolean boolean => w.Boolean(boolean.Value),
        CommandValue.Text text => w.Text(text.Value),
        CommandValue.Civilization civ => w.Integer(civ.Value.Value),
        CommandValue.Unit unit => w.Object(("slot", w.Integer(unit.Value.Slot.Value)),
            ("token", w.Text(Decimal(unit.Value.Token.Value)))),
        CommandValue.City city => w.Object(("slot", w.Integer(city.Value.Slot.Value)),
            ("token", w.Text(Decimal(city.Value.Token.Value)))),
        _ => throw new InvalidOperationException("unsupported_command_value")
    };

    private void Stop(ScriptStatus status, string error)
    {
        Status = status;
        Error = error;
        machine.Dispose();
        reply = LuaValue.Nil;
        // Cancellation uses the ordinary command protocol. The owner must continue
        // pumping accepted cancellations/required replies; already committed effects survive.
        foreach (var id in commands)
        {
            try
            {
                if (client.Result(id) is { } result && !result.Status.Terminal())
                {
                    client.Cancel(id);
                }
            }
            catch (AccessException) { }
        }
    }

    public void Cancel()
    {
        if (!disposed && Status == ScriptStatus.Running)
        {
            Stop(ScriptStatus.Cancelled, "");
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Cancel();
        machine.Dispose();
        disposed = true;
    }
}
