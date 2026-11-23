using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static Choice SpawnDraw(string site, int bound, int value, int index = 0) =>
        Choice.Create(new("W06.spawn-" + site, "spawn", index), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value));
    private static List<Choice> UprisingChoices(int size = 8, int count = 6, int mounted = 1) =>
        [SpawnDraw("treasury", 100, 99), SpawnDraw("land-x", 13, 8), SpawnDraw("land-y", 13, 6),
            .. size > 1 ? new[] { SpawnDraw("size", size, count) } : [], SpawnDraw("mounted", 3, mounted)];
    private sealed class SpawnFixture : IBarbarianSpawningEffects
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public EliminationFixture Life { get; }
        public GameplayRandom Random => Life.Random;
        public BarbarianSpawning Spawning { get; }
        public List<BarbarianUprisingNotice> Notices { get; } = [];
        public List<MapPoint> Draws { get; } = [];
        public bool FailPresentation;
        public SpawnFixture(List<Choice> choices, IReadOnlyDictionary<int, CivilizationSet>? backing = null)
        {
            Life = new(choices, Data);
            var incidents = new IncidentFixture(Random, Data);
            World.Globals.Turn = 30; World.Globals.Difficulty = Difficulty.Deity;
            World.Globals.BarbarianActivity = 3; World.Globals.Rules.ExpandedBarbarianTypes = true;
            foreach (ref var tile in World.Map.Tiles)
            { tile.Region = 1; tile.RevealedTo = CivilizationSet.Only(1).With(2); }
            World.Map.Regions[1] = new(480, 0, 16, default);
            for (int i = 0; i < RuleLimits.UnitTypes; ++i)
            { Data.Rules.Units[i] = Data.Rules.Units[0] with { Identity = new("custom" + i, "Renamed " + i), Role = RuleRole.Attack }; }
            Data.Rules.Units[46] = Data.Rules.Units[0] with { Role = RuleRole.Diplomacy };
            foreach (int i in new[] { 32, 33, 35 }) { Data.Rules.Units[i] = Data.Rules.Units[i] with { Domain = RuleDomain.Sea }; }
            Data.Rules.Units[22] = Data.Rules.Units[22] with { Prerequisite = (AdvanceReference)AdvanceIds.MobileWarfare };
            Spawning = new(World, Data.Rules, Random, new(World, Data.Process, Data.Stacks, Data), Data.Stacks,
                Life.Loss, Life.Elimination, incidents.Incidents, this, backing);
        }
        public void City(int size = 8, MapPoint? position = null)
        {
            Data.AddCity(0, 1, position ?? new(10, 10), size);
            World.Entities.Cities.Storage[0].Founder = new(1);
            World.Entities.Cities.Storage[0].Name = "First city";
            World.Civilizations[1].Gold = 1000; World.Civilizations[1].TotalCitySize = (short)size;
            World.Civilizations[1].CityCount = 1;
        }
        public void Known(params int[] advances) { foreach (int a in advances) { World.Globals.AdvanceKnownBy[a] = CivilizationSet.Only(2); } }
        public BarbarianSpawning.Operation Begin() => Spawning.Begin("spawn");
        public void Complete(BarbarianSpawning.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) { Equal(null, op.Resume()); }
            Equal(true, op.Completed); Random.RequireChoicesConsumed();
        }
        public int[] Types() => Enumerable.Range(0, World.Entities.Units.End).Where(i => Data.Unit(i).Token.Occupied).Select(i => (int)Data.Unit(i).Type.Value).ToArray();
        public void Notice(BarbarianUprisingNotice notice)
        { Notices.Add(notice); if (FailPresentation) { throw new InvalidOperationException("Notice unavailable"); } }
        public void Redraw(MapPoint point)
        { Draws.Add(point); if (FailPresentation) { throw new InvalidOperationException("Map unavailable"); } }
    }

    // W06 periodic spawning: format flag and cold defaults, without promoting other opaque bits.
    private static void BarbarianTypeFlagConversion()
    {
        Equal(true, GameRules.Normal.ExpandedBarbarianTypes);
        for (int bits = 0; bits <= ushort.MaxValue; ++bits)
        {
            var decoded = StateConversion.DecodeGameRules((ushort)bits);
            Equal((bits & 1) != 0, decoded.ExpandedBarbarianTypes);
            Equal(bits & StateConversion.GameRulesMask, StateConversion.EncodeGameRules(decoded) & StateConversion.GameRulesMask);
        }
        var stored = new StoredGlobals { Rules = 0x7fff };
        var live = StateConversion.ToLive(stored); live.Rules.ExpandedBarbarianTypes = false;
        Equal((ushort)0x7ffe, StateConversion.PreserveOpaque(StateConversion.ToStored(live), stored).Rules);
        var preferences = new StoredOrdinaryPreferences { Rules = 0x7ffe };
        var settings = StateConversion.ToLive(preferences); settings.Rules.ExpandedBarbarianTypes = true;
        Equal((ushort)0x7fff, StateConversion.PreserveOpaque(StateConversion.ToStored(settings), preferences).Rules);
    }

    private static void BarbarianSpawnEntryAndCityGates()
    {
        foreach (int activity in new[] { 1, 2, 3 })
            foreach (int difficulty in new[] { 0, 4, 5, 9 })
            {
                int threshold = difficulty < 5 ? ((3 - activity) * 3 + 30) * (5 - difficulty) : (3 - activity) * 3 + 15;
                foreach (bool admitted in new[] { false, true })
                {
                    var f = new SpawnFixture(admitted ? [SpawnDraw("treasury", 100, 0)] : []); f.City();
                    f.World.Globals.BarbarianActivity = (byte)activity; f.World.Globals.Difficulty = (Difficulty)difficulty;
                    f.World.Globals.Turn = (short)(threshold - (admitted ? 0 : 1));
                    f.Known(AdvanceIds.MobileWarfare); f.World.Civilizations[1].Gold = 0;
                    using var op = f.Begin(); f.Complete(op); Equal(0, f.Types().Length);
                }
            }
        foreach (int variant in new[] { 0, 1, 2, 3, 4 })
        {
            var f = new SpawnFixture(variant == 4 ? [SpawnDraw("city", 100, 99)] : []);
            if (variant == 0) { f.World.Globals.BarbarianActivity = 0; }
            if (variant is 1 or 2) { f.City(); }
            if (variant == 1) { f.World.Entities.Cities.Storage[0].Token = default; }
            if (variant == 2) { f.World.Globals.HumanPlayers = default; }
            if (variant == 4) { f.City(); f.World.Entities.Cities.SetEnd(100); }
            using var op = f.Begin(); f.Complete(op); Equal(0, f.Types().Length);
        }
    }

    private static void BarbarianSeaTypesAndSleep()
    {
        // Human/global discovery are distinct; fixed slots survive arbitrary names.
        foreach (var (expanded, humanIron, humanTactics, techs, escort, ship) in new[]
        {
            (false, false, false, Array.Empty<int>(), 2, 32),
            (true, false, false, new[] { 39, 81 }, 4, 32),
            (true, true, false, new[] { 57 }, 5, 33),
            (true, false, false, new[] { 45, 57, 51 }, 18, 35),
            (false, false, true, new[] { 51 }, 19, 32),
            (true, false, true, new[] { 51, 11 }, 20, 32),
            (true, false, false, new[] { 11 }, 18, 32)
        })
        {
            var f = new SpawnFixture([SpawnDraw("sea-x", 40, 10), SpawnDraw("sea-y", 18, 6), SpawnDraw("ship-veteran", 2, 0)]);
            f.World.Globals.Turn = 31; f.World.Globals.Rules.ExpandedBarbarianTypes = expanded;
            var p = new MapPoint(11, 9); f.World.Map.TileAt(p).Terrain = Terrain.Ocean; f.Known(techs);
            if (humanIron) { f.World.Globals.AdvanceKnownBy[39] = CivilizationSet.Only(1); }
            if (humanTactics) { f.World.Globals.AdvanceKnownBy[81] = CivilizationSet.Only(1); }
            using var op = f.Begin(); f.Complete(op);
            Sequence(new[] { 46, escort, escort, ship }, f.Types());
            for (int i = 0; i < 3; ++i)
            { Equal(UnitOrder.Sleeping, f.Data.Unit(i).Orders); Equal(new MapPoint(-1, -1), f.Data.Unit(i).Destination); Equal((UnitSlot?)null, f.Data.Unit(i).Carrier); }
            Equal(true, f.Data.Unit(3).Flags.Veteran); Equal(UnitOrder.None, f.Data.Unit(3).Orders);
            Equal(3, f.Data.Stacks.FirstAt(p)); Equal(4, f.World.UnitTotals[0]); Equal(3, (int)f.World.Civilizations[0].MilitaryCount);
            Equal(0, f.Notices.Count); Equal(0, f.Draws.Count);
        }
    }

    private static void BarbarianSeaCadenceAndCounts()
    {
        foreach (var (activity, turn, count) in new[] { (1, 47, 2), (2, 47, 2), (3, 47, 3), (1, 1007, 6), (3, 511, 7) })
        {
            var f = new SpawnFixture([SpawnDraw("sea-x", 40, 10), SpawnDraw("sea-y", 18, 7), SpawnDraw("ship-veteran", 2, 1)]);
            f.World.Globals.Turn = (short)turn; f.World.Globals.BarbarianActivity = (byte)activity;
            f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean; f.Known(AdvanceIds.Chivalry);
            using var op = f.Begin(); f.Complete(op); Equal(count + 1, f.Types().Length);
            Equal(activity == 3 ? 19 : 4, f.Types()[1]); Equal(false, f.Data.Unit(count).Flags.Veteran);
        }
        // Activity 1 does not raid on the intervening activity-2/3 cadence.
        var off = new SpawnFixture([]); off.World.Globals.Turn = 39; off.World.Globals.BarbarianActivity = 1;
        using var absent = off.Begin(); off.Complete(absent);
        var known = new SpawnFixture([]); known.World.Globals.Turn = 31; known.Known(AdvanceIds.MobileWarfare);
        using var skipped = known.Begin(); known.Complete(skipped);
    }

    private static void BarbarianSeaSearchAndExhaustion()
    {
        var f = new SpawnFixture([SpawnDraw("sea-x", 40, 10), SpawnDraw("sea-y", 18, 7),
            SpawnDraw("sea-x", 40, 12, 1), SpawnDraw("sea-y", 18, 7, 1),
            SpawnDraw("sea-x", 40, 14, 2), SpawnDraw("sea-y", 18, 7, 2), SpawnDraw("ship-veteran", 2, 1)]);
        f.World.Globals.Turn = 31;
        foreach (int x in new[] { 10, 12, 14 }) { f.World.Map.TileAt(new(x, 10)).Terrain = Terrain.Ocean; }
        f.World.Map.TileAt(new(10, 10)).Region = 2; f.World.Map.Regions[2] = new(0, 0, -1, default);
        f.Data.AddUnit(0, 1, 32, new(12, 10));
        using var op = f.Begin(); f.Complete(op); Equal(new MapPoint(14, 10), f.Data.Unit(1).Position);
        List<Choice> choices = [];
        for (int i = 0; i < 1000; ++i) { choices.Add(SpawnDraw("sea-x", 40, 10, i)); choices.Add(SpawnDraw("sea-y", 18, 7, i)); }
        var exhausted = new SpawnFixture(choices); exhausted.City(); exhausted.World.Globals.Turn = 31;
        using var failure = exhausted.Begin(); exhausted.Complete(failure); Equal(0, exhausted.Types().Length);
        Equal(0, exhausted.Notices.Count); // No treasury draw after the failed sea search.
    }

    private static void BarbarianSeaFailureActualDeletion()
    {
        foreach (bool allFail in new[] { false, true })
        {
            var f = new SpawnFixture([SpawnDraw("sea-x", 40, 10), SpawnDraw("sea-y", 18, 7), SpawnDraw("treasury", 100, 0)]);
            f.City(position: new(4, 4)); f.World.Civilizations[1].Gold = 0;
            f.World.Globals.Turn = 31; f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
            f.World.Civilizations[0].MilitaryCount = (short)(allFail ? 1950 : 1947);
            using var op = f.Begin(); f.Complete(op);
            Equal(0, f.Types().Length); Equal(0, f.World.UnitTotals[0]); Equal(-1, f.Data.Stacks.FirstAt(new(10, 10)));
            Equal(0, f.World.Entities.Units.End);
            Equal(allFail ? 1U : 4U, f.Data.Process.Tokens.NextUnit);
            Equal(allFail ? 1950 : 1947, (int)f.World.Civilizations[0].MilitaryCount);
        }
    }

    private static void BarbarianUprisingCountsAndGrouping()
    {
        foreach (int mounted in new[] { 0, 1 })
        {
            var f = new SpawnFixture(UprisingChoices(mounted: mounted)); f.City();
            using var op = f.Begin(); f.Complete(op);
            Sequence(mounted == 0 ? new[] { 46, 15, 15, 16, 16, 16, 16 } : new[] { 46, 16, 16, 15, 15, 15, 15 }, f.Types());
            for (int i = 0; i < 7; ++i)
            { Equal(new MapPoint(20, 10), f.Data.Unit(i).Position); Equal(CivilizationSet.Only(0).With(1).With(2), f.Data.Unit(i).Visibility); Equal(UnitOrder.None, f.Data.Unit(i).Orders); }
            var notice = f.Notices.Single(); Equal("First city", notice.CityName); Equal(new UnitSlot(6), notice.Unit.Slot);
            Equal(BarbarianUprisingKind.Uprising, notice.Kind); Sequence(new[] { new MapPoint(20, 10) }, f.Draws);
            op.Resume(); Equal(1, f.Notices.Count);
        }
        foreach (int size in new[] { -128, 1, 127 })
        {
            var f = new SpawnFixture(UprisingChoices(size, 126)); f.City(size); f.World.Civilizations[1].TotalCitySize = 0;
            using var op = f.Begin(); f.Complete(op); Equal(size > 1 ? 100 : 3, f.Types().Length);
            Equal(size > 1 ? BarbarianUprisingKind.LargeUprising : BarbarianUprisingKind.Uprising, f.Notices.Single().Kind);
        }
    }

    private static void BarbarianUprisingMovingCenter()
    {
        var f = new SpawnFixture([SpawnDraw("city", 2, 0), SpawnDraw("treasury", 100, 99),
            SpawnDraw("land-x", 13, 8), SpawnDraw("land-y", 13, 6),
            SpawnDraw("land-x", 13, 8, 1), SpawnDraw("land-y", 13, 6, 1), SpawnDraw("size", 4, 0), SpawnDraw("mounted", 3, 1)]);
        f.City(); f.Data.AddCity(1, 2, new(18, 10));
        using var op = f.Begin(); f.Complete(op); Equal(new MapPoint(28, 10), f.Data.Unit(0).Position);
        Equal(0, f.Notices.Count); Equal(0, f.Draws.Count); // Nearest AI city replaces the original human city.
    }

    private static void BarbarianUprisingSearchLimitAndControl()
    {
        List<Choice> choices = [SpawnDraw("treasury", 100, 99)];
        for (int i = 0; i < 50; ++i)
        { choices.Add(SpawnDraw("land-x", 13, i == 49 ? 8 : 3, i)); choices.Add(SpawnDraw("land-y", 13, 6, i)); }
        var exhausted = new SpawnFixture(choices); exhausted.City(); exhausted.World.Globals.BarbarianActivity = 1;
        using var limit = exhausted.Begin(); exhausted.Complete(limit); Equal(0, exhausted.Types().Length);
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            bool blocked = variant == 0;
            choices = [SpawnDraw("treasury", 100, 99), SpawnDraw("land-x", 13, 8), SpawnDraw("land-y", 13, 6)];
            if (blocked) { choices.Add(SpawnDraw("land-x", 13, 8, 1)); choices.Add(SpawnDraw("land-y", 13, 12, 1)); }
            choices.AddRange([SpawnDraw("size", 8, 0), SpawnDraw("mounted", 3, 1)]);
            var f = new SpawnFixture(choices); f.City(); f.Data.AddUnit(0, variant == 3 ? 0 : 2, 2, new(22, 10));
            if (variant == 1) { f.World.Map.TileAt(new(22, 10)).Terrain = Terrain.Ocean; }
            if (variant == 2) { f.World.Civilizations[0].Treaties[2].Alliance = true; }
            using var op = f.Begin(); f.Complete(op); Equal(new MapPoint(20, blocked ? 16 : 10), f.Data.Unit(1).Position);
        }
    }

    private static void BarbarianUprisingTypesAndRank()
    {
        foreach (var (expanded, techs, republic, original, main, alternate, kind) in new[]
        {
            (false, new[] { 35, 34 }, true, true, 16, 15, BarbarianUprisingKind.Gunpowder),
            (true, new[] { 35 }, true, true, 4, 17, BarbarianUprisingKind.Gunpowder),
            (false, new[] { 37, 34 }, true, true, 24, 7, BarbarianUprisingKind.Industrial),
            (false, new[] { 37, 53 }, true, true, 24, 11, BarbarianUprisingKind.Modern),
            (true, new[] { 37 }, true, true, 25, 7, BarbarianUprisingKind.Industrial),
            (true, new[] { 37, 53, 34 }, true, true, 25, 8, BarbarianUprisingKind.Fanatic),
            (true, new[] { 37, 53, 34 }, false, true, 21, 9, BarbarianUprisingKind.Modern),
            (true, new[] { 37, 34 }, true, false, 21, 9, BarbarianUprisingKind.Industrial)
        })
        {
            var f = new SpawnFixture(UprisingChoices()); f.City(); f.Known(techs);
            f.World.Globals.Rules.ExpandedBarbarianTypes = expanded;
            f.World.Globals.BestHumanRank = 3; f.World.Globals.PowerRank[1] = 3;
            f.World.Civilizations[1].Government = Government.Democracy;
            f.World.Civilizations[3].Government = republic ? Government.Republic : Government.Monarchy;
            if (!original) { f.World.Entities.Cities.Storage[0].Founder = new(2); }
            using var op = f.Begin(); f.Complete(op);
            Sequence(new[] { 46, main, main, alternate, alternate, alternate, alternate }, f.Types());
            Equal(kind, f.Notices.Single().Kind); Equal(alternate == 8, f.Data.Unit(6).Flags.Veteran);
        }
    }

    private static void BarbarianUprisingSurge()
    {
        foreach (var (difficulty, size, roll, bonus, expected) in new[] { (5, 8, 0, 3, 10), (9, 8, 0, 0, 7), (5, 1, 0, 0, 3), (5, 8, 1, 0, 3) })
        {
            var choices = UprisingChoices(size, 2); choices.RemoveAt(choices.Count - 1);
            if (difficulty < 9) { choices.Add(SpawnDraw("surge", 10 - difficulty, roll)); }
            if (roll == 0 && size / 2 > 1) { choices.Add(SpawnDraw("surge-bonus", size / 2, bonus)); }
            choices.Add(SpawnDraw("mounted", 3, 1));
            var f = new SpawnFixture(choices); f.City(size); f.World.Globals.Turn = 150; f.World.Globals.Difficulty = (Difficulty)difficulty;
            using var op = f.Begin(); f.Complete(op); Equal(expected, f.Types().Length);
        }
    }

    private static void BarbarianUprisingNoticeAndCapacity()
    {
        foreach (int variant in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var f = new SpawnFixture(UprisingChoices(count: 2)); f.City();
            if (variant == 0) { f.World.Multiplayer.Saved.AutoplayActive = true; }
            if (variant == 1) { f.World.Map.TileAt(new(20, 10)).RevealedTo = default; }
            if (variant == 2) { f.World.Multiplayer.LocalHuman = 2; }
            if (variant == 3) { f.World.Civilizations[0].MilitaryCount = 1948; }
            if (variant == 4) { f.World.Civilizations[0].MilitaryCount = 1950; }
            if (variant == 5) { f.FailPresentation = true; }
            using var op = f.Begin(); f.Complete(op);
            Equal(variant == 5 ? 1 : 0, f.Notices.Count); Equal(1, f.Draws.Count);
            Equal(variant == 3 ? 2 : variant == 4 ? 0 : 3, f.Types().Length);
            Equal(variant == 5 ? 2 : 0, op.PresentationFailures.Count);
        }
    }

    private static void BarbarianSeaLastCandidateAndShortMap()
    {
        List<Choice> choices = [];
        for (int i = 0; i < 1000; ++i)
        { choices.Add(SpawnDraw("sea-x", 40, i == 999 ? 14 : 10, i)); choices.Add(SpawnDraw("sea-y", 18, 7, i)); }
        choices.Add(SpawnDraw("ship-veteran", 2, 1));
        var f = new SpawnFixture(choices); f.World.Globals.Turn = 31;
        f.World.Map.TileAt(new(14, 10)).Terrain = Terrain.Ocean;
        using var last = f.Begin(); f.Complete(last); Equal(new MapPoint(14, 10), f.Data.Unit(0).Position);
        var small = new SpawnFixture([SpawnDraw("sea-x", 40, 10), SpawnDraw("ship-veteran", 2, 0)]);
        small.World.Globals.Turn = 31; small.World.Map.Allocate(new(40, 6));
        foreach (ref var tile in small.World.Map.Tiles) { tile.Terrain = Terrain.Ocean; tile.Region = 1; }
        using var shortMap = small.Begin(); small.Complete(shortMap); Equal(new MapPoint(11, 3), small.Data.Unit(0).Position);
    }

    private static void BarbarianRaidThenUprising()
    {
        var f = new SpawnFixture([SpawnDraw("sea-x", 40, 2), SpawnDraw("sea-y", 18, 1), SpawnDraw("ship-veteran", 2, 1), .. UprisingChoices(count: 2)]);
        f.City(); f.World.Globals.Turn = 31; f.World.Map.TileAt(new(2, 4)).Terrain = Terrain.Ocean;
        using var op = f.Begin(); f.Complete(op); Equal(7, f.Types().Length);
        Equal(new MapPoint(2, 4), f.Data.Unit(0).Position); Equal(new MapPoint(20, 10), f.Data.Unit(4).Position);
        Equal(new UnitSlot(6), f.Notices.Single().Unit.Slot); Equal(1, f.Draws.Count);
    }

    private static void BarbarianUprisingSpatialRejections()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            int firstX = variant == 3 ? 0 : 8;
            int firstY = variant == 3 ? 0 : 6;
            var f = new SpawnFixture([SpawnDraw("treasury", 100, 99), SpawnDraw("land-x", 13, firstX), SpawnDraw("land-y", 13, firstY),
                SpawnDraw("land-x", 13, 8, 1), SpawnDraw("land-y", 13, 12, 1), SpawnDraw("size", 8, 0), SpawnDraw("mounted", 3, 1)]);
            f.City(position: variant == 3 ? new(10, 2) : null);
            if (variant == 0) { f.World.Map.TileAt(new(20, 10)).Terrain = Terrain.Ocean; }
            if (variant == 1) { f.World.Map.TileAt(new(20, 10)).Region = 2; }
            if (variant == 2) { f.Data.AddUnit(0, 0, 2, new(20, 10)); }
            // Variant 3 first samples outside the map; no nearest-city query replaces its center.
            using var op = f.Begin(); f.Complete(op);
            Equal(new MapPoint(20, variant == 3 ? 8 : 16), f.Data.Unit(variant == 2 ? 1 : 0).Position);
        }
        var wrapped = new SpawnFixture(UprisingChoices(count: 2)); wrapped.City(position: new(36, 10));
        wrapped.World.Globals.Rules.FlatWorld = false;
        using var wrap = wrapped.Begin(); wrapped.Complete(wrap); Equal(new MapPoint(6, 10), wrapped.Data.Unit(0).Position);
    }

    private static void BarbarianUprisingTreasuryBoundary()
    {
        foreach (var (gold, roll, spawn) in new[] { (100, 87, true), (100, 88, false), (-100, 99, true) })
        {
            var choices = spawn ? UprisingChoices(count: 2) : [SpawnDraw("treasury", 100, roll)];
            choices[0] = SpawnDraw("treasury", 100, roll);
            var f = new SpawnFixture(choices); f.City(); f.World.Civilizations[1].Gold = gold;
            using var op = f.Begin(); f.Complete(op); Equal(spawn ? 3 : 0, f.Types().Length);
        }
        var zero = new SpawnFixture([SpawnDraw("treasury", 100, 0)]); zero.City(0);
        using var invalid = zero.Begin(); Throws<DivideByZeroException>(() => invalid.Resume());
        Equal(0, zero.Types().Length); Throws<InvalidOperationException>(() => invalid.Resume());
    }

    private static void BarbarianSpawnRequiredFailures()
    {
        var invalid = new SpawnFixture([]); invalid.World.Globals.BarbarianActivity = 4;
        using var activity = invalid.Begin(); Throws<InvalidOperationException>(() => activity.Resume());
        Throws<InvalidOperationException>(() => activity.Resume());
        var network = new SpawnFixture([]); network.World.Globals.Multiplayer = GameMode.Tcp;
        using var unsupported = network.Begin(); Throws<InvalidOperationException>(() => unsupported.Resume());
        foreach (bool supplied in new[] { false, true })
        {
            var f = new SpawnFixture([], supplied ? new Dictionary<int, CivilizationSet> { [-2] = CivilizationSet.Only(1) } : null);
            f.World.Globals.Turn = 31; f.Data.Rules.Units[22] = f.Data.Rules.Units[22] with { Prerequisite = (AdvanceReference)(-2) };
            using var op = f.Begin();
            if (supplied) { f.Complete(op); } else { Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume()); }
        }
        var late = new SpawnFixture([SpawnDraw("sea-x", 40, 10), SpawnDraw("sea-y", 18, 7)]);
        late.World.Globals.Turn = 31; late.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
        using var failure = late.Begin(); Throws<ChoiceException>(() => failure.Resume()); // Missing required veteran choice after creation.
        Equal(4, late.Types().Length); Throws<InvalidOperationException>(() => failure.Resume());
        failure.Dispose(); Equal(4, late.Types().Length); Throws<ObjectDisposedException>(() => failure.Resume());
        var disposed = new SpawnFixture([]); var untouched = disposed.Begin(); untouched.Dispose();
        Throws<ObjectDisposedException>(() => untouched.Resume()); Equal(0, disposed.Types().Length);
    }
}
