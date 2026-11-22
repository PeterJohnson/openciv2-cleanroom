using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // C06 AI purchase phase: controlled A02 boundary, actual V07 queries and treasury/stock writes.
    private sealed class PurchaseFixture
    {
        public RevelationFixture Data { get; } = new();
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public ref City City => ref World.Entities.Cities.Storage[0];
        public int Choices;
        public Action? Choose, Notice;
        public List<AlmostWonderNotice> Notices { get; } = [];
        public PurchaseFixture()
        {
            Data.AddCity(0, 1, new(10, 10));
            Data.AddUnit(0, 2, 0, City.Position); // A foreign occupant still prevents the undefended override.
            World.Map.TileAt(City.Position).Region = 1;
            World.Globals.HumanPlayers = CivilizationSet.Only(2);
            World.Globals.Difficulty = Difficulty.Prince;
            World.Civilizations[1].Gold = 640;
            World.Calculation.ShieldRows = 10;
            City.Production = 4;
            Rules.Units[4] = Rules.Units[0] with { CostRows = 100 };
            for (int i = 1; i < RuleLimits.Improvements; ++i)
                Rules.Improvements[i] = Rules.Improvements[i] with { CostRows = 100, Prerequisite = AdvanceReference.None };
        }
        public void Run(int net = 1) => new AiProductionPurchase(World, Rules,
            _ => { ++Choices; Choose?.Invoke(); },
            notice => { Notices.Add(notice); Notice?.Invoke(); }).Run(new(0), 1, net);
    }

    private static void SpaceshipPartCapacityAndThrust()
    {
        var f = new RevelationFixture(); var w = f.World;
        int[] first = [0, 3, 5, 7, 11, 15];
        for (int p = 1; p < 6; ++p)
        {
            w.Civilizations[1].SpaceshipParts[0] = (short)(first[p] - 1);
            Equal(0, SpaceshipQueries.PartLimit(w, ClassicShip, 1, p));
            w.Civilizations[1].SpaceshipParts[0] = (short)first[p];
            Equal(1, SpaceshipQueries.PartLimit(w, ClassicShip, 1, p));
        }
        w.Civilizations[1].SpaceshipParts[0] = 39;
        w.Civilizations[2].SpaceshipParts[0] = 39;
        for (int p = 3; p < 6; ++p)
        {
            Equal(4, SpaceshipQueries.PartLimit(w, ClassicShip, 1, p));
            Equal(1, SpaceshipQueries.PartLimit(w, ClassicShip, 2, p));
        }
        w.Civilizations[1].SpaceshipParts[0] = -10;
        w.Civilizations[1].SpaceshipParts[1] = 8;
        w.Civilizations[1].SpaceshipParts[3] = 4;
        Equal(-2, SpaceshipQueries.PartLimit(w, ClassicShip, 1, 1));
        Equal(-2, SpaceshipQueries.PartCount(w, ClassicShip, 1, 1));
        Equal(0, SpaceshipQueries.PartCount(w, ClassicShip, 1, 3));
        Equal((short)8, w.Civilizations[1].SpaceshipParts[1]);
        Equal(39, SpaceshipQueries.PartLimit(w, ClassicShip, 999, 0)); // Structural maximum does not read a civilization.
        Throws<IndexOutOfRangeException>(() => SpaceshipQueries.PartLimit(w, ClassicShip, 1, 6));
        Sequence(new[] { 0, 1, 2, 3, 4, 6, 8, 11, 14 }, Enumerable.Range(0, 9).Select(SpaceshipQueries.Thrust));
        Equal(0, SpaceshipQueries.Thrust(-1)); Equal(int.MaxValue - 12, SpaceshipQueries.Thrust(int.MaxValue));
    }

    private static void SpaceshipGroupRawAndSupportedCounts()
    {
        var f = new PurchaseFixture(); var w = f.World;
        Equal(true, SpaceshipQueries.GroupAtLimit(w, ClassicShip, 1, 1));
        Equal(true, SpaceshipQueries.GroupCanBuild(w, f.Rules, ClassicShip, 1, 1)); // Empty unsupported group may start.
        w.Civilizations[1].SpaceshipParts[1] = 1;
        Equal(false, SpaceshipQueries.GroupCanBuild(w, f.Rules, ClassicShip, 1, 1));
        w.Civilizations[1].SpaceshipParts[1] = 16;
        Equal(true, SpaceshipQueries.GroupFull(w, ClassicShip, 1, 1));
        Equal(false, SpaceshipQueries.PartMaxed(w, ClassicShip, 1, BuildingIds.Component));
        Equal(16, SpaceshipQueries.GroupCount(w, 1, 1));
        Equal(0, SpaceshipQueries.GroupCountClamped(w, ClassicShip, 1, 1));
        w.Civilizations[1].SpaceshipParts[0] = 39;
        w.Civilizations[1].SpaceshipParts[1] = w.Civilizations[1].SpaceshipParts[2] = 8;
        Equal(true, SpaceshipQueries.PartMaxed(w, ClassicShip, 1, BuildingIds.Component));
        Equal(16, SpaceshipQueries.GroupLimit(w, ClassicShip, 1, 1));
        for (int p = 3; p < 6; ++p) w.Civilizations[1].SpaceshipParts[p] = short.MaxValue;
        Equal(98301, SpaceshipQueries.GroupCount(w, 1, -35));
        Equal(3, SpaceshipQueries.GroupCountClamped(w, ClassicShip, 1, -35));
        Equal(12, SpaceshipQueries.GroupMaximum(ClassicShip, -35));
        Equal(true, SpaceshipQueries.PartMaxed(w, ClassicShip, 1, -100));
        for (int p = 3; p < 6; ++p) w.Civilizations[1].SpaceshipParts[p] = 0;
        f.Rules.Improvements[0] = f.Rules.Improvements[0] with { Prerequisite = AdvanceReference.None };
        w.Globals.Rules.Bloodlust = true; w.Civilizations[1].SpaceshipFlags.Launched = true;
        Equal(true, SpaceshipQueries.GroupCanBuild(w, f.Rules, ClassicShip, 1, -35)); // Caller-owned race gates are absent here.
        f.Rules.Improvements[0] = f.Rules.Improvements[0] with { Prerequisite = AdvanceReference.Never };
        Equal(false, SpaceshipQueries.GroupCanBuild(w, f.Rules, ClassicShip, 1, -35));
    }

    private static void SpaceshipRaceAndArrivalPredicates()
    {
        var f = new PurchaseFixture(); var w = f.World;
        Equal(false, SpaceshipQueries.RaceActive(w));
        w.Globals.WonderCity[WonderIds.Apollo] = BuildingQueries.Destroyed;
        Equal(true, SpaceshipQueries.RaceActive(w));
        w.Globals.Rules.Bloodlust = true; Equal(false, SpaceshipQueries.RaceActive(w));
        w.Globals.Rules.Bloodlust = false;
        w.Civilizations[2].SpaceshipFlags.Launched = true; // No Exists, alive membership or knowledge required.
        Equal(true, SpaceshipQueries.RaceBehind(w, f.Rules, 1));
        w.Civilizations[1].SpaceshipFlags.Launched = true; Equal(false, SpaceshipQueries.RaceBehind(w, f.Rules, 1));
        w.Civilizations[1].SpaceshipFlags.Launched = false;
        w.Globals.Status.Won = true; Equal(false, SpaceshipQueries.RaceBehind(w, f.Rules, 1)); w.Globals.Status.Won = false;
        w.Globals.Difficulty = Difficulty.Chieftain; Equal(false, SpaceshipQueries.RaceBehind(w, f.Rules, 1));
        w.Globals.Difficulty = Difficulty.Prince;
        w.Globals.HumanPlayers = w.Globals.HumanPlayers.With(1); Equal(false, SpaceshipQueries.RaceBehind(w, f.Rules, 1));
        w.Globals.HumanPlayers = CivilizationSet.Only(2); w.Civilizations[2].SpaceshipFlags.Launched = false;
        w.Civilizations[2].Advances = default(AdvanceSet).With(AdvanceIds.SpaceFlight);
        f.Rules.Improvements[35] = f.Rules.Improvements[35] with { CostRows = 1 };
        f.Rules.Improvements[36] = f.Rules.Improvements[36] with { CostRows = 7 };
        w.Civilizations[1].SpaceshipParts[1] = 2; w.Civilizations[2].SpaceshipParts[0] = 13;
        Equal(false, SpaceshipQueries.RaceBehind(w, f.Rules, 1)); // Unsupported stored propulsion still contributes 14.
        w.Civilizations[2].SpaceshipParts[0] = 14; Equal(true, SpaceshipQueries.RaceBehind(w, f.Rules, 1));
        w.Globals.WonderCity[WonderIds.Apollo] = BuildingQueries.Unbuilt;
        w.Globals.AdvanceKnownBy[AdvanceIds.SpaceFlight] = CivilizationSet.Only(0); w.Globals.HumanPlayers = CivilizationSet.Only(0);
        Equal(true, SpaceshipQueries.RaceActive(w)); Equal(false, SpaceshipQueries.RaceBehind(w, f.Rules, 1));
        w.Civilizations[1].SpaceshipFlags.Launched = true; w.Civilizations[1].SpaceshipFlags.Arrived = true;
        w.Civilizations[1].SpaceshipArrivalYear = -10; w.Globals.Year = -11;
        Equal(false, SpaceshipQueries.Arrived(w, 1)); w.Globals.Year = -10; Equal(true, SpaceshipQueries.Arrived(w, 1));
        w.Civilizations[1].SpaceshipFlags.Launched = false; Equal(false, SpaceshipQueries.Arrived(w, 1));
    }

    private static void HumanWonderSummaryRetention()
    {
        var f = new PurchaseFixture(); var w = f.World;
        w.Globals.HumanPlayers = CivilizationSet.Only(0); w.Civilizations[0].WondersByGroup[0] = 20;
        w.HumanWonders.Update(w); Equal(99, w.HumanWonders.Minimum[0]); Equal(0, w.HumanWonders.Maximum[0]);
        w.Globals.HumanPlayers = CivilizationSet.Only(2).With(7);
        for (int g = 0; g < 4; ++g)
        {
            w.Civilizations[2].WondersByGroup[g] = (byte)(g + 2);
            w.Civilizations[7].WondersByGroup[g] = 255; // Dead human seats remain inputs; counts are unsigned.
        }
        w.HumanWonders.Update(w);
        for (int g = 0; g < 4; ++g) { Equal(g + 2, w.HumanWonders.Minimum[g]); Equal(255, w.HumanWonders.Maximum[g]); }
        w.Civilizations[2].WondersByGroup[0] = 200; Equal(2, w.HumanWonders.Minimum[0]);
        w.HumanWonders.Update(w); Equal(99, w.HumanWonders.Minimum[0]);
        Equal(0, new GameState(f.Data.Process).HumanWonders.Maximum[0]);
    }

    private static void PurchaseRegionalPolicyAndReselection()
    {
        foreach (int policy in new[] { 0, 1, 4 })
        {
            var f = new PurchaseFixture(); f.World.Civilizations[1].ContinentPolicy[1] = (byte)policy;
            f.Rules.Units[4] = f.Rules.Units[4] with { Role = (RuleRole)policy };
            f.Run(); Equal((short)10, f.City.ShieldBox); Equal(630, f.World.Civilizations[1].Gold); Equal(0, f.Choices);
            f.City.ShieldBox = 0; f.Run(0); Equal((short)0, f.City.ShieldBox);
        }
        var ocean = new PurchaseFixture(); ocean.World.Map.TileAt(ocean.City.Position).Terrain = Terrain.Ocean;
        ocean.World.Civilizations[1].ContinentPolicy[1] = 4;
        ocean.Run(); Equal((short)10, ocean.City.ShieldBox); // Null land region selects policy0.
        var mismatch = new PurchaseFixture(); mismatch.World.Civilizations[1].ContinentPolicy[1] = 2;
        mismatch.Run(); Equal((short)0, mismatch.City.ShieldBox);
        var selected = new PurchaseFixture(); selected.City.Flags.DefenderLost = true;
        selected.Choose = () => selected.City.Production = -BuildingIds.Palace;
        selected.City.ShieldBox = 1; selected.Run(); Equal(1, selected.Choices); Equal((short)81, selected.City.ShieldBox);
        Equal(true, selected.City.Flags.DefenderLost); // Only the production parent clears it.
        var unsigned = new PurchaseFixture(); unsigned.Rules.Units[4] = unsigned.Rules.Units[4] with { CostRows = 255 };
        unsigned.Run(); Equal((short)10, unsigned.City.ShieldBox);
        unsigned.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => unsigned.Run()); Equal((short)10, unsigned.City.ShieldBox);
    }

    private static void PurchaseWonderGroupsAndThresholds()
    {
        foreach (int group in Enumerable.Range(0, 4))
            foreach (var (count, expected) in new[] { (0, 16), (2, 16), (3, 8), (5, 4) })
            {
                var f = new PurchaseFixture(); f.City.Production = (sbyte)(-39 - group * 7);
                f.World.Civilizations[1].Gold = 1024; f.World.Civilizations[1].WondersByGroup[group] = (byte)count;
                f.World.Civilizations[2].WondersByGroup[group] = 3; f.World.HumanWonders.Update(f.World);
                f.World.Civilizations[2].WondersByGroup[group] = 0;
                f.Run(); Equal((short)expected, f.City.ShieldBox); Equal(3, f.World.HumanWonders.Minimum[group]);
            }
        foreach (var (gold, expected) in new[] { (2000, 31), (2001, 34), (2499, 43), (2500, 82) })
        {
            var f = new PurchaseFixture(); f.City.Production = -39; f.World.Civilizations[1].Gold = gold;
            f.Run(); Equal((short)expected, f.City.ShieldBox);
        }
    }

    private static void PurchaseOrderedOverridesAndNuclearIdentity()
    {
        foreach (var (production, disorder, lost, active, expected) in new (sbyte, bool, bool, byte, short)[]
        {
            (4, false, false, 0, 145), (4, false, true, 0, 1000),
            (45, false, true, 0, 529), (45, false, true, 1, 1000),
            (-39, true, false, 0, 1000), (-BuildingIds.Palace, false, false, 0, 1000)
        })
        {
            var f = new PurchaseFixture(); f.World.Civilizations[1].Gold = 8192; f.City.Production = production;
            f.Rules.Units[45] = f.Rules.Units[4]; f.Rules.Units[4] = f.Rules.Units[4] with { Attack = 99 };
            f.World.Civilizations[1].UnitsActive[45] = active;
            f.City.Flags.Disorder = disorder; f.City.Flags.DefenderLost = lost; f.City.ShieldBox = 1;
            f.Run(); Equal(expected, f.City.ShieldBox);
        }
        var empty = new PurchaseFixture(); empty.Data.Stacks.Detach(0, false); empty.City.ShieldBox = 1;
        empty.Run(); Equal(1, empty.Choices); Equal((short)81, empty.City.ShieldBox);
    }

    private static void PurchaseSpaceshipThreatInputs()
    {
        foreach (int improvement in Enumerable.Range(35, 4))
        {
            var f = new PurchaseFixture(); f.City.Production = (sbyte)-improvement;
            f.World.Civilizations[1].Gold = 1024; f.World.Civilizations[2].SpaceshipFlags.Exists = true;
            f.Run(); Equal((short)8, f.City.ShieldBox);
            f.City.ShieldBox = 0; f.World.Civilizations[1].Gold = 1024;
            f.World.Globals.WonderCity[WonderIds.Apollo] = BuildingQueries.Destroyed;
            f.World.Civilizations[2].SpaceshipFlags.Launched = true;
            f.Run(); Equal((short)16, f.City.ShieldBox);
            f.City.ShieldBox = 0; f.World.Civilizations[1].Gold = 1024;
            f.World.Civilizations[2].SpaceshipFlags.Exists = false;
            f.Run(); Equal((short)0, f.City.ShieldBox); // Race threat alone doubles a zero base.
            f.World.Civilizations[2].SpaceshipFlags.Exists = true; f.World.Globals.Rules.Bloodlust = true;
            f.Run(); Equal((short)8, f.City.ShieldBox);
        }
    }

    private static void PurchaseRefundArithmeticAndFailure()
    {
        foreach (var (gold, rank, worst, expected) in new[] { (640, 7, 1000, 635), (640, 6, 1000, 632), (1024, 7, 0, 1012), (2048, 7, 0, 2012) })
        {
            var f = new PurchaseFixture(); var w = f.World;
            w.Civilizations[1].Gold = gold; w.Globals.Difficulty = Difficulty.Deity;
            w.Globals.BestHumanRank = 3; w.Globals.WorstHumanRank = 4;
            w.Globals.PowerRank[3] = (byte)rank; w.Civilizations[4].Gold = worst;
            f.Run(); Equal(expected, w.Civilizations[1].Gold);
        }
        var negative = new PurchaseFixture(); negative.Rules.Units[4] = negative.Rules.Units[4] with { CostRows = 1 };
        negative.City.ShieldBox = 15; negative.World.Civilizations[1].Gold = 512;
        negative.World.Globals.Difficulty = Difficulty.Deity; negative.World.Globals.PowerRank[0] = 7;
        negative.World.Civilizations[0].Gold = 1000;
        negative.Run(); Equal((short)10, negative.City.ShieldBox); Equal(515, negative.World.Civilizations[1].Gold);
        var wrap = new PurchaseFixture(); wrap.Rules.Units[4] = wrap.Rules.Units[4] with { CostRows = 255 };
        wrap.World.Calculation.ShieldRows = 65536; wrap.World.Globals.Difficulty = (Difficulty)255;
        wrap.World.Civilizations[0].Gold = wrap.World.Civilizations[1].Gold = int.MaxValue;
        wrap.Run(); Equal((short)0, wrap.City.ShieldBox); Equal(2130353356, wrap.World.Civilizations[1].Gold);
        // Budget16711680 narrows to zero stock; unsigned difficulty multiplication wraps before division.
        var failure = new PurchaseFixture(); failure.World.Globals.PowerRank[0] = 8;
        Throws<DivideByZeroException>(() => failure.Run());
        Equal((short)10, failure.City.ShieldBox); Equal(640, failure.World.Civilizations[1].Gold); Equal(0, failure.Notices.Count);
    }

    private static void PurchaseAlmostWonderTiming()
    {
        var f = new PurchaseFixture(); f.City.Production = -39; f.City.ShieldBox = 18;
        f.Rules.Improvements[39] = f.Rules.Improvements[39] with { CostRows = 2 };
        f.World.Civilizations[1].Gold = 0; f.City.Flags.Disorder = true;
        f.World.Calculation.Shields = 3; f.World.Calculation.ShieldSupport = 1;
        f.Run(0); f.Run(0); Equal(2, f.Notices.Count); Equal(new AlmostWonderNotice(new(0), 1, 0), f.Notices[0]);
        f.World.Globals.WonderCity[0] = BuildingQueries.Destroyed; f.Run(); Equal(2, f.Notices.Count);
        f.World.Globals.WonderCity[0] = BuildingQueries.Unbuilt; f.World.Multiplayer.Saved.AutoplayActive = true;
        f.Run(); Equal(2, f.Notices.Count);
        f.World.Multiplayer.Saved.AutoplayActive = false; f.World.Civilizations[1].Gold = 640;
        f.Notice = () => throw new InvalidOperationException("Announcement failed");
        Throws<InvalidOperationException>(() => f.Run()); Equal((short)20, f.City.ShieldBox); Equal(638, f.World.Civilizations[1].Gold);
    }

    private static void UnitProductionActualPurchaseContinuation()
    {
        foreach (bool nextWonder in new[] { false, true })
        {
            var f = new UnitProductionFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            f.World.Civilizations[1].Gold = 1280; f.City.ShieldBox = 16;
            if (nextWonder)
            {
                f.Data.Rules.Improvements[39] = f.Data.Rules.Improvements[39] with { CostRows = 2 };
                f.Choose = _ => f.City.Production = -39;
            }
            Equal(UnitProductionOutcome.Completed, f.Run().Outcome);
            Equal(1, f.World.Entities.Units.End); Equal((short)20, f.City.ShieldBox); Equal(1260, f.World.Civilizations[1].Gold);
            Equal(2, f.Choices); Equal(false, f.City.Flags.DefenderLost);
            Equal(nextWonder ? 1 : 0, f.AlmostWonders.Count);
            Equal(BuildingQueries.Unbuilt, f.World.Globals.WonderCity[0]); // Purchase never runs a second completion.
        }
    }
}
