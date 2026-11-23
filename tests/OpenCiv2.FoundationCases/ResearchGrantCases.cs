using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // V03 §§5,7,10 specification-derived values and actual C08 effect integrations.
    private sealed class GrantFixture : IResearchGrantEffects, IResearchGovernmentEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public RuleTables Rules => Data.Rules;
        public GameplayRandom Random { get; }
        public ResearchGrants Grants { get; }
        public ResearchGovernmentPrompts GovernmentPrompts { get; }
        public int RevolutionReply;
        public List<ResearchGovernmentChoice> GovernmentChoices { get; } = [];
        public List<ResearchTutorial> Tutorials { get; } = [];
        public Action? OnGovernmentChoice;
        public bool Session, Accept;
        public bool DiplomacyActive => Session;
        public List<string> Order { get; } = [];
        public List<AdvanceAcquisitionNotice> Acquisitions { get; } = [];
        public List<BarracksSaleNotice> Sales { get; } = [];
        public List<GoldenAgeNotice> Golden { get; } = [];
        public List<WonderExpiryNotice> Expired { get; } = [];
        public List<LeonardoUpgradeNotice> Upgrades { get; } = [];
        public List<(int Owner, int Advance)> Prompts { get; } = [];
        public Action<AdvanceAcquisitionNotice>? OnAcquired;
        public Action<BarracksSaleNotice>? OnSale;
        public Action? OnGolden, OnPrompt;

        public GrantFixture(params Choice[] choices) : this(null, choices) { }
        public GrantFixture(RevelationFixture? data, params Choice[] choices)
            : this(data, new(new ControlledChoices(new(choices)), "research-grants"), true) { }
        public GrantFixture(RevelationFixture data, GameplayRandom random) : this(data, random, false) { }
        private GrantFixture(RevelationFixture? data, GameplayRandom random, bool configure)
        {
            Data = data ?? new();
            Random = random;
            if (configure)
            {
                World.Globals.Turn = 10; World.Globals.CivsAlive = CivilizationSet.Only(1);
                World.Globals.BestHumanRank = 1;
                Rules.Cosmic = Rules.Cosmic with { TechnologyCostParadigm = 10, ShieldBoxRows = 10 };
                Rules.EnabledAdvanceCount = 67;
                for (int c = 0; c < 8; ++c)
                {
                    World.Civilizations[c].Researching = -1;
                    World.Names[c] = new() { Tribe = $"People {c}", Adjective = $"Nation {c}" };
                }
                for (int a = 0; a < 100; ++a) Rules.Advances[a] = new() { Identity = new($"advance-{a}", $"Advance {a}"), Enabled = a == 53 };
                for (int i = 0; i < 67; ++i) Rules.Improvements[i] = new() { Identity = new($"building-{i}", $"Building {i}"), CostRows = 3 };
            }
            var governments = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("No government selector in this grant"),
                _ => throw new InvalidOperationException("No rates dialog in direct Anarchy setter"),
                _ => throw new InvalidOperationException("No announced-change wrapper"));
            GovernmentPrompts = new(World, governments, this);
            Grants = new(World, Rules, Random,
                new WonderEffects(World, Rules, n => { Upgrades.Add(n); Order.Add("upgrade:" + n.Unit.Value); }, _ => Order.Add("redraw")),
                (c, a) => { Prompts.Add((c, a)); Order.Add("prompts:" + c); OnPrompt?.Invoke(); GovernmentPrompts.Run(c, a); }, this);
        }
        public void Grant(int c = 1, int a = 5, int source = 0, string call = "grant", bool skip = false) => Grants.Grant(c, a, source, skip, call);
        public void Acquired(AdvanceAcquisitionNotice n) { Acquisitions.Add(n); Order.Add("acquired:" + n.Civilization); OnAcquired?.Invoke(n); }
        public bool ReceiveEmissary(int c) { Order.Add("emissary:" + c); return Accept; }
        public void EmissaryKnowledge(int c, int a) => Order.Add("gape:" + a);
        public void ResetDiplomacy() { Session = false; Order.Add("reset"); }
        public void Encyclopedia(int a) => Order.Add("pedia:" + a);
        public void BarracksSold(BarracksSaleNotice n) { Sales.Add(n); Order.Add("barracks"); OnSale?.Invoke(n); }
        public void GoldenAge(GoldenAgeNotice n) { Golden.Add(n); Order.Add("golden"); OnGolden?.Invoke(); }
        public void WonderExpired(WonderExpiryNotice n) { Expired.Add(n); Order.Add("expiry:" + n.Wonder); }
        public void RefreshStatus() => Order.Add("status");
        public int ConfirmGovernment(ResearchGovernmentChoice choice) { GovernmentChoices.Add(choice); OnGovernmentChoice?.Invoke(); return RevolutionReply; }
        public void RevolutionStarting(int c) => Order.Add("revolution:" + c);
        public void Tutorial(int c, ResearchTutorial tutorial) => Tutorials.Add(tutorial);
    }

    private static void ResearchFullCostVectors()
    {
        var f = new GrantFixture(); var w = f.World; w.Globals.Turn = 1;
        foreach (var (count, expected) in new[] { (0, 10), (2, 18), (10, 80), (20, 420) })
        {
            w.Civilizations[1].NumAdvances = (byte)count; Equal(expected, Research.Cost(w, f.Rules, 1));
        }
        w.Globals.Status.LargeMap = w.Globals.Status.SmallMap = true; Equal(400, Research.Cost(w, f.Rules, 1));
        w.Globals.Status.LargeMap = w.Globals.Status.SmallMap = false;
        f.Rules.EnabledAdvanceCount = 100; Equal(280, Research.Cost(w, f.Rules, 1));
        f.Rules.EnabledAdvanceCount = ushort.MaxValue; Equal(420, Research.Cost(w, f.Rules, 1)); // Signed count, not 65535 enabled advances.
        f.Rules.EnabledAdvanceCount = 67;
        w.Civilizations[1].NumAdvances = 10; w.Civilizations[2].NumAdvances = 20; w.Globals.BestHumanRank = 2;
        w.Globals.CivByRank[2] = 1; w.Globals.Difficulty = Difficulty.Deity;
        w.Globals.Turn = 150; Equal(170, Research.Cost(w, f.Rules, 1));
        w.Globals.Turn = 151; Equal(160, Research.Cost(w, f.Rules, 1));
        w.Globals.HumanPlayers = CivilizationSet.Only(7); Equal(110, Research.Cost(w, f.Rules, 1));
        w.Globals.HumanPlayers = CivilizationSet.Only(1); w.Globals.Difficulty = (Difficulty)(-1);
        Equal(170, Research.Cost(w, f.Rules, 1));
        w.Globals.Difficulty = Difficulty.Chieftain; w.Globals.BestHumanRank = 1; w.Globals.Turn = 1;
        w.Civilizations[1].NumAdvances = w.Civilizations[1].ResearchCostAdvances = 255;
        Equal(10710, Research.Cost(w, f.Rules, 1));
        var before = w.Civilizations[1]; w.Civilizations[1].Researching = 99;
        Equal(10710, Research.Cost(w, f.Rules, 1)); Equal(before with { Researching = 99 }, w.Civilizations[1]);
    }

    private static void ResearchScenarioCostAndBounds()
    {
        var f = new GrantFixture(); var w = f.World;
        w.Globals.Status.ScenarioExport = true; w.Scenario.YearIncrement = 1; w.Scenario.TechnologyParadigm = 20;
        w.Globals.BestHumanRank = 255; w.Globals.HumanPlayers = default; w.Civilizations[1].NumAdvances = 10;
        Equal(380, Research.Cost(w, f.Rules, 1)); // No leader access in this scenario branch.
        w.Scenario.TechnologyParadigm = 0; Equal(32000, Research.Cost(w, f.Rules, 1));
        w.Globals.HumanPlayers = CivilizationSet.Only(1); Equal(10, Research.Cost(w, f.Rules, 1));
        w.Civilizations[1].NumAdvances = 20; Equal(32000, Research.Cost(w, f.Rules, 1));
        w.Scenario.TechnologyParadigm = -1; Equal(32000, Research.Cost(w, f.Rules, 1));
        w.Scenario.TechnologyParadigm = short.MaxValue; Equal(32000, Research.Cost(w, f.Rules, 1));
        w.Scenario.YearIncrement = 0; Throws<IndexOutOfRangeException>(() => Research.Cost(w, f.Rules, 1));
        w.Globals.BestHumanRank = 1; w.Scenario.TechnologyParadigm = 10; w.Globals.Turn = 1;
        Equal(420, Research.Cost(w, f.Rules, 1));
        w.Globals.Status.ScenarioExport = false; f.Rules.Cosmic = f.Rules.Cosmic with { TechnologyCostParadigm = 20 };
        Equal(840, Research.Cost(w, f.Rules, 1));
    }

    private static void ResearchSelectionOptionRules()
    {
        var f = new GrantFixture(); var w = f.World; w.Globals.Difficulty = Difficulty.Prince;
        for (int a = 0; a < 7; ++a) f.Rules.Advances[a] = f.Rules.Advances[a] with { Enabled = true, Prerequisites = new([AdvanceReference.None, AdvanceReference.None]) };
        Sequence(new[] { 0, 1, 2, 4, 5 }, Research.Options(w, f.Rules, 1));
        w.Civilizations[1].NumAdvances = 2; Sequence(new[] { 0, 1, 3, 4, 6 }, Research.Options(w, f.Rules, 1));
        Sequence(Enumerable.Range(0, 7), Research.Options(w, f.Rules, 1, -2));
        w.Globals.Status.Scenario = true; Sequence(Enumerable.Range(0, 7), Research.Options(w, f.Rules, 1));
        w.Globals.Status.Scenario = false; w.Globals.Difficulty = Difficulty.Chieftain;
        Sequence(Enumerable.Range(0, 7), Research.Options(w, f.Rules, 1));
        w.Globals.Difficulty = Difficulty.Prince; w.Civilizations[1].NumAdvances = 0;
        for (int a = 0; a < 7; ++a) f.Rules.Advances[a] = f.Rules.Advances[a] with { Enabled = a is 3 or 6 };
        Sequence(new[] { 3 }, Research.Options(w, f.Rules, 1));
        f.Rules.Advances[89] = new() { Enabled = true, Prerequisites = new([AdvanceReference.None, AdvanceReference.None]) };
        w.Civilizations[1].Advances = default(AdvanceSet).With(89);
        Sequence(new[] { 3, 89 }, Research.Options(w, f.Rules, 1)); Equal(0, f.Order.Count);
    }

    private static void ResearchGrantTurnZeroAndScalarPrefixes()
    {
        var f = new GrantFixture(); var w = f.World; w.Globals.Turn = 0;
        w.Civilizations[1].Researching = 5; w.Civilizations[1].ResearchProgress = 77; w.Civilizations[1].NumAdvances = 255;
        w.Globals.FirstDiscoverer[5] = 4; f.Grant(source: -2);
        Equal((short)-1, w.Civilizations[1].Researching); Equal((short)77, w.Civilizations[1].ResearchProgress);
        Equal((byte)255, w.Civilizations[1].NumAdvances); Equal((byte)254, w.Civilizations[1].AdvanceSource[5]);
        Equal(CivilizationSet.Only(1), w.Globals.AdvanceKnownBy[5]); Equal((byte)4, w.Globals.FirstDiscoverer[5]);
        Equal(0, f.Order.Count);
        w.Civilizations[1].Researching = 89; w.Civilizations[1].FutureTechs = 255; f.Grant(a: 89);
        Equal((byte)0, w.Civilizations[1].FutureTechs); Equal((byte)255, w.Civilizations[1].NumAdvances);
        Equal(default(CivilizationSet), w.Globals.AdvanceKnownBy[89]);
        w.Civilizations[1].Researching = -3;
        Throws<ArgumentOutOfRangeException>(() => f.Grant(a: -3)); Equal((short)-1, w.Civilizations[1].Researching);
        w.Globals.Multiplayer = GameMode.Tcp; w.Civilizations[1].Researching = 6;
        Throws<InvalidOperationException>(() => f.Grant(a: 6)); Equal((short)6, w.Civilizations[1].Researching);
        Equal(false, w.Civilizations[1].Advances.Contains(6));
    }

    private static void ResearchGrantDuplicateFutureAndFirstMarker()
    {
        var f = new GrantFixture(); var w = f.World;
        w.Civilizations[1].Advances = default(AdvanceSet).With(5); w.Civilizations[1].NumAdvances = 255;
        w.Civilizations[1].ResearchCostAdvances = 7; w.Globals.FirstDiscoverer[5] = 4;
        f.Grant(); Equal((byte)0, w.Civilizations[1].NumAdvances); Equal((byte)5, w.Globals.FirstDiscoverer[5]);
        f.Grant(source: 258); Equal((byte)1, w.Civilizations[1].NumAdvances); Equal((byte)2, w.Civilizations[1].AdvanceSource[5]);
        Equal((byte)7, w.Civilizations[1].ResearchCostAdvances); Equal(1, f.Prompts.Count);
        w.Multiplayer.Saved.AutoplayActive = true; w.Civilizations[1].FutureTechs = 255; w.Civilizations[1].NumAdvances = 255;
        f.Grant(a: 89); f.Grant(a: 100);
        Equal((byte)1, w.Civilizations[1].FutureTechs); Equal((byte)1, w.Civilizations[1].NumAdvances);
        Equal(false, CivilizationKnowledge.KnowsAdvance(w.Civilizations, 1, 89)); Equal(default(CivilizationSet), w.Globals.AdvanceKnownBy[89]);
    }

    private static void ResearchGrantAnnouncementsAndPrompts()
    {
        var f = new GrantFixture(); var w = f.World;
        w.Globals.CivsAlive = w.Globals.CivsAlive.With(2); w.Civilizations[1].Treaties[2].Embassy = true;
        f.Rules.Improvements[0] = f.Rules.Improvements[0] with { Prerequisite = (AdvanceReference)5 };
        f.Rules.Improvements[66] = f.Rules.Improvements[66] with { Prerequisite = (AdvanceReference)5 };
        f.Rules.Units[0] = f.Rules.Units[0] with { Prerequisite = (AdvanceReference)5, Identity = new("unit", "Enabled unit") };
        f.Grant(c: 2); var n = f.Acquisitions.Single();
        Equal((string?)null, n.SourceName); Equal("Advance 5", n.AdvanceName); Equal(true, n.IncludeImage);
        Sequence(new[] { ResearchUnlockKind.Improvement, ResearchUnlockKind.Improvement, ResearchUnlockKind.Unit }, n.Unlocks.Select(u => u.Kind));
        Sequence(new[] { 0, 66, 0 }, n.Unlocks.Select(u => u.Identity)); Equal(0, f.Prompts.Count);
        f.Grant(source: -9); Equal("Building 43", f.Acquisitions.Last().SourceName); Equal(0, f.Acquisitions.Last().Unlocks.Count);
        w.Globals.GameOptions.AdvanceCivilopedia = true; f.Order.Clear(); f.Grant(source: 1, skip: true);
        Sequence(new[] { "prompts:1" }, f.Order);
        f.Order.Clear(); f.Grant(source: 1); Sequence(new[] { "pedia:5", "prompts:1" }, f.Order);
        f.Session = true; f.Order.Clear(); f.Grant(source: 1); Sequence(new[] { "prompts:1" }, f.Order);
        w.Multiplayer.Saved.AutoplayActive = true; f.Order.Clear(); f.Grant(source: -2); Equal(0, f.Order.Count);
    }

    private static void ResearchGrantEmissaryDrawAndReset()
    {
        Choice draw = Choice.Create(new("V03.emissary", "grant"), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0));
        var f = new GrantFixture(draw); f.Grant(c: 2); f.Random.RequireChoicesConsumed(); Equal(0, f.Order.Count); // Draw precedes Contact.
        foreach (bool accept in new[] { false, true })
        {
            f = new(draw) { Accept = accept, Session = true }; f.World.Civilizations[2].Treaties[1].Contact = true;
            f.Grant(c: 2); f.Random.RequireChoicesConsumed(); Equal(false, f.Session);
            Sequence(accept ? new[] { "emissary:2", "gape:5", "reset" } : new[] { "emissary:2", "reset" }, f.Order);
        }
        f = new(draw); f.World.Civilizations[2].Treaties[1] = new() { Contact = true, Alliance = true };
        f.Grant(c: 2); f.Random.RequireChoicesConsumed(); Equal(0, f.Order.Count);
        f = new(); f.World.Globals.AdvanceKnownBy[5] = CivilizationSet.Only(7); f.Grant(c: 2); f.Random.RequireChoicesConsumed();
        f = new(); f.World.Multiplayer.Saved.AutoplayActive = true; f.Grant(c: 2); f.Random.RequireChoicesConsumed();
    }

    private static void ResearchGrantBarracksAndFailure()
    {
        var f = new GrantFixture(); var w = f.World;
        f.Data.AddCity(0, 1, new(10, 10)); f.Data.AddCity(1, 1, new(12, 10)); f.Data.AddCity(2, 2, new(14, 10));
        w.Entities.Cities.Storage[0].Improvements = default(ImprovementSet).With(2);
        w.Entities.Cities.Storage[2].Improvements = default(ImprovementSet).With(2);
        w.Entities.Cities.Storage[0].ShieldBox = 9; w.Calculation.ShieldRows = 99; w.Civilizations[1].Gold = 5;
        w.Multiplayer.Saved.AutoplayActive = true;
        f.OnSale = n =>
        {
            Equal(30, n.Value); Equal(1, n.Count); Equal(5, w.Civilizations[1].Gold);
            Equal(false, BuildingQueries.HasBuilding(w, new(0), 2));
            f.Rules.Improvements[2] = f.Rules.Improvements[2] with { CostRows = 255 }; w.Civilizations[1].Gold = 7;
        };
        f.Grant(a: 35); Equal(37, w.Civilizations[1].Gold); Equal(1, f.Sales.Count);
        Equal(true, BuildingQueries.HasBuilding(w, new(2), 2)); Equal((short)9, w.Entities.Cities.Storage[0].ShieldBox);
        Equal(false, w.Entities.Cities.Storage[0].Flags.ImprovementSold); Sequence(new[] { "barracks", "status" }, f.Order);
        w.Entities.Cities.Storage[0].Improvements = default(ImprovementSet).With(2);
        f.OnSale = _ => throw new InvalidOperationException("notice stopped");
        Throws<InvalidOperationException>(() => f.Grant(a: 35)); Equal(37, w.Civilizations[1].Gold); Equal(false, BuildingQueries.HasBuilding(w, new(0), 2));
        f = new(); f.Rules.Advances[53] = new() { Prerequisites = new([(AdvanceReference)53, AdvanceReference.None]) };
        Throws<ArgumentException>(() => f.Grant(a: 35)); // Walk still occurs for Gunpowder, after the grant prefix.
        Equal(true, f.World.Civilizations[1].Advances.Contains(35)); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        Equal((byte)0, f.World.Globals.FirstDiscoverer[35]);
    }

    private static void ResearchGrantPhilosophyAndWonderExpiry()
    {
        var f = new GrantFixture(); var w = f.World;
        f.Data.AddCity(0, 1, new(10, 10), 1); f.Data.AddCity(1, 1, new(12, 10), 1);
        w.Entities.Cities.Storage[1].Name = "Last tie"; w.Globals.FirstDiscoverer[60] = 4;
        f.OnGolden = () => { Equal(true, w.Civilizations[1].Flags.PendingFreeAdvance); Equal((byte)4, w.Globals.FirstDiscoverer[60]); };
        f.Grant(a: 60, source: 1); Equal(new CitySlot(1), f.Golden.Single().City.Slot); Equal("Last tie", f.Golden[0].CityName);
        Equal((byte)5, w.Globals.FirstDiscoverer[60]); f.Random.RequireChoicesConsumed();
        var local = new GrantFixture(); local.Data.AddCity(0, 1, new(10, 10), 1); local.Grant(a: 60);
        Equal(0, local.Golden.Count); Equal(true, local.World.Civilizations[1].Flags.PendingFreeAdvance);
        f = new(); f.Data.AddCity(0, 1, new(10, 10)); f.World.Entities.Cities.Storage[0].Token = default;
        f.World.Globals.WonderCity[0] = 0; f.Rules.WonderObsolescence[0] = (AdvanceReference)5;
        f.Grant(); Equal(1, f.Expired.Count); Equal(new CitySlot(0), f.Expired[0].City); // Raw location, not active/liveness query.
    }

    private static void ResearchGoldenAgeWeightedSelection()
    {
        var f = new GrantFixture(
            Choice.Create(new("V03.golden-age", "notice", 0), new SamplingLaw.PrimaryModulo(4), ChoiceCode.Primary(3)),
            Choice.Create(new("V03.golden-age", "notice", 1), new SamplingLaw.PrimaryModulo(4), ChoiceCode.Primary(3)));
        f.Data.AddCity(0, 1, new(10, 10), 2); f.Data.AddCity(1, 1, new(12, 10), 4); f.Data.AddCity(2, 2, new(14, 10), 100);
        f.World.Entities.Cities.Storage[0].Improvements = default(ImprovementSet).With(1);
        f.Grants.ShowGoldenAge(1, "notice"); f.Random.RequireChoicesConsumed(); Equal(new CitySlot(1), f.Golden.Single().City.Slot);
        Equal((sbyte)4, f.World.Entities.Cities.Storage[1].Size); Equal(false, f.World.Entities.Cities.Storage[1].Flags.Celebrating);
        var none = new GrantFixture(); none.Grants.ShowGoldenAge(1, "none"); Equal(0, none.Golden.Count);
    }

    private static void ResearchGrantLeonardoAndGreatLibrary()
    {
        var f = new GrantFixture(); var w = f.World;
        f.Data.AddCity(0, 1, new(10, 10)); f.Data.AddCity(1, 2, new(12, 10));
        w.Globals.WonderCity[WonderIds.Leonardo] = 0; w.Globals.WonderCity[WonderIds.GreatLibrary] = 1;
        w.Globals.HumanPlayers = w.Globals.HumanPlayers.With(2); w.Globals.CivsAlive = w.Globals.CivsAlive.With(2);
        w.Multiplayer.LocalHuman = 2; w.Civilizations[2].Treaties[1].Embassy = true;
        w.Civilizations[7].Advances = default(AdvanceSet).With(5); // Inactive second holder still qualifies the Library.
        f.Rules.Units[0] = f.Rules.Units[0] with { ObsoleteBy = (AdvanceReference)5 };
        f.Rules.Units[61] = f.Rules.Units[0] with { ObsoleteBy = AdvanceReference.Never, Prerequisite = (AdvanceReference)5 };
        f.Data.AddUnit(0, 1, 0, new(10, 10)); f.Data.Unit(0).Flags.Veteran = true;
        f.OnAcquired = n => { if (n.Civilization == 2) Equal(new UnitType(61), f.Data.Unit(0).Type); };
        f.Grant(); Equal(new UnitType(61), f.Data.Unit(0).Type); Equal(false, f.Data.Unit(0).Flags.Veteran);
        Equal((byte)254, w.Civilizations[2].AdvanceSource[5]); Equal((byte)1, w.Civilizations[2].NumAdvances);
        Equal(CivilizationSet.Only(1).With(2), w.Globals.AdvanceKnownBy[5]);
        Sequence(new[] { "acquired:1", "upgrade:0", "redraw", "acquired:2", "prompts:2" }, f.Order);
        f.Data.Unit(0).Type = new(0); f.Data.Unit(0).Flags.Veteran = true; f.Grant(call: "duplicate");
        Equal(2, f.Upgrades.Count); Equal((byte)1, w.Civilizations[2].NumAdvances); // Duplicate still upgrades, no second Library grant.
    }

    private static void ResearchGrantFailurePrefixes()
    {
        var f = new GrantFixture(); f.World.Globals.GameOptions.AdvanceCivilopedia = true;
        f.OnPrompt = () => throw new InvalidOperationException("government choice pending");
        Throws<InvalidOperationException>(() => f.Grant(source: 1));
        Equal(true, f.World.Civilizations[1].Advances.Contains(5)); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        Equal((byte)0, f.World.Globals.FirstDiscoverer[5]); Sequence(new[] { "pedia:5", "prompts:1" }, f.Order);
        f = new(); f.OnAcquired = _ => throw new InvalidOperationException("notice stopped");
        Throws<InvalidOperationException>(() => f.Grant(source: -2));
        Equal((byte)254, f.World.Civilizations[1].AdvanceSource[5]); Equal((byte)1, f.World.Civilizations[1].NumAdvances);
        Equal((byte)0, f.World.Globals.FirstDiscoverer[5]); Equal(0, f.Prompts.Count);
    }

    private static void ResearchGovernmentPromptTransitions()
    {
        var f = new GrantFixture { RevolutionReply = 1 };
        f.World.Civilizations[1].Government = Government.Despotism;
        for (int c = 0; c < 8; ++c) f.World.Civilizations[c].Treaties[1].Vendetta = true;
        f.OnGovernmentChoice = () => f.World.Civilizations[1].Government = Government.Democracy;
        f.Grant(a: AdvanceIds.Monarchy, source: 1);
        Equal(Government.Anarchy, f.World.Civilizations[1].Government);
        Equal(new ResearchGovernmentChoice(1, 54, Government.Despotism, Government.Monarchy, true), f.GovernmentChoices.Single());
        for (int c = 0; c < 8; ++c) Equal(false, f.World.Civilizations[c].Treaties[1].Vendetta);
        Equal(true, f.Order.Contains("revolution:1"));
        f = new(); f.World.Civilizations[1].Government = Government.Despotism;
        f.GovernmentPrompts.Run(1, AdvanceIds.Monarchy); Equal(Government.Despotism, f.World.Civilizations[1].Government);
        Equal(0, f.Order.Count); Equal(1, f.GovernmentChoices.Count);
        f.World.Civilizations[1].Government = Government.Monarchy; f.GovernmentPrompts.Run(1, AdvanceIds.Monarchy);
        Equal(1, f.GovernmentChoices.Count);
    }

    private static void ResearchGovernmentTutorialAndReturnGates()
    {
        var f = new GrantFixture(); f.World.Globals.GameOptions.Tutorial = true;
        f.World.Civilizations[1].Government = Government.Monarchy; f.GovernmentPrompts.Run(1, AdvanceIds.Republic);
        Sequence(new[] { ResearchTutorial.Republic }, f.Tutorials); Equal(0, f.GovernmentChoices.Count);
        f.World.Civilizations[1].Government = Government.Fundamentalism; f.GovernmentPrompts.Run(1, AdvanceIds.Republic);
        Equal(1, f.GovernmentChoices.Count); Equal(false, f.GovernmentChoices[0].Monarchy);
        foreach (var (government, advance) in new[] { (Government.Communism, 15), (Government.Fundamentalism, 31), (Government.Republic, 71), (Government.Democracy, 21), (Government.Anarchy, 21) })
        {
            f.World.Civilizations[1].Government = government; f.GovernmentPrompts.Run(1, advance);
        }
        Equal(1, f.GovernmentChoices.Count);
        foreach (int a in new[] { 67, 70, 84, 75, 88 }) f.GovernmentPrompts.Run(1, a);
        Sequence(Enum.GetValues<ResearchTutorial>(), f.Tutorials);
        f.World.Globals.GameOptions.Tutorial = false; f.GovernmentPrompts.Run(1, 67); Equal(6, f.Tutorials.Count);
    }
}
