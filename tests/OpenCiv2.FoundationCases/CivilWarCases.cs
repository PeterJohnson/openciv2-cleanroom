using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CivilWarFixture : ICivilWarEffects
    {
        public RevelationFixture Data { get; } = CreationFixture();
        public GameState World => Data.World;
        public GameplayRandom Random { get; }
        public CivilWar War { get; }
        public List<CivilWarNotice> Notices { get; } = [];
        public List<ulong> Events { get; } = [];
        public int Created = -1, Redraws;
        public bool FailNotice, FailRedraw;
        public CivilWarFixture(bool creates = true, int candidate = 7)
        {
            var choices = creates ? CreationAttitudes() : [];
            if (creates) CreationChoice(choices, "gender", 3, 1);
            Random = CreationRandom(choices);
            World.Globals.Turn = 20; World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(candidate);
            for (int i = 1; i < 8; ++i) World.Civilizations[i].CityCount = 1;
            World.Civilizations[2].CityCount = World.Civilizations[candidate].CityCount = 0;
            Data.Process.Rules.Leaders[candidate].Color = (byte)candidate;
            War = new(World, Data.Stacks, owner => { Created = owner; return Creation(Data, owner, Random); }, e => Events.Add(e.Identity), this);
        }
        public void City(int slot, MapPoint point, int size, int region = 1, bool palace = false, int owner = 2)
        {
            Data.AddCity(slot, owner, point, size); World.Map.TileAt(point).Region = (byte)region;
            ++World.Civilizations[owner].CityCount;
            World.Civilizations[owner].TotalCitySize = unchecked((short)(World.Civilizations[owner].TotalCitySize + size));
            if (palace) BuildingQueries.SetBuilding(World, new((ushort)slot), BuildingIds.Palace, true);
        }
        public void Schism(CivilWarNotice notice) { Notices.Add(notice); if (FailNotice) throw new InvalidOperationException("Schism notice failed"); }
        public void RedrawMaps() { ++Redraws; if (FailRedraw) throw new InvalidOperationException("Map refresh failed"); }
        public void Complete(CivilWar.Operation op)
        { int limit = 20; while (op.Advance(1) is null && --limit > 0) { } Equal(true, limit > 0); Equal(1, op.Result); Random.RequireChoicesConsumed(); }
    }

    // C09 arithmetic branches and C01's explicit old owner, signed16 wrap, and intentionally narrow mutation.
    private static void CaptureQueriesAndElementaryTransfer()
    {
        var f = CreationFixture(); var w = f.World; f.AddCity(0, 2, new(4, 4), 3);
        w.Civilizations[2].TotalCitySize = 10; w.Civilizations[2].Gold = 100;
        Equal(27, CityCaptureQueries.TreasuryShare(w, new(0))); w.Civilizations[2].Gold = 10667;
        Equal(2907, CityCaptureQueries.TreasuryShare(w, new(0))); w.Civilizations[2].Gold = -100;
        Equal(32000, CityCaptureQueries.TreasuryShare(w, new(0))); w.Civilizations[2].Gold = 200000;
        Equal(54543, CityCaptureQueries.TreasuryShare(w, new(0)));
        w.Entities.Cities.Storage[0].Size = 0; Throws<DivideByZeroException>(() => CityCaptureQueries.TreasuryShare(w, new(0)));
        w.Entities.Cities.Storage[0].Size = 3; w.Civilizations[2].TotalCitySize = -1;
        Throws<DivideByZeroException>(() => CityCaptureQueries.TreasuryShare(w, new(0)));
        w.Civilizations[2].CityCount = 8; w.Civilizations[2].TotalCitySize = 10;
        w.Civilizations[3].CityCount = short.MinValue; w.Civilizations[3].TotalCitySize = short.MinValue;
        w.Civilizations[4].CityCount = short.MaxValue; w.Civilizations[4].TotalCitySize = short.MaxValue;
        ref var city = ref w.Entities.Cities.Storage[0]; city.Founder = new(5); city.FoodBox = 44; city.ShieldBox = 55;
        city.Flags.Disorder = city.Flags.ImprovementSold = true; city.TurnsCaptured = 77; city.Name = "Retained";
        BuildingQueries.SetBuilding(w, new(0), BuildingIds.Palace, true);
        CityTransfer.Transfer(w, new(0), 3, 4);
        Equal(short.MaxValue, w.Civilizations[3].CityCount); Equal(short.MinValue, w.Civilizations[4].CityCount);
        Equal((short)32765, w.Civilizations[3].TotalCitySize); Equal((short)-32766, w.Civilizations[4].TotalCitySize);
        Equal((short)8, w.Civilizations[2].CityCount); Equal((short)10, w.Civilizations[2].TotalCitySize);
        Equal(new CivilizationSlot(4), city.Owner); Equal((byte?)4, w.Map.Owner(new(4, 4)));
        Equal(new CivilizationSlot(5), city.Founder); Equal((short)44, city.FoodBox); Equal((short)55, city.ShieldBox);
        Equal(true, city.Flags.Disorder && city.Flags.ImprovementSold); Equal((byte)77, city.TurnsCaptured); Equal("Retained", city.Name);
        Equal(true, BuildingQueries.HasBuilding(w, new(0), BuildingIds.Palace)); Equal(default(CivilizationSet), city.KnownTo);
        CityTransfer.Transfer(w, new(0), 4, 4); Equal(short.MinValue, w.Civilizations[4].CityCount); Equal((short)-32766, w.Civilizations[4].TotalCitySize);
        f.AddCity(1, 4, new(20, 4), 9); Equal(new CitySlot(0), CityCaptureQueries.CentralCity(w, 4));
        f.AddCity(2, 4, new(12, 4), 1); Equal(new CitySlot(2), CityCaptureQueries.CentralCity(w, 4));
        Equal(null, CityCaptureQueries.CentralCity(w, 260));
    }

    private static void CivilWarVetoAndRefusal()
    {
        var f = new CivilWarFixture(false); f.City(0, new(4, 4), 9, palace: true);
        var events = f.World.Events.Collection; f.World.Events.Loaded = false;
        var ignored = events.Append(); ignored.Values.Trigger = EventTrigger.CityTaken;
        var first = events.Append(); first.Values.Trigger = EventTrigger.NoSchism; first.Values.WhomCiv = -2;
        first.Values.Actions.JustOnce = first.Values.Actions.Consumed = true;
        var wrong = events.Append(); wrong.Values.Trigger = EventTrigger.NoSchism; wrong.Values.WhomCiv = 3;
        var last = events.Append(); last.Values.Trigger = EventTrigger.NoSchism; last.Values.WhomCiv = 2;
        using var veto = f.War.Begin(2); Equal(0, veto.Advance()); Sequence(new[] { first.Identity, last.Identity }, f.Events);
        Equal(-2, first.Values.WhomCiv); Equal(-1, f.Created); Equal(true, f.World.Globals.CivsAlive.Contains(7));
        var noSlot = new CivilWarFixture(false); noSlot.City(0, new(4, 4), 9, palace: true); noSlot.World.Civilizations[7].MilitaryCount = 1;
        using var full = noSlot.War.Begin(2); Equal(0, full.Advance()); Equal(-1, noSlot.Created);
        var noPalace = new CivilWarFixture(false); noPalace.City(0, new(4, 4), 9);
        using var absent = noPalace.War.Begin(2); Equal(0, absent.Advance()); Equal(-1, noPalace.Created);
        var human = new CivilWarFixture(false); human.City(0, new(4, 4), 9, palace: true);
        human.World.Globals.HumanPlayers = human.World.Globals.HumanPlayers.With(7);
        using var refused = human.War.Begin(2); Equal(0, refused.Advance()); Equal(7, human.Created);
        Equal(false, human.World.Globals.CivsAlive.Contains(7)); Equal(0, human.Notices.Count);
        foreach (var fixture in new[] { f, noSlot, noPalace, human }) fixture.Random.RequireChoicesConsumed();
        var lifetime = new CivilWarFixture(false); var entry = lifetime.World.Events.Collection.Append();
        entry.Values.Trigger = EventTrigger.NoSchism; entry.Values.WhomCiv = 2;
        Throws<InvalidOperationException>(() => ScenarioPermissions.AllowSchism(lifetime.World, 2, _ => lifetime.World.Events.Collection.Reset()));
    }

    private static void CivilWarRegionsKnowledgeAndUnits()
    {
        var f = new CivilWarFixture(); var w = f.World;
        f.City(0, new(2, 2), 12, palace: true); f.City(1, new(14, 2), 8, 2); f.City(2, new(16, 4), 3, 2);
        f.City(3, new(30, 10), 2, owner: 4); f.City(4, new(4, 4), 1);
        w.Civilizations[2].CapitalX = 2; w.Civilizations[2].Gold = 101; w.Civilizations[2].MilitaryStrength = ushort.MaxValue;
        w.Civilizations[2].Government = Government.Democracy; w.Civilizations[2].ResearchProgress = 123; w.Civilizations[2].NumAdvances = 7;
        w.Civilizations[2].Advances = default(AdvanceSet).With(5); w.Civilizations[7].Researching = 42; w.Civilizations[7].FutureTechs = 9;
        MapPoint explored = new(10, 12); w.Map.TileAt(explored).RevealedTo = CivilizationSet.Only(2);
        w.Map.TileAt(explored).Features = MapFeatures.Pollution; w.Map.Remembered(7, explored) = MapFeatures.Road;
        f.Data.AddUnit(0, 2, 4, new(14, 2)); f.Data.Unit(0).Home = new(0);
        f.Data.AddUnit(1, 4, 4, new(14, 2)); f.Data.Unit(0).Next = new(1); f.Data.Unit(1).Previous = new(0);
        f.Data.AddUnit(2, 2, 4, new(20, 10)); f.Data.Unit(2).Home = new(2);
        f.Data.AddUnit(3, 2, 4, new(2, 2)); f.Data.Unit(3).Home = new(1);
        f.Data.AddUnit(4, 2, 4, new(24, 10)); f.Data.AddUnit(5, 2, 4, new(30, 10)); f.Data.Unit(5).Home = new(1);
        using var op = f.War.Begin(2); f.Complete(op); Equal(7, f.Created); Equal(1, f.Notices.Count); Equal(1, f.Redraws);
        Equal((short)2, w.Civilizations[2].CityCount); Equal((short)13, w.Civilizations[2].TotalCitySize);
        Equal((short)2, w.Civilizations[7].CityCount); Equal((short)11, w.Civilizations[7].TotalCitySize);
        Equal(51, w.Civilizations[2].Gold); Equal(50, w.Civilizations[7].Gold); Equal((ushort)32767, w.Civilizations[7].MilitaryStrength);
        Equal(ushort.MaxValue, w.Civilizations[2].MilitaryStrength); Equal(Government.Democracy, w.Civilizations[7].Government);
        Equal((short)123, w.Civilizations[7].ResearchProgress); Equal((byte)7, w.Civilizations[7].NumAdvances);
        Equal(w.Civilizations[2].Advances, w.Civilizations[7].Advances); Equal((short)42, w.Civilizations[7].Researching); Equal((byte)9, w.Civilizations[7].FutureTechs);
        Equal(byte.MaxValue, w.Civilizations[7].AdvanceSource[5]); Equal(false, w.Globals.AdvanceKnownBy[5].Contains(7));
        Equal(true, w.Civilizations[2].Treaties[7].Aggressor && w.Civilizations[2].Treaties[7].ExtendedAggressor);
        Equal(false, w.Civilizations[7].Treaties[2].Aggressor || w.Civilizations[2].Treaties[7].WarThisTurn);
        for (int c = 1; c < 8; ++c) Equal((short)12, w.Civilizations[c].LastContactTurn[7]);
        Equal(true, w.Map.TileAt(explored).RevealedTo.Contains(7)); Equal(MapFeatures.Road, w.Map.Remembered(7, explored));
        Equal(true, w.Entities.Cities.Storage[0].KnownTo.Contains(7)); Equal(true, w.Entities.Cities.Storage[1].KnownTo.Contains(2));
        Equal(false, w.Entities.Cities.Storage[1].KnownTo.Contains(7)); Equal((byte)8, w.Entities.Cities.Storage[1].RememberedSize[2]);
        for (int i = 0; i < 3; ++i) Equal(new CivilizationSlot(7), f.Data.Unit(i).Owner);
        Equal(new CitySlot(1), f.Data.Unit(0).Home); Equal(null, f.Data.Unit(1).Home); Equal(new CitySlot(2), f.Data.Unit(2).Home);
        Equal(new CitySlot(0), f.Data.Unit(3).Home); Equal(new CivilizationSlot(2), f.Data.Unit(4).Owner); Equal(new CivilizationSlot(2), f.Data.Unit(5).Owner);
        Equal((byte)0, w.Civilizations[7].UnitsActive[4]); Equal(default(CivilizationSet), f.Data.Unit(0).Visibility);
        foreach (int city in new[] { 0, 1, 4 }) Equal(true, BuildingQueries.HasBuilding(w, new((ushort)city), BuildingIds.Palace));
        Equal((short)14, w.Civilizations[7].CapitalX); Equal((short)4, w.Civilizations[2].CapitalX); Equal(new CivilizationSlot(2), w.Entities.Cities.Storage[0].Owner);
        Equal(1, op.Advance()); Equal(1, f.Notices.Count);
    }

    private static void CivilWarFallbackAndRetainedCapital()
    {
        var f = new CivilWarFixture(); var w = f.World;
        f.City(0, new(2, 2), 8, palace: true); f.City(1, new(18, 2), 3); f.City(2, new(2, 18), 3); f.City(3, new(4, 2), 1);
        w.Civilizations[2].TotalCitySize = 30;
        using var op = f.War.Begin(2); Equal(null, op.Advance()); Equal(new CivilizationSlot(7), w.Entities.Cities.Storage[1].Owner);
        Equal(new CivilizationSlot(2), w.Entities.Cities.Storage[2].Owner); Equal(0, f.Redraws);
        Equal(null, op.Advance()); Equal(new CivilizationSlot(7), w.Entities.Cities.Storage[2].Owner);
        Equal(1, op.Advance()); Equal((short)24, w.Civilizations[2].TotalCitySize); Equal((short)6, w.Civilizations[7].TotalCitySize);
        Equal(new CivilizationSlot(2), w.Entities.Cities.Storage[3].Owner); // Distance one cannot win.
        for (int i = 0; i < 4; ++i) Equal(true, w.Entities.Cities.Storage[i].KnownTo.Contains(2) && w.Entities.Cities.Storage[i].KnownTo.Contains(7));
        f.Random.RequireChoicesConsumed();
        // Later Palace with matching capital X wins even with another Y; that changes the farthest-city choice.
        var multiple = new CivilWarFixture(); multiple.City(0, new(2, 2), 4, palace: true); multiple.City(1, new(2, 18), 4, palace: true);
        multiple.City(2, new(18, 2), 4); multiple.World.Civilizations[2].CapitalX = 2;
        using var selected = multiple.War.Begin(2); Equal(null, selected.Advance());
        Equal(new CivilizationSlot(7), multiple.World.Entities.Cities.Storage[2].Owner); multiple.Complete(selected);
    }

    private static void CivilWarFailuresAndAlias()
    {
        var notice = new CivilWarFixture { FailNotice = true }; notice.City(0, new(2, 2), 9, palace: true);
        notice.World.Civilizations[2].Gold = 101; using var failed = notice.War.Begin(2);
        Throws<InvalidOperationException>(() => failed.Advance()); Throws<InvalidOperationException>(() => failed.Advance());
        Equal(101, notice.World.Civilizations[2].Gold); Equal(true, notice.World.Globals.CivsAlive.Contains(7)); Equal(1, notice.Notices.Count); notice.Random.RequireChoicesConsumed();
        var redraw = new CivilWarFixture { FailRedraw = true }; redraw.City(0, new(2, 2), 9, palace: true); redraw.City(1, new(18, 2), 9);
        using var failedTail = redraw.War.Begin(2); Equal(null, failedTail.Advance());
        Throws<InvalidOperationException>(() => failedTail.Advance()); Throws<InvalidOperationException>(() => failedTail.Advance());
        Equal(new CivilizationSlot(7), redraw.World.Entities.Cities.Storage[1].Owner); Equal(1, redraw.Redraws); redraw.Random.RequireChoicesConsumed();
        var invalid = new CivilWarFixture(false); invalid.City(0, new(2, 2), 9, 64, true);
        using var region = invalid.War.Begin(2); Throws<ArgumentOutOfRangeException>(() => region.Advance()); Equal(-1, invalid.Created);
        var stopped = new CivilWarFixture(); stopped.City(0, new(2, 2), 9, palace: true); stopped.City(1, new(18, 2), 9);
        var partial = stopped.War.Begin(2); Equal(null, partial.Advance()); partial.Dispose();
        Throws<ObjectDisposedException>(() => partial.Advance()); Equal(new CivilizationSlot(7), stopped.World.Entities.Cities.Storage[1].Owner);
        Equal(0, stopped.Redraws); stopped.Random.RequireChoicesConsumed();
        var network = new CivilWarFixture(false); network.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = network.War.Begin(2); Throws<InvalidOperationException>(() => denied.Advance()); Equal(-1, network.Created);
        var alias = new CivilWarFixture(candidate: 2); alias.City(0, new(2, 2), 9, palace: true); alias.City(1, new(18, 2), 9);
        alias.World.Civilizations[2].CityCount = 0; alias.World.Civilizations[2].Gold = 101;
        using var same = alias.War.Begin(2); alias.Complete(same); Equal(2, alias.Created); Equal(0, alias.World.Civilizations[2].Gold);
        Equal((short)0, alias.World.Civilizations[2].CityCount); Equal(true, alias.World.Civilizations[2].Treaties[2].ExtendedAggressor);
    }

    private static void CivilWarSessionContinuation()
    {
        var f = new CivilWarFixture(); f.City(0, new(2, 2), 9, palace: true); f.City(1, new(18, 2), 9);
        int publications = 0, effects = 0;
        var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(5, "schism"),
            new(default, new(1)), default, (_, _) => ++effects, () => ++publications);
        using var op = new CivilWarOperation(f.War.Begin(2)); Equal(CommandStatus.Pending, op.Resume(context, new()).Status);
        Equal(CommandStatus.Completed, op.Resume(context, new(OperationInputKind.Cancel)).Status);
        Equal(CommandStatus.Completed, op.Resume(context, new()).Status); Equal(1, effects); Equal(1, publications); f.Random.RequireChoicesConsumed();
    }
}
