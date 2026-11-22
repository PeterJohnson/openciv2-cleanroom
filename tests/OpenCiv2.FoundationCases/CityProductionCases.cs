using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CityProductionFixture : ICityProductionEffects, ISpaceshipConstructionEffects
    {
        public UnitProductionFixture Units { get; } = new();
        public ResearchFlowFixture Research { get; }
        public GameState World => Units.World;
        public RuleTables Rules => Units.Data.Rules;
        public ref City City => ref Units.City;
        public CityProduction Production { get; }
        public List<string> Events { get; } = [];
        public List<ImprovementCompletionNotice> Notices { get; } = [];
        public List<CitySlot> Choices { get; } = [];
        public int NextItem = 4, PartReply;
        public bool WindowShown { get; set; }
        public Action? OnBuilt, OnDraw;
        public Func<CitySlot, int>? OnChoose;
        public CityProductionFixture()
        {
            Research = new(new GrantFixture(Units.Data));
            Rules.Cosmic = Rules.Cosmic with { SpaceshipMassThrustParadigm = 75 };
            var orders = new ProductionOrders(World, Rules, c => { Choices.Add(c); return OnChoose?.Invoke(c) ?? NextItem; }, n => Events.Add("wonder-change"));
            Production = new(World, Rules, Units.Production, orders, Research.Flow,
                new(World, Rules, n => Events.Add("upgrade"), p => Events.Add("redraw")),
                new(World, Rules, ClassicShip, ClassicCalendar),
                new(World, Rules, ClassicShip, ClassicCalendar, c => orders.Set(c, 99), this),
                ClassicShip, new(World, Units.Data.Process.Rules, Units.Data.Stacks), this);
        }
        public CityProduction.Operation Start(int improvement, short stock = 40)
        {
            City.Production = (sbyte)-improvement; City.ShieldBox = stock;
            return Production.Begin(new(0), "production");
        }
        public CityProductionResult Run(int improvement, short stock = 40)
        {
            var op = Start(improvement, stock); Equal((ResearchChoiceNotice?)null, op.Resume()); return op.Result!;
        }
        public void DrawProduction(CitySlot city) { Events.Add("draw-production"); OnDraw?.Invoke(); }
        public void DrawImprovements(CitySlot city) => Events.Add("draw-improvements");
        public void InvalidBuild(CitySlot city, int improvement, string name) => Events.Add("invalid:" + improvement);
        public void BadSpace(CitySlot city, int improvement, string name) => Events.Add("bad-space:" + improvement);
        public void Manhattan(CitySlot city, int recipient) => Events.Add("manhattan:" + recipient);
        public void CompletionSound(CitySlot city, int improvement) { Events.Add("sound:" + improvement); }
        public void Built(ImprovementCompletionNotice notice) { Notices.Add(notice); Events.Add("built:" + notice.Improvement); OnBuilt?.Invoke(); }
        public void WonderMovie(int wonder) => Events.Add("movie:" + wonder);
        public void CapitalMoved(CitySlot city, int owner, string name, string adjective) => Events.Add("capital");
        public void RedrawMaps() => Events.Add("maps");
        public void Astronauts() => Events.Add("astronauts");
        public void AlmostWonder(AlmostWonderNotice notice) => Events.Add("almost");
        public int ChoosePart(SpaceshipPartChoice choice) { Events.Add("choose-part"); return PartReply; }
        public void NoFurther(int civilization, int part) => Events.Add("full-part");
        public void Launched(SpaceshipLaunchNotice notice) => Events.Add("launched");
        public void ShowShip(int civilization) => Events.Add("ship");
    }

    // C06 production parent, with actual U01/C08/V03/V07 child services.
    private static void CityProductionDispatchStocksAndDuplicates()
    {
        var f = new CityProductionFixture(); f.City.Production = 4; f.City.ShieldBox = 15;
        f.World.Production.CompletedImprovement = 8;
        var unit = f.Production.Begin(new(0), "unit"); unit.Resume();
        Equal(UnitProductionOutcome.NotReady, unit.Result!.Unit!.Outcome); Equal((short)19, f.City.ShieldBox); Equal((int?)null, f.World.Production.CompletedImprovement);
        var result = f.Run(BuildingIds.Temple, 29); Equal(CityProductionOutcome.Completed, result.Outcome);
        Equal(true, BuildingQueries.HasBuilding(f.World, new(0), 4)); Equal((short)0, f.City.ShieldBox);
        Equal((int?)4, f.World.Production.CompletedImprovement); Equal((sbyte)-4, f.City.Production); Equal(false, f.City.Flags.DefenderLost);
        result = f.Run(4, 40); Equal(CityProductionOutcome.AlreadyBuilt, result.Outcome); Equal((short)44, f.City.ShieldBox);
        Equal((int?)null, f.World.Production.CompletedImprovement); Equal(true, f.Events.Contains("invalid:4")); Equal(1, f.Notices.Count);
        f.World.Multiplayer.Saved.AutoplayActive = true; f.Events.Clear(); f.Run(4, 40); Equal(0, f.Events.Count);
        result = f.Run(BuildingIds.Capitalization, 10); Equal(CityProductionOutcome.NotReady, result.Outcome); Equal((short)10, f.City.ShieldBox);
        f.World.Multiplayer.Saved.AutoplayActive = false; result = f.Run(6, short.MaxValue);
        Equal(CityProductionOutcome.NotReady, result.Outcome); Equal((short)-32765, f.City.ShieldBox);
    }

    private static void CityProductionPalaceAndAiRemainder()
    {
        var f = new CityProductionFixture(); f.Units.Data.AddCity(1, 1, new(14, 10)); f.Units.Data.AddCity(2, 2, new(16, 10));
        BuildingQueries.SetBuilding(f.World, new(1), 1, true); BuildingQueries.SetBuilding(f.World, new(2), 1, true);
        f.World.Civilizations[1].CapitalX = 99; f.Run(1, 40);
        Equal(true, BuildingQueries.HasBuilding(f.World, new(0), 1)); Equal(false, BuildingQueries.HasBuilding(f.World, new(1), 1));
        Equal(true, BuildingQueries.HasBuilding(f.World, new(2), 1)); Equal((short)10, f.World.Civilizations[1].CapitalX);
        Equal(true, f.Events.Contains("capital")); Equal((short)0, f.City.ShieldBox);
        f = new(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.Saved.AutoplayActive = true;
        f.Units.Data.AddUnit(0, 1, 4, f.City.Position); f.City.Flags.DefenderLost = false;
        f.OnChoose = c => { Equal((sbyte)-2, f.City.Production); return 4; };
        f.Run(1, 40); Equal((short)14, f.City.ShieldBox); Equal(1, f.Choices.Count); Equal(0, f.Notices.Count);
        Equal((byte)1, f.World.Civilizations[1].UnitsInProduction[4]); Equal((int?)null, f.World.Production.CompletedImprovement);
    }

    private static void CityProductionWonderRaceAndLoss()
    {
        var f = new CityProductionFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        f.World.Calculation.ShieldRows = 5; f.Units.Data.AddCity(1, 2, new(14, 10));
        f.World.Entities.Cities.Storage[1].Production = -39; f.World.Entities.Cities.Storage[1].ShieldBox = 30;
        f.City.Flags.WonderBuilt = true; f.City.Flags.DefenderLost = true;
        var result = f.Run(39, 20); Equal(CityProductionOutcome.HumanWonderPriority, result.Outcome);
        Equal((short)24, f.City.ShieldBox); Equal(false, f.City.Flags.WonderBuilt); Equal(true, f.City.Flags.DefenderLost);
        Equal((short)-1, f.World.Globals.WonderCity[0]); Equal(0, f.Choices.Count);
        f.World.Entities.Cities.Storage[1].ShieldBox = 29; f.World.Multiplayer.Saved.AutoplayActive = true;
        result = f.Run(39, 20); Equal(CityProductionOutcome.Completed, result.Outcome); Equal((short)0, f.World.Globals.WonderCity[0]);
        Equal((short)0, f.City.ShieldBox); Equal(true, f.City.Flags.WonderBuilt); Equal(8, f.City.KnownTo.Count);
        f = new(); f.World.Globals.WonderCity[0] = -2;
        result = f.Run(39, 40); Equal(CityProductionOutcome.LostWonder, result.Outcome); Equal((short)44, f.City.ShieldBox);
        Equal((short)-2, f.World.Globals.WonderCity[0]); Equal(false, f.City.Flags.DefenderLost);
    }

    private static void CityProductionNoticeOrderAndFailure()
    {
        var f = new CityProductionFixture { WindowShown = true }; f.World.Globals.GameOptions.WonderMovies = true;
        f.OnBuilt = () => { Equal((short)14, f.City.ShieldBox); Equal((short)0, f.World.Globals.WonderCity[0]); };
        int draws = 0; f.OnDraw = () => { if (++draws == 2) f.WindowShown = false; };
        f.Run(39, 40); Sequence(new[] { "draw-production", "sound:39", "built:39", "movie:0", "draw-production", "draw-improvements" }, f.Events);
        Equal(true, f.Notices.Single().ForcedCityNotice); Equal(187, f.Notices[0].WordingLabel);
        f = new(); f.World.Globals.MessageOptions.ImprovementBuilt = true; f.Run(4); Equal(0, f.Notices.Count); Equal(true, f.Events.Contains("sound:4"));
        f = new(); f.World.Multiplayer.LocalHuman = 2; f.World.Globals.MessageOptions.ImprovementBuilt = true; f.Run(39);
        Equal(false, f.Notices.Single().ForcedCityNotice);
        f = new(); f.OnBuilt = () => throw new InvalidOperationException("Notice stopped");
        var op = f.Start(39); Throws<InvalidOperationException>(() => op.Resume());
        Equal((short)0, f.World.Globals.WonderCity[0]); Equal((short)14, f.City.ShieldBox); Equal(true, f.City.Flags.DefenderLost);
        Throws<InvalidOperationException>(() => op.Resume()); Equal((short)14, f.City.ShieldBox);
    }

    private static void CityProductionManhattanAndWonderChildren()
    {
        var f = new CityProductionFixture(); for (int c = 0; c < 8; ++c) f.World.Civilizations[c].Reputation = 20;
        f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2); f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        f.City.KnownTo = CivilizationSet.Only(2); f.Run(39 + WonderIds.Manhattan);
        Equal((byte)20, f.World.Civilizations[0].Reputation);
        for (int c = 1; c < 8; ++c) Equal((byte)6, f.World.Civilizations[c].Reputation);
        Equal("manhattan:1", f.Events.First());
        f = new(); f.World.Civilizations[1].Reputation = 7; f.World.Civilizations[2].Treaties[1].Vendetta = true;
        f.Run(39 + WonderIds.EiffelTower); Equal((byte)3, f.World.Civilizations[1].Reputation); Equal(true, f.World.Civilizations[1].Flags.EiffelApplied);
        Equal(false, f.World.Civilizations[2].Treaties[1].Vendetta);
        f = new(); f.Units.Data.AddCity(1, 2, new(14, 10), 5); f.Run(39 + WonderIds.Apollo);
        Equal(8, f.World.Map.TileAt(new(0, 0)).RevealedTo.Count); Equal((byte)5, f.World.Entities.Cities.Storage[1].RememberedSize[7]);
        Equal(true, f.Events.IndexOf("maps") < f.Events.IndexOf("astronauts"));
        f = new(); f.Units.Data.AddUnit(0, 1, 0, f.City.Position);
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(5);
        f.Rules.Units[0] = f.Rules.Units[0] with { ObsoleteBy = (AdvanceReference)5 };
        f.Rules.Units[61] = f.Rules.Units[0] with { ObsoleteBy = AdvanceReference.Never, Prerequisite = (AdvanceReference)5 };
        f.Run(39 + WonderIds.Leonardo); Equal(new UnitType(61), f.Units.Data.Unit(0).Type); Equal(true, f.Events.Contains("upgrade"));
    }

    private static void CityProductionDarwinSequentialResearch()
    {
        var f = new CityProductionFixture(); f.Research.Enable(0, 1, 2); f.World.Civilizations[1].Researching = 0;
        f.Units.Data.AddCity(1, 2, new(14, 10)); f.World.Entities.Cities.Storage[1].Production = -57;
        var op = f.Start(57); var first = op.Resume()!;
        Equal((short)0, f.World.Globals.WonderCity[18]); Equal((short)0, f.City.ShieldBox);
        Sequence(new[] { 0 }, f.Research.Completions.Select(n => n.Advance)); Equal(0, f.Choices.Count); Equal(true, f.City.Flags.DefenderLost);
        Equal(first, op.Resume()); var second = op.Resume(new(1))!;
        Sequence(new[] { 0, 1 }, f.Research.Completions.Select(n => n.Advance)); Equal(0, f.Choices.Count);
        Equal((ResearchChoiceNotice?)null, op.Resume(new(2))); Equal(true, op.Finished);
        Equal((short)2, f.World.Civilizations[1].Researching); Equal((byte)2, f.World.Civilizations[1].NumAdvances);
        Sequence(new[] { new CitySlot(1) }, f.Choices); Equal((sbyte)4, f.World.Entities.Cities.Storage[1].Production);
        Equal(false, f.City.Flags.DefenderLost); Equal(186, f.Notices[0].WordingLabel);
    }

    private static void CityProductionSpaceshipAndAutomation()
    {
        var f = new CityProductionFixture(); f.World.Globals.Year = 1900; f.World.Globals.Turn = 401;
        var result = f.Run(BuildingIds.Structural); Equal(CityProductionOutcome.Completed, result.Outcome);
        Equal((short)1, f.World.Civilizations[1].SpaceshipParts[0]); Equal(true, f.World.Civilizations[1].SpaceshipFlags.Exists);
        Equal(false, BuildingQueries.HasBuilding(f.World, new(0), 35)); Equal((short)0, f.City.ShieldBox);
        f.World.Civilizations[1].SpaceshipFlags.Launched = true; f.World.Multiplayer.Saved.AutoplayActive = true;
        result = f.Run(35); Equal(CityProductionOutcome.BadSpace, result.Outcome); Equal((short)44, f.City.ShieldBox);
        Equal(true, f.Events.Contains("bad-space:35"));
        f = new() { PartReply = 1 }; f.World.Civilizations[1].SpaceshipParts[1] = 16;
        Equal(true, SpaceshipQueries.GroupFull(f.World, ClassicShip, 1, 1));
        f.Run(BuildingIds.Component); // Production tests each subtype maximum, not the full group's sum.
        Equal((short)1, f.World.Civilizations[1].SpaceshipParts[2]); Equal(true, f.Events.Contains("choose-part"));
        f = new(); f.City.Flags.Autobuild = true; f.Run(4);
        Equal(2, f.Choices.Count); Equal((short)0, f.City.ShieldBox); Equal((sbyte)4, f.City.Production); // Announcement and common tail each reconsider.
    }
    private static void CityProductionAiSpaceshipRetarget()
    {
        var f = new CityProductionFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        f.World.Globals.Turn = 401; f.World.Globals.Year = 1900; f.World.Multiplayer.Saved.AutoplayActive = true;
        f.City.Flags.DefenderLost = false; f.Units.Data.AddUnit(0, 1, 4, f.City.Position);
        short[] parts = [15, 4, 3, 0, 0, 0];
        for (int i = 0; i < parts.Length; ++i) f.World.Civilizations[1].SpaceshipParts[i] = parts[i];
        for (int i = 35; i <= 37; ++i) f.Rules.Improvements[i] = f.Rules.Improvements[i] with { Prerequisite = AdvanceReference.None };
        f.Rules.Improvements[37] = f.Rules.Improvements[37] with { CostRows = 10 };
        var result = f.Run(35, 40); Equal(CityProductionOutcome.NotReady, result.Outcome);
        Equal((sbyte)-37, f.City.Production); Equal((short)44, f.City.ShieldBox);
        Equal((short)15, f.World.Civilizations[1].SpaceshipParts[0]); Equal((short)0, f.World.Civilizations[1].SpaceshipParts[3]);
        f.Rules.Improvements[37] = f.Rules.Improvements[37] with { CostRows = 3 };
        result = f.Run(35, 40); Equal(CityProductionOutcome.Completed, result.Outcome);
        Equal((short)1, f.World.Civilizations[1].SpaceshipParts[3]); Equal((short)15, f.World.Civilizations[1].SpaceshipParts[0]);
        Equal((short)0, f.City.ShieldBox); Equal((sbyte)4, f.City.Production);
    }

}
