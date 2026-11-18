using System.Globalization;
using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Console;
using OpenCiv2.Contracts;

namespace OpenCiv2.Automation;

public sealed record ScriptLimits(uint MemoryBytes = 2097152, uint InstructionsPerTick = 100000, uint Ticks = 200,
    int Commands = 64, int OutputBytes = 4096)
{
    internal void Validate()
    {
        if (MemoryBytes is < 32768 or > 16777216 || InstructionsPerTick is < 1000 or > 1000000 ||
            Ticks is < 1 or > 10000 || Commands is < 1 or > 128 || OutputBytes is < 0 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(ScriptLimits));
    }
}
public enum ScriptStatus { Running, Completed, Cancelled, Failed }

/// <summary>Single mutation-lane task over an exclusive automation binding. Each Tick resumes
/// Lua once and handles at most one request; the owner pumps/retire session commands separately.
/// No managed callback runs inside Lua. Cancellation never rolls back completed effects.</summary>
public sealed class ScriptTask : IDisposable
{
    private readonly IPlayerClient client;
    private readonly ScriptLimits limits;
    private readonly LuaMachine machine;
    private readonly List<CommandId> commands = [];
    private readonly StringBuilder output = new();
    private int outputBytes, attempts;
    private byte[] reply = [0];
    private bool disposed;
    public ScriptStatus Status { get; private set; }
    public string Error { get; private set; } = "";
    public string Output => output.ToString();
    public uint MemoryBytes => disposed ? 0 : machine.MemoryBytes;
    public OwnedList<CommandId> SubmittedCommands => new(commands);
    /// <summary>Current command outcomes/effects through the original lease. Read after the
    /// owner pumps cancellation and before retiring commands; revoked leases still fail.</summary>
    public OwnedList<CommandUpdate> Results => new(commands.Select(id => client.Result(id) ??
        throw new InvalidOperationException("command_unavailable")));

