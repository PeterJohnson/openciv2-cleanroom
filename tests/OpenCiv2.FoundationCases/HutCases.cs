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
    private static Choice HutDraw(string site, int bound, int value, int index = 0) =>
        Choice.Create(new("W06.hut-" + site, "hut", index), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value));
    private static List<Choice> HutStart(int outcome, params Choice[] rest) => [HutDraw("outcome", 5, outcome), HutDraw("settlement-override", 3, 1), .. rest];
    private sealed class HutFixture : IHutEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public GameplayRandom Random { get; }
        public GrantFixture Grant { get; }
        public RetainedGrantFixture? Retained { get; }
        public HutEntry Huts { get; }
        public MovementContinuation Movement { get; }
        public MapPoint Point { get; } = new(16, 12);
        public List<HutNotice> Notices { get; } = [];
        public List<MapPoint> Draws { get; } = [];
        public int NeighborDraws, Status, CityLimits;
        public bool FailNotice, FailDraw;
        public HutFixture(List<Choice> choices, RevelationFixture? data = null, IReadOnlyDictionary<int, CivilizationSet>? backing = null, bool retainGrants = false)
        {
            Data = data ?? new(); Random = new(new ControlledChoices(new(choices)), "hut");
            World.Globals.Turn = 80; World.Globals.Year = 999; World.Globals.BarbarianActivity = 3;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2); World.Civilizations[1].Tribe = 0;
            World.Civilizations[1].CityCount = 1; World.Civilizations[1].ContinentCities[1] = 1;
            foreach (ref var tile in World.Map.Tiles) { tile.Region = 1; tile.Fertility = 15; tile.ResourceSuppressed = true; }
            World.Map.Regions[1] = new(480, 0, 0, default);
            for (int i = 0; i < RuleLimits.UnitTypes; ++i) Data.Rules.Units[i] = Data.Rules.Units[0] with { Domain = RuleDomain.Land, Role = RuleRole.Attack, CostRows = 1, Identity = new("type" + i, "Custom " + i) };
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Role = RuleRole.Settle };
            Data.Rules.Units[UnitTypeIds.Armor] = Data.Rules.Units[UnitTypeIds.Armor] with { Prerequisite = (AdvanceReference)AdvanceIds.MobileWarfare };
            for (int a = 0; a < RuleLimits.Advances; ++a) Data.Rules.Advances[a] = new() { Enabled = false, Identity = new("advance" + a, "Advance " + a), Prerequisites = new([AdvanceReference.None, AdvanceReference.None]) };
            Data.AddCity(0, 1, new(4, 4)); Data.AddUnit(0, 1, 9, Point);
            Grant = new(Data, Random);
            Retained = retainGrants ? new(Grant) : null;
            Huts = new(World, Data.Rules, Random, new(World, Data.Process, Data.Stacks, Data), CityCreator(Data, () => ++CityLimits), Retained?.Grants ?? Grant.Grants, this, backing);
            Movement = new(new(0), World.Entities.Units.End, Point) { LocalHuman = true };
        }
        public HutEntry.Operation Begin(string call = "hut") => Huts.Begin(new(0), Movement, call);
        public void Complete(HutEntry.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) { var visit = op.Resume(); if (visit is not null) op.Resume(0); }
            Equal(true, op.Completed); Random.RequireChoicesConsumed();
        }
        public void Known(params int[] advances) { foreach (int a in advances) World.Globals.AdvanceKnownBy[a] = CivilizationSet.Only(2); }
        public void Notice(HutNotice notice) { Notices.Add(notice); if (FailNotice) throw new InvalidOperationException("Required hut notice unavailable"); }
        public void RedrawNeighbors(MapPoint point) { Equal(Point, point); ++NeighborDraws; if (FailDraw) throw new InvalidOperationException("Map unavailable"); }
        public void Redraw(MapPoint point) { Draws.Add(point); if (FailDraw) throw new InvalidOperationException("Map unavailable"); }
        public void RefreshStatus() { ++Status; if (FailDraw) throw new InvalidOperationException("Status unavailable"); }
    }
    private static void HutMetalsAndAdmission()
    {
        foreach (var (year, difficulty, size, value, amount) in new[] { (1000, 0, 1, 0, 50), (1001, 0, 1, 0, 100),
            (999, 5, 0, 7, 25), (1000, 5, 0, 8, 100), (1001, 0, 0, 0, 50) })
        {
            var choices = HutStart(2, HutDraw("metals-size", 3, size)); if (size == 0) choices.Add(HutDraw("metals-value", 10, value));
            var f = new HutFixture(choices); f.World.Globals.Year = (short)year; f.World.Globals.Difficulty = (Difficulty)difficulty;
            f.World.Globals.Multiplayer = GameMode.Hotseat; f.World.Multiplayer.Saved.AutoplayActive = true;
            f.World.Civilizations[1].Gold = int.MaxValue - 10;
            Equal(false, f.World.Map.Hut(f.Point)); using var op = f.Begin(); f.Complete(op);
            Equal(unchecked(int.MaxValue - 10 + amount), f.World.Civilizations[1].Gold); Equal(amount, f.Notices.Single().Amount); Equal(1, f.Status);
            op.Resume(); Equal(1, f.Status); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        }
        foreach (int variant in new[] { 0, 1 })
        {
            var f = new HutFixture([]); if (variant == 0) f.Data.Unit(0).Owner = new(0); else f.Data.Rules.Units[9] = f.Data.Rules.Units[9] with { Domain = RuleDomain.Sea };
            using var op = f.Begin(); f.Complete(op); Equal(0, f.Notices.Count);
        }
        var repeated = new HutFixture([.. HutStart(2, HutDraw("metals-size", 3, 1)), .. HutStart(2, HutDraw("metals-size", 3, 1)).Select(c => c with { Key = c.Key with { Call = "second" } })]);
        repeated.Movement.LocalHuman = false; repeated.World.Multiplayer.LocalHuman = 2;
        using var first = repeated.Begin(); first.Resume(); using var second = repeated.Begin("second"); repeated.Complete(second);
        Equal(100, repeated.World.Civilizations[1].Gold); Equal(0, repeated.Status); Equal(0, repeated.Notices.Count);
    }
    private static void HutOverrideAndRegionalFlags()
    {
        for (int bits = 0; bits < 256; ++bits)
        { Equal((byte)bits, StateConversion.EncodeRegionalPresence(StateConversion.DecodeRegionalPresence((byte)bits))); }
        foreach (int bits in new[] { 0, 1, 32, 64, 128, 192 })
        {
            bool overrideCity = (bits & 127) == 0;
            var f = new HutFixture(overrideCity ? [HutDraw("outcome", 5, 2)] : [HutDraw("outcome", 5, 2), HutDraw("metals-size", 3, 1)]);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Civilizations[1].ContinentCities[1] = 0;
            f.World.Civilizations[1].ContinentFlags[1] = StateConversion.DecodeRegionalPresence((byte)bits);
            using var op = f.Begin(); f.Complete(op); Equal(overrideCity ? 2 : 1, f.World.Entities.Cities.End);
            Equal(overrideCity ? HutNoticeKind.Tribe : HutNoticeKind.Metals, f.Notices.Single().Kind);
        }
        var imported = new StoredCivilization { ContinentFlags = new(Enumerable.Repeat((byte)0xe0, 64)) };
        var live = StateConversion.ToLive(imported); Equal((byte)3, live.ContinentFlags[1].SettlementRestrictions);
        live.ContinentFlags[1].SettlementRestrictions = 1;
        Equal((byte)0xa0, StateConversion.PreserveOpaque(StateConversion.ToStored(live), imported).ContinentFlags[1]);
        var noCities = new HutFixture(HutStart(2, HutDraw("metals-size", 3, 1))); noCities.World.Civilizations[1].CityCount = 0;
        using var human = noCities.Begin(); noCities.Complete(human); Equal(50, noCities.World.Civilizations[1].Gold);
    }
    private static void HutTribeCreationAndFailures()
    {
        foreach (int year in new[] { 999, 1000, 1001 })
        {
            var choices = HutStart(0); if (year >= 1000) choices.AddRange([HutDraw("city-size", 4, 3), HutDraw("city-marketplace", 2, 0), HutDraw("city-granary", 3, 0), HutDraw("city-library", 4, 0)]);
            var f = new HutFixture(choices) { FailDraw = true }; f.World.Globals.Year = (short)year;
            using var op = f.Begin(); var visit = (HutCityVisit)op.Resume()!; Equal(new CitySlot(1), visit.City.Slot); Equal(visit, op.Resume());
            ref var city = ref f.World.Entities.Cities.At(new(1)); Equal(f.Point, city.Position); Equal(new CivilizationSlot(1), city.Owner);
            Equal(year < 1000 ? (sbyte)1 : (sbyte)4, city.Size); Equal((short)2, f.World.Civilizations[1].CityCount);
            foreach (int building in new[] { BuildingIds.Temple, BuildingIds.Marketplace, BuildingIds.Granary, BuildingIds.Library }) Equal(year >= 1000, BuildingQueries.HasBuilding(f.World, new(1), building));
            Equal(1, op.PresentationFailures.Count); op.Resume(0); f.Complete(op);
        }
        foreach (bool late in new[] { false, true })
        {
            var choices = HutStart(0); if (late) choices.Add(HutDraw("city-size", 4, 2));
            var f = new HutFixture(choices); f.World.Globals.Year = late ? (short)1000 : (short)999;
            f.World.Entities.Cities.SetEnd(EntityLimits.CreatableCities);
            for (int i = 0; i < EntityLimits.CreatableCities; ++i) f.World.Entities.Cities.Storage[i] = new() { Token = new((uint)i + 1), Position = new(2, 2), Owner = new(1) };
            using var op = f.Begin();
            if (late) { Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume()); }
            else f.Complete(op);
            Equal(1, f.CityLimits); Equal(late ? 0 : 1, f.NeighborDraws); Equal((sbyte)0, f.World.Entities.Cities.Storage[EntityLimits.CreatableCities - 1].Size);
            f.Random.RequireChoicesConsumed();
        }
        var gold = new HutFixture(HutStart(2, HutDraw("metals-size", 3, 1))) { FailNotice = true };
        using var failed = gold.Begin(); Throws<InvalidOperationException>(() => failed.Resume()); Equal(50, gold.World.Civilizations[1].Gold); Equal(0, gold.Status);
        Throws<InvalidOperationException>(() => failed.Resume());
    }
    private static void HutMercenaryTypesAndHome()
    {
        foreach (var (advances, mounted, foot) in new[] { (Array.Empty<int>(), 16, 4), (new[] { 64 }, 17, 4),
            (new[] { 11, 55 }, 18, 4), (new[] { 11, 42, 55 }, 20, 4), (new[] { 11, 17, 42, 55, 39 }, 11, 5),
            (new[] { 11, 34, 35, 39 }, 19, 8), (new[] { 11, 35, 39 }, 19, 7) })
            foreach (bool chooseFoot in new[] { false, true })
            {
                var choices = HutStart(1); if (!advances.Contains(11)) choices.Add(HutDraw("mounted", 3, 0));
                if (!advances.Contains(11) && advances.Contains(64)) choices.Add(HutDraw("elephant", 2, 1));
                choices.Add(HutDraw("mercenary-type", 2, chooseFoot ? 1 : 0));
                var f = new HutFixture(choices); f.Known(advances); using var op = f.Begin(); f.Complete(op);
                Equal(new UnitType((byte)(chooseFoot ? foot : mounted)), f.Data.Unit(1).Type); Equal((CitySlot?)new CitySlot(0), f.Data.Unit(1).Home);
                Equal(f.Point, f.Data.Unit(1).Position); Equal(1, f.NeighborDraws);
            }
        foreach (bool observer in new[] { false, true })
        {
            var f = new HutFixture(HutStart(1, HutDraw("mounted", 3, 1), HutDraw("mercenary-type", 2, 0)));
            f.Movement.LocalHuman = false; f.World.Multiplayer.LocalHuman = observer ? 1 : 2;
            using var op = f.Begin(); f.Complete(op); Equal(observer ? (CitySlot?)new CitySlot(0) : null, f.Data.Unit(1).Home);
            Equal(observer ? 1 : 0, f.NeighborDraws); Equal(0, f.Notices.Count);
        }
    }
    private static void HutNomadsScrollsAndRedirects()
    {
        var f = new HutFixture(HutStart(0)); f.World.Map.TileAt(f.Point).Fertility = 1;
        using var nomads = f.Begin(); f.Complete(nomads); Equal(new UnitType(0), f.Data.Unit(1).Type); Equal(null, f.Data.Unit(1).Home);
        f = new HutFixture(HutStart(4, HutDraw("scroll-start", 100, 99))); f.Data.Rules.Advances[2] = f.Data.Rules.Advances[2] with { Enabled = true };
        using var scroll = f.Begin(); f.Complete(scroll); Equal(true, f.World.Civilizations[1].Advances.Contains(2));
        Equal((byte)1, f.World.Civilizations[1].AdvanceSource[2]); Equal(2, f.Grant.Acquisitions.Single().Advance); Equal(HutNoticeKind.Scrolls, f.Notices.Single().Kind);
        f = new HutFixture(HutStart(0, HutDraw("metals-size", 3, 1))); f.World.Map.TileAt(f.Point).Fertility = 0; f.Known(AdvanceIds.Explosives);
        using var explosives = f.Begin(); f.Complete(explosives); Equal(50, f.World.Civilizations[1].Gold);
        f = new HutFixture(HutStart(0, HutDraw("mounted", 3, 1), HutDraw("mercenary-type", 2, 1)));
        f.World.Civilizations[1].CityCount = 0; f.World.Globals.Turn = 20;
        f.Data.AddUnit(1, 2, 0, new(4, 4)); f.Data.AddUnit(2, 3, 0, new(6, 4));
        using var foreignSettlers = f.Begin(); f.Complete(foreignSettlers); Equal(new UnitType(4), f.Data.Unit(3).Type);
        // No fallback reward is invented for the demonstrated repeating 0 -> 5 -> 4 cycle.
        f = new HutFixture(HutStart(0, HutDraw("scroll-start", 100, 99, 2), HutDraw("scroll-start", 100, 0, 5)));
        f.World.Map.TileAt(f.Point).Fertility = 0; f.Data.AddUnit(1, 1, 0, new(4, 4));
        using var cycle = f.Begin(); for (int i = 0; i < 6; ++i) { Equal(null, cycle.Resume()); Equal(false, cycle.Completed); }
        f.Random.RequireChoicesConsumed(); Equal(0, f.World.Civilizations[1].Gold); Equal(2, f.World.Entities.Units.End); Equal(0, f.Notices.Count);
    }
    private static void HutBarbarianPlacementAndTypes()
    {
        foreach (int cities in new[] { 1, 2, 4 })
        {
            var f = new HutFixture(HutStart(3)); f.World.Civilizations[1].CityCount = (short)cities;
            f.World.Map.TileAt(f.Point).RevealedTo = CivilizationSet.Only(3).With(4);
            using var op = f.Begin(); f.Complete(op);
            int[] directions = cities == 1 ? [0, 3, 6] : cities == 2 ? [0, 2, 4, 6] : [0, 1, 2, 3, 4, 5, 6, 7];
            MapPoint[] offsets = [new(1, -1), new(2, 0), new(1, 1), new(0, 2), new(-1, 1), new(-2, 0), new(-1, -1), new(0, -2)];
            Equal(directions.Length + 1, f.World.Entities.Units.End);
            for (int i = 0; i < directions.Length; ++i)
            {
                ref var unit = ref f.Data.Unit(i + 1); var delta = offsets[directions[i]];
                Equal(new MapPoint(f.Point.X + delta.X, f.Point.Y + delta.Y), unit.Position);
                Equal(new CivilizationSlot(0), unit.Owner); Equal(new UnitType(15), unit.Type);
                Equal(true, unit.Visibility.Contains(3) && unit.Visibility.Contains(4));
            }
        }
        foreach (int level in new[] { 1, 2, 3 })
            foreach (bool rough in new[] { false, true })
            {
                var f = new HutFixture(HutStart(3)); f.World.Globals.Turn = 40;
                f.Known(AdvanceIds.Metallurgy); if (level >= 2) f.Known(AdvanceIds.MobileWarfare); if (level == 3) f.Known(AdvanceIds.GuerrillaWarfare);
                f.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = rough ? (byte)3 : (byte)255 };
                using var op = f.Begin(); f.Complete(op); Equal(2, f.World.Entities.Units.End);
                Equal(new UnitType((byte)(rough ? level == 1 ? 7 : level == 2 ? 11 : 8 : level == 1 ? 19 : level == 2 ? 21 : 9)), f.Data.Unit(1).Type);
            }
        var early = new HutFixture(HutStart(3)); early.World.Globals.Turn = 20; early.World.Globals.BarbarianActivity = 0;
        using var nothing = early.Begin(); early.Complete(nothing); Equal(HutNoticeKind.Nothing, early.Notices.Single().Kind); Equal(1, early.World.Entities.Units.End);
        early = new HutFixture(HutStart(3)); early.World.Globals.Turn = 20; early.World.Globals.BarbarianActivity = 0;
        early.World.Civilizations[1].ContinentCities[1] = 0;
        using var activeZero = early.Begin(); early.Complete(activeZero); Equal(HutNoticeKind.Barbarians, early.Notices.Single().Kind); Equal(2, early.World.Entities.Units.End);
        var backed = new HutFixture(HutStart(3), backing: new Dictionary<int, CivilizationSet> { [-1] = CivilizationSet.Only(2) });
        backed.Data.Rules.Units[22] = backed.Data.Rules.Units[22] with { Prerequisite = AdvanceReference.None }; backed.World.Globals.Turn = 40;
        using var alias = backed.Begin(); backed.Complete(alias); Equal(new UnitType(21), backed.Data.Unit(1).Type);
        var unbacked = new HutFixture(HutStart(3)); unbacked.Data.Rules.Units[22] = unbacked.Data.Rules.Units[22] with { Prerequisite = AdvanceReference.None };
        using var invalid = unbacked.Begin(); Throws<InvalidOperationException>(() => invalid.Resume()); Equal(1, unbacked.World.Entities.Units.End); Equal(HutNoticeKind.Barbarians, unbacked.Notices.Single().Kind);
    }
    private static void HutRedirectGatesAndCapacity()
    {
        var ai = new HutFixture([HutDraw("outcome", 5, 3), HutDraw("barbarian-redirect", 8, 0)]);
        ai.World.Globals.HumanPlayers = CivilizationSet.Only(2); ai.World.Globals.Difficulty = Difficulty.Prince;
        ai.World.Globals.PowerRank[1] = 1; ai.World.Globals.WorstHumanRank = 3; ai.World.Globals.PowerRank[3] = 2;
        ai.World.Globals.CivByRank[3] = 4; ai.World.Globals.PowerRank[4] = 0;
        using var ranked = ai.Begin(); ai.Complete(ranked); Equal(HutNoticeKind.Tribe, ai.Notices.Single().Kind);
        var far = new HutFixture([HutDraw("outcome", 5, 2)]); far.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        far.World.Map.TileAt(new(4, 4)).Features &= ~MapFeatures.CitySite; far.Data.AddCity(0, 1, new(2, 2));
        far.World.Civilizations[1].ContinentCities[1] = 0; far.World.Civilizations[1].ContinentFlags[1].SettlementRestrictions = 3;
        using var distance = far.Begin(); far.Complete(distance); Equal(HutNoticeKind.Tribe, far.Notices.Single().Kind);
        foreach (bool invention in new[] { false, true })
        {
            var f = new HutFixture(HutStart(4, HutDraw("metals-size", 3, 1)));
            if (invention) f.World.Civilizations[1].Advances = f.World.Civilizations[1].Advances.With(AdvanceIds.Invention); else f.World.Globals.Turn = 0;
            using var scroll = f.Begin(); f.Complete(scroll); Equal(50, f.World.Civilizations[1].Gold); Equal(0, f.Grant.Acquisitions.Count);
        }
        foreach (bool single in new[] { false, true })
        {
            var f = new HutFixture(HutStart(3, HutDraw("single-barbarian", 2, single ? 0 : 1)));
            f.World.Globals.Turn = 60; f.World.Civilizations[1].CityCount = 4;
            using var op = f.Begin(); f.Complete(op); Equal(single ? 2 : 9, f.World.Entities.Units.End);
        }
        var early = new HutFixture(HutStart(3, HutDraw("early-barbarians", 2, 0))); early.World.Globals.Turn = 40; early.World.Globals.BarbarianActivity = 2;
        using var nothing = early.Begin(); early.Complete(nothing); Equal(HutNoticeKind.Nothing, early.Notices.Single().Kind);
        var full = new HutFixture(HutStart(3)); full.World.Globals.Turn = 40; full.World.Civilizations[0].MilitaryCount = 1949;
        using var failedSpawn = full.Begin(); full.Complete(failedSpawn); Equal(1, full.World.Entities.Units.End); Equal(0, full.Draws.Count);
        full = new HutFixture(HutStart(1, HutDraw("mounted", 3, 0), HutDraw("mercenary-type", 2, 0))); full.World.Civilizations[1].MilitaryCount = 1949;
        using var failedMercenary = full.Begin(); full.Complete(failedMercenary); Equal(1, full.World.Entities.Units.End); Equal(1, full.NeighborDraws);
        Equal(UnitCreationNotice.TooManyUnits, full.Data.Notices.Single());
        var nomad = new HutFixture(HutStart(0)); nomad.World.Map.TileAt(nomad.Point).Fertility = 0;
        nomad.Data.AddUnit(1, 2, 0, new(6, 4));
        using var ownCount = nomad.Begin(); nomad.Complete(ownCount); Equal(new UnitType(0), nomad.Data.Unit(2).Type); Equal(null, nomad.Data.Unit(2).Home);
        var negative = new HutFixture(HutStart(3)); negative.World.Globals.Turn = -1;
        using var invalid = negative.Begin(); Throws<InvalidOperationException>(() => invalid.Resume()); Equal(1, negative.World.Entities.Units.End);
    }
    private static void HutPrivateCityAndFailureRouting()
    {
        using var runtime = new SessionRuntime(new(), new(228));
        var f = new HutFixture(HutStart(0), new(runtime.Process, runtime.World));
        ParticipantId actorSeat = default, otherSeat = default; int windows = 0;
        runtime.RegisterCommand(new(new(1, "test.hut", "Authored hut entry", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
            _ => new HutOperation(f.Begin(), _ => actorSeat, visit =>
            { ++windows; return new("Created city", otherSeat, new CommandValue.City(visit.City), new([0])); })));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var observer = SessionClient(runtime, 2);
        actorSeat = actor.Identity.Participant; otherSeat = observer.Identity.Participant;
        var id = actor.Submit(new("test.hut", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(actor, id); Equal(false, observer.Reply(prompt, 0).Accepted); Equal(2, f.World.Entities.Cities.End);
        Equal(true, actor.Cancel(id).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, id).Status);
        Equal(1, windows); f.Random.RequireChoicesConsumed();
        var remote = new HutFixture([]); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var unsupported = remote.Begin(); Throws<InvalidOperationException>(() => unsupported.Resume()); Equal(0, remote.Notices.Count);
        var stopped = new HutFixture(HutStart(0)); var disposed = stopped.Begin(); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(0)); Equal(2, stopped.World.Entities.Cities.End);
    }
}
