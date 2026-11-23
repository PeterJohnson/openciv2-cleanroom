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
    private sealed class UnitTurnFixture
    {
        public EliminationFixture Life { get; }
        public IncidentFixture Incidents { get; }
        public RevelationFixture Data => Life.Data;
        public GameState World => Data.World;
        public UnitTurnReset Reset { get; }
        public List<MapPoint> Draws { get; } = [];
        public bool FailDraw;
        public CivilizationRecovery Recovery { get; }
        public UnitTurnFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Life = new(choices, data); Incidents = new(Life.Random, Data);
            World.Entities.Units.SetEnd(0); World.Entities.Cities.SetEnd(0);
            for (int i = 0; i < EntityLimits.Civilizations; ++i) World.Civilizations[i] = default;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2); World.Globals.HumanPlayers = CivilizationSet.Only(1);
            World.Multiplayer.LocalHuman = 1; World.Globals.Turn = 9;
            foreach (ref var tile in World.Map.Tiles) { tile = default; tile.Terrain = Terrain.Grassland; tile.Region = 1; }
            Data.Rules.Units[4] = new() { Domain = RuleDomain.Land, Role = RuleRole.Attack, HitPoints = 10, Movement = 6 };
            Data.Rules.Units[47] = Data.Rules.Units[4] with { Role = RuleRole.Diplomacy };
            Data.AddUnit(0, 1, 4, new(10, 10));
            Reset = new(World, Data.Rules, Life.Elimination, Incidents.Incidents);
            Recovery = new(World, Data.Rules, p => { Draws.Add(p); if (FailDraw) throw new InvalidOperationException("Display unavailable"); });
        }
        public void Complete(UnitTurnReset.Operation op)
        { for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume()); Equal(true, op.Completed); Life.Random.RequireChoicesConsumed(); }
        public void City(MapPoint point, int building = 0, int slot = 0)
        {
            Data.AddCity(slot, 1, point); World.Civilizations[1].CityCount++;
            BuildingQueries.SetBuilding(World, new((ushort)slot), building, true);
        }
    }
    private static void UnitResetOrdinaryAndSelectors()
    {
        foreach (int selector in new[] { UnitTurnReset.All, UnitTurnReset.AllAlternate, UnitTurnReset.ExhaustAll, 1, 7 })
        {
            var f = new UnitTurnFixture(); f.Data.AddUnit(1, 2, 4, new(12, 10));
            foreach (ref var unit in f.World.Entities.Units.Scanned)
            {
                unit.MovesUsed = 4; unit.AuxiliaryTimer = 7; unit.Orders = UnitOrder.GoTo; unit.Destination = new(20, 10);
                unit.Flags.Waiting = unit.Flags.Moved = unit.Flags.Paradropped = unit.Flags.BorderWarned = true;
            }
            f.World.Map.TileAt(new(8, 8)).Features |= MapFeatures.Unit;
            using var op = f.Reset.Begin(selector, "reset"); f.Complete(op);
            Equal(false, f.World.Map.TileAt(new(8, 8)).Unit);
            for (int i = 0; i < 2; ++i)
            {
                bool admitted = selector is -1 or -2 || selector == i + 1;
                Equal((byte)(selector == -3 ? 6 : admitted ? 0 : 4), f.Data.Unit(i).MovesUsed);
                Equal((byte)(admitted ? 0 : 7), f.Data.Unit(i).AuxiliaryTimer); Equal(!admitted, f.Data.Unit(i).Flags.Waiting);
                Equal(true, f.Data.Unit(i).Flags.Moved); Equal(true, f.Data.Unit(i).Flags.Paradropped);
                Equal(!(admitted && i == 1), f.Data.Unit(i).Flags.BorderWarned);
                Equal(UnitOrder.GoTo, f.Data.Unit(i).Orders); Equal(new MapPoint(20, 10), f.Data.Unit(i).Destination);
                Equal(true, f.World.Map.TileAt(f.Data.Unit(i).Position).Unit);
            }
        }
        var intruder = new UnitTurnFixture(); intruder.World.Globals.Turn = 8;
        intruder.Data.Unit(0).Flags.BorderIntruder = intruder.Data.Unit(0).Flags.BorderWarned = true;
        using var reset = intruder.Reset.Begin(-1, "reset"); intruder.Complete(reset);
        Equal(true, intruder.Data.Unit(0).Flags.BorderWarned); Equal(false, intruder.Data.Unit(0).Flags.BorderIntruder);
    }
    private static void UnitResetSpyTimersAndSurvival()
    {
        foreach (int selector in new[] { -1, -2, 1 })
        {
            var f = new UnitTurnFixture([EscapeChoice("reset/expiry-0", 2, 1)]);
            f.Data.Unit(0).Type = new(47); f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).Flags.Waiting = true;
            f.Data.Unit(0).AuxiliaryTimer = 1; f.Data.Unit(0).Counter = 2; f.Data.Unit(0).MovesUsed = 254;
            f.World.Civilizations[1].Treaties[2].SpyChat = true; f.World.Names[1].Tribe = "Preserved";
            f.Data.AddUnit(1, 1, 47, new(12, 10)); f.Data.Unit(1).Flags.SpyMission = true; f.Data.Unit(1).AuxiliaryTimer = 0;
            using var op = f.Reset.Begin(selector, "reset"); f.Complete(op);
            Equal(false, f.Data.Unit(0).Flags.SpyMission); Equal(true, f.Data.Unit(0).Flags.Veteran); Equal(true, f.Data.Unit(0).Flags.Waiting);
            Equal((byte)1, f.Data.Unit(0).MovesUsed); Equal(selector < 0, f.World.Civilizations[1].Treaties[2].SpyChat);
            Equal("Preserved", f.World.Names[1].Tribe); Equal((byte)255, f.Data.Unit(1).AuxiliaryTimer); Equal(true, f.Data.Unit(1).Flags.SpyMission);
        }
    }
    private static void UnitResetSpyHomeAndRecoveryVisibility()
    {
        var home = new UnitTurnFixture([EscapeChoice("reset/expiry-0", 2, 1)]); home.City(new(16, 10));
        home.Data.Unit(0).Type = new(47); home.Data.Unit(0).Flags.SpyMission = home.Data.Unit(0).Flags.Waiting = true;
        home.Data.Unit(0).AuxiliaryTimer = 1; home.Data.Unit(0).Counter = 8;
        using var reset = home.Reset.Begin(1, "reset"); home.Complete(reset);
        Equal(new MapPoint(16, 10), home.Data.Unit(0).Position); Equal((byte)6, home.Data.Unit(0).MovesUsed);
        Equal(true, home.Data.Unit(0).Flags.Waiting); Equal(true, home.Data.Unit(0).Flags.Veteran);
        foreach (bool visible in new[] { false, true })
        {
            var f = new UnitTurnFixture(); f.World.Multiplayer.LocalHuman = 2;
            f.Data.Unit(0).Damage = 5; f.Data.Unit(0).Visibility = visible ? CivilizationSet.Only(2) : default;
            f.Data.AddUnit(1, 2, 4, new(12, 10)); f.Data.Unit(1).Damage = 5; f.Data.Unit(1).Flags.Moved = true;
            f.Recovery.Run(1); Equal((byte)4, f.Data.Unit(0).Damage); Equal(visible ? 1 : 0, f.Draws.Count);
            Equal((byte)5, f.Data.Unit(1).Damage); Equal(true, f.Data.Unit(1).Flags.Moved);
        }
    }
    private static void UnitResetActualLossAndContinuation()
    {
        var f = new UnitTurnFixture(); f.City(new(16, 10));
        f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).AuxiliaryTimer = 1; f.Data.Unit(0).Counter = 255;
        f.Data.AddUnit(1, 1, 4, new(12, 10)); f.Data.Unit(1).MovesUsed = 4;
        using var op = f.Reset.Begin(-1, "reset"); f.Complete(op);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal((byte)0, f.Data.Unit(1).MovesUsed); Equal(2, f.World.Entities.Units.End);
        Equal(0, f.Incidents.Notices.Count);
    }
    private static void RecoveryHealingFactors()
    {
        foreach (var (fortress, cityDistance, building, healed) in new[] { (false, -1, 0, 1), (true, -1, 0, 2),
            (false, 3, 0, 2), (false, 3, BuildingIds.Barracks, 3), (false, 0, 0, 4), (false, 0, BuildingIds.Barracks, 8) })
        {
            var f = new UnitTurnFixture(); f.Data.Unit(0).Damage = 8; f.Data.Unit(0).Orders = UnitOrder.Sleeping;
            if (fortress) f.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.Fortification;
            if (cityDistance >= 0) f.City(new(10 + cityDistance * 2, 10), building);
            Equal(0, f.Recovery.Run(1).Count); Equal((byte)(8 - healed), f.Data.Unit(0).Damage);
            Equal(healed == 8 ? UnitOrder.None : UnitOrder.Sleeping, f.Data.Unit(0).Orders); Equal(1, f.Draws.Count);
        }
    }
    private static void RecoveryNearestAndRegions()
    {
        var f = new UnitTurnFixture(); f.Data.Unit(0).Damage = 8; f.City(new(16, 10), BuildingIds.Barracks);
        f.World.Map.TileAt(new(16, 10)).Region = 2;
        f.Recovery.Run(1); Equal((byte)5, f.Data.Unit(0).Damage); // Origin-region query still selects the cross-region city.
        var ocean = new UnitTurnFixture(); ocean.Data.Unit(0).Damage = 8; ocean.City(new(10, 10), BuildingIds.Barracks);
        ocean.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean; ocean.World.Map.TileAt(new(10, 10)).Region = 7;
        ocean.Data.Unit(0).Orders = UnitOrder.Sleeping;
        ocean.Recovery.Run(1); Equal((byte)0, ocean.Data.Unit(0).Damage); // C01 disables its region gate on Ocean.
        Equal(UnitOrder.Sleeping, ocean.Data.Unit(0).Orders);
    }
    private static void RecoveryFacilitiesAndAirbases()
    {
        foreach (var (domain, building) in new[] { (RuleDomain.Air, BuildingIds.Airport), (RuleDomain.Sea, BuildingIds.PortFacility) })
        {
            var f = new UnitTurnFixture(); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = domain };
            f.Data.Unit(0).Damage = 8; f.Data.Unit(0).Orders = UnitOrder.Sleeping; f.City(new(10, 10), building);
            f.Recovery.Run(1); Equal((byte)0, f.Data.Unit(0).Damage); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
        }
        foreach (byte owner in new byte[] { 0, 2 })
        {
            var f = new UnitTurnFixture(); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air };
            f.Data.Unit(0).Damage = 8; f.Data.Unit(0).Orders = UnitOrder.Sleeping;
            f.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.CitySite | MapFeatures.Fortification;
            f.World.Map.TileAt(new(10, 10)).Owner = owner;
            f.Recovery.Run(1); Equal((byte)(owner == 0 ? 7 : 0), f.Data.Unit(0).Damage);
            Equal(owner == 0 ? UnitOrder.Sleeping : UnitOrder.None, f.Data.Unit(0).Orders);
        }
    }
    private static void RecoveryMovedAirAndResetOrder()
    {
        foreach (bool cityComponent in new[] { false, true })
        {
            var f = new UnitTurnFixture(); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air, Range = 0, HitPoints = 20 };
            f.Data.Unit(0).Damage = 18; f.Data.Unit(0).Flags.Moved = f.Data.Unit(0).Flags.Paradropped = true;
            f.Data.Unit(0).MovesUsed = 4; f.Data.Unit(0).AuxiliaryTimer = 7;
            if (cityComponent) f.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.CitySite | MapFeatures.Fortification;
            using var reset = f.Reset.Begin(-1, "reset"); f.Complete(reset); Equal(true, f.Data.Unit(0).Flags.Moved);
            f.Recovery.Run(1); Equal((byte)(cityComponent ? 18 : 19), f.Data.Unit(0).Damage); Equal(0, f.Draws.Count);
            Equal(false, f.Data.Unit(0).Flags.Moved); Equal(false, f.Data.Unit(0).Flags.Paradropped); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        }
    }
    private static void RecoverySignedHpAndFacilityRetention()
    {
        foreach (var (hp, damage) in new[] { (9, 8), (246, 9) })
        {
            var f = new UnitTurnFixture(); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { HitPoints = (byte)hp };
            f.Data.Unit(0).Damage = 8; f.Recovery.Run(1); Equal((byte)damage, f.Data.Unit(0).Damage);
        }
        foreach (var (hp, expected) in new[] { (0, 255), (246, 245) })
        {
            var f = new UnitTurnFixture(); f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Air, HitPoints = (byte)hp };
            f.Data.Unit(0).Flags.Moved = true; f.Recovery.Run(1); Equal((byte)expected, f.Data.Unit(0).Damage);
        }
        var retained = new UnitTurnFixture(); retained.City(new(10, 10), BuildingIds.Barracks);
        retained.Data.Unit(0).Damage = 8; retained.Data.AddUnit(1, 1, 5, new(10, 10));
        retained.Data.Rules.Units[5] = retained.Data.Rules.Units[4] with { Domain = (RuleDomain)7 }; retained.Data.Unit(1).Damage = 8;
        retained.Recovery.Run(1); Equal((byte)0, retained.Data.Unit(1).Damage);
        var missing = new UnitTurnFixture(); missing.City(new(10, 10), BuildingIds.Barracks);
        missing.Data.Rules.Units[4] = missing.Data.Rules.Units[4] with { Domain = (RuleDomain)7 }; missing.Data.Unit(0).Damage = 8;
        missing.Data.Unit(0).Flags.Paradropped = true;
        Throws<InvalidOperationException>(() => missing.Recovery.Run(1)); Equal(false, missing.Data.Unit(0).Flags.Paradropped);
        missing.Recovery.Run(1, BuildingIds.Barracks); Equal((byte)0, missing.Data.Unit(0).Damage);
    }
    private static void RecoveryWakeAndDiagnostics()
    {
        var f = new UnitTurnFixture(); f.Data.Unit(0).Damage = 1; f.Data.Unit(0).Orders = UnitOrder.Sleeping;
        f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean; f.World.Map.TileAt(new(10, 10)).Region = 7;
        f.FailDraw = true; Equal(1, f.Recovery.Run(1).Count); Equal(UnitOrder.Sleeping, f.Data.Unit(0).Orders); Equal((byte)0, f.Data.Unit(0).Damage);
        f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Grassland; Equal(0, f.Recovery.Run(1).Count); Equal(UnitOrder.Sleeping, f.Data.Unit(0).Orders);
        f.World.Globals.Multiplayer = GameMode.Tcp; f.Data.Unit(0).Flags.Moved = true;
        Throws<InvalidOperationException>(() => f.Recovery.Run(1)); Equal(true, f.Data.Unit(0).Flags.Moved);
    }
    private static void UnitResetPrivateIncident()
    {
        foreach (bool declare in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(733)); ParticipantId victimSeat = default;
            var f = new UnitTurnFixture(data: new(runtime.Process, runtime.World)); f.City(new(16, 10));
            f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2; f.Incidents.Treaty();
            f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).AuxiliaryTimer = 1; f.Data.Unit(0).Counter = 2;
            f.Data.AddUnit(1, 1, 4, new(12, 10)); f.Data.Unit(1).MovesUsed = 4;
            runtime.RegisterCommand(new(new(1, "test.reset", "Reset units", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
                _ => new UnitTurnResetOperation(f.Reset.Begin(-1, "reset"), _ => victimSeat)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2); victimSeat = victim.Identity.Participant;
            var command = actor.Submit(new("test.reset", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, f.Data.Unit(0).Token.Occupied); Equal((byte)4, f.Data.Unit(1).MovesUsed);
            Equal(false, actor.Reply(prompt, 1).Accepted);
            if (declare) victim.Reply(prompt, 1); else victim.Cancel(command);
            for (int n = 0; n < 20 && RequiredResult(actor, command).Status != CommandStatus.Completed; ++n) runtime.AdvanceOne();
            Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal((byte)0, f.Data.Unit(1).MovesUsed);
            Equal(declare, f.World.Civilizations[2].Treaties[1].War); Equal(false, victim.Reply(prompt, 1).Accepted);
            f.Life.Random.RequireChoicesConsumed();
        }
    }
    private static void UnitResetFailureAndFallback()
    {
        var f = new UnitTurnFixture(); f.Data.Unit(0).Type = new(47); f.Data.Unit(0).Flags.SpyMission = true;
        f.Data.Unit(0).AuxiliaryTimer = 1; f.Data.Unit(0).Counter = 2; f.World.Civilizations[1].Treaties[2].SpyChat = true;
        using var failure = f.Reset.Begin(1, "reset"); Throws<ChoiceException>(() => failure.Resume());
        Equal(false, f.Data.Unit(0).Flags.SpyMission); Equal(false, f.World.Civilizations[1].Treaties[2].SpyChat);
        Equal((byte)3, f.Data.Unit(0).MovesUsed); Throws<InvalidOperationException>(() => failure.Resume());
        var off = new UnitTurnFixture(); off.Data.Stacks.Relocate(0, new(-2, 10), false);
        using var reset = off.Reset.Begin(-1, "reset"); off.Complete(reset); Equal(true, off.World.Map.TileAt(new(-2, 10)).Unit);
        var remote = new UnitTurnFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        remote.Data.Unit(0).MovesUsed = 4; using var rejected = remote.Reset.Begin(-1, "reset");
        Throws<InvalidOperationException>(() => rejected.Resume()); Equal((byte)4, remote.Data.Unit(0).MovesUsed);
        var pending = new UnitTurnFixture(); pending.City(new(16, 10)); pending.Incidents.Treaty();
        pending.World.Globals.HumanPlayers = CivilizationSet.Only(2); pending.World.Multiplayer.LocalHuman = 2;
        pending.Data.Unit(0).Flags.SpyMission = true; pending.Data.Unit(0).AuxiliaryTimer = 1; pending.Data.Unit(0).Counter = 2;
        pending.Data.AddUnit(1, 1, 4, new(12, 10)); pending.Data.Unit(1).MovesUsed = 4;
        using var stopped = pending.Reset.Begin(-1, "reset"); Equal(true, stopped.Resume() is EspionagePretext);
        stopped.Dispose(); Equal(false, pending.Data.Unit(0).Token.Occupied); Equal((byte)4, pending.Data.Unit(1).MovesUsed);
        Throws<ObjectDisposedException>(() => stopped.Resume());
    }
    private static void LocalTurnResetRecoveryAdmission()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame(); var stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var units = stored.Units.ToArray(); var first = StateConversion.ToLive(units[0]);
            first.Flags.SpyMission = true; units[0] = StateConversion.ToStored(first);
            Equal(LocalLoadResult.UnsupportedGame, game.LoadClassic(ClassicFiles.EncodeStoredSave(stored with { Units = new(units) })));
            first.Flags.SpyMission = false; first.Flags.Moved = first.Flags.Paradropped = true;
            units[0] = StateConversion.ToStored(first);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(stored with { Units = new(units) })));
            EndLocalTurn(game, console, stored.Globals.Turn + 1);
            var after = StateConversion.ToLive(ClassicFiles.DecodeStoredSave(game.SaveClassic()).Units[0]);
            Equal(false, after.Flags.Moved); Equal(false, after.Flags.Paradropped); Equal((byte)0, after.MovesUsed);
        }
    }

}