    public ScriptTask(IPlayerClient client, string script, ScriptLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(client); ArgumentNullException.ThrowIfNull(script);
        this.limits = limits ?? new(); this.limits.Validate();
        if (LuaMachine.Utf8.GetByteCount(script) > 65536) throw new ArgumentException("script_budget", nameof(script));
        if (client.Identity.Kind != ControllerKind.Automation) throw new ArgumentException("An exclusive automation binding is required.", nameof(client));
        this.client = client; machine = LuaMachine.Create(script, this.limits);
    }
    public void Tick()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Status != ScriptStatus.Running) return;
        try
        {
            _ = client.Identity; // Even pure waits must stop on revoked/session-ended authority.
            client.Updates(); // Dedicated binding; authoritative results remain in the session.
            int state = machine.Resume(reply);
            if (state < 0) { Stop(ScriptStatus.Failed, machine.Error); return; }
            if (state == 0) { Stop(ScriptStatus.Completed, ""); return; }
            reply = Dispatch(machine.Argument(0), machine.Argument(1));
        }
        catch (Exception exception) when (exception is DecoderFallbackException or EncoderFallbackException)
        { Stop(ScriptStatus.Failed, "invalid_utf8"); }
        catch (Exception exception) when (exception is AccessException or ArgumentException or InvalidOperationException)
        { Stop(ScriptStatus.Failed, exception.Message); }
    }
    private byte[] Dispatch(string operation, string argument)
    {
        var w = new ReplyWriter();
        switch (operation)
        {
            case "units":
                Empty(argument); var units = client.OwnUnits().Records; w.Array(units.Count);
                foreach (var unit in units)
                {
                    w.Object(5); w.Field("slot", (int)unit.Slot.Value); w.Field("token", Decimal(unit.Token.Value));
                    w.Field("x", unit.X); w.Field("y", unit.Y); w.Field("type", (int)unit.Type.Value);
                }
                break;
            case "commands":
                Empty(argument); var descriptors = client.Commands(); w.Array(descriptors.Count);
                foreach (var descriptor in descriptors) w.Text(CommandText.Help(descriptor));
                break;
            case "command":
                if (attempts++ >= limits.Commands) throw new InvalidOperationException("command_budget");
                var update = client.Submit(CommandText.Parse(argument, client.Commands()));
                if (update.Status != CommandStatus.Rejected) commands.Add(update.Correlation.Command);
                WriteUpdate(w, update); break;
            case "result": WriteUpdate(w, Result(argument)); break;
            case "reply":
                string[] parts = argument.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int choice) ||
                    choice.ToString(CultureInfo.InvariantCulture) != parts[1]) throw new ArgumentException("invalid_reply");
                var interaction = new InteractionId(Unsigned(parts[0]));
                if (!commands.Any(id => client.Result(id)?.Interaction?.Id == interaction)) throw new ArgumentException("interaction_not_owned");
                WriteReceipt(w, client.Reply(interaction, choice)); break;
            case "cancel": WriteReceipt(w, client.Cancel(Result(argument).Correlation.Command)); break;
            case "wait": Empty(argument); w.Nil(); break;
            case "log":
                int size = LuaMachine.Utf8.GetByteCount(argument) + 1;
                if (size > limits.OutputBytes - outputBytes) throw new InvalidOperationException("output_budget");
                output.Append(argument).Append('\n'); outputBytes += size; w.Nil(); break;
            default: throw new ArgumentException("unknown_host_operation");
        }
        return w.Finish();
    }
    private static void Empty(string value) { if (value.Length != 0) throw new ArgumentException("unexpected_host_argument"); }
    private static string Decimal(ulong value) => value.ToString(CultureInfo.InvariantCulture);
    private static ulong Unsigned(string value)
    {
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) || Decimal(id) != value)
            throw new ArgumentException("invalid_identity");
        return id;
    }
    private CommandUpdate Result(string value)
    {
        var id = new CommandId(Unsigned(value));
        if (!commands.Contains(id)) throw new ArgumentException("command_not_owned");
        return client.Result(id) ?? throw new InvalidOperationException("command_unavailable");
    }
    private static void WriteReceipt(ReplyWriter w, InputReceipt receipt)
    { w.Object(2); w.Field("accepted", receipt.Accepted); w.Field("code", receipt.Code); }
    private static void WriteUpdate(ReplyWriter w, CommandUpdate update)
    {
        w.Object(6); w.Field("id", Decimal(update.Correlation.Command.Value)); w.Field("status", update.Status.ToString());
        w.Field("code", update.Code); w.Field("effects", update.Committed.Count); w.Text("interaction");
        if (update.Interaction is { } interaction)
        {
            w.Object(3); w.Field("id", Decimal(interaction.Id.Value)); w.Field("prompt", interaction.Spec.Prompt);
            w.Text("choices"); w.Array(interaction.Spec.Choices.Count); foreach (int choice in interaction.Spec.Choices) w.Integer(choice);
        }
        else w.Nil();
        w.Text("result"); w.Object(update.Result.Count);
        foreach (var item in update.Result) { w.Text(item.Name); WriteValue(w, item.Value); }
    }
    private static void WriteValue(ReplyWriter w, CommandValue value)
    {
        switch (value)
        {
            case CommandValue.Integer number: w.Integer(number.Value); break;
            case CommandValue.Boolean boolean: w.Boolean(boolean.Value); break;
            case CommandValue.Text text: w.Text(text.Value); break;
            case CommandValue.Civilization civ: w.Integer(civ.Value.Value); break;
            case CommandValue.Unit unit: w.Object(2); w.Field("slot", (int)unit.Value.Slot.Value); w.Field("token", Decimal(unit.Value.Token.Value)); break;
            case CommandValue.City city: w.Object(2); w.Field("slot", (int)city.Value.Slot.Value); w.Field("token", Decimal(city.Value.Token.Value)); break;
            default: throw new InvalidOperationException("unsupported_command_value");
        }
    }
    private void Stop(ScriptStatus status, string error)
    {
        Status = status; Error = error;
        // Cancellation uses the ordinary command protocol. The owner must continue
        // pumping accepted cancellations/required replies; already committed effects survive.
        foreach (var id in commands)
            try { if (client.Result(id) is { } result && !result.Status.Terminal()) client.Cancel(id); }
            catch (AccessException) { }
    }
    public void Cancel() { if (!disposed && Status == ScriptStatus.Running) Stop(ScriptStatus.Cancelled, ""); }
    public void Dispose()
    {
        if (disposed) return;
        Cancel(); machine.Dispose(); disposed = true;
    }
}
