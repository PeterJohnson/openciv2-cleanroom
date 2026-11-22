using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Specification-derived C08 maintenance/acquisition, V04 §§2,11 and W03 §8 cases.
    private sealed class BuildingEffectsFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public ref City City => ref World.Entities.Cities.Storage[0];
        public BuildingMaintenance Maintenance { get; }
        public WonderEffects Wonders { get; }
        public List<ImprovementLostNotice> Losses { get; } = [];
        public List<LeonardoUpgradeNotice> Upgrades { get; } = [];
        public List<MapPoint> Redraws { get; } = [];
        public Action<ImprovementLostNotice>? OnLoss;
        public Action<LeonardoUpgradeNotice>? OnUpgrade;
        public Action<MapPoint>? OnRedraw;

        public BuildingEffectsFixture()
        {
            Data.AddCity(0, 1, new(10, 10));
            World.Globals.Difficulty = Difficulty.Prince;
            World.Civilizations[1].Government = Government.Monarchy;
            World.Calculation.ShieldRows = 10;
            Rules.Advances[AdvanceIds.MobileWarfare] = new() { Enabled = true };
            for (int i = 1; i < Rules.Improvements.Length; ++i)
                Rules.Improvements[i] = new() { Identity = new($"building-{i}", $"Building {i}"), CostRows = 2, Upkeep = 1 };
            Maintenance = new(World, Rules, n => { Losses.Add(n); OnLoss?.Invoke(n); });
            Wonders = new(World, Rules, n => { Upgrades.Add(n); OnUpgrade?.Invoke(n); }, p => { Redraws.Add(p); OnRedraw?.Invoke(p); });
        }
        public void Own(int wonder) => World.Globals.WonderCity[wonder] = 0;
        public void Facilities(params int[] buildings)
        {
            City.Improvements = default;
            foreach (int building in buildings) City.Improvements = City.Improvements.With(building);
        }
        public void Know(params int[] advances)
        {
            World.Civilizations[1].Advances = default;
            foreach (int advance in advances) World.Civilizations[1].Advances = World.Civilizations[1].Advances.With(advance);
        }
        public void UpgradeRules()
        {
            Own(WonderIds.Leonardo); Know(3, 4);
            Rules.Units[0] = new() { Identity = new("old", "Old unit"), Role = RuleRole.Attack, ObsoleteBy = (AdvanceReference)3, Capacity = 255 };
            Rules.Units[10] = new() { Identity = new("early", "Earlier unit"), Role = RuleRole.Attack, Prerequisite = (AdvanceReference)3, Capacity = 127 };
            Rules.Units[61] = new() { Identity = new("last", "Last unit"), Role = RuleRole.Attack, Prerequisite = (AdvanceReference)3, Capacity = 0, Domain = RuleDomain.Air, ObsoleteBy = (AdvanceReference)4 };
            Rules.Units[60] = new() { Role = RuleRole.Attack, Prerequisite = (AdvanceReference)4 };
        }
    }

    private static void BuildingMaintenanceQuotes()
    {
        var f = new BuildingEffectsFixture();
        f.Rules.Improvements[2] = f.Rules.Improvements[2] with { Upkeep = 255 };
        f.Know(AdvanceIds.Gunpowder, AdvanceIds.MobileWarfare); f.Own(WonderIds.AdamSmith);
        Equal(257, f.Maintenance.Quote(1, 2)); // No presence gate or byte narrowing.
        f.World.Globals.Difficulty = (Difficulty)(-1); Equal(257, f.Maintenance.Quote(1, 2)); // Unsigned saved difficulty.
        f.Rules.Improvements[2] = f.Rules.Improvements[2] with { Upkeep = 1 };
        f.World.Globals.Difficulty = Difficulty.Chieftain; f.Know();
        Equal(0, f.Maintenance.Quote(1, 2));
        f.Know(AdvanceIds.Gunpowder); Equal(0, f.Maintenance.Quote(1, 2));
        f.World.Globals.WonderCity[WonderIds.AdamSmith] = -1; Equal(1, f.Maintenance.Quote(1, 2));
        f.World.Civilizations[1].Government = Government.Fundamentalism;
        foreach (var (building, cost) in new[] { (BuildingIds.Temple, 3), (BuildingIds.Colosseum, 255), (BuildingIds.Cathedral, 2) })
        {
            f.Rules.Improvements[building] = f.Rules.Improvements[building] with { Upkeep = (byte)cost };
            Equal(0, f.Maintenance.Quote(1, building));
        }
        f.Rules.Improvements[BuildingIds.Library] = f.Rules.Improvements[BuildingIds.Library] with { Upkeep = 3 };
        Equal(3, f.Maintenance.Quote(1, BuildingIds.Library));
        f.World.Civilizations[1].Government = Government.Anarchy;
        Equal(3, f.Maintenance.Quote(1, BuildingIds.Library));
    }

    private static void BuildingMaintenancePrerequisiteWalk()
    {
        var f = new BuildingEffectsFixture();
        f.Rules.Advances[53] = new() { Prerequisites = new([(AdvanceReference)2, AdvanceReference.None]) };
        f.Rules.Advances[2] = new() { Prerequisites = new([AdvanceReference.None, AdvanceReference.Never]) };
        Equal(2, f.Maintenance.Quote(1, 2));
        f.Rules.Advances[2] = new() { Prerequisites = new([AdvanceReference.Never, AdvanceReference.None]) };
        Equal(1, f.Maintenance.Quote(1, 2)); // Never search the second branch.
        f.Rules.Advances[2] = new() { Prerequisites = new([(AdvanceReference)53, AdvanceReference.None]) };
        f.Facilities(1, 2); f.World.Civilizations[1].Gold = 2;
        Throws<InvalidOperationException>(() => f.Maintenance.Run(new(0)));
        Equal(1, f.World.Civilizations[1].Gold); Equal(0, f.Losses.Count);
        f.Rules.Advances[2] = new() { Prerequisites = new([(AdvanceReference)100, AdvanceReference.None]) };
        Throws<IndexOutOfRangeException>(() => f.Maintenance.Run(new(0)));
        Equal(0, f.World.Civilizations[1].Gold); Equal(true, f.City.Improvements.Contains(2));
    }

    private static void BuildingMaintenanceOrderedLosses()
    {
        var f = new BuildingEffectsFixture(); f.Facilities(1, 4, 6);
        f.Rules.Improvements[1] = f.Rules.Improvements[1] with { CostRows = 0 };
        f.Rules.Improvements[4] = f.Rules.Improvements[4] with { CostRows = 0 };
        f.Rules.Improvements[6] = f.Rules.Improvements[6] with { CostRows = 3 };
        f.City.ShieldBox = 7; f.World.Civilizations[1].CapitalX = 99;
        var flags = f.City.Flags; var work = f.City.WorkedTiles;
        f.Maintenance.Run(new(0));
        Equal("1,4,6", string.Join(',', f.Losses.Select(n => n.Building)));
        Equal("0,0,30", string.Join(',', f.Losses.Select(n => n.Credit)));
        Equal(30, f.World.Civilizations[1].Gold); Equal(default(ImprovementSet), f.City.Improvements);
        Equal(flags, f.City.Flags); Equal(work, f.City.WorkedTiles);
        Equal((short)7, f.City.ShieldBox); Equal((short)99, f.World.Civilizations[1].CapitalX);
    }

    private static void BuildingMaintenanceLiveContinuation()
    {
        var f = new BuildingEffectsFixture(); f.Facilities(1, 4);
        f.Rules.Improvements[4] = f.Rules.Improvements[4] with { Upkeep = 30 };
        f.World.Civilizations[2].Gold = 99;
        f.OnLoss = n =>
        {
            Equal(1, n.Owner); Equal(0, f.World.Civilizations[1].Gold);
            Equal(false, f.City.Improvements.Contains(n.Building));
            if (n.Building == 1)
            {
                Equal(20, n.Credit); f.City.Owner = new(2);
                f.World.Civilizations[1].Government = Government.Anarchy; f.World.Civilizations[1].Gold = 3;
                f.Rules.Improvements[1] = f.Rules.Improvements[1] with { CostRows = 4 }; f.World.Calculation.ShieldRows = 5;
                f.City.Improvements = f.City.Improvements.With(6);
                f.Rules.Improvements[6] = f.Rules.Improvements[6] with { Upkeep = 2 };
            }
            else
            {
                Equal(4, n.Building); Equal(10, n.Credit); f.World.Civilizations[1].Gold = 7;
                f.Rules.Improvements[4] = f.Rules.Improvements[4] with { CostRows = 1 }; f.World.Calculation.ShieldRows = 3;
            }
        };
        f.Maintenance.Run(new(0));
        Equal(2, f.Losses.Count); Equal(8, f.World.Civilizations[1].Gold); Equal(99, f.World.Civilizations[2].Gold);
        Equal(default(ImprovementSet).With(6), f.City.Improvements);
    }

    private static void BuildingMaintenanceWrapAndFailure()
    {
        var f = new BuildingEffectsFixture(); f.Facilities(1); f.World.Civilizations[1].Gold = -1;
        f.Rules.Improvements[1] = f.Rules.Improvements[1] with { Upkeep = 0 };
        f.World.Multiplayer.Saved.AutoplayActive = true; f.Maintenance.Run(new(0));
        Equal(0, f.Losses.Count); Equal(20, f.World.Civilizations[1].Gold); Equal(default(ImprovementSet), f.City.Improvements);
        f.Facilities(1); f.Rules.Improvements[1] = f.Rules.Improvements[1] with { Upkeep = 1 };
        f.World.Civilizations[1].Gold = int.MinValue; f.Maintenance.Run(new(0));
        Equal(int.MaxValue, f.World.Civilizations[1].Gold); Equal(true, f.City.Improvements.Contains(1));
        f.World.Civilizations[1].Gold = -1; f.World.Civilizations[1].Government = Government.Anarchy;
        f.Maintenance.Run(new(0)); Equal(-1, f.World.Civilizations[1].Gold);
        f.World.Civilizations[1].Government = Government.Monarchy; f.World.Multiplayer.Saved.AutoplayActive = false;
        f.World.Calculation.ShieldRows = int.MaxValue; f.Maintenance.Run(new(0));
        Equal(-2, f.Losses.Single().Credit); Equal(-2, f.World.Civilizations[1].Gold);
        f.Facilities(1); f.OnLoss = _ => throw new InvalidOperationException("notice stopped");
        Throws<InvalidOperationException>(() => f.Maintenance.Run(new(0)));
        Equal(0, f.World.Civilizations[1].Gold); Equal(default(ImprovementSet), f.City.Improvements);
        f.Facilities(1); f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => f.Maintenance.Run(new(0)));
        Equal(true, f.City.Improvements.Contains(1));
    }

    private static void DiplomaticAttitudePrimitives()
    {
        var f = new BuildingEffectsFixture(); var w = f.World;
        w.Civilizations[2].Attitude[1] = 255; Equal(255, DiplomaticAttitudes.Get(w, 2, 1));
        DiplomaticAttitudes.Set(w, 2, 1, 101); Equal(100, DiplomaticAttitudes.Get(w, 2, 1));
        w.Diplomacy.SelectedPair = (2, 1); w.Diplomacy.Attitude = 95;
        Equal(100, DiplomaticAttitudes.Adjust(w, 2, 1, 100)); Equal(195, w.Diplomacy.Attitude);
        DiplomaticAttitudes.Adjust(w, 1, 2, 10); Equal(195, w.Diplomacy.Attitude);
        w.Globals.Multiplayer = GameMode.Hotseat; w.Multiplayer.HotseatParticipants = CivilizationSet.Only(2);
        Equal(100, DiplomaticAttitudes.Adjust(w, 2, 1, -50)); Equal(145, w.Diplomacy.Attitude);
        w.Multiplayer.LocalHuman = 2; Equal(50, DiplomaticAttitudes.Adjust(w, 2, 1, -50));
        w.Multiplayer.LocalHuman = 1; w.Globals.Multiplayer = GameMode.Email;
        Equal(0, DiplomaticAttitudes.Adjust(w, 2, 1, -100));
        w.Civilizations[2].Attitude[1] = 1; w.Diplomacy.Attitude = 1;
        Equal(0, DiplomaticAttitudes.Adjust(w, 2, 1, int.MaxValue)); Equal(int.MinValue, w.Diplomacy.Attitude);
        var fresh = new BuildingEffectsFixture(); DiplomaticAttitudes.Adjust(fresh.World, 0, 0, 1);
        Equal(0, fresh.World.Diplomacy.Attitude); Equal(((int, int)?)null, fresh.World.Diplomacy.SelectedPair);
        foreach (var (value, category) in new[] { (-1, 0), (0, 1), (10, 1), (11, 2), (25, 2), (26, 3), (38, 3), (39, 4), (61, 4), (62, 5), (74, 5), (75, 6), (89, 6), (90, 7), (99, 7), (100, 8), (255, 8) })
            Equal(category, DiplomaticAttitudes.Category(value));
        w.Civilizations[2].Treaties[1] = new() { War = true, Alliance = true }; Equal(true, DiplomaticAttitudes.Hostile(w, 2, 1));
        w.Civilizations[2].Treaties[1] = new() { Contact = true, Alliance = true }; w.Civilizations[2].Attitude[1] = 100;
        Equal(false, DiplomaticAttitudes.Hostile(w, 2, 1));
        w.Civilizations[2].Treaties[1] = new() { Contact = true, Ceasefire = true }; w.Civilizations[2].Attitude[1] = 50;
        Equal(true, DiplomaticAttitudes.Hostile(w, 2, 1)); Equal(false, DiplomaticAttitudes.Hostile(w, 1, 2));
        w.Civilizations[2].Attitude[1] = 49; Equal(false, DiplomaticAttitudes.Hostile(w, 2, 1));
        w.Civilizations[2].Attitude[1] = 100; w.Civilizations[2].Treaties[1].Peace = true;
        Equal(false, DiplomaticAttitudes.Hostile(w, 2, 1));
    }

    private static void EiffelAcquisitionEffects()
    {
        var f = new BuildingEffectsFixture(); var w = f.World; w.Civilizations[1].Reputation = 255;
        w.Globals.Status.Scenario = true; w.Scenario.Flags.ObjectiveVictory = true; w.Multiplayer.Saved.AutoplayActive = true;
        for (int c = 0; c < 8; ++c)
        {
            w.Civilizations[c].Attitude[1] = (byte)(c == 3 ? 10 : 55);
            w.Civilizations[c].Treaties[1].Vendetta = true; w.Civilizations[1].Treaties[c].Vendetta = true;
        }
        w.Globals.Multiplayer = GameMode.Hotseat; w.Multiplayer.HotseatParticipants = CivilizationSet.Only(2);
        w.Diplomacy.SelectedPair = (2, 1); w.Diplomacy.Attitude = 1000;
        f.Wonders.ApplyEiffel(1);
        Equal((byte)127, w.Civilizations[1].Reputation); Equal(true, w.Civilizations[1].Flags.EiffelApplied);
        Equal(975, w.Diplomacy.Attitude);
        for (int c = 0; c < 8; ++c)
        {
            Equal(c is 0 or 1 or 2 ? 55 : c == 3 ? 0 : 30, DiplomaticAttitudes.Get(w, c, 1));
            Equal(c is 0 or 1, w.Civilizations[c].Treaties[1].Vendetta);
            Equal(true, w.Civilizations[1].Treaties[c].Vendetta);
        }
        f.Own(WonderIds.EiffelTower); f.Wonders.ApplyEiffel(1);
        Equal((byte)127, w.Civilizations[1].Reputation); Equal(975, w.Diplomacy.Attitude);
    }

    private static void GlobalReputationMaintenance()
    {
        var f = new BuildingEffectsFixture(); var w = f.World;
        w.Civilizations[0].Reputation = 255; w.Civilizations[1].Reputation = 2; w.Civilizations[2].Reputation = 2;
        w.Civilizations[3].Reputation = 1;
        for (int c = 0; c < 8; ++c)
        {
            w.Civilizations[c].ReputationCredit[1] = (sbyte)(c % 2 == 0 ? -5 : 7);
            w.Civilizations[c].ReputationCredit[2] = 7;
        }
        foreach (short turn in new short[] { 35, 36 })
        {
            w.Globals.Turn = turn; DiplomaticAttitudes.MaintainReputation(w, f.Rules); Equal((byte)2, w.Civilizations[1].Reputation);
        }
        w.Globals.Turn = 72; DiplomaticAttitudes.MaintainReputation(w, f.Rules);
        Equal((byte)254, w.Civilizations[0].Reputation); Equal((byte)1, w.Civilizations[1].Reputation);
        Equal((byte)1, w.Civilizations[2].Reputation); Equal((byte)1, w.Civilizations[3].Reputation); Equal((byte)0, w.Civilizations[4].Reputation);
        for (int c = 0; c < 8; ++c)
        {
            Equal((sbyte)(c % 2 == 0 ? 0 : 1), w.Civilizations[c].ReputationCredit[1]); Equal((sbyte)7, w.Civilizations[c].ReputationCredit[2]);
        }
        f.Own(WonderIds.EiffelTower); w.Globals.Turn = 36; DiplomaticAttitudes.MaintainReputation(w, f.Rules);
        Equal((byte)0, w.Civilizations[1].Reputation);
        w.Civilizations[1].Reputation = 1; f.Rules.WonderObsolescence[WonderIds.EiffelTower] = (AdvanceReference)4;
        w.Civilizations[7].Advances = default(AdvanceSet).With(4); DiplomaticAttitudes.MaintainReputation(w, f.Rules);
        Equal((byte)1, w.Civilizations[1].Reputation);
        w.Civilizations[7].Advances = default; w.Globals.Status.Scenario = true; w.Scenario.Flags.ObjectiveVictory = true;
        DiplomaticAttitudes.MaintainReputation(w, f.Rules); Equal((byte)1, w.Civilizations[1].Reputation);
        w.Civilizations[1].Reputation = 2; w.Globals.Turn = -72; DiplomaticAttitudes.MaintainReputation(w, f.Rules);
        Equal((byte)1, w.Civilizations[1].Reputation);
        w.Civilizations[1].Reputation = 2; w.Globals.Difficulty = (Difficulty)255; w.Globals.Turn = 6144;
        DiplomaticAttitudes.MaintainReputation(w, f.Rules); Equal((byte)1, w.Civilizations[1].Reputation);
    }

    private static void LeonardoSelectionAndRetention()
    {
        var f = new BuildingEffectsFixture(); f.UpgradeRules(); f.World.Multiplayer.Saved.AutoplayActive = true;
        for (int i = 0; i < 2; ++i) f.Data.AddUnit(i, 1, 0, new(10, 10));
        f.Data.Unit(0).Flags.Veteran = true; f.Data.Unit(0).MovesUsed = 255; f.Data.Unit(0).Damage = 254;
        f.Data.Unit(0).Orders = UnitOrder.Sleeping; f.Data.Unit(0).Home = new(0); f.Data.Unit(0).Counter = 253;
        var before = f.Data.Unit(0); f.World.Civilizations[1].UnitsActive[0] = 7;
        f.Wonders.UpgradeForLeonardo(1);
        var expected = before with { Type = new(61), Flags = before.Flags with { Veteran = false } };
        Equal(expected, f.Data.Unit(0)); Equal(new UnitType(61), f.Data.Unit(1).Type);
        Equal((byte)7, f.World.Civilizations[1].UnitsActive[0]); Equal((byte)0, f.World.Civilizations[1].UnitsActive[61]);
        Equal(1, f.Upgrades.Count); Equal(2, f.Redraws.Count);
        Equal("Old unit", f.Upgrades[0].OldName); Equal("Last unit", f.Upgrades[0].NewName); Equal("Building 53", f.Upgrades[0].WonderName);
        // Replacement itself is obsolete, but no recursive upgrade on this visit.
        f.Wonders.UpgradeForLeonardo(1); Equal(new UnitType(60), f.Data.Unit(0).Type); Equal(2, f.Upgrades.Count);
        f.Data.Unit(0).Type = new(0); f.World.Globals.HumanPlayers = default; f.Wonders.UpgradeForLeonardo(1);
        Equal(new UnitType(61), f.Data.Unit(0).Type); Equal(2, f.Upgrades.Count); Equal(5, f.Redraws.Count);
    }

    private static void LeonardoDefensiveAndSameType()
    {
        var f = new BuildingEffectsFixture(); f.Own(WonderIds.Leonardo); f.Know(AdvanceIds.Gunpowder);
        f.Rules.Units[0] = new() { Role = RuleRole.Defend, Defense = 255, Capacity = 128, ObsoleteBy = AdvanceReference.Never, Prerequisite = (AdvanceReference)AdvanceIds.Gunpowder };
        f.Rules.Units[7] = new() { Role = RuleRole.Defend, Defense = 0, Prerequisite = AdvanceReference.Never };
        f.Data.AddUnit(0, 1, 0, new(10, 10)); f.Data.Unit(0).Flags.Veteran = true;
        f.Wonders.UpgradeForLeonardo(1);
        Equal(new UnitType(0), f.Data.Unit(0).Type); Equal(false, f.Data.Unit(0).Flags.Veteran); Equal(1, f.Upgrades.Count);
        f.Data.Unit(0).Flags.Veteran = true; f.Wonders.UpgradeForLeonardo(1); Equal(2, f.Upgrades.Count);
        f.Rules.Units[7] = f.Rules.Units[7] with { Defense = 128 }; // -1 is not below -128.
        f.Wonders.UpgradeForLeonardo(1); Equal(2, f.Upgrades.Count);
        f.Rules.Units[7] = f.Rules.Units[7] with { Defense = 0 }; f.Know();
        f.Wonders.UpgradeForLeonardo(1); Equal(2, f.Upgrades.Count);
        f.Know(AdvanceIds.Gunpowder); f.Rules.Units[0] = f.Rules.Units[0] with { Prerequisite = AdvanceReference.Never };
        f.Wonders.UpgradeForLeonardo(1); Equal(2, f.Upgrades.Count);
        f.City.Owner = new(2); f.Data.Unit(0).Owner = new(2);
        f.Wonders.UpgradeForLeonardo(258); Equal(2, f.Upgrades.Count); // Wonder owner aliases; unit owner does not.
    }

    private static void LeonardoLiveContinuationAndFailure()
    {
        var f = new BuildingEffectsFixture(); f.UpgradeRules(); f.Data.AddUnit(0, 1, 0, new(10, 10));
        f.OnUpgrade = n =>
        {
            f.Data.Unit(n.Unit.Value).Token = default; f.Data.Unit(n.Unit.Value).Owner = new(2);
            f.Data.Unit(n.Unit.Value).Position = new(12, 10); f.Data.Unit(n.Unit.Value).Flags.Veteran = true;
        };
        f.OnRedraw = _ => { if (f.Redraws.Count == 1) f.Data.AddUnit(1, 1, 0, new(14, 10)); };
        f.Wonders.UpgradeForLeonardo(1);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(new UnitType(61), f.Data.Unit(0).Type);
        Equal(false, f.Data.Unit(0).Flags.Veteran); Equal(new MapPoint(12, 10), f.Redraws[0]);
        Equal(new UnitType(61), f.Data.Unit(1).Type); Equal(1, f.Upgrades.Count); Equal(2, f.Redraws.Count);
        f = new(); f.UpgradeRules(); f.Data.AddUnit(0, 1, 0, new(10, 10)); f.Data.AddUnit(1, 1, 9, new(12, 10));
        f.Rules.Units[9] = f.Rules.Units[0];
        f.OnUpgrade = n => { if (n.Unit.Value == 1) throw new InvalidOperationException("notice stopped"); };
        Throws<InvalidOperationException>(() => f.Wonders.UpgradeForLeonardo(1));
        Equal(new UnitType(61), f.Data.Unit(0).Type); Equal(new UnitType(9), f.Data.Unit(1).Type); Equal(1, f.Redraws.Count);
        f.OnUpgrade = null; f.OnRedraw = _ => throw new InvalidOperationException("redraw stopped");
        Throws<InvalidOperationException>(() => f.Wonders.UpgradeForLeonardo(1)); Equal(new UnitType(60), f.Data.Unit(0).Type);
        f.World.Globals.WonderCity[WonderIds.Leonardo] = -2; f.Wonders.UpgradeForLeonardo(1); // No callbacks without wonder.
    }

    private static void ApolloKnowledgePublication()
    {
        var f = new BuildingEffectsFixture(); var w = f.World;
        f.City.Size = -1; f.Data.AddCity(1, 2, new(12, 10), 7); f.Data.AddCity(2, 3, new(14, 10), 8);
        w.Entities.Cities.Storage[2].Token = default;
        f.Data.AddUnit(0, 1, 0, new(10, 10)); f.Data.Unit(0).Visibility = CivilizationSet.Only(1);
        w.Map.RememberedBarbarian = Enumerable.Repeat(MapFeatures.Road, w.Map.Geometry.Area).ToArray();
        w.Map.Tiles[0].Features = MapFeatures.Pollution | MapFeatures.Irrigation;
        int redraws = 0, notices = 0;
        ApolloRevelation.Run(w, () => ++redraws, () => ++notices);
        Equal(1, redraws); Equal(1, notices); Equal((byte)0, w.Globals.RevealMap);
        for (int i = 0; i < w.Map.Geometry.Area; ++i)
        {
            Equal(MapFeatures.Road, w.Map.RememberedLayer(0)[i]);
            for (int c = 0; c < 8; ++c)
            {
                Equal(true, w.Map.Tiles[i].RevealedTo.Contains(c));
                if (c != 0) Equal(w.Map.Tiles[i].Features, w.Map.RememberedLayer(c)[i]);
            }
        }
        for (int c = 0; c < 8; ++c)
        {
            Equal(true, f.City.KnownTo.Contains(c)); Equal((byte)255, f.City.RememberedSize[c]);
            Equal((byte)7, w.Entities.Cities.Storage[1].RememberedSize[c]);
            Equal((byte)0, w.Entities.Cities.Storage[2].RememberedSize[c]);
        }
        Equal(CivilizationSet.Only(1), f.Data.Unit(0).Visibility);
        w.Multiplayer.Saved.AutoplayActive = true; ApolloRevelation.Run(w, () => ++redraws, () => ++notices);
        Equal(2, redraws); Equal(1, notices);
        w.Multiplayer.Saved.AutoplayActive = false; f.City.Size = 5;
        Throws<InvalidOperationException>(() => ApolloRevelation.Run(w, () => throw new InvalidOperationException("redraw stopped"), () => ++notices));
        Equal((byte)5, f.City.RememberedSize[7]); Equal(1, notices);
        w.Globals.Multiplayer = GameMode.Tcp; f.City.Size = 6;
        Throws<InvalidOperationException>(() => ApolloRevelation.Run(w, () => ++redraws, () => ++notices));
        Equal((byte)5, f.City.RememberedSize[7]); Equal(2, redraws);
    }
}
