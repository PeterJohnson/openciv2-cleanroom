using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static SpaceshipValues ClassicShip => GameplayContent.Builtin.Values.Spaceship;

    private sealed class ShipFixture : ISpaceshipConstructionEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public SpaceshipPerformance Performance { get; }
        public SpaceshipConstruction Construction { get; }
        public SpaceshipWorkspace Work => World.Spaceship;
        public SpaceshipValues Values { get; }
        public List<SpaceshipPartChoice> Choices { get; } = [];
        public List<int> FullParts { get; } = [];
        public List<int> Windows { get; } = [];
        public List<CitySlot> Reselections { get; } = [];
        public List<SpaceshipLaunchNotice> Launches { get; } = [];
        public Func<SpaceshipPartChoice, int>? Choose;
        public Action? OnFull, OnLaunch;
        public Action<CitySlot>? OnReselect;

        public ShipFixture(bool human = true, SpaceshipValues? values = null, RevelationFixture? data = null)
        {
            Data = data ?? new();
            Values = values ?? ClassicShip;
            World.Globals.HumanPlayers = CivilizationSet.Only(human ? 1 : 2);
            World.Globals.Difficulty = Difficulty.Prince;
            World.Globals.FirstLaunchTurn = -1; World.Globals.Turn = 401; World.Globals.Year = 1900;
            Data.Rules.Cosmic = Data.Rules.Cosmic with { SpaceshipMassThrustParadigm = 75 };
            for (int i = 35; i <= 37; ++i) Data.Rules.Improvements[i] = Data.Rules.Improvements[i] with { Prerequisite = AdvanceReference.None };
            Performance = new(World, Data.Rules, Values, ClassicCalendar);
            Construction = new(World, Data.Rules, Values, ClassicCalendar,
                city => { Reselections.Add(city); OnReselect?.Invoke(city); }, this);
        }
        public void Parts(params short[] parts)
        {
            for (int p = 0; p < 6; ++p) World.Civilizations[1].SpaceshipParts[p] = parts[p];
        }
        public int ChoosePart(SpaceshipPartChoice choice)
        {
            Choices.Add(choice);
            return Choose?.Invoke(choice) ?? throw new InvalidOperationException("Human part selection requires a reply");
        }
        public void NoFurther(int civilization, int part) { Equal(1, civilization); FullParts.Add(part); OnFull?.Invoke(); }
        public void Launched(SpaceshipLaunchNotice notice) { Launches.Add(notice); OnLaunch?.Invoke(); }
        public void ShowShip(int civilization) => Windows.Add(civilization);
    }

    private static void SpaceshipPerformanceClassicAndFusion()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 8, 4, 4, 4); f.Performance.Calculate(1, true);
        Equal(295, f.Work.Mass); Equal(156, f.Work.FlightTime); Equal(100, f.Work.Success);
        Equal(100, f.Work.Support); Equal(100, f.Work.Energy); Equal(100, f.Work.Fuel);
        Equal((short)4, f.World.Civilizations[1].SpaceshipPopulation);
        Equal((short)1900, f.World.Civilizations[1].SpaceshipLaunchYear);
        Equal((short)1915, f.World.Civilizations[1].SpaceshipArrivalYear); Equal(1915, f.Work.ProjectedArrival);
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.FusionPower);
        f.Performance.Calculate(1, true); Equal(117, f.Work.FlightTime); Equal((short)1911, f.World.Civilizations[1].SpaceshipArrivalYear);
        f.World.Civilizations[1].Advances = default; f.Performance.Calculate(1, true);
        Equal(true, f.World.Civilizations[1].SpaceshipFlags.FusionAtLaunch); Equal(117, f.Work.FlightTime);
        f.World.Civilizations[1].SpaceshipFlags.FusionAtLaunch = false;
        f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { SpaceshipMassThrustParadigm = 151 };
        f.Performance.Calculate(1, true); Equal(312, f.Work.FlightTime); Equal(84, f.Work.Success); Equal((short)1931, f.World.Civilizations[1].SpaceshipArrivalYear);
    }

    private static void SpaceshipPerformanceMissingAndUnsupported()
    {
        var f = new ShipFixture(); f.Parts(15, 8, 8, 1, 1, 1); f.Performance.Calculate(1, false);
        Equal(127, f.Work.Mass); Equal(307, f.Work.FlightTime); Equal(85, f.Work.Success); Equal(75, f.Work.Fuel);
        Equal((short)1930, f.World.Civilizations[1].SpaceshipArrivalYear); Equal(1906, f.Work.ProjectedArrival);
        f.Parts(39, 8, 8, 4, 0, 4); f.Performance.Calculate(1, false);
        Equal(247, f.Work.Mass); Equal(131, f.Work.FlightTime); Equal(0, f.Work.Support); Equal(200, f.Work.Energy);
        Equal(100, f.Work.Success); Equal((short)1912, f.World.Civilizations[1].SpaceshipArrivalYear); Equal(1912, f.Work.ProjectedArrival);
        f.Performance.Calculate(1, true); Equal(0, f.Work.Success);
        f.Parts(0, 0, 0, 0, 0, 0); f.Performance.Calculate(1, false);
        Equal(57, f.Work.Mass); Equal(4275, f.Work.FlightTime); Equal(0, f.Work.Success); Equal((short)1900, f.World.Civilizations[1].SpaceshipArrivalYear);
        f.Parts(-10, 8, 8, 4, 4, 4); f.Performance.Calculate(1, false);
        Equal(246, f.Work.Mass); Equal(18450, f.Work.FlightTime); Equal(-200, f.Work.Fuel); Equal(1913, f.Work.ProjectedArrival);
    }

    private static void SpaceshipPreviewRetentionAndCalendar()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 8, 4, 4, 4);
        f.World.Globals.Status.Scenario = true; f.World.Scenario.StartYear = short.MaxValue; f.World.Scenario.YearIncrement = 1;
        f.World.Globals.Turn = 1; f.Performance.Calculate(1, true);
        Equal(short.MaxValue, f.World.Civilizations[1].SpaceshipLaunchYear);
        Equal((short)-32754, f.World.Civilizations[1].SpaceshipArrivalYear); Equal(32782, f.Work.ProjectedArrival);
        f.World.Civilizations[1].SpaceshipFlags.Launched = true;
        f.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.FusionPower);
        f.World.Civilizations[1].SpaceshipPopulation = 7; f.World.Globals.Turn = 2;
        f.Performance.Calculate(1, true);
        Equal(false, f.World.Civilizations[1].SpaceshipFlags.FusionAtLaunch); Equal(156, f.Work.FlightTime);
        Equal((short)7, f.World.Civilizations[1].SpaceshipPopulation); Equal(short.MaxValue, f.World.Civilizations[1].SpaceshipLaunchYear);
        Equal((short)-32754, f.World.Civilizations[1].SpaceshipArrivalYear); Equal(32782, f.Work.ProjectedArrival);
        SpaceshipLifecycle.Reset(f.World, 1); Equal(32782, f.Work.ProjectedArrival); Equal(295, f.Work.Mass);
        Equal(0, new GameState(f.Data.Process).Spaceship.ProjectedArrival);
        f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => f.Performance.Calculate(1, true)); Equal(295, f.Work.Mass);
    }

    private static SpaceshipValues CustomShipValues()
    {
        var input = new BundleInput(); input.Custom(); input.Edit("spaceship", root =>
        {
            int[] maxima = [2, 1, 2, 0, 1, 2];
            for (int p = 0; p < 6; ++p) root["parts"]![GameplayNames.SpaceshipParts[p]]!["maximumCount"] = maxima[p];
            root["parts"]!["structural"]!["massTons"] = 500;
        });
        return input.Load().Values.Spaceship;
    }

    private static void SpaceshipConfiguredMaximaAndMass()
    {
        var values = CustomShipValues(); var f = new ShipFixture(values: values);
        f.Parts(39, 3, 0, 4, 4, 4);
        Equal(2, SpaceshipQueries.PartLimit(f.World, values, 1, 0));
        Equal(3, SpaceshipQueries.GroupMaximum(values, 1)); Equal(3, SpaceshipQueries.GroupMaximum(values, 2));
        Equal(true, SpaceshipQueries.GroupFull(f.World, values, 1, 1));
        Equal(false, SpaceshipQueries.PartMaxed(f.World, values, 1, BuildingIds.Component));
        Equal(0, SpaceshipQueries.PartCount(f.World, values, 1, 3));
        Equal(2, SpaceshipQueries.PartCount(f.World, values, 1, 5));
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); Equal(1, SpaceshipQueries.PartCount(f.World, values, 1, 5));
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        f.Parts(2, 1, 2, 0, 1, 2); Equal(-1, f.Construction.AddPart(1, 0)); Equal(-1, f.Construction.AddPart(1, 1)); Equal(-1, f.Construction.AddPart(1, 2));
        f.Parts(39, 8, 8, 4, 4, 4); f.Performance.Calculate(1, false); Equal(451, f.Work.Mass); // Stored excess contributes mass.
        f.Parts(39, 0, 2, 0, 1, 2); f.Choose = _ => 1;
        f.OnFull = () => f.Choose = _ => 0;
        Equal(1, f.Construction.AddPart(1, 1)); Sequence(new[] { 2 }, f.FullParts); Equal((short)1, f.World.Civilizations[1].SpaceshipParts[1]);
    }

    private static void SpaceshipGroupChoiceAndRace()
    {
        var f = new ShipFixture(false); f.Parts(15, 4, 3, 0, 0, 0);
        Equal(2, f.Performance.ChooseGroup(1, 0));
        f.Parts(31, 4, 4, 1, 1, 1); f.World.Globals.Year = 1910;
        Equal(0, f.Performance.ChooseGroup(1, 1)); // Own arrival1920: close race rejects components.
        f.World.Civilizations[2].SpaceshipArrivalYear = 1919; // Preview alone, without Exists/Launched/alive.
        Equal(1, f.Performance.ChooseGroup(1, 1));
        f.World.Civilizations[1].SpaceshipFlags.Launched = true; f.Work.Mass = -1;
        Equal(-1, f.Performance.ChooseGroup(1, 0)); Equal(111, f.Work.Mass); // Calculation precedes the rejection.
        f.World.Civilizations[1].SpaceshipFlags.Launched = false;
        for (int i = 35; i <= 37; ++i) f.Data.Rules.Improvements[i] = f.Data.Rules.Improvements[i] with { Prerequisite = AdvanceReference.Never };
        Equal(-2, f.Performance.ChooseGroup(1, 0));
    }

    private static void SpaceshipGroupEmergencyEquality()
    {
        var f = new ShipFixture(false); f.Parts(15, 4, 3, 1, 1, 1);
        f.World.Civilizations[2].SpaceshipFlags.Launched = true; f.World.Civilizations[2].SpaceshipArrivalYear = 1900;
        Equal(false, SpaceshipQueries.GroupCanBuild(f.World, f.Data.Rules, f.Values, 1, 1));
        Equal(1, f.Performance.ChooseGroup(1, 0)); // Raw engine equals support limit; emergency path bypasses groupCanBuild.
        f.Parts(15, 5, 4, 1, 1, 1); Equal(0, f.Performance.ChooseGroup(1, 0));
        f.Parts(31, 4, 4, 1, 1, 1); f.World.Civilizations[2].SpaceshipFlags.Launched = false;
        f.World.Globals.Status.Scenario = true; f.World.Scenario.StartYear = short.MaxValue; f.World.Scenario.YearIncrement = 1; f.World.Globals.Turn = 1;
        Equal(0, f.Performance.ChooseGroup(1, 1)); // Wrapped negative preview still satisfies raceClose.
        Equal(true, f.World.Civilizations[1].SpaceshipArrivalYear < 0);
    }

    private static void SpaceshipHumanSelectionAndRepeats()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 7, 4, 4, 4); int reply = 0; f.Choose = _ => reply++;
        Equal(2, f.Construction.AddPart(1, 1)); Equal(2, f.Choices.Count); Sequence(new[] { 1 }, f.FullParts);
        Equal(new SpaceshipPartChoice(1, 1, 8, 7, 0), f.Choices[0]); Equal((short)8, f.World.Civilizations[1].SpaceshipParts[2]);
        Equal(false, f.World.Civilizations[1].SpaceshipFlags.Launched);
        f = new(); f.Parts(39, 8, 7, 4, 4, 4); f.Choose = _ => 0;
        f.OnFull = () => f.World.Civilizations[1].SpaceshipParts[2] = 8;
        Equal(1, f.Construction.AddPart(1, 1)); Equal(false, f.World.Civilizations[1].SpaceshipFlags.Exists); Equal(1, f.Choices.Count);
        f = new(); f.Parts(10, 0, 0, 0, 0, 0); f.Choose = _ => -1;
        Equal(0, f.Construction.AddPart(1, 1)); Equal((short)11, f.World.Civilizations[1].SpaceshipParts[0]);
        f = new(); f.Choose = _ => 99;
        Throws<IndexOutOfRangeException>(() => f.Construction.AddPart(1, 1)); Equal(false, f.World.Civilizations[1].SpaceshipFlags.Exists);
        Equal((short)1900, f.World.Civilizations[1].SpaceshipLaunchYear); Equal(57, f.Work.Mass);
        f = new(); f.Choose = choice => { Equal(2, choice.Group); return 2; };
        Equal(5, f.Construction.AddPart(1, -35)); Equal((short)1, f.World.Civilizations[1].SpaceshipParts[5]);
        foreach (var (stock, expected) in new (short, short)[] { (8, 9), (short.MaxValue, short.MinValue) })
        {
            f = new(); f.Parts(39, stock, 7, 1, 0, 0);
            f.Choose = _ => { f.World.Globals.HumanPlayers = CivilizationSet.Only(2); return 0; };
            Equal(3, f.Construction.AddPart(1, 1)); Equal(expected, f.World.Civilizations[1].SpaceshipParts[1]); Equal(0, f.FullParts.Count);
        }
    }

    private static void SpaceshipFirstShipContactsAndAiChoices()
    {
        var f = new ShipFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2).With(7);
        f.World.Civilizations[2].SpaceshipFlags.Exists = true;
        for (int a = 0; a < 8; ++a) for (int b = 0; b < 8; ++b) f.World.Civilizations[a].LastContactTurn[b] = 77;
        Equal(0, f.Construction.AddPart(1, 0)); Equal(true, f.World.Civilizations[1].SpaceshipFlags.Exists);
        for (int a = 0; a < 8; ++a) for (int b = 0; b < 8; ++b)
            Equal((short)((a is 1 or 7) && b != 0 ? 0 : 77), f.World.Civilizations[a].LastContactTurn[b]);
        f.World.Civilizations[1].LastContactTurn[2] = 5; f.Construction.AddPart(1, 0); Equal((short)5, f.World.Civilizations[1].LastContactTurn[2]);
        f = new(false); Equal(3, f.Construction.AddPart(1, 1)); Equal((short)1, f.World.Civilizations[1].SpaceshipParts[2]);
        Equal(3, f.Construction.AddPart(1, 2)); Equal((short)1, f.World.Civilizations[1].SpaceshipParts[3]);
        Equal(3, f.Construction.AddPart(1, 2)); Equal((short)1, f.World.Civilizations[1].SpaceshipParts[4]); Equal(0, f.Choices.Count);
        var large = ClassicShip with { Parts = new(ClassicShip.Parts.Select(p => p with { MaximumCount = 2000 })) };
        f = new(false, large); f.Parts(39, 8, 8, 1000, 1000, 1000); f.World.Civilizations[2].LastContactTurn[1] = 77;
        Throws<ArgumentOutOfRangeException>(() => f.Construction.AddPart(1, 2)); // AI minimum999 selects no backed subtype.
        Equal(true, f.World.Civilizations[1].SpaceshipFlags.Exists); Equal((short)0, f.World.Civilizations[2].LastContactTurn[1]);
        Equal((short)1000, f.World.Civilizations[1].SpaceshipParts[3]); // Stop before the invalid count access.
    }

    private static void SpaceshipAiLaunchThresholds()
    {
        var full = new ShipFixture(false); full.Parts(38, 8, 8, 1, 0, 1);
        Equal(3, full.Construction.AddPart(1, 0)); Equal(true, full.World.Civilizations[1].SpaceshipFlags.Launched);
        Equal(0, full.Work.Support); Equal(100, full.Work.Success); Sequence(new[] { -1 }, full.Windows);
        foreach (var (rank, gold, expected) in new[] { (0, 999, false), (1, 999, true), (0, 1000, true) })
        {
            var f = new ShipFixture(false); f.Parts(15, 1, 1, 1, 1, 1);
            f.World.Globals.PowerRank[2] = (byte)rank; f.World.Civilizations[2].SpaceshipFlags.Exists = true; f.World.Civilizations[2].Gold = gold;
            Equal(3, f.Construction.AddPart(1, 0)); Equal(66, f.Work.Success); Equal(expected, f.World.Civilizations[1].SpaceshipFlags.Launched);
        }
        var quick = new ShipFixture(false); quick.Parts(15, 4, 3, 1, 1, 1);
        quick.Construction.AddPart(1, 0); Equal(true, quick.Work.Success > 75); Equal(true, quick.World.Civilizations[1].SpaceshipFlags.Launched);
    }

    private static void SpaceshipAiLaunchRaceAndSlotZero()
    {
        foreach (var (arrival, expected) in new[] { (1948, false), (1949, false), (1950, true) })
        {
            var f = new ShipFixture(false); f.Parts(15, 1, 1, 1, 1, 1);
            f.World.Civilizations[2].SpaceshipFlags.Launched = true; f.World.Civilizations[2].SpaceshipArrivalYear = (short)arrival;
            f.Construction.AddPart(1, 0); Equal((short)1949, f.World.Civilizations[1].SpaceshipArrivalYear);
            Equal(expected, f.World.Civilizations[1].SpaceshipFlags.Launched);
        }
        foreach (var (slotZero, expected) in new[] { (2000, false), (1900, true) })
        {
            var f = new ShipFixture(false); f.Parts(15, 1, 1, 1, 1, 1); f.World.Globals.Difficulty = Difficulty.Emperor;
            // Keep the same calendar basis while changing the policy difficulty.
            f.World.Globals.Status.Scenario = true; f.World.Scenario.StartYear = 1500; f.World.Scenario.YearIncrement = 1;
            f.World.Civilizations[0].SpaceshipArrivalYear = (short)slotZero;
            f.World.Civilizations[3].SpaceshipFlags.Launched = true; f.World.Civilizations[3].SpaceshipArrivalYear = 1905;
            f.World.Globals.PowerRank[2] = 1;
            f.Construction.AddPart(1, 0); Equal(expected, f.World.Civilizations[1].SpaceshipFlags.Launched);
        }
    }

    private static void SpaceshipLaunchMutationsAndCallbacks()
    {
        var f = new ShipFixture(false); f.World.Globals.Turn = 100; f.World.Names[1].Tribe = "Alders";
        f.World.Civilizations[1].SpaceshipLaunchYear = 111; f.World.Civilizations[1].SpaceshipArrivalYear = 222;
        f.Data.AddCity(0, 1, new(10, 10)); f.Data.AddCity(1, 2, new(12, 10)); f.Data.AddCity(2, 1, new(14, 10));
        f.OnReselect = city => { if (city.Value == 0) f.Data.AddCity(3, 1, new(16, 10)); };
        f.Construction.Launch(1); Equal((short)100, f.World.Globals.FirstLaunchTurn);
        Equal(-24, Calendar.Year(101, f.World.Globals, f.World.Scenario, ClassicCalendar));
        Equal(new SpaceshipLaunchNotice(1, "Alders", 222), f.Launches.Single()); Sequence(new[] { -1 }, f.Windows);
        Sequence(new[] { new CitySlot(0), new CitySlot(2), new CitySlot(3) }, f.Reselections);
        Equal(false, f.World.Civilizations[1].SpaceshipFlags.Exists); Equal((short)111, f.World.Civilizations[1].SpaceshipLaunchYear);
        f.World.Globals.Turn = 101; f.Construction.Launch(1); Equal((short)100, f.World.Globals.FirstLaunchTurn);
        f = new(false); f.OnLaunch = () => f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        f.World.Civilizations[1].SpaceshipArrivalYear = 10; f.World.Civilizations[3].SpaceshipArrivalYear = 20;
        f.World.Civilizations[3].SpaceshipFlags.Launched = true;
        f.Construction.Launch(1); Sequence(new[] { -1 }, f.Windows); Equal(0, f.Reselections.Count);
        Equal(true, f.World.Civilizations[1].Treaties[3].Incident); Equal(true, f.World.Civilizations[3].Treaties[1].Incident);
        f = new(false); f.OnLaunch = () => throw new InvalidOperationException("Presentation failed");
        Throws<InvalidOperationException>(() => f.Construction.Launch(1));
        Equal(true, f.World.Civilizations[1].SpaceshipFlags.Launched); Equal((short)401, f.World.Globals.FirstLaunchTurn); Equal(0, f.Windows.Count);
        f = new(false); f.Construction.Launch(0); Sequence(new[] { 0 }, f.Windows);
    }

    private static void SpaceshipHumanLaunchRelations()
    {
        foreach (var (difficulty, reputation, alliance) in new[]
        {
            (Difficulty.Warlord, 7, false), (Difficulty.Prince, 0, false), (Difficulty.King, 1, false),
            (Difficulty.Emperor, 0, false), (Difficulty.Deity, 0, true)
        })
        {
            var f = new ShipFixture(); var w = f.World; w.Globals.Difficulty = difficulty; w.Civilizations[1].Reputation = (byte)reputation;
            w.Civilizations[1].SpaceshipArrivalYear = 10; w.Civilizations[2].SpaceshipArrivalYear = 20; w.Civilizations[2].SpaceshipFlags.Launched = true;
            w.Civilizations[1].Treaties[2].Alliance = alliance;
            w.Civilizations[2].Treaties[1] = new() { Alliance = true, Ceasefire = true, Peace = true, HostileIntent = true, Contact = true };
            var forward = w.Civilizations[1].Treaties[2];
            f.Construction.Launch(1); Equal(0, f.Launches.Count); Equal(0, f.Windows.Count);
            var reverse = w.Civilizations[2].Treaties[1];
            bool incident = difficulty < Difficulty.Prince || (difficulty < Difficulty.Emperor && reputation == 0);
            Equal(incident, reverse.Incident); Equal(incident, w.Civilizations[1].Treaties[2].Incident);
            bool broken = difficulty >= Difficulty.Emperor && !alliance;
            Equal(broken, reverse.BrokenTreaty && reverse.Aggressor && reverse.ExtendedAggressor);
            Equal(!broken, reverse.Peace); Equal(!broken, reverse.Ceasefire); Equal(!broken, reverse.HostileIntent);
            Equal(true, reverse.Alliance && reverse.Contact); // Direct reverse writes preserve even contradictory Alliance.
            if (!incident) Equal(forward, w.Civilizations[1].Treaties[2]);
        }
    }

    private static void DiplomaticRelationCascades()
    {
        var f = new ShipFixture(); var w = f.World;
        Relationship original = new()
        {
            Contact = true,
            War = true,
            Aggressor = true,
            SneakAttack = true,
            Fighting = true,
            BrokenTreaty = true,
            HostileIntent = true,
            Vendetta = true,
            Embassy = true,
            RecentContact = true
        };
        w.Civilizations[1].Treaties[2] = w.Civilizations[2].Treaties[1] = original;
        DiplomaticRelations.Set(w, 1, 2, new() { Alliance = true });
        var relation = w.Civilizations[1].Treaties[2]; Equal(relation, w.Civilizations[2].Treaties[1]);
        Equal(true, relation.Alliance && relation.Peace && relation.Contact && relation.Vendetta && relation.Embassy);
        Equal(false, relation.War || relation.Aggressor || relation.SneakAttack || relation.Fighting || relation.BrokenTreaty || relation.HostileIntent);
        DiplomaticRelations.Set(w, 1, 2, new() { Alliance = true, War = true }); relation = w.Civilizations[1].Treaties[2];
        Equal(true, relation.Alliance && relation.War && relation.WarThisTurn); Equal(false, relation.Peace);
        DiplomaticRelations.Clear(w, 1, 2, new() { Contact = true }); relation = w.Civilizations[1].Treaties[2];
        Equal(false, relation.Contact || relation.War || relation.Aggressor || relation.SneakAttack);
        Equal(true, relation.Alliance && relation.RecentContact && relation.Embassy && relation.WarThisTurn);
        DiplomaticRelations.Clear(w, 1, 2, new() { Peace = true }); Equal(false, w.Civilizations[1].Treaties[2].Alliance);
        DiplomaticRelations.Set(w, 0, 0, new() { War = true, Contact = true, Nuked = true, CityCaptured = true });
        Equal(true, w.Civilizations[0].Treaties[0].WarThisTurn); DiplomaticRelations.Clear(w, 0, 0, new() { Contact = true });
        Equal(false, w.Civilizations[0].Treaties[0].War); Equal(true, w.Civilizations[0].Treaties[0].Nuked && w.Civilizations[0].Treaties[0].CityCaptured);
    }
}
