using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V05 §8 city items; actual S02, U01/V01, C06 and C08 children.
    private sealed class CessionFixture : ICaptureProductionEffects
    {
        public EventFixture Events { get; }
        public RevelationFixture Data => Events.Data;
        public GameState World => Data.World;
        public DiplomaticCities Transfers { get; }
        public List<MapPoint> Neighbors { get; } = [];
        public List<MapPoint> Redraws { get; } = [];
        public List<LeonardoUpgradeNotice> Upgrades { get; } = [];
        public bool FailView;
        public CessionFixture(RevelationFixture? data = null)
        {
            Events = new(data); World.Entities.Units.SetEnd(0); World.Entities.Cities.SetEnd(0);
            foreach (ref var tile in World.Map.Tiles) { tile.Features = default; tile.RevealedTo = default; }
            for (int c = 0; c < EntityLimits.Civilizations; ++c) { World.Civilizations[c] = default; World.UnitTotals[c] = 0; }
            World.Globals.Turn = 257; World.Globals.Multiplayer = GameMode.SinglePlayer;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            World.Globals.HumanPlayers = CivilizationSet.Only(1); World.Multiplayer.LocalHuman = 1;
            World.Civilizations[1].Government = Government.Despotism; World.Civilizations[1].Treaties[2].Embassy = true;
            for (int type = 0; type < RuleLimits.UnitTypes; ++type)
                Data.Rules.Units[type] = Data.Rules.Units[type] with { Prerequisite = AdvanceReference.Never, ObsoleteBy = AdvanceReference.None };
            Data.Rules.Units[4] = Data.Rules.Units[4] with { Prerequisite = AdvanceReference.None, Domain = RuleDomain.Land, Role = RuleRole.Attack, Movement = 3 };
            Transfers = new(World, Data.Rules, Events.Life.Elimination, Events.Life.Loss,
                Events.Contact.F.T.T.Damage.Incidents.Incidents, Events.Triggers,
                new(World, Data.Rules, Upgrades.Add, Redraw), this);
            City(0, 2, new(10, 10));
        }
        public void City(int slot, int owner, MapPoint point)
        { Data.AddCity(slot, owner, point); World.Entities.Cities.Storage[slot].Production = 4; World.Civilizations[owner].CityCount++; }
        public void Unit(int slot, int owner, MapPoint point, CitySlot? home = null)
        {
            Data.AddUnit(slot, owner, 4, point); Data.Unit(slot).Home = home; Data.Unit(slot).MovesUsed = 5; Data.Unit(slot).Orders = UnitOrder.Fortified;
            World.Civilizations[owner].UnitsActive[4]++; World.Civilizations[owner].MilitaryCount++; World.UnitTotals[owner]++;
        }
        public DiplomaticCities.Operation Begin(int recipient = 1, params CityToken[] tokens) =>
            Transfers.Begin(new(tokens.Length == 0 ? [new(1)] : tokens), recipient, "cession");
        public void Complete(DiplomaticCities.Operation op)
        { for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume()); Equal(true, op.Completed); Events.Consumed(); }
        public void RedrawNeighbors(MapPoint point) { Neighbors.Add(point); if (FailView) throw new IOException("No view"); }
        public void Redraw(MapPoint point) { Redraws.Add(point); if (FailView) throw new IOException("No view"); }
    }
    private static void DiplomaticCityBasicTransfer()
    {
        var f = new CessionFixture(); var w = f.World;
        foreach (int building in new[] { BuildingIds.Palace, BuildingIds.Temple, BuildingIds.Cathedral, BuildingIds.Courthouse, BuildingIds.Granary })
            BuildingQueries.SetBuilding(w, new(0), building, true);
        f.Unit(0, 2, new(10, 10), new(0)); f.Unit(1, 3, new(10, 10));
        w.Civilizations[1].UnitsActive[4] = 255; w.Civilizations[1].MilitaryCount = short.MaxValue; w.UnitTotals[1] = int.MaxValue;
        using var op = f.Begin(); f.Complete(op);
        Equal((byte)1, w.Entities.Cities.Storage[0].Owner.Value); Equal((byte)2, w.Entities.Cities.Storage[0].Founder.Value);
        Equal((byte)1, w.Entities.Cities.Storage[0].TurnsCaptured); Equal((short)257, w.Civilizations[1].LastCityTurn);
        Equal((short)1, w.Civilizations[1].CityCount); Equal((short)0, w.Civilizations[2].CityCount);
        Equal(false, BuildingQueries.HasBuilding(w, new(0), BuildingIds.Palace)); Equal(true, BuildingQueries.HasBuilding(w, new(0), BuildingIds.Granary));
        Equal(true, w.Entities.Cities.Storage[0].KnownTo.Contains(2)); Equal((byte)4, w.Entities.Cities.Storage[0].RememberedSize[2]);
        Equal((byte)1, f.Data.Unit(0).Owner.Value); Equal(new CitySlot(0), f.Data.Unit(0).Home!.Value);
        Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal(true, f.Data.Unit(0).Visibility.Contains(1));
        Equal((byte)3, f.Data.Unit(1).Owner.Value); Equal((byte)0, w.Civilizations[1].UnitsActive[4]);
        Equal(short.MinValue, w.Civilizations[1].MilitaryCount); Equal(int.MinValue, w.UnitTotals[1]);
        Equal((short)0, w.Civilizations[2].MilitaryCount); Equal(0, w.UnitTotals[2]);
        foreach (var offset in MapNeighborhoods.City)
        {
            var point = MapNeighborhoods.Offset(w.Map.Geometry, new(10, 10), offset, true);
            Equal((byte)1, w.Map.TileAt(point).Claimant); Equal(true, w.Map.TileAt(point).RevealedTo.Contains(1));
        }
        Equal(new MapPoint(10, 10), f.Neighbors.Single()); Equal(null, op.Resume(new(0)));
    }
    private static void DiplomaticCityOfferLookupAndDuplicates()
    {
        var f = new CessionFixture(); f.City(2, 3, new(20, 10)); f.World.Entities.Cities.Storage[2].Token = new(1);
        f.City(3, 2, new(24, 10)); f.World.Entities.Cities.Storage[3].Token = default;
        using var op = f.Begin(1, new(99), new(1), new(1)); f.Complete(op);
        Equal((byte)2, f.World.Entities.Cities.Storage[0].Owner.Value); Equal((byte)1, f.World.Entities.Cities.Storage[2].Owner.Value);
        Equal((byte)1, f.World.Entities.Cities.Storage[2].Founder.Value); Equal(2, f.Neighbors.Count);
        Equal((short)1, f.World.Civilizations[1].CityCount); Equal((short)0, f.World.Civilizations[3].CityCount);
    }
    private static void DiplomaticCityMutableIndex()
    {
        var f = new CessionFixture(); f.City(1, 1, new(10, 10));
        f.World.Entities.Cities.Storage[0].Production = 5; f.World.Entities.Cities.Storage[1].Production = 6;
        f.Unit(2, 2, new(10, 10), new(0)); f.Unit(1, 2, new(14, 10), new(1)); f.Unit(0, 2, new(16, 10), new(0));
        using var op = f.Begin(); f.Complete(op);
        Equal(new CitySlot(1), op.CurrentCity!.Value); Equal(new CitySlot(1), f.Data.Unit(2).Home!.Value);
        Equal(false, f.Data.Unit(1).Token.Occupied); Equal(true, f.Data.Unit(0).Token.Occupied);
        Equal((sbyte)5, f.World.Entities.Cities.Storage[0].Production); Equal((sbyte)0, f.World.Entities.Cities.Storage[1].Production);
        Equal((byte)0, f.World.Entities.Cities.Storage[0].TurnsCaptured); Equal((byte)1, f.World.Entities.Cities.Storage[1].TurnsCaptured);
        Equal((byte)2, f.World.Entities.Cities.Storage[1].Founder.Value);
    }
    private static void DiplomaticCityProductionRepair()
    {
        foreach (var (defense, cost, horse, selected) in new[]
            { (0, 8, false, 0), (1, 8, false, 61), (255, 8, false, 0), (0, 8, true, 61), (255, 255, false, 61), (1, 255, false, 0) })
        {
            var f = new CessionFixture(); f.World.Entities.Cities.Storage[0].Production = 5;
            f.Data.Rules.Units[61] = f.Data.Rules.Units[4] with
            { Role = RuleRole.Defend, Defense = (byte)defense, CostRows = (byte)cost, Abilities = new() { AntiHorseDefense = horse } };
            f.Data.Rules.Units[60] = f.Data.Rules.Units[61] with { Defense = 20 };
            using var op = f.Begin(); f.Complete(op);
            Equal((sbyte)selected, f.World.Entities.Cities.Storage[0].Production);
        }
        var zero = new CessionFixture(); zero.World.Entities.Cities.Storage[0].Production = 5;
        zero.Data.Rules.Units[61] = zero.Data.Rules.Units[4] with { Role = RuleRole.Defend, Defense = 1, CostRows = 0 };
        using var fault = zero.Begin(); Throws<DivideByZeroException>(() => fault.Resume());
        Equal((byte)1, zero.World.Entities.Cities.Storage[0].Owner.Value); Equal((sbyte)0, zero.World.Entities.Cities.Storage[0].Production);
        Equal((byte)0, zero.World.Entities.Cities.Storage[0].TurnsCaptured); Throws<InvalidOperationException>(() => fault.Resume());
    }
    private static Event CessionText(CessionFixture f)
    {
        f.World.Entities.Cities.Storage[0].Name = "Birch";
        var e = f.Events.Text("Transferred Birch"); e.Values.Trigger = EventTrigger.CityTaken;
        e.SetString(1, new("Birch")); e.Values.WhoCiv = e.Values.WhomCiv = -2;
        e.Values.Actions.JustOnce = true; f.World.Events.Loaded = true; return e;
    }
    private static void DiplomaticCityRetainedEvent()
    {
        var f = new CessionFixture(); var e = CessionText(f); var center = new MapPoint(10, 10);
        f.Unit(0, 2, center, new(0));
        using var op = f.Begin(); Equal(true, op.Resume() is ScenarioText);
        Equal((byte)1, f.World.Entities.Cities.Storage[0].Owner.Value); Equal((byte?)2, f.World.Map.Owner(center));
        Equal((byte)2, f.Data.Unit(0).Owner.Value); Equal(false, e.Values.Actions.Consumed); Equal(0, f.Neighbors.Count);
        f.World.Map.TileAt(center).RevealedTo = default;
        f.World.Map.Remembered(1, center) = MapFeatures.Irrigation;
        f.World.Globals.Turn = 9; f.Unit(1, 2, center); // Unit scan is captured after the real event decision.
        Equal(null, op.Resume(new(0))); f.Complete(op);
        Equal((byte?)1, f.World.Map.Owner(center)); Equal((byte)1, f.Data.Unit(0).Owner.Value); Equal((byte)1, f.Data.Unit(1).Owner.Value);
        Equal((byte)1, f.World.Entities.Cities.Storage[0].TurnsCaptured); Equal(true, e.Values.Actions.Consumed);
        Equal(true, f.Redraws.Contains(center)); Equal(true, f.World.Map.TileAt(center).RevealedTo.Contains(1));
        Equal(MapFeatures.Irrigation, f.World.Map.Remembered(1, center));
        Equal(1, e.Values.WhoCiv); Equal(2, e.Values.WhomCiv);
        var disposed = new CessionFixture(); var unconsumed = CessionText(disposed); var stopped = disposed.Begin();
        Equal(true, stopped.Resume() is ScenarioText); stopped.Dispose(); Throws<ObjectDisposedException>(() => stopped.Resume(new(0)));
        Equal((byte)1, disposed.World.Entities.Cities.Storage[0].Owner.Value); Equal((byte?)2, disposed.World.Map.Owner(center));
        Equal(false, unconsumed.Values.Actions.Consumed);
    }
    private static void DiplomaticCityFailurePrefixes()
    {
        var f = new CessionFixture(); CessionText(f); f.Unit(0, 2, new(10, 10), new(0));
        using var missing = f.Begin(); Equal(true, missing.Resume() is ScenarioText);
        // An accepted decision can change the city again; a missing nearest result cannot address city[-1].
        f.World.Entities.Cities.Storage[0].Owner = new(3);
        var before = f.World.Civilizations[7]; Throws<InvalidOperationException>(() => missing.Resume(new(0)));
        Equal(null, missing.CurrentCity); Equal((byte)1, f.Data.Unit(0).Owner.Value); Equal(null, f.Data.Unit(0).Home);
        Equal(1, f.World.UnitTotals[1]); Equal(before, f.World.Civilizations[7]); Equal((byte)0, f.World.Entities.Cities.Storage[0].TurnsCaptured);
        Throws<InvalidOperationException>(() => missing.Resume());
        var media = new CessionFixture(); var e = CessionText(media); e.Values.Actions.PlayWaveFile = true;
        e.SetString(34, new("bad.wav")); media.World.Globals.GameOptions.Sound = true; media.Events.Media.FailProbe = true;
        using var bad = media.Begin(); Throws<IOException>(() => bad.Resume());
        Equal((byte)1, media.World.Entities.Cities.Storage[0].Owner.Value); Equal((byte?)2, media.World.Map.Owner(new(10, 10)));
        Equal(false, e.Values.Actions.Consumed); Throws<InvalidOperationException>(() => bad.Resume());
        var cosmetic = new CessionFixture { FailView = true }; using var child = cosmetic.Begin();
        using var adapter = new DiplomaticCitiesOperation(child, c => new((ulong)c));
        var result = adapter.Resume(cosmetic.Events.Context(), new());
        Equal(CommandStatus.Failed, result.Status); Equal("city_transfer_presentation_failed", result.Code);
        Equal(true, child.Completed); Equal((byte)1, cosmetic.World.Entities.Cities.Storage[0].TurnsCaptured);
        Equal(1, child.PresentationFailures.Count); Equal(result, adapter.Resume(cosmetic.Events.Context(), new()));
        var remote = new CessionFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = remote.Begin(); Throws<InvalidOperationException>(() => rejected.Resume());
        Equal((byte)2, remote.World.Entities.Cities.Storage[0].Owner.Value);
    }
    private static void DiplomaticCityCargoAndLeonardo()
    {
        var f = new CessionFixture(); f.City(1, 2, new(24, 10));
        f.Data.Rules.Units[4] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Sea, Capacity = 2 };
        f.Data.Rules.Units[10] = f.Data.Rules.Units[4] with { Domain = RuleDomain.Land };
        f.World.Map.TileAt(new(18, 10)).Terrain = Terrain.Ocean;
        f.Unit(0, 2, new(18, 10), new(0)); f.Unit(1, 2, new(18, 10)); f.Data.Unit(1).Type = new(10);
        f.World.Civilizations[2].UnitsActive[4] = 1; f.World.Civilizations[2].UnitsActive[10] = 1;
        using (var op = f.Begin()) { f.Complete(op); }
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(false, f.Data.Unit(1).Token.Occupied);
        Equal(true, f.World.Globals.CivsAlive.Contains(2)); Equal(false, f.World.UnitDeletion.CargoActive);
        var upgrade = new CessionFixture(); upgrade.Unit(0, 2, new(10, 10));
        upgrade.World.Globals.WonderCity[WonderIds.Leonardo] = 0;
        upgrade.Data.Rules.WonderObsolescence[WonderIds.Leonardo] = AdvanceReference.None;
        upgrade.World.Civilizations[1].Advances = upgrade.World.Civilizations[1].Advances.With(53);
        upgrade.Data.Rules.Units[4] = upgrade.Data.Rules.Units[4] with { ObsoleteBy = (AdvanceReference)53 };
        upgrade.Data.Rules.Units[61] = upgrade.Data.Rules.Units[4] with { Prerequisite = (AdvanceReference)53, ObsoleteBy = AdvanceReference.None };
        using (var op = upgrade.Begin()) { upgrade.Complete(op); }
        Equal((byte)61, upgrade.Data.Unit(0).Type.Value); Equal(1, upgrade.Upgrades.Count);
        Equal((byte)1, upgrade.World.Entities.Cities.Storage[0].TurnsCaptured);
    }
    private static void DiplomaticCityPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(258)); var f = new CessionFixture(new(runtime.Process, runtime.World));
        f.World.Entities.Cities.Storage[0].Name = "Birch"; var e = CaptureTrigger(f.Events);
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.city-transfer", "City transfer", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => new DiplomaticCitiesOperation(f.Begin(), _ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.city-transfer", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(CommandStatus.AwaitingInteraction, RequiredResult(actor, command).Status);
        var grant = RequiredInteraction(human, command); Equal(false, actor.Reply(grant, 0).Accepted); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, human.Reply(grant, 0).Accepted); runtime.AdvanceOne(); Equal(false, human.Reply(grant, 0).Accepted);
        var text = RequiredInteraction(human, command); Equal(false, e.Values.Actions.Consumed);
        Equal(true, human.Reply(text, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, e.Values.Actions.Consumed); f.Events.Consumed();
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            var local = new CessionFixture(); var entry = CessionText(local);
            using var adapter = new DiplomaticCitiesOperation(local.Begin(), c => new((ulong)c));
            Equal(CommandStatus.AwaitingInteraction, adapter.Resume(local.Events.Context(), new()).Status);
            Equal(CommandStatus.Completed, adapter.Resume(local.Events.Context(), new(input)).Status); Equal(true, entry.Values.Actions.Consumed);
        }
    }
}
