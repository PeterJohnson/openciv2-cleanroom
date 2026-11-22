using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class GovernmentMoodFixture : ICityMoodEffects
    {
        public RevelationFixture Data { get; } = FoodFixture();
        public GameState World => Data.World;
        public GovernmentChanges Governments { get; }
        public CityMoodTurn Mood { get; }
        public List<int> RateRequests { get; } = [];
        public List<int> SelectionRequests { get; } = [];
        public List<GovernmentChangeNotice> Announcements { get; } = [];
        public List<CityMoodNotice> Notices { get; } = [];
        public List<CitySlot> Reconsidered { get; } = [];
        public List<CitySlot> Redraws { get; } = [];
        public Action<int>? Rates;

        public GovernmentMoodFixture()
        {
            Governments = new(World, Data.Process.Rules, Data.Stacks,
                c => SelectionRequests.Add(c), c => { RateRequests.Add(c); Rates?.Invoke(c); }, Announcements.Add);
            Mood = new(World, Data.Rules, Governments, Reconsidered.Add, this);
            World.Globals.TransientFlags.AttitudeDirty = true;
            World.Civilizations[1].Gold = 100;
            World.Mood.City = new(0);
            World.Calculation.Shields = 5; World.Calculation.ShieldSupport = 2;
            World.Calculation.Tax = 7;
        }
        public void MoodNotice(CityMoodNotice notice) => Notices.Add(notice);
        public void RedrawCity(CitySlot city) => Redraws.Add(city);
    }

    private static void GovernmentQueriesAndFixedIdentities()
    {
        var f = new GovernmentMoodFixture();
        foreach (var (government, cap) in new[] { (0, 6), (1, 6), (2, 7), (3, 8), (4, 8), (5, 8), (6, 10), (255, 10), (256, 6) })
        {
            f.World.Civilizations[1].Government = (Government)government;
            Equal(cap, f.Governments.MaximumRate(1));
        }
        foreach (var (government, advance) in new[] { (2, 54), (3, 15), (4, 31), (5, 71), (6, 21) })
        {
            f.World.Civilizations[1].Advances = default;
            Equal(false, f.Governments.Available(1, government));
            f.World.Civilizations[1].Advances = default(AdvanceSet).With(advance);
            f.Data.Rules.Advances[advance] = f.Data.Rules.Advances[advance] with { Enabled = false };
            Equal(government != 4, f.Governments.Available(1, government));
            f.Data.Rules.Advances[advance] = f.Data.Rules.Advances[advance] with { Enabled = true };
            Equal(true, f.Governments.Available(1, government));
        }
        f.World.Civilizations[1].Advances = default;
        f.World.Globals.WonderCity[19] = 0;
        Equal(true, f.Governments.Available(1, 6));
        f.Data.Rules.Advances[31] = f.Data.Rules.Advances[31] with { Enabled = false };
        Equal(false, f.Governments.Available(1, 4)); // Liberty does not bypass disabled Fundamentalism.
        f.Data.Rules.WonderObsolescence[19] = (AdvanceReference)0;
        f.World.Civilizations[7].Advances = default(AdvanceSet).With(0);
        Equal(false, f.Governments.Available(1, 6)); // Real wonder expiry, including dead seats.
        foreach (int g in new[] { 0, 1, -1, 256 }) Equal(true, f.Governments.Available(1, g));
    }

    private static void GovernmentDirectRecalculationAndProduction()
    {
        var f = new GovernmentMoodFixture(); var world = f.World;
        f.Data.AddCity(1, 2, new(12, 6)); f.Data.AddCity(3, 1, new(4, 6));
        world.Civilizations[1].CityCount = 2;
        world.Civilizations[1].Government = Government.Fundamentalism;
        world.Civilizations[1].UnitsInProduction[8] = 5;
        world.Civilizations[1].UnitsInProduction[11] = 4;
        foreach (int i in new[] { 0, 1, 3 })
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            city.Production = 8; city.TotalFood = 199; city.ShieldBox = 37;
        }
        world.Entities.Cities.Storage[2].Production = 8; // Free slot is skipped.
        for (int i = 0; i < 8; ++i)
        {
            world.Civilizations[i].Treaties[1] = new() { Vendetta = true, Peace = true };
            world.Civilizations[1].Treaties[i].Vendetta = true;
        }
        f.Data.Rules.Units[8] = f.Data.Rules.Units[8] with { Identity = new("renamed", "Not called Fanatics") };
        f.Governments.Set(1, 0);
        Equal(Government.Anarchy, world.Civilizations[1].Government);
        Equal(false, world.Globals.TransientFlags.AttitudeDirty);
        Equal((CitySlot?)new CitySlot(3), world.Mood.City);
        foreach (int i in new[] { 0, 3 })
        {
            Equal((sbyte)11, world.Entities.Cities.Storage[i].Production);
            Equal(true, world.Entities.Cities.Storage[i].TotalFood != 199);
            Equal((short)37, world.Entities.Cities.Storage[i].ShieldBox);
        }
        Equal((sbyte)8, world.Entities.Cities.Storage[1].Production);
        Equal((byte)199, world.Entities.Cities.Storage[1].TotalFood);
        Equal((sbyte)8, world.Entities.Cities.Storage[2].Production);
        Equal((byte)5, world.Civilizations[1].UnitsInProduction[8]);
        Equal((byte)4, world.Civilizations[1].UnitsInProduction[11]);
        for (int i = 0; i < 8; ++i)
        {
            Equal(false, world.Civilizations[i].Treaties[1].Vendetta);
            Equal(true, world.Civilizations[i].Treaties[1].Peace);
            if (i != 1) Equal(true, world.Civilizations[1].Treaties[i].Vendetta);
        }
        Equal(0, f.RateRequests.Count); Equal(false, world.Civilizations[1].Flags.Revolution);
        Equal(false, world.Civilizations[1].Flags.GovernmentChanged);
    }

    private static void GovernmentFullArgumentsAndDecisionBoundaries()
    {
        var f = new GovernmentMoodFixture(); var world = f.World;
        f.Governments.Set(1, 2); // Same positive government still requests rates, without recalculation.
        Equal(1, f.RateRequests.Count); Equal(true, world.Globals.TransientFlags.AttitudeDirty);
        f.Governments.Set(1, 256); Equal(Government.Anarchy, world.Civilizations[1].Government);
        Equal(2, f.RateRequests.Count);
        world.Globals.TransientFlags.AttitudeDirty = true;
        f.Governments.Set(1, -256); Equal(2, f.RateRequests.Count);
        Equal(true, world.Globals.TransientFlags.AttitudeDirty);
        world.Multiplayer.Saved.AutoplayActive = true;
        f.Governments.Set(1, 2); Equal(2, f.RateRequests.Count);
        world.Multiplayer.Saved.AutoplayActive = false;
        f.Governments.Change(1, 6); // Selection is required, not a forced Democracy store.
        Equal(1, f.SelectionRequests.Count); Equal(Government.Monarchy, world.Civilizations[1].Government);
        Equal(true, world.Civilizations[1].Flags.GovernmentChanged);
        f.Rates = _ => throw new InvalidOperationException("Rate decision failed");
        Throws<InvalidOperationException>(() => f.Governments.Set(1, 5));
        Equal(Government.Republic, world.Civilizations[1].Government); // Committed transition survives.
        Equal(false, world.Globals.TransientFlags.AttitudeDirty);
        world.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => f.Governments.Change(1, 0));
        Equal(Government.Republic, world.Civilizations[1].Government);
    }

    private static void GovernmentAiAnnouncementGates()
    {
        foreach (int government in Enumerable.Range(0, 7))
        {
            var f = new GovernmentMoodFixture(); var world = f.World;
            world.Civilizations[2].Government = (Government)200;
            world.Civilizations[2].TaxRate = 9; world.Civilizations[2].ScienceRate = 7;
            world.Civilizations[1].Treaties[2] = new() { Embassy = true, Vendetta = true };
            f.Governments.Change(2, government);
            Equal(new byte[] { 4, 4, 3, 3, 3, 2, 2 }[government], world.Civilizations[2].TaxRate);
            Equal((byte)7, world.Civilizations[2].ScienceRate);
            Equal(new GovernmentChangeNotice(2, 200, government, true), f.Announcements.Single());
            Equal(true, world.Civilizations[2].Flags.Revolution);
            Equal(false, world.Civilizations[1].Treaties[2].Vendetta);
            Equal(true, world.Globals.TransientFlags.AttitudeDirty); // AI does not run human C03 path.
            f.Governments.Change(2, government);
            Equal(false, world.Civilizations[2].Flags.Revolution);
            Equal(false, f.Announcements.Last().Overthrown);
            f.Governments.Change(2, government); Equal(2, f.Announcements.Count);
        }
        foreach (int eligibility in Enumerable.Range(0, 4))
        {
            var f = new GovernmentMoodFixture(); var world = f.World;
            world.Globals.HumanPlayers = default; // Observer needs neither human nor alive membership.
            world.Civilizations[2].Government = Government.Democracy; world.Civilizations[2].TaxRate = 9;
            world.Multiplayer.Saved.AutoplayActive = true;
            if (eligibility == 1) world.Globals.RevealMap = 1;
            if (eligibility == 2) world.Globals.WonderCity[24] = 0;
            if (eligibility == 3) world.Globals.WonderCity[9] = 0;
            f.Governments.Change(2, 0);
            Equal(eligibility == 0 ? (byte)9 : (byte)4, world.Civilizations[2].TaxRate);
            Equal(0, f.Announcements.Count); Equal(false, world.Civilizations[2].Flags.Revolution);
        }
    }

    private static void CityDisorderDurationAndRestoration()
    {
        var f = new GovernmentMoodFixture(); var world = f.World;
        ref var city = ref world.Entities.Cities.Storage[0];
        city.Happy = 200; city.Unhappy = 0; // Saved bytes do not determine the entry branch.
        world.Mood.Happy = 0; world.Mood.Unhappy = 1;
        f.Mood.Run(new(0), 0);
        Equal(true, city.Flags.Disorder && city.Flags.MoodOnEdge);
        Equal(false, city.Flags.SecondDisorderTurn || city.Flags.LongDisorder);
        Equal(100, world.Civilizations[1].Gold); Equal(1, f.Redraws.Count);
        f.Mood.Run(new(0), 0); Equal(true, city.Flags.SecondDisorderTurn); Equal(false, city.Flags.LongDisorder);
        f.Mood.Run(new(0), 0); Equal(true, city.Flags.LongDisorder); Equal(1, f.Redraws.Count);
        world.Mood.Happy = world.Mood.Unhappy = 1;
        f.Mood.Run(new(0), -1);
        Equal(false, city.Flags.Disorder || city.Flags.SecondDisorderTurn || city.Flags.LongDisorder || city.Flags.MoodOnEdge);
        Equal(107, world.Civilizations[1].Gold); Equal(2, f.Redraws.Count);
        Equal(CityMoodNoticeKind.Restored, f.Notices.Last().Kind);
        city.Flags.LongDisorder = true;
        f.Mood.Run(new(0), 0); Equal(true, city.Flags.MoodOnEdge && city.Flags.LongDisorder);
        Equal(114, world.Civilizations[1].Gold); // Tax every qualifying call, duration clears only on restoration.
        world.Calculation.ShieldSupport = 5; f.Mood.Run(new(0), 0); Equal(false, city.Flags.MoodOnEdge);
        Equal((CitySlot?)new CitySlot(0), world.Mood.City); Equal(0, f.Reconsidered.Count);
    }

    private static void CityDisorderDemocracyCollapse()
    {
        foreach (var (scenario, locked, collapse) in new[] { (false, true, true), (true, false, true), (true, true, false) })
        {
            var f = new GovernmentMoodFixture(); var world = f.World;
            world.Civilizations[1].Government = Government.Democracy;
            world.Globals.Status.Scenario = scenario; world.Scenario.Flags.GovernmentLock = locked;
            world.Scenario.Flags.RevolutionRestriction = true; // The bit1 restriction is not C04's bit16 gate.
            world.Globals.MessageOptions.Disorder = true;
            world.Multiplayer.LocalHuman = 2; // Human membership, not local equality, governs collapse.
            world.Civilizations[2].Treaties[1].Vendetta = true;
            world.Entities.Cities.Storage[0].Production = 8;
            world.Mood.Unhappy = 1;
            f.Mood.Run(new(0), 0); Equal(Government.Democracy, world.Civilizations[1].Government);
            Equal(0, f.Notices.Count);
            f.Mood.Run(new(0), 0);
            Equal(collapse ? Government.Anarchy : Government.Democracy, world.Civilizations[1].Government);
            Equal(collapse, world.Civilizations[1].Flags.Revolution);
            Equal(!collapse, world.Civilizations[2].Treaties[1].Vendetta);
            Equal(collapse ? (sbyte)11 : (sbyte)8, world.Entities.Cities.Storage[0].Production);
            Equal(100, world.Civilizations[1].Gold); Equal(collapse ? 1 : 0, f.Notices.Count);
            if (collapse) Equal(CityMoodNoticeKind.Revolt, f.Notices.Single().Kind);
            Equal(0, f.RateRequests.Count); Equal(0, f.SelectionRequests.Count);
            Equal(false, world.Civilizations[1].Flags.GovernmentChanged);
        }
    }

    private static void CityMoodTaxWrapAndPlayerGates()
    {
        var f = new GovernmentMoodFixture(); var world = f.World;
        world.Civilizations[1].Gold = int.MaxValue; world.Calculation.Tax = 2;
        world.Entities.Cities.Storage[0].Tax = 99;
        f.Mood.Run(new(0), 0); Equal(int.MinValue + 1, world.Civilizations[1].Gold);
        world.Calculation.Tax = -3; f.Mood.Run(new(0), 0); Equal(int.MaxValue - 1, world.Civilizations[1].Gold);
        world.Civilizations[1].Government = Government.Anarchy;
        f.Mood.Run(new(0), 0); Equal(int.MaxValue - 1, world.Civilizations[1].Gold);
        world.Globals.HumanPlayers = default;
        world.Civilizations[1].Government = Government.Monarchy;
        world.Mood.Unhappy = 1;
        f.Mood.Run(new(0), 0); f.Mood.Run(new(0), 0);
        world.Mood.Unhappy = 0; f.Mood.Run(new(0), 0);
        Equal(3, f.Reconsidered.Count); Equal(int.MaxValue - 4, world.Civilizations[1].Gold);
        ref var city = ref world.Entities.Cities.Storage[0];
        city.Owner = new(0); city.Flags.Celebrating = true;
        var before = city;
        world.Globals.Multiplayer = GameMode.Tcp;
        f.Mood.Run(new(0), 0); Equal(before, city); // Barbarian return precedes all other work.
        city.Owner = new(1); Throws<InvalidOperationException>(() => f.Mood.Run(new(0), 0));
        foreach (var (local, autoplay, suppressed) in new[] { (2, false, false), (1, true, false), (1, false, true) })
        {
            f = new(); f.World.Mood.Unhappy = 1; f.World.Multiplayer.LocalHuman = local;
            f.World.Multiplayer.Saved.AutoplayActive = autoplay; f.World.Globals.MessageOptions.Disorder = suppressed;
            f.Mood.Run(new(0), 0); Equal(0, f.Notices.Count);
            Equal(true, f.World.Entities.Cities.Storage[0].Flags.Disorder);
        }
    }

    private static void CityCelebrationGrowthAndFacilityGates()
    {
        var f = new GovernmentMoodFixture(); var world = f.World;
        ref var city = ref world.Entities.Cities.Storage[0];
        world.Civilizations[1].Government = Government.Republic;
        world.Mood.Happy = 2; world.Calculation.Food = 9;
        city.FoodBox = 29;
        f.Mood.Run(new(0), -100); Equal(true, city.Flags.Celebrating); Equal((sbyte)3, city.Size);
        Equal(CityMoodNoticeKind.Celebration, f.Notices.Single().Kind);
        f.Mood.Run(new(0), -100); Equal((sbyte)4, city.Size); Equal((short)29, city.FoodBox);
        world.Calculation.SupportedSettlers = 1; world.Calculation.SettlerFood = 1;
        f.Mood.Run(new(0), 100); Equal((sbyte)4, city.Size); // Strict food surplus, equality fails; cached surplus ignored.
        world.Calculation.SupportedSettlers = 0;
        f.Data.Rules.Cosmic = f.Data.Rules.Cosmic with { AqueductSize = 4 };
        f.Mood.Run(new(0), 100); Equal((sbyte)4, city.Size); Equal(true, city.Flags.Celebrating);
        BuildingQueries.SetBuilding(world, new(0), BuildingIds.Aqueduct, true);
        f.Mood.Run(new(0), 100); Equal((sbyte)5, city.Size);
        f.Mood.Run(new(0), 100); Equal(false, city.Flags.Celebrating); // Five needs three happy, including specialists.
        Equal(CityMoodNoticeKind.CelebrationEnded, f.Notices.Last().Kind);
        Equal(2, f.Notices.Count); Equal((short)29, city.FoodBox);
        Equal((CitySlot?)new CitySlot(0), world.Mood.City); Equal(2, world.Mood.Happy);
        city.Size = 127; city.Flags.Celebrating = true; world.Mood.Happy = 64;
        world.Calculation.Food = 1000;
        BuildingQueries.SetBuilding(world, new(0), BuildingIds.SewerSystem, true);
        f.Mood.Run(new(0), 100); Equal((sbyte)-128, city.Size); // Scalar wrap is retained, no food-box/refill path.
    }

    private static void CityCelebrationQualificationAndNotices()
    {
        foreach (var (government, human, suppressed) in new[]
        { (Government.Monarchy, true, false), (Government.Fundamentalism, false, false), (Government.Democracy, true, true) })
        {
            var f = new GovernmentMoodFixture(); var world = f.World;
            world.Civilizations[1].Government = government;
            if (!human) world.Globals.HumanPlayers = default;
            world.Globals.MessageOptions.Celebration = suppressed;
            world.Mood.Happy = 2; world.Calculation.Food = 7;
            f.Mood.Run(new(0), 0);
            Equal(true, world.Entities.Cities.Storage[0].Flags.Celebrating);
            Equal(human && !suppressed ? 1 : 0, f.Notices.Count);
            f.Mood.Run(new(0), 0);
            Equal(government == Government.Democracy ? (sbyte)4 : (sbyte)3, world.Entities.Cities.Storage[0].Size);
            world.Mood.Unhappy = 1; // Even with H >= ceil(size/2), unhappy citizens disqualify.
            f.Mood.Run(new(0), 0);
            Equal(false, world.Entities.Cities.Storage[0].Flags.Celebrating);
        }
    }
}
