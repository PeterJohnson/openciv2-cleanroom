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
    private sealed class PlayerOrderEffects : IUnitPlayerOrderEffects
    {
        public List<CitySlot?> Homes { get; } = [];
        public List<MapPoint> Redraws { get; } = [];
        public List<UnitSlot> Changes { get; } = [];
        public bool Fail;
        private void Check() { if (Fail) { throw new IOException("Presentation failed"); } }
        public void Redraw(MapPoint position) { Redraws.Add(position); Check(); }
        public void UnitChanged(UnitSlot unit) { Changes.Add(unit); Check(); }
        public void RefreshHome(CitySlot? city) { Homes.Add(city); Check(); }
    }
    private static UnitPlayerOrders PlayerOrders(RevelationFixture f, PlayerOrderEffects effects) => new(f.World, f.Rules, f.Stacks, effects);
    private static void PlayerOrdersFortifyGates()
    {
        foreach (int type in new[] { 0, 1, 2 })
        {
            for (int site = 0; site < 4; site++)
            {
                var f = new RevelationFixture(); var p = new MapPoint(10, 10); f.AddUnit(0, 1, type, p);
                if (site == 1) { f.World.Map.TileAt(p).Terrain = Terrain.Ocean; }
                if (site is 2 or 3)
                {
                    f.World.Map.TileAt(p).Features |= MapFeatures.CitySite;
                    if (site == 3) { f.World.Map.TileAt(p).Features |= MapFeatures.Fortification; }
                    f.World.Map.TileAt(p).Owner = 2;
                }
                f.Unit(0).AuxiliaryTimer = 9; f.Unit(0).AiTask = 17; f.Unit(0).Home = new(130);
                var effects = new PlayerOrderEffects(); var result = PlayerOrders(f, effects).Fortify(new(0));
                bool accepted = site != 1 && (type != 2 || site is 2 or 3);
                Equal(accepted, result.Accepted); Equal(accepted ? UnitOrder.Fortifying : UnitOrder.None, f.Unit(0).Orders);
                Equal((byte)(accepted ? UnitMovement.Allowance(f.World, f.Rules, f.Unit(0)) : 0), f.Unit(0).MovesUsed);
                Equal((CitySlot?)new CitySlot(130), f.Unit(0).Home); Equal((byte)9, f.Unit(0).AuxiliaryTimer); Equal((byte)17, f.Unit(0).AiTask);
                Equal(accepted ? 1 : 0, effects.Redraws.Count); Equal(accepted ? 1 : 0, effects.Changes.Count);
                Equal(0, effects.Homes.Count); Equal(0, result.PresentationFailures.Count);
            }
        }
    }
    private static void PlayerOrdersSleepAirAndPreservation()
    {
        for (int site = 0; site < 8; site++)
        {
            var f = new RevelationFixture(); var p = new MapPoint(10, 10); f.AddUnit(0, 1, 2, p);
            if (site >= 3) { f.World.Map.TileAt(p).Terrain = Terrain.Ocean; }
            if (site is 1 or 2)
            {
                f.World.Map.TileAt(p).Features |= MapFeatures.CitySite;
                if (site == 2) { f.World.Map.TileAt(p).Features |= MapFeatures.Fortification; }
                f.World.Map.TileAt(p).Owner = 2;
            }
            if (site >= 4)
            {
                f.Rules.Units[1] = f.Rules.Units[1] with { Abilities = new() { CarryAircraft = site is 4 or 7, Submarine = site is 5 or 6 } };
                // Placement builds owner-specific stacks. A foreign carrier at the
                // same coordinates is not a member of the aircraft's stack.
                f.AddUnit(1, site == 7 ? 2 : 1, 1, p);
                f.Rules.Units[2] = f.Rules.Units[2] with { Abilities = new() { DestroyedAfterAttack = site == 6 } };
            }
            f.Unit(0).MovesUsed = 2; f.Unit(0).Destination = new(20, 14); f.Unit(0).Carrier = new(1);
            var effects = new PlayerOrderEffects(); var orders = PlayerOrders(f, effects); var result = orders.Sleep(new(0));
            bool accepted = site is 1 or 4 or 6;
            Equal(accepted, result.Accepted); Equal(accepted ? UnitOrder.Sleeping : UnitOrder.None, f.Unit(0).Orders);
            Equal(new MapPoint(accepted ? -1 : 20, 14), f.Unit(0).Destination); Equal((byte)2, f.Unit(0).MovesUsed);
            Equal(accepted ? null : (UnitSlot?)new UnitSlot(1), f.Unit(0).Carrier);
            Equal(0, effects.Redraws.Count + effects.Changes.Count + effects.Homes.Count);
            if (accepted)
            {
                f.Unit(0).Destination = new(22, 16); f.Unit(0).Carrier = new(1);
                Equal(true, orders.Sleep(new(0)).Accepted); Equal(new MapPoint(22, 16), f.Unit(0).Destination);
                Equal((UnitSlot?)new UnitSlot(1), f.Unit(0).Carrier);
            }
        }
        var land = new RevelationFixture(); land.AddUnit(0, 1, 0, new(10, 10)); land.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
        Equal(true, PlayerOrders(land, new()).Sleep(new(0)).Accepted);
    }
    private static void PlayerOrdersSleepCoast()
    {
        foreach (bool wrap in new[] { false, true })
        {
            var f = new RevelationFixture(); foreach (ref var tile in f.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; }
            f.World.Globals.Rules.FlatWorld = !wrap; f.AddUnit(0, 1, 1, new(0, 10));
            f.Rules.Units[1] = f.Rules.Units[1] with { Abilities = new() { CoastBound = true } };
            f.World.Map.TileAt(new(38, 10)).Terrain = Terrain.Grassland;
            var service = PlayerOrders(f, new()); Equal(wrap, service.Sleep(new(0)).Accepted);
            f.World.Map.TileAt(new(0, 10)).Terrain = Terrain.Grassland;
            Equal(true, service.Sleep(new(0)).Accepted); Equal((byte)0, f.Unit(0).MovesUsed);
        }
    }
    private static void PlayerOrdersHomeAndRecovery()
    {
        foreach (CitySlot? old in new CitySlot?[] { null, new(0), new(130), new(254) })
        {
            var f = new RevelationFixture(); var p = new MapPoint(10, 10); f.AddUnit(0, 1, 0, p); f.AddCity(1, 2, p);
            f.Unit(0).Home = old; var effects = new PlayerOrderEffects(); var result = PlayerOrders(f, effects).Home(new(0));
            Equal(true, result.Accepted); Equal((CitySlot?)new CitySlot(1), f.Unit(0).Home); Sequence([old, new CitySlot(1)], effects.Homes);
            Equal((byte)0, f.Unit(0).MovesUsed); Equal(UnitOrder.None, f.Unit(0).Orders);
        }
        var trade = new RevelationFixture(); trade.AddUnit(0, 1, 3, new(10, 10)); trade.AddCity(0, 2, new(10, 10));
        trade.Unit(0).Home = new(130); var display = new PlayerOrderEffects(); var command = PlayerOrders(trade, display);
        Equal(UnitOrderRefusal.CaravanHome, command.Home(new(0)).Refusal); Equal((CitySlot?)new CitySlot(130), trade.Unit(0).Home); Equal(0, display.Homes.Count);
        trade.Unit(0).Home = null; Equal(true, command.Home(new(0)).Accepted); Equal((CitySlot?)new CitySlot(0), trade.Unit(0).Home);
        var recovery = new RevelationFixture(); recovery.AddUnit(0, 1, 0, new(10, 10)); recovery.AddCity(0, 1, new(14, 10));
        recovery.Unit(0).Home = new(130); recovery.Unit(0).AiTask = 19; recovery.Unit(0).AuxiliaryTimer = 23;
        var presentation = new PlayerOrderEffects(); Equal(true, PlayerOrders(recovery, presentation).Home(new(0)).Accepted);
        Equal(UnitOrder.GoTo, recovery.Unit(0).Orders); Equal(new MapPoint(14, 10), recovery.Unit(0).Destination);
        Equal((CitySlot?)new CitySlot(130), recovery.Unit(0).Home); Equal((byte)19, recovery.Unit(0).AiTask); Equal((byte)23, recovery.Unit(0).AuxiliaryTimer);
        Equal(new MapPoint(10, 10), recovery.Unit(0).Position); Equal(0, presentation.Homes.Count);
    }
    private static void PlayerOrdersWaitRetainsContact()
    {
        var f = new ContactFixture(); ContactNeighbors(f); f.World.Globals.TurnCiv = 1;
        f.Data.AddUnit(0, 1, 0, new(2, 2)); f.World.Globals.ActiveUnit = new(0);
        var selection = new UnitSelection(f.World, f.Data.Rules, new(), ContactRevelation(f), new SelectionEffects());
        using var wait = PlayerOrders(f.Data, new()).Wait(selection, new(2, 2), "wait");
        Equal(true, f.Data.Unit(0).Flags.Waiting); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        var pending = wait.Resume(); Equal(true, pending is DiplomaticContactDecision.Audience);
        Equal((UnitSlot?)new UnitSlot(10), f.World.Globals.ActiveUnit); Equal(pending, wait.Resume());
        Equal(false, f.Data.Unit(12).Visibility.Contains(1)); wait.Resume(new(1));
        Equal(true, wait.Completed); Equal(true, f.Data.Unit(12).Visibility.Contains(1)); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        Equal(0, wait.PresentationFailures.Count); f.F.T.T.Random.RequireChoicesConsumed();
    }
    private static void PlayerOrdersAutomationExhaustAndFailures()
    {
        var f = new RevelationFixture(); f.AddUnit(0, 1, 0, new(10, 10)); f.AddUnit(1, 2, 0, new(12, 10));
        f.World.Globals.ActiveUnit = new(0); f.World.PlayerPhaseActive = f.World.TurnLoopActive = true;
        f.Unit(0).Orders = UnitOrder.GoTo; f.Unit(0).Destination = new(20, 10); f.Unit(0).AuxiliaryTimer = 7;
        var orders = PlayerOrders(f, new()); orders.BeginAutomation(); Equal(false, f.Unit(0).Flags.Automated);
        f.Rules.Units[0] = f.Rules.Units[0] with { Role = RuleRole.Settle }; orders.BeginAutomation();
        Equal(true, f.Unit(0).Flags.Automated); Equal(UnitOrder.GoTo, f.Unit(0).Orders); Equal((byte)7, f.Unit(0).AuxiliaryTimer);
        UnitPlayerOrders.EndPlayerTurn(f.World, f.Rules); Equal((byte)6, f.Unit(0).MovesUsed); Equal((byte)0, f.Unit(1).MovesUsed);
        Equal(true, f.World.PlayerPhaseActive); Equal(true, f.World.TurnLoopActive); Equal((short)0, f.World.Globals.Turn);
        Equal(UnitOrder.GoTo, f.Unit(0).Orders); Equal((byte)7, f.Unit(0).AuxiliaryTimer);
        var fail = new PlayerOrderEffects { Fail = true }; var service = PlayerOrders(f, fail);
        Equal(2, service.Fortify(new(0)).PresentationFailures.Count); Equal(UnitOrder.Fortifying, f.Unit(0).Orders);
        f.AddCity(0, 2, new(10, 10)); Equal(2, service.Home(new(0)).PresentationFailures.Count);
        Equal((CitySlot?)new CitySlot(0), f.Unit(0).Home); Equal(2, fail.Homes.Count);
    }
    public static (byte[] Sleeping, byte[] Homed, byte[] Continued) LocalUnitOrdersReference() => LocalUnitOrdersReference(false);
    private static (byte[] Sleeping, byte[] Homed, byte[] Continued) LocalUnitOrdersReference(bool console)
    {
        var recorder = new LocalReplayRecorder(new string('e', 64)); using var game = new LocalGame(recorder);
        void Order(string name, int slot)
        {
            var unit = game.Player.OwnUnits().Records.Single(u => u.Slot.Value == slot);
            RunLocal(game, console, $"{name} unit={slot}:{unit.Token.Value}", name, new NamedValue("unit", new CommandValue.Unit(new(unit.Slot, unit.Token))));
        }
        Order(LocalPlayCommands.Wait, 0); Equal((ushort)1, game.Player.ObserveMap()!.Annotations!.ActiveUnit!.Value.Slot.Value);
        Equal((byte)0, game.Player.OwnUnits().Records.Single(u => u.Slot.Value == 0).MovesUsed);
        Order(LocalPlayCommands.Sleep, 1); Equal(UnitOrder.Sleeping, game.Player.OwnUnits().Records.Single(u => u.Slot.Value == 1).Orders);
        byte[] sleeping = game.SaveClassic(); BytesEqual(sleeping, LocalReplay.Run(recorder.Capture()));
        Equal(LocalLoadResult.Loaded, game.LoadClassic(sleeping)); FoundCouncil(game); Order(LocalPlayCommands.Select, 1);
        var rider = game.Player.OwnUnits().Records.Single();
        RunLocal(game, console, $"unit.move unit=1:{rider.Token.Value} direction=5", LocalPlayCommands.Move,
            new("unit", new CommandValue.Unit(new(rider.Slot, rider.Token))), new("direction", new CommandValue.Integer(5)));
        Order(LocalPlayCommands.Home, 1); Equal((CitySlot?)new CitySlot(0), game.Player.OwnUnits().Records.Single().Home);
        Order(LocalPlayCommands.Fortify, 1); byte[] homed = game.SaveClassic(); BytesEqual(homed, LocalReplay.Run(recorder.Capture()));
        Equal(LocalLoadResult.Loaded, game.LoadClassic(homed)); EndLocalTurn(game, console, 2);
        byte[] continued = game.SaveClassic(); BytesEqual(continued, LocalReplay.Run(recorder.Capture()));
        return (sleeping, homed, continued);
    }
    private static void LocalUnitOrdersReplay()
    {
        var direct = LocalUnitOrdersReference(false); var console = LocalUnitOrdersReference(true);
        BytesEqual(direct.Sleeping, console.Sleeping); BytesEqual(direct.Homed, console.Homed); BytesEqual(direct.Continued, console.Continued);
        foreach (var order in new[] { UnitOrder.Fortifying, UnitOrder.Irrigate })
        { BytesEqual(WaitQueuedOrder(false, order), WaitQueuedOrder(true, order)); }
    }
    private static byte[] WaitQueuedOrder(bool console, UnitOrder order)
    {
        var recorder = new LocalReplayRecorder(new string('f', 64)); using var game = new LocalGame(recorder);
        var stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        var units = stored.Units.ToArray(); var queued = StateConversion.ToLive(units[0]);
        queued.Orders = order; queued.Counter = 1;
        units[0] = StateConversion.ToStored(queued);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(stored with { Units = new(units) })));
        var rider = game.Player.OwnUnits().Records.Single(u => u.Slot.Value == 1);
        foreach (var command in new[] { LocalPlayCommands.Select, LocalPlayCommands.Wait })
        {
            RunLocal(game, console, $"{command} unit=1:{rider.Token.Value}", command,
                new NamedValue("unit", new CommandValue.Unit(new(rider.Slot, rider.Token))));
        }
        var saved = game.SaveClassic(); var completed = StateConversion.ToLive(ClassicFiles.DecodeStoredSave(saved).Units[0]);
        Equal(order == UnitOrder.Fortifying ? UnitOrder.Fortified : UnitOrder.Irrigate, completed.Orders);
        Equal((byte)(order == UnitOrder.Fortifying ? 1 : 2), completed.Counter);
        Equal((ushort)1, game.Player.ObserveMap()!.Annotations!.ActiveUnit!.Value.Slot.Value);
        Equal((byte)0, game.Player.OwnUnits().Records.Single(u => u.Slot.Value == 1).MovesUsed);
        BytesEqual(saved, LocalReplay.Run(recorder.Capture()));
        return saved;
    }
}
