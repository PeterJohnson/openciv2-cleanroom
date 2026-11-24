using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U04 Disband: specification expectations with actual C06/U01/C01/V01/V06 children.
    private sealed class DisbandFixture : IDisbandEffects
    {
        public EliminationFixture Life { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public UnitDisband Disband { get; }
        public CitySlot? Display;
        public bool FailQuery, FailPresentation;
        public List<string> Effects { get; } = [];
        public CitySlot? DisplayedCity => FailQuery ? throw new IOException("City window query failed") : Display;
        public DisbandFixture(RevelationFixture? data = null)
        {
            Life = new(data: data);
            if (World.Entities.Units.End == 0) { Data.AddUnit(0, 1, 4, CargoPoint); }
            World.Globals.ActiveUnit = new(0); World.MovementMode = MovementMode.MoveUnits;
            var removal = new CityRemoval(World, Data.Rules, Data.Stacks, Life.Loss, city => Effects.Add("removed:" + city.Slot.Value));
            var production = new ProductionOrders(World, Data.Rules, _ => throw new InvalidOperationException("No choice during snapshot"),
                _ => throw new InvalidOperationException("No wonder notice during snapshot"));
            var incidents = new IncidentFixture(Life.Random, Data);
            Disband = new(World, Data.Rules, Data.Stacks, production, Life.Loss, removal, Life.Elimination, incidents.Incidents, this);
        }
        public UnitDisband.Operation Begin(MapPoint? cursor = null) => Disband.Begin(cursor ?? CargoPoint, "disband");
        private void Note(string value) { Effects.Add(value); if (FailPresentation) { throw new IOException("Redraw failed"); } }
        public void RefreshCity(CitySlot city) => Note("city:" + city.Value);
        public void RedrawArea(MapPoint position, int observer) => Note($"area:{position.X},{position.Y}:{observer}");
        public void RedrawMaps(int observer) => Note("maps:" + observer);
    }
    private static void DisbandAdmissionAndLiveCredit()
    {
        foreach (bool revealed in new[] { false, true })
        {
            var f = new DisbandFixture(); f.Data.Unit(0).Owner = new(2); f.World.Globals.RevealMap = (byte)(revealed ? 1 : 0);
            using var op = f.Begin(); Equal(revealed, op.Resume() is DisbandDecision.Unit);
            if (revealed) { op.Resume(0); }
            Equal(true, op.Completed); Equal(false, op.Disbanded); Equal(true, f.Data.Unit(0).Token.Occupied);
        }
        var live = new DisbandFixture(); live.Data.Rules.Units[4] = live.Data.Rules.Units[4] with { Identity = new("old", "Old name"), CostRows = 2 };
        using var retained = live.Begin(); var prompt = (DisbandDecision.Unit)retained.Resume()!;
        Equal("Old name", prompt.Name); Equal(prompt, retained.Resume());
        live.Data.AddCity(0, 2, new(14, 10)); live.World.Entities.Cities.Storage[0].ShieldBox = 7;
        live.Data.Stacks.Relocate(0, new(14, 10), false); live.World.Globals.ActiveUnit = null;
        live.Data.Unit(0).Type = new(1); live.Data.Rules.Units[1] = live.Data.Rules.Units[1] with { CostRows = 3 };
        live.Data.Rules.Cosmic = live.Data.Rules.Cosmic with { ShieldBoxRows = 3 };
        live.Display = new(0); live.World.Multiplayer.LocalHuman = 3;
        retained.Resume(1); Equal(true, retained.Completed); Equal(true, retained.Disbanded);
        Equal((short)11, live.World.Entities.Cities.Storage[0].ShieldBox); Equal(11, live.World.Production.SavedStock);
        Sequence(new[] { "city:0", "area:14,10:3" }, live.Effects); Equal(false, live.Data.Unit(0).Token.Occupied);
        int count = live.Effects.Count; retained.Resume(1); Equal(count, live.Effects.Count);
    }
    private static void DisbandSignedShieldCredit()
    {
        foreach (var (cost, rows, stock) in new[] { (3, 3, 7), (255, 3, -2), (128, 255, -30000), (127, 255, 32760) })
            foreach (bool displayed in new[] { false, true })
            {
                var f = new DisbandFixture(); f.Data.AddCity(0, 2, CargoPoint); f.Display = displayed ? new(0) : null;
                f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { CostRows = (byte)cost };
                f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { ShieldBoxRows = (byte)rows };
                f.World.Entities.Cities.Storage[0].ShieldBox = (short)stock; f.World.Entities.Cities.Storage[0].Production = -1;
                f.World.Production.SavedStock = 987; f.World.Production.SavedCategory = ProductionCategory.Units;
                using var op = f.Begin(); op.Resume(); op.Resume(1);
                short expected = unchecked((short)(stock + (sbyte)cost * rows / 2));
                Equal(expected, f.World.Entities.Cities.Storage[0].ShieldBox);
                Equal(displayed ? expected : 987, f.World.Production.SavedStock);
                Equal(displayed ? ProductionCategory.Buildings : ProductionCategory.Units, f.World.Production.SavedCategory);
                Equal(false, f.Data.Unit(0).Token.Occupied);
            }
    }
    private static void DisbandCargoLoss()
    {
        var f = new DisbandFixture(CargoFixture(1, 0, 0)); f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean;
        f.Data.AddCity(0, 2, CargoPoint); f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { CostRows = 3 };
        f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { CostRows = 100 }; f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { ShieldBoxRows = 4 };
        for (int i = 1; i <= 2; i++) { f.Data.Unit(i).Orders = UnitOrder.Sleeping; f.Data.Unit(i).Carrier = new(0); }
        using var op = f.Begin(); op.Resume(); op.Resume(1);
        Equal(true, op.Completed); Equal((short)6, f.World.Entities.Cities.Storage[0].ShieldBox);
        for (int i = 0; i < 3; i++) { Equal(false, f.Data.Unit(i).Token.Occupied); }
        Equal(true, f.Life.Order.Contains("units")); Equal(false, f.World.UnitDeletion.CargoActive); Equal(false, f.World.UnitDeletion.DestroyActive);
        Sequence(new[] { "area:10,10:1" }, f.Effects);
    }
    private static void DisbandCitySelectionAndGates()
    {
        foreach (var mode in new[] { MovementMode.View, MovementMode.MoveUnits, (MovementMode)2 })
            foreach (bool cheat in new[] { false, true })
            {
                var f = new DisbandFixture(); f.Life.Deletion.Erase(0, false); f.World.Globals.ActiveUnit = null;
                f.Data.AddCity(0, 1, CargoPoint); f.World.Civilizations[1].CityCount = 1; f.World.Entities.Cities.Storage[0].Name = "Removed city";
                f.World.MovementMode = mode; f.World.Globals.GameOptions.CheatMode = cheat;
                using var op = f.Begin(); bool offered = cheat && mode == MovementMode.View;
                Equal(offered, op.Resume() is DisbandDecision.City);
                if (offered) { op.Resume(1); }
                Equal(true, op.Completed); Equal(offered, op.Disbanded); Equal(!offered, f.World.Entities.Cities.Storage[0].Token.Occupied);
                if (offered) { Equal(EndReason.Defeat, f.World.PendingEndReason); Sequence(new[] { "removed:0", "maps:1" }, f.Effects); }
            }
        var cursor = new DisbandFixture(); cursor.World.MovementMode = MovementMode.View; cursor.World.Globals.ActiveUnit = null;
        using var selected = cursor.Begin(); Equal(new UnitSlot(0), ((DisbandDecision.Unit)selected.Resume()!).Target.Slot);
        selected.Resume(2); Equal(false, selected.Disbanded); Equal(true, cursor.Data.Unit(0).Token.Occupied);
    }
    private static DisbandFixture DisbandLastSettler(RevelationFixture? data = null)
    {
        var f = new DisbandFixture(data); f.Data.Unit(0).Type = new(0); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
        f.World.Civilizations[1].UnitsActive[0] = 1;
        f.Data.AddUnit(1, 3, 4, new(24, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).Counter = 1;
        f.World.Civilizations[1].Treaties[3].Peace = true;
        return f;
    }
    private static void DisbandRetainsActualLoss()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = DisbandLastSettler(); using var op = f.Begin(); op.Resume();
            var pending = (DisbandDecision.Lifecycle)op.Resume(1)!; Equal(1, ((EspionagePretext)pending.Decision).Victim.Civilization);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal(false, f.Data.Unit(1).Token.Occupied); Equal(pending, op.Resume());
            Equal(false, op.Completed); Equal(0, f.Effects.Count); Equal(0, f.Life.Checks);
            if (dispose) { op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(0)); Equal(0, f.Effects.Count); }
            else { op.Resume(0); Equal(true, op.Completed); Equal(true, op.Disbanded); Equal(EndReason.Defeat, f.World.PendingEndReason); Equal(1, f.Effects.Count); }
            f.Life.Random.RequireChoicesConsumed();
        }
    }
    private static void DisbandFailurePrefixes()
    {
        foreach (bool query in new[] { false, true })
        {
            var f = new DisbandFixture(); f.Data.AddCity(0, 1, CargoPoint); f.Display = new(0);
            f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { CostRows = 2 }; f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { ShieldBoxRows = 4 };
            using var op = f.Begin(); op.Resume(); f.FailQuery = query; f.FailPresentation = !query;
            if (query)
            {
                Throws<IOException>(() => op.Resume(1)); Throws<InvalidOperationException>(() => op.Resume(1));
                Equal(true, f.Data.Unit(0).Token.Occupied); Equal(false, op.Completed);
            }
            else { op.Resume(1); Equal(true, op.Disbanded); Equal(2, op.PresentationFailures.Count); Equal(false, f.Data.Unit(0).Token.Occupied); }
            Equal((short)4, f.World.Entities.Cities.Storage[0].ShieldBox);
        }
        var network = new DisbandFixture(); network.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = network.Begin(); Throws<InvalidOperationException>(() => rejected.Resume()); Equal(true, network.Data.Unit(0).Token.Occupied);
    }
    private static void DisbandPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(255)); var data = new RevelationFixture(runtime.Process, runtime.World);
        var f = DisbandLastSettler(data); ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.disband", "Disband", AccessClass.Player, new([]), new([new("disbanded", ValueKind.Boolean)])),
            (_, _, _) => new(), _ => new UnitDisbandOperation(f.Begin(), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.disband", new([]))).Correlation.Command; runtime.AdvanceOne(); var first = RequiredInteraction(player, command);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(first, 1).Accepted);
        Equal(true, player.Reply(first, 1).Accepted); runtime.AdvanceOne(); var second = RequiredInteraction(player, command);
        Equal(false, player.Reply(first, 0).Accepted); Equal(false, f.Data.Unit(0).Token.Occupied); Equal(0, f.Effects.Count);
        Equal(true, player.Reply(second, 0).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var cancel = new DisbandFixture(); var context = new OperationContext(cancel.World, cancel.Data.Process, cancel.Life.Random,
                new PresentationRandom(1, "disband"), new(default, new(1)), default, (_, _) => { }, () => { });
            using var op = new UnitDisbandOperation(cancel.Begin(), owner => new((ulong)owner)); op.Resume(context, new());
            var done = op.Resume(context, new(input)); Equal(CommandStatus.Completed, done.Status); Equal(true, cancel.Data.Unit(0).Token.Occupied);
            Equal(done, op.Resume(context, new(input)));
        }
    }
    public static (byte[] Disbanded, byte[] Continued) LocalDisbandReference() => LocalDisbandReference(false);
    private static (byte[] Disbanded, byte[] Continued) LocalDisbandReference(bool console)
    {
        var recorder = new LocalReplayRecorder(new string('d', 64)); using var game = new LocalGame(recorder); FoundCouncil(game);
        var unit = game.Player.OwnUnits().Records.Single(); var value = new NamedValue("unit", new CommandValue.Unit(new(unit.Slot, unit.Token)));
        RunLocal(game, console, $"unit.move unit=1:{unit.Token.Value} direction=5", LocalPlayCommands.Move, value, new("direction", new CommandValue.Integer(5)));
        var before = game.SaveClassic();
        CommandUpdate Start() => AwaitLocal(game, SubmitLocal(game, console, $"unit.disband unit=1:{unit.Token.Value}", LocalPlayCommands.Disband, value));
        Equal(CommandStatus.Completed, ReplyLocal(game, console, Start(), 0).Status); BytesEqual(before, game.SaveClassic());
        Equal(CommandStatus.Completed, ReplyLocal(game, console, Start(), 1).Status); Equal(0, game.Player.OwnUnits().Records.Count);
        byte[] disbanded = game.SaveClassic(); Equal((short)4, ClassicFiles.DecodeStoredSave(disbanded).Cities[0].ShieldBox);
        BytesEqual(disbanded, LocalReplay.Run(recorder.Capture())); Equal(LocalLoadResult.Loaded, game.LoadClassic(disbanded)); EndLocalTurn(game, console, 2);
        byte[] continued = game.SaveClassic(); BytesEqual(continued, LocalReplay.Run(recorder.Capture())); return (disbanded, continued);
    }
    private static void LocalDisbandReplayAndDefeat()
    {
        var direct = LocalDisbandReference(false); var console = LocalDisbandReference(true);
        BytesEqual(direct.Disbanded, console.Disbanded); BytesEqual(direct.Continued, console.Continued);
        foreach (bool useConsole in new[] { false, true })
        {
            using (var cancel = new LocalGame())
            {
                var stored = ClassicFiles.DecodeStoredSave(cancel.SaveClassic()); var units = stored.Units.ToArray();
                var queued = StateConversion.ToLive(units[0]); queued.Orders = UnitOrder.Fortifying; units[0] = StateConversion.ToStored(queued);
                Equal(LocalLoadResult.Loaded, cancel.LoadClassic(ClassicFiles.EncodeStoredSave(stored with { Units = new(units) })));
                var before = cancel.SaveClassic(); var target = cancel.Player.OwnUnits().Records.Single(u => u.Slot.Value == 0);
                var confirmation = AwaitLocal(cancel, SubmitLocal(cancel, useConsole, $"unit.disband unit=0:{target.Token.Value}", LocalPlayCommands.Disband,
                    new NamedValue("unit", new CommandValue.Unit(new(target.Slot, target.Token)))));
                Equal(CommandStatus.Completed, ReplyLocal(cancel, useConsole, confirmation, 0).Status);
                BytesEqual(before, cancel.SaveClassic());
            }
            var recorder = new LocalReplayRecorder(new string('b', 64)); using var game = new LocalGame(recorder);
            var unit = game.Player.OwnUnits().Records.Single(u => u.Slot.Value == 0);
            var step = AwaitLocal(game, SubmitLocal(game, useConsole, $"unit.disband unit=0:{unit.Token.Value}", LocalPlayCommands.Disband,
                new NamedValue("unit", new CommandValue.Unit(new(unit.Slot, unit.Token)))));
            step = ReplyLocal(game, useConsole, step, 1);
            foreach (string title in new[] { "Defeat", "Power history", "Civilization score", "Civilization rating", "Hall of Fame" })
            { Equal(true, step.Interaction!.Spec.Prompt.StartsWith(title, StringComparison.Ordinal)); step = ReplyLocal(game, useConsole, step, 0); }
            Equal(CommandStatus.Completed, step.Status); Equal(false, game.Player.ObserveLocalPlay()!.GameActive);
            Equal(0, game.Player.OwnUnits().Records.Count); BytesEqual(game.SaveClassic(), LocalReplay.Run(recorder.Capture()));
        }
    }
}
