using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F06 §11, S02 §11 and actual U01/V01 children; authored inputs, no original execution claim.
    private sealed class RetainedLoadFixture : IDisposable
    {
        public LoadDialogFixture Load { get; } = new();
        public EventFixture Events { get; }
        public GameState World => Load.File.Runtime.World;
        public int Factories;
        public bool FailFactory;
        public RetainedLoadFixture()
        {
            Events = new(new(Load.File.Runtime.Process, World));
            var save = DialogSave(true);
            Load.File.Bytes = ClassicFiles.EncodeStoredSave(save with
            {
                Globals = save.Globals with { UnitEnd = 1 },
                Civilizations = new(save.Civilizations.Select((c, i) => c with { Gold = i == 1 ? 10 : 0 })),
                Units = new([new() { Token = new(17), Owner = new(1), Type = new(4), X = -1 }])
            });
            var units = Load.Rules.Definitions.Units.ToArray(); units[4] = units[4] with { Role = RuleRole.Attack };
            Load.Rules = Load.Rules with { Definitions = Load.Rules.Definitions with { Units = new(units) } };
            Load.Parsed = new([new(new()
            {
                Trigger = EventTrigger.ScenarioLoaded, MoneyReceiverCiv = 1, MoneyAmount = 7,
                Actions = new() { ChangeMoney = true, Text = true, JustOnce = true }
            }, new(Enumerable.Range(0, Event.StringCount).Select(i => i == 4 ? "Loaded scenario" : null)))]);
            // These synchronous gameplay ports must be unused by the retained path.
            Load.Services = Load.Services with { ScenarioLoaded = null!, Deletion = null! };
        }
        private ClassicLoadDialog.RetainedChildren Children()
        {
            ++Factories; Equal(1, Load.File.Closes); Equal(10, World.Civilizations[1].Gold);
            if (FailFactory) { throw new IOException("Child composition unavailable"); }
            Events.RebindDispatcher();
            return new(Events.Triggers, new(World, new(World, Load.Budget, Load.Services.Diagnostics), Events.Life.Elimination,
                Events.Contact.F.T.T.Damage.Incidents.Incidents));
        }
        public ClassicLoadDialog.Operation Begin(string? path = "C:\\home\\world.sav", bool title = true) =>
            ClassicLoadDialog.Begin(Load.File.Runtime, Load.File.Files, Load.Dialog, path, title, Load.Services, Load.Budget, "retained-load", Children);
        public void Dispose() => Load.Dispose();
    }
    private static void LoadRetainsScenarioBeforeCleanup()
    {
        using var f = new RetainedLoadFixture(); var runtime = f.Load.File.Runtime; var client = SessionClient(runtime, 1); var identity = client.Identity;
        using var op = f.Begin(); Throws<InvalidOperationException>(() => runtime.PrepareNewGame());
        var pending = op.Resume(); Equal(true, pending is ScenarioText { Recipient: 1 }); Equal(pending, op.Resume());
        Equal(false, op.Completed); Equal(null, op.Result); Equal(1, f.Factories); Equal(17, f.World.Civilizations[1].Gold);
        Equal(false, f.World.Multiplayer.NetworkHost); Equal(true, f.World.Entities.Units.Storage[0].Token.Occupied);
        Equal(false, f.Load.File.Trace.Contains("music") || f.Load.File.Trace.Contains("title")); Equal("prior", f.Load.Dialog.ChosenPath);
        var entry = f.World.Events.Collection.At(0); Equal(false, entry.Values.Actions.Consumed);
        Equal(1, client.OwnUnits().Records.Count); Throws<InvalidOperationException>(() => runtime.PrepareNewGame());
        Equal(null, op.Resume(new(0))); Equal(true, op.Completed); Equal((CoreLoadStatus?)CoreLoadStatus.Success, op.Result);
        Equal(true, entry.Values.Actions.Consumed); Equal(true, f.World.Multiplayer.NetworkHost);
        Equal(false, f.World.Entities.Units.Storage[0].Token.Occupied); Equal(0, client.OwnUnits().Records.Count);
        Equal(true, f.Load.File.Trace.Contains("music") && f.Load.File.Trace.Contains("title")); Equal("C:\\home\\world.sav", f.Load.Dialog.ChosenPath);
        Equal(identity, client.Identity); Equal(null, op.Resume(new(1))); Equal(17, f.World.Civilizations[1].Gold);
        runtime.PrepareNewGame();
    }
    private static void LoadRetainedGatesAndStatuses()
    {
        using (var f = new RetainedLoadFixture())
        {
            f.Load.File.Runtime.Process.OrdinaryPreferences.GameOptions.Music = false;
            using var op = f.Begin(title: false); Equal(null, op.Resume()); Equal(true, op.Completed);
            Equal((CoreLoadStatus?)CoreLoadStatus.Success, op.Result); Equal(10, f.World.Civilizations[1].Gold);
            Equal(false, f.World.Events.Collection.At(0).Values.Actions.Consumed); Equal(0, f.Events.Texts.Count);
            Equal(1, f.Factories); Equal(true, f.World.Multiplayer.NetworkHost); Equal(false, f.Load.File.Trace.Contains("title"));
        }
        using (var f = new RetainedLoadFixture())
        {
            f.Load.ResourceExists = false; using var op = f.Begin(); Equal(null, op.Resume()); Equal(true, op.Completed);
            Equal(false, f.World.Events.Loaded); Equal(0, f.World.Events.Collection.Count); Equal(10, f.World.Civilizations[1].Gold);
        }
        using (var f = new RetainedLoadFixture())
        {
            f.Load.File.OpenOk = false; using var op = f.Begin(); Equal(null, op.Resume()); Equal(true, op.Completed);
            Equal((CoreLoadStatus?)CoreLoadStatus.Input, op.Result); Equal(0, f.Factories); Equal(0, f.Load.Dialog.LoadedFileKind);
            f.Load.File.Runtime.PrepareNewGame();
        }
        using (var f = new RetainedLoadFixture())
        {
            using var op = f.Begin(null); Equal(null, op.Resume()); Equal(true, op.Completed); Equal(null, op.Result);
            Equal(0, f.Factories); Equal(0, f.Load.File.Opens); Equal("prior", f.Load.Dialog.ChosenPath);
        }
    }
    private static void LoadRetainedFailureAndDisposal()
    {
        using (var f = new RetainedLoadFixture())
        {
            using var op = f.Begin(); op.Resume(); var entry = f.World.Events.Collection.At(0); op.Dispose();
            Equal(17, f.World.Civilizations[1].Gold); Equal(false, entry.Values.Actions.Consumed);
            Equal(false, f.Load.File.Trace.Contains("music")); Equal(true, f.World.Entities.Units.Storage[0].Token.Occupied);
            Throws<ObjectDisposedException>(() => op.Resume(new(0))); f.Load.File.Runtime.PrepareNewGame();
        }
        using (var f = new RetainedLoadFixture { FailFactory = true })
        {
            using var op = f.Begin(); Throws<IOException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume());
            Equal(1, f.Load.File.Closes); Equal(10, f.World.Civilizations[1].Gold); Equal("prior", f.Load.Dialog.ChosenPath);
            Equal(false, f.Load.File.Trace.Contains("music")); f.Load.File.Runtime.PrepareNewGame();
        }
        using (var f = new RetainedLoadFixture())
        {
            using var op = f.Begin(); op.Resume(); f.World.Events.Collection.Reset();
            Throws<InvalidOperationException>(() => op.Resume(new(0))); Equal(17, f.World.Civilizations[1].Gold);
            Equal("prior", f.Load.Dialog.ChosenPath); Equal(false, f.World.Multiplayer.NetworkHost); f.Load.File.Runtime.PrepareNewGame();
        }
        using (var f = new RetainedLoadFixture())
        {
            using var op = f.Begin(); op.Dispose(); Equal(0, f.Load.File.Opens); f.Load.File.Runtime.PrepareNewGame();
        }
        using (var f = new RetainedLoadFixture())
        {
            using var op = f.Begin(); op.Resume(); f.Load.Budget.Steps = 0;
            Throws<StackResourceStop>(() => op.Resume(new(0)));
            Equal(true, f.World.Events.Collection.At(0).Values.Actions.Consumed); Equal(true, f.World.Multiplayer.NetworkHost);
            Equal(true, f.World.Entities.Units.Storage[0].Token.Occupied); Equal("prior", f.Load.Dialog.ChosenPath);
            Equal(true, f.Load.File.Trace.Contains("title")); Equal(null, op.Result); f.Load.File.Runtime.PrepareNewGame();
        }
    }
    private static void LoadedCleanupRetainedScan()
    {
        var life = new EliminationFixture(); var incidents = new IncidentFixture(life.Random, life.Data); var w = life.World;
        life.Data.AddUnit(0, 1, 4, new(-1, 2)); life.Data.AddUnit(2, 1, 4, new(100, 100));
        life.Data.Unit(1).Previous = life.Data.Unit(1).Next = new UnitSlot(99);
        using var op = new ClassicLoadedUnitCleanup(w, life.Data.Stacks, life.Elimination, incidents.Incidents).Begin("cleanup");
        Equal(null, op.Resume()); Equal(true, op.Completed); Equal(true, w.Multiplayer.NetworkHost);
        Equal(false, life.Data.Unit(0).Token.Occupied); Equal(true, life.Data.Unit(2).Token.Occupied);
        Equal(null, life.Data.Unit(1).Previous); Equal(null, life.Data.Unit(1).Next);
        Equal(0, life.Order.Count); Equal(null, op.Resume(new(0))); life.Random.RequireChoicesConsumed();
    }
    private static void LoadedCleanupRetainsRestart()
    {
        foreach (bool fail in new[] { false, true })
        {
            var life = new EliminationFixture(); var incidents = new IncidentFixture(life.Random, life.Data);
            var f = new RestartGrantFixture(life.Data); life.Recreate = _ => f.Create();
            f.Data.AddUnit(0, 2, 0, new(-1, 8)); f.World.Civilizations[2].UnitsActive[0] = 1;
            using var op = new ClassicLoadedUnitCleanup(f.World, f.Data.Stacks, life.Elimination, incidents.Incidents).Begin("cleanup");
            var pending = op.Resume(); f.Pending(pending); Equal(pending, op.Resume()); Equal(false, op.Completed);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(true, f.World.Multiplayer.NetworkHost);
            if (fail)
            {
                f.Research.FailRevolution = true; Throws<InvalidOperationException>(() => op.Resume(new(1)));
                Throws<InvalidOperationException>(() => op.Resume()); Equal(false, op.Completed);
            }
            else
            {
                // A real player decision can expose a later live scan tail.
                f.Data.AddUnit(2, 1, 4, new(10, -1)); f.Data.Unit(1).Previous = f.Data.Unit(1).Next = new UnitSlot(99);
                Equal(null, op.Resume(new(0))); Equal(true, op.Completed);
                Equal(false, f.Data.Unit(2).Token.Occupied); Equal(null, f.Data.Unit(1).Next); Equal(1, life.Restarts);
            }
            f.Random.RequireChoicesConsumed(); life.Random.RequireChoicesConsumed();
        }
    }
    private static void LoadedCleanupRetainedAdmission()
    {
        var life = new EliminationFixture(); var incidents = new IncidentFixture(life.Random, life.Data);
        life.Data.AddUnit(0, 1, 4, new(10, 8)); life.World.Globals.Multiplayer = GameMode.Tcp;
        using (var op = new ClassicLoadedUnitCleanup(life.World, life.Data.Stacks, life.Elimination, incidents.Incidents).Begin("network"))
        { Throws<InvalidOperationException>(() => op.Resume()); Equal(false, life.World.Multiplayer.NetworkHost); }
        life.World.Globals.Multiplayer = GameMode.SinglePlayer;
        using var stopped = new ClassicLoadedUnitCleanup(life.World, new(life.World, new(0, 64)), life.Elimination, incidents.Incidents).Begin("budget");
        Throws<StackResourceStop>(() => stopped.Resume()); Equal(true, life.World.Multiplayer.NetworkHost);
        Equal(true, life.Data.Unit(0).Token.Occupied); Equal(false, stopped.Completed);
    }
}
