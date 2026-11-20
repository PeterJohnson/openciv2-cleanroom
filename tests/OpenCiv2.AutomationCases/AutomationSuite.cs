using OpenCiv2.Automation;
using OpenCiv2.Base;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.Session;

namespace OpenCiv2.AutomationCases;

public static class AutomationSuite
{
    private static readonly (string Name, Action Run)[] cases = [
        ("command-equivalence", CommandEquivalence), ("observer-and-descriptor", ObserverAndDescriptor),
        ("instruction-budget", InstructionBudget), ("memory-budget", MemoryBudget),
        ("host-output-command-budgets", HostBudgets), ("script-errors", ScriptErrors),
        ("cancel-before-and-after-effects", Cancellation), ("disposal-and-gc", DisposalAndGc),
        ("revoked-authority", RevokedAuthority), ("task-command-ownership", TaskCommandOwnership),
        ("stale-target", StaleTarget), ("sandbox-and-limits", SandboxAndLimits), ("error-preserves-effects", ErrorPreservesEffects)
    ];
    public static IReadOnlyList<string> Names => cases.Select(c => c.Name).ToArray();
    public static void Run(string name) => cases.Single(c => c.Name == name).Run();
    private static void Check(bool value, string message = "Assertion failed") { if (!value) throw new InvalidOperationException(message); }
    private sealed class Fixture : IDisposable
    {
        internal readonly SessionRuntime Runtime = new(new(), new(701));
        internal readonly IPlayerClient Client;
        internal readonly ParticipantId Participant;
        internal Fixture()
        {
            Runtime.RegisterCommand(LocalUnitCommands.StackBottom()); Runtime.BeginClientSession(new(17, 23));
            Runtime.World.Map.Allocate(new(8, 6)); Runtime.World.Entities.Units.SetEnd(4); Runtime.World.Globals.UnitEnd = 4;
            for (int i = 0; i < 4; ++i) Runtime.World.Entities.Units.Storage[i] = new()
            { Token = new((uint)i + 100), Owner = new((byte)(i == 3 ? 2 : 1)), Previous = (((short)(i == 3 ? -1 : i - 1)) < 0 ? (UnitSlot?)null : new UnitSlot(unchecked((ushort)((short)(i == 3 ? -1 : i - 1))))), Next = (((short)(i >= 2 ? -1 : i + 1)) < 0 ? (UnitSlot?)null : new UnitSlot(unchecked((ushort)((short)(i >= 2 ? -1 : i + 1))))), Position = new(2, 2) };
            Participant = Runtime.AddParticipant(new(1)); Client = Runtime.Bind(Participant, ControllerKind.Automation);
        }
        internal ScriptTask Task(string code, ScriptLimits? limits = null) => new(Client, code, limits);
        internal void Pump(ScriptTask task, bool gc = false)
        {
            for (int i = 0; i < 1000 && task.Status == ScriptStatus.Running; ++i)
            {
                Runtime.AdvanceOne(); if (gc) { GC.Collect(); GC.WaitForPendingFinalizers(); }
                task.Tick();
            }
            Check(task.Status != ScriptStatus.Running, "Task did not stop within test tick bound");
            while (Runtime.AdvanceOne()) { }
        }
        internal string Stack() => string.Join(";", Runtime.World.Entities.Units.Storage[..4].ToArray().Select(u => FormattableString.Invariant($"{(u.Previous?.Value ?? -1)},{(u.Next?.Value ?? -1)},{u.Token.Value},{u.Owner.Value}")));
        public void Dispose() => Runtime.Dispose();
    }
    private const string Command = "unit.stack-bottom unit=0:100";
    private const string Complete = """
        local x=game.command('unit.stack-bottom unit=0:100')
        while x.status=='Pending' do game.wait(); x=game.result(x.id) end
        assert(x.status=='AwaitingInteraction')
        assert(game.reply(x.interaction.id,1).accepted)
        repeat game.wait(); x=game.result(x.id) until x.status=='Completed'
        assert(x.effects==1 and x.result.changed==true and x.result.unit.token=='100')
        game.log('completed')
        """;
    public static ScriptOutcome Outcome()
    {
        using var f = new Fixture(); using var task = f.Task(Complete); f.Pump(task);
        Check(task.Status == ScriptStatus.Completed, task.Error); var result = task.Results.Single();
        return new(f.Stack(), result.Committed.Count, ((CommandValue.Boolean)CommandDescriptor.Value(result.Result, "changed")).Value, task.Output);
    }
    private static void CommandEquivalence()
    {
        using var direct = new Fixture(); using var console = new Fixture(); using var lua = new Fixture();
        var game = lua.Runtime.GameplayRandom.Capture(); var presentation = lua.Runtime.PresentationRandom.Capture();
        foreach (var f in new[] { direct, console })
        {
            var request = f == direct ? new CommandRequest(LocalUnitCommands.StackBottomName, new([new("unit", new CommandValue.Unit(new(new(0), new(100))))])) : CommandText.Parse(Command, f.Client.Commands());
            var id = f.Client.Submit(request).Correlation.Command; Check(f.Runtime.AdvanceOne());
            Check(f.Client.Reply(f.Client.Result(id)!.Interaction!.Id, 1).Accepted); Check(f.Runtime.AdvanceOne());
            Check(f.Client.Result(id)!.Status == CommandStatus.Completed && f.Client.Result(id)!.Committed.Count == 1);
        }
        using var task = lua.Task(Complete); lua.Pump(task);
        Check(task.Status == ScriptStatus.Completed, task.Error); Check(task.Output == "completed\n");
        Check(lua.Stack() == direct.Stack() && lua.Stack() == console.Stack());
        Check(game == lua.Runtime.GameplayRandom.Capture() && presentation == lua.Runtime.PresentationRandom.Capture());
        Check(lua.Client.Result(task.SubmittedCommands.Single())!.Committed.Count == 1);
    }
    private static void ObserverAndDescriptor()
    {
        using var f = new Fixture(); string before = f.Stack();
        using var task = f.Task("local u=game.units(); assert(#u==3 and u[1].token=='100' and u[3].slot==2); assert(#game.commands()==1); local x=game.command('unit.stack-bottom unit=3:103'); assert(x.status=='Rejected' and x.effects==0)");
        f.Pump(task); Check(task.Status == ScriptStatus.Completed, task.Error); Check(f.Stack() == before && task.SubmittedCommands.Count == 0);
    }
    private static void Fails(string code, string expected, ScriptLimits? limits = null)
    {
        using var f = new Fixture(); string before = f.Stack(); using var task = f.Task(code, limits);
        f.Pump(task); Check(task.Status == ScriptStatus.Failed && task.Error.Contains(expected, StringComparison.Ordinal), task.Error);
        Check(f.Stack() == before && !f.Runtime.HasPendingCommands); Check(task.MemoryBytes <= (limits ?? new()).MemoryBytes);
    }
    private static void InstructionBudget()
    {
        Fails("while true do end", "instruction_budget");
        Fails("pcall(function() while true do end end); game.log('escaped')", "instruction_budget");
    }
    private static void MemoryBudget()
    {
        Fails("local x=string.rep('0123456789abcdef', 65536)", "memory", new(MemoryBytes: 65536));
        Fails("local x={} while true do x[#x+1]=string.rep('a',1000) end", "memory", new(MemoryBytes: 65536));
        // The typed response fits the transport but its decoded Lua tables exceed
        // the VM allocator cap. This exercises protected reply allocation on both ABIs.
        using var f = new Fixture(); f.Runtime.World.Entities.Units.SetEnd(512); f.Runtime.World.Globals.UnitEnd = 512;
        for (int i = 4; i < 512; ++i) f.Runtime.World.Entities.Units.Storage[i] = new()
        { Token = new((uint)i + 100), Owner = new(1), Previous = (UnitSlot?)null, Next = (UnitSlot?)null, Position = new(2, 2) };
        using var task = f.Task("local units=game.units()", new(MemoryBytes: 65536)); f.Pump(task);
        Check(task.Status == ScriptStatus.Failed && task.Error.Contains("memory", StringComparison.Ordinal), task.Error);
        Check(task.MemoryBytes <= 65536 && !f.Runtime.HasPendingCommands);

    }
    private static void HostBudgets()
    {
        Fails("string.rep('', 9223372036854775807)", "native_work_budget");
        Fails("table.move({},1,9223372036854775807,1)", "nil value");
        Fails("while true do game.wait() end", "host_call_budget", new(Ticks: 3));
        Fails("game.log('12345')", "output_budget", new(OutputBytes: 4));
        Fails("game.command('unit.stack-bottom unit=0:100'); game.command('unit.stack-bottom unit=1:101')", "command_budget", new(Commands: 1));
    }
    private static void ScriptErrors()
    {
        Fails("this is not lua", "syntax"); Fails("error({})", "lua_error"); Fails("error(42)", "lua_error");
        Fails("game.command('not.registered')", "unavailable");
        Fails("game.log(string.char(255))", "invalid_utf8");
    }
    private static void Cancellation()
    {
        using var f = new Fixture(); string before = f.Stack();
        using (var task = f.Task("game.command('unit.stack-bottom unit=0:100'); while true do game.wait() end"))
        {
            task.Tick(); Check(task.SubmittedCommands.Count == 1); f.Runtime.AdvanceOne(); task.Cancel(); f.Pump(task);
            Check(task.Status == ScriptStatus.Cancelled && f.Stack() == before);
            Check(f.Client.Result(task.SubmittedCommands[0])!.Status == CommandStatus.Cancelled);
        }
        using (var task = f.Task(Complete + "\nwhile true do game.wait() end"))
        {
            for (int i = 0; i < 30 && task.Output.Length == 0; ++i) { f.Runtime.AdvanceOne(); task.Tick(); }
            Check(task.Output == "completed\n"); string committed = f.Stack(); task.Cancel(); f.Pump(task);
            Check(task.Status == ScriptStatus.Cancelled && committed != before && f.Stack() == committed);
            Check(f.Client.Result(task.SubmittedCommands[0])!.Committed.Count == 1);
        }
    }
    private static void DisposalAndGc()
    {
        using var f = new Fixture(); using (var task = f.Task(Complete)) { f.Pump(task, true); Check(task.Status == ScriptStatus.Completed, task.Error); }
        var pending = f.Task("game.command('unit.stack-bottom unit=1:101'); game.wait()"); pending.Tick(); var id = pending.SubmittedCommands[0];
        pending.Dispose(); pending.Dispose(); while (f.Runtime.AdvanceOne()) { }
        Check(f.Client.Result(id)!.Status == CommandStatus.Cancelled && pending.MemoryBytes == 0);
        try { pending.Tick(); throw new Exception("Disposed task tick admitted"); } catch (ObjectDisposedException) { }
    }
    private static void RevokedAuthority()
    {
        using var f = new Fixture(); using var task = f.Task("while true do game.wait() end"); task.Tick();
        f.Runtime.Revoke(f.Client.Identity.Controller); task.Tick(); Check(task.Status == ScriptStatus.Failed && task.Error.Contains("revoked", StringComparison.Ordinal));
    }
    private static void TaskCommandOwnership()
    {
        using var f = new Fixture(); var other = f.Runtime.Bind(f.Participant, ControllerKind.Automation);
        var command = other.Submit(CommandText.Parse(Command, other.Commands())).Correlation.Command; f.Runtime.AdvanceOne();
        using var task = f.Task($"game.cancel('{command.Value}')"); f.Pump(task);
        Check(task.Status == ScriptStatus.Failed && task.Error == "command_not_owned");
        Check(other.Result(command)!.Status == CommandStatus.AwaitingInteraction);
    }
    private static void StaleTarget()
    {
        using var f = new Fixture(); using var task = f.Task("local x=game.command('unit.stack-bottom unit=0:100'); game.wait(); x=game.result(x.id); assert(x.status=='Failed' and x.effects==0)");
        task.Tick(); f.Runtime.World.Entities.Units.Storage[0].Token = new(999); string before = f.Stack(); f.Pump(task);
        Check(task.Status == ScriptStatus.Completed, task.Error); Check(f.Stack() == before);
    }
    private static void ErrorPreservesEffects()
    {
        using var f = new Fixture(); using var task = f.Task(Complete + "\nerror('after_effect')"); f.Pump(task);
        Check(task.Status == ScriptStatus.Failed && task.Error.Contains("after_effect", StringComparison.Ordinal));
        Check(task.Results.Single().Status == CommandStatus.Completed && task.Results.Single().Committed.Count == 1);
        Check((f.Runtime.World.Entities.Units.Storage[0].Next?.Value ?? -1) == -1);
    }
    private static void SandboxAndLimits()
    {
        using var f = new Fixture(); using var task = f.Task("assert(io==nil and os==nil and package==nil and debug==nil and load==nil and coroutine==nil); assert(math.random==nil and setmetatable==nil and _oc2_call==nil)");
        f.Pump(task); Check(task.Status == ScriptStatus.Completed, task.Error);
        try { using var invalid = f.Task("return", new(Ticks: 0)); throw new Exception("Invalid limits admitted"); } catch (ArgumentOutOfRangeException) { }
        try { using var invalid = f.Task(new string('a', 65537)); throw new Exception("Oversize script admitted"); } catch (ArgumentException) { }
    }
}
