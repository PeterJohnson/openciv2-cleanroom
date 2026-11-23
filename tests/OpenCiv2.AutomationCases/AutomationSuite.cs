using OpenCiv2.Automation;
using OpenCiv2.Base;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.AutomationCases;

public static class AutomationSuite
{
    private static readonly (string Name, Action Run)[] cases = [
        ("command-equivalence", CommandEquivalence), ("observer-and-descriptor", ObserverAndDescriptor),
        ("instruction-budget", InstructionBudget), ("lua-csharp-runtime", ManagedRuntime),
        ("host-output-command-budgets", HostBudgets), ("script-errors", ScriptErrors),
        ("cancel-before-and-after-effects", Cancellation), ("disposal-and-gc", DisposalAndGc),
        ("revoked-authority", RevokedAuthority), ("task-command-ownership", TaskCommandOwnership),
        ("stale-target", StaleTarget), ("sandbox-and-limits", SandboxAndLimits), ("error-preserves-effects", ErrorPreservesEffects),
        ("city-production-policy-replay", CityProductionPolicyReplay)
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
        Check(task.CommandAttempts.Single().Status == CommandStatus.Rejected);
    }

    private static void CityProductionPolicyReplay()
    {
        using var game = new LocalGame();
        var pioneer = game.Player.OwnUnits().Records.Single(unit => unit.Type.Value == 0);
        CompleteLocalCommand(game, new(LocalPlayCommands.Found, new([
            new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))),
            new("name", new CommandValue.Text("Workshop"))
        ])));
        var initial = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        byte[] checkpoint = ClassicFiles.EncodeStoredSave(initial with
        {
            Cities = new([initial.Cities[0] with { ShieldBox = 40 }])
        });
        Check(game.LoadClassic(checkpoint) == LocalLoadResult.Loaded);
        var automation = game.Bind(ControllerKind.Automation);
        using var example = new StreamReader(typeof(AutomationSuite).Assembly
            .GetManifestResourceStream("OpenCiv2.AutomationCases.NextProduction")!);
        using var task = new ScriptTask(automation, """
            assert(game.production_choices({slot=0, token='0'}) == nil)
            assert(game.production_choices({slot=200, token='1'}) == nil)

            """ + example.ReadToEnd());
        task.Tick();
        task.Pause();
        for (int i = 0; i < 210; ++i)
        {
            task.Tick();
        }
        Check(task.Status == ScriptStatus.Paused && task.CommandAttempts.Count == 0);
        var turn = CompleteLocalCommand(game, new(LocalPlayCommands.EndTurn, OwnedList<NamedValue>.Empty));
        Check(turn.Committed.Any(effect => effect.Name == "unit.produced"));
        task.Resume();
        for (int i = 0; i < 40 && task.Status == ScriptStatus.Running; ++i)
        {
            game.AdvanceOne();
            task.Tick();
        }
        Check(task.Status == ScriptStatus.Completed, task.Error);
        var attempt = task.CommandAttempts.Single();
        var city = game.Player.OwnCities().Records.Single();
        var manual = new CommandRequest(LocalPlayCommands.Production, new([
            new("city", new CommandValue.City(new(city.Slot, city.Token))),
            new("type", new CommandValue.Integer(4))
        ]));
        Check(attempt.Request == manual && attempt.Status == CommandStatus.Pending);
        Check(task.Results.Single().Status == CommandStatus.Completed && task.Output == "Selected Outrider\n");
        byte[] automated = game.SaveClassic();
        foreach (CommandRequest request in new[] { manual, attempt.Request })
        {
            using var comparison = new LocalGame();
            // Match the lifetime history so the recorded target token is valid in this replay.
            var founder = comparison.Player.OwnUnits().Records.Single(unit => unit.Type.Value == 0);
            CompleteLocalCommand(comparison, new(LocalPlayCommands.Found, new([
                new("unit", new CommandValue.Unit(new(founder.Slot, founder.Token))),
                new("name", new CommandValue.Text("Workshop"))
            ])));
            Check(comparison.LoadClassic(checkpoint) == LocalLoadResult.Loaded);
            CompleteLocalCommand(comparison, new(LocalPlayCommands.EndTurn, OwnedList<NamedValue>.Empty));
            CompleteLocalCommand(comparison, request);
            Check(automated.AsSpan().SequenceEqual(comparison.SaveClassic()));
        }
    }

    private static CommandUpdate CompleteLocalCommand(LocalGame game, CommandRequest request)
    {
        var submitted = game.Player.Submit(request);
        Check(submitted.Status == CommandStatus.Pending, submitted.Code);
        for (int i = 0; i < 8; ++i)
        {
            Check(game.AdvanceOne());
            var update = game.Player.Result(submitted.Correlation.Command)!;
            if (update.Status == CommandStatus.Completed)
            {
                return update;
            }
            Check(update.Interaction is not null, update.Code);
            var interaction = update.Interaction!;
            int selected = i == 0 && request.Name == LocalPlayCommands.EndTurn ? 1 : interaction.Spec.Choices[0];
            Check(game.Player.Reply(interaction.Id, selected).Accepted);
        }
        throw new InvalidOperationException("Local command did not finish");
    }
    private static void Fails(string code, string expected, ScriptLimits? limits = null)
    {
        using var f = new Fixture(); string before = f.Stack(); using var task = f.Task(code, limits);
        f.Pump(task); Check(task.Status == ScriptStatus.Failed && task.Error.Contains(expected, StringComparison.Ordinal), task.Error);
        Check(f.Stack() == before && !f.Runtime.HasPendingCommands);
    }
    private static void InstructionBudget()
    {
        Fails("while true do end", "instruction_budget");
        Fails("pcall(function() while true do end end); game.log('escaped')", "instruction_budget");
        Fails("while true do pcall(function() while true do end end) end", "instruction_budget");
        // A host suspension inside pcall must remain on the owner tick and retain its hook.
        Fails("pcall(function() game.wait(); while true do end end)", "instruction_budget");
    }
    private static void ManagedRuntime()
    {
        using var f = new Fixture();
        using var task = f.Task("""
            assert(_VERSION == 'Lua 5.2')
            -- Byte strings preserve the existing API's UTF-8 text boundary.
            assert(#'é' == 2 and string.byte('é', 1) == 195)
            local large = string.rep('a', 65536)
            local values = {}
            for i=1,40 do values[i] = large .. tostring(i) end
            assert(#values[40] == 65538)
            local ok = pcall(function()
                for i=1,5 do game.log('é'); game.wait() end
            end)
            assert(ok)
            """);
        // Each owner tick dispatches exactly one host call, even under a UI context.
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new RejectPostingContext());
            for (int i = 0; i < 5; ++i)
            {
                task.Tick(); Check(task.Output == string.Concat(Enumerable.Repeat("é\n", i + 1)));
                task.Tick(); Check(task.Output == string.Concat(Enumerable.Repeat("é\n", i + 1)));
                GC.Collect();
            }
            task.Tick();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        Check(task.Status == ScriptStatus.Completed, task.Error);
        Check(task.Output == string.Concat(Enumerable.Repeat("é\n", 5)));
    }
    private sealed class RejectPostingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            throw new InvalidOperationException("Script escaped the owner tick");
    }
    private static void HostBudgets()
    {
        Fails("string.rep('', 9223372036854775807)", "host_work_budget");
        Fails("table.move({},1,9223372036854775807,1)", "nil value");
        Fails("while true do game.wait() end", "host_call_budget", new(Ticks: 3));
        Fails("game.log('12345')", "output_budget", new(OutputBytes: 4));
        Fails("game.log('éé')", "output_budget", new(OutputBytes: 4));
        Fails("pcall(function() game.log(string.rep('a',65536)) end)", "host_request_budget");
        // A query can exceed the payload limit even though the VM has no memory cap.
        using var f = new Fixture();
        f.Runtime.World.Entities.Units.SetEnd(1024); f.Runtime.World.Globals.UnitEnd = 1024;
        for (int i = 4; i < 1024; ++i) f.Runtime.World.Entities.Units.Storage[i] = new()
        { Token = new((uint)i + 100), Owner = new(1), Position = new(2, 2) };
        using var task = f.Task("game.units()"); f.Pump(task);
        Check(task.Status == ScriptStatus.Failed && task.Error == "host_reply_budget", task.Error);
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
        using (var cancelled = f.Task("pcall(function() game.wait() end); game.log('escaped')"))
        {
            cancelled.Tick(); cancelled.Cancel(); cancelled.Tick();
            Check(cancelled.Status == ScriptStatus.Cancelled && cancelled.Output.Length == 0);
        }
        var pending = f.Task("game.command('unit.stack-bottom unit=1:101'); game.wait()"); pending.Tick(); var id = pending.SubmittedCommands[0];
        pending.Dispose(); pending.Dispose(); while (f.Runtime.AdvanceOne()) { }
        Check(f.Client.Result(id)!.Status == CommandStatus.Cancelled);
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
        using var f = new Fixture(); using var task = f.Task("assert(io==nil and os==nil and package==nil and debug==nil and load==nil and coroutine==nil); assert(math.random==nil and math.randomseed==nil and setmetatable==nil and _oc2_call==nil); assert(require==nil and loadfile==nil and dofile==nil and collectgarbage==nil and print==nil and xpcall==nil); assert(string.dump==nil and string.find==nil and table.sort==nil)");
        f.Pump(task); Check(task.Status == ScriptStatus.Completed, task.Error);
        try { using var invalid = f.Task("return", new(Ticks: 0)); throw new Exception("Invalid limits admitted"); } catch (ArgumentOutOfRangeException) { }
        try { using var invalid = f.Task(new string('a', 65537)); throw new Exception("Oversize script admitted"); } catch (ArgumentException) { }
    }
}
