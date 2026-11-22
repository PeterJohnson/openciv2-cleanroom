using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class AvailabilityFixture
    {
        public OrdersFixture Data { get; } = new();
        public AvailabilityFixture()
        {
            for (int i = 1; i < Data.Rules.Improvements.Length; ++i)
                Data.Rules.Improvements[i] = Data.Rules.Improvements[i] with { Prerequisite = AdvanceReference.None };
        }
        public bool Can(int item, CitySlot? city = null) => BuildingAvailability.Improvement(Data.World, Data.Rules, 1, city ?? new CitySlot(0), item);
        public void Has(params int[] items)
        {
            Data.City.Improvements = default;
            foreach (int item in items) Data.City.Improvements = Data.City.Improvements.With(item);
        }
    }

    // C06 nonunit eligibility; C08 actual stored facilities, active effects and expiration.
    private static void BuildingEligibilityKnowledgeAndCoasts()
    {
        var f = new AvailabilityFixture(); var d = f.Data;
        d.Rules.Improvements[4] = d.Rules.Improvements[4] with { Prerequisite = (AdvanceReference)5 };
        Equal(false, f.Can(4)); Equal(false, BuildingAvailability.Improvement(d.World, d.Rules, 1, null, 4));
        d.World.Civilizations[1].Advances = default(AdvanceSet).With(5); Equal(true, f.Can(4));
        f.Has(4); Equal(false, f.Can(4)); Equal(true, BuildingAvailability.Improvement(d.World, d.Rules, 1, null, 4));
        d.Rules.Improvements[4] = d.Rules.Improvements[4] with { Prerequisite = AdvanceReference.Never };
        Equal(false, BuildingAvailability.Improvement(d.World, d.Rules, 1, null, 4));
        foreach (int item in new[] { 28, 30, 31, 34 }) Equal(false, f.Can(item));
        d.City.Flags.Coastal = true;
        foreach (int item in new[] { 30, 31 }) Equal(true, f.Can(item));
        foreach (int item in new[] { 28, 34 }) Equal(false, f.Can(item));
        d.City.Flags.OceanAccess = true;
        foreach (int item in new[] { 28, 34 }) Equal(true, f.Can(item));
        d.City.Flags.Coastal = false;
        foreach (int item in new[] { 28, 30, 31, 34 }) Equal(false, f.Can(item));
    }

    private static void BuildingEligibilityFacilityChainsAndEffects()
    {
        var f = new AvailabilityFixture(); var d = f.Data;
        foreach (var (item, prerequisite) in new[] { (10, 5), (22, 10), (12, 6), (26, 12), (16, 15) })
        {
            f.Has(); Equal(false, f.Can(item)); f.Has(prerequisite); Equal(true, f.Can(item));
            d.Rules.Improvements[prerequisite] = d.Rules.Improvements[prerequisite] with { Prerequisite = (AdvanceReference)7 };
            Equal(false, f.Can(item)); d.World.Civilizations[1].Advances = default(AdvanceSet).With(7); Equal(true, f.Can(item));
            d.World.Civilizations[1].Advances = default;
            d.Rules.Improvements[prerequisite] = d.Rules.Improvements[prerequisite] with { Prerequisite = AdvanceReference.None };
        }
        f.Has(); Equal(false, f.Can(23)); f.Has(9);
        d.Rules.Improvements[9] = d.Rules.Improvements[9] with { Prerequisite = AdvanceReference.Never };
        Equal(true, f.Can(23)); // Sewer needs the stored Aqueduct, not its prerequisite.
        f.Has(1); Equal(false, f.Can(7)); f.Has(); Equal(true, f.Can(7));
        foreach (var (item, wonder) in new[] { (3, 0), (11, 10), (26, 26), (33, 21) })
        {
            f.Has(12); Equal(true, f.Can(item)); d.World.Globals.WonderCity[wonder] = 0; Equal(false, f.Can(item));
            Equal(true, BuildingAvailability.Improvement(d.World, d.Rules, 1, null, item));
            d.Rules.WonderObsolescence[wonder] = (AdvanceReference)8;
            d.World.Civilizations[2].Advances = default(AdvanceSet).With(8); Equal(true, f.Can(item));
            d.World.Globals.WonderCity[wonder] = -1;
        }
    }

    private static void BuildingEligibilityPowerReplacement()
    {
        var f = new AvailabilityFixture(); var d = f.Data;
        foreach (int plant in new[] { 19, 20, 21, 29 }) Equal(false, f.Can(plant));
        f.Has(15); Equal(true, f.Can(19)); Equal(true, f.Can(21)); Equal(true, f.Can(29)); Equal(false, f.Can(20));
        d.City.Flags.HydroPossible = true; Equal(true, f.Can(20));
        f.Has(16, 19); Equal(true, f.Can(21)); Equal(true, f.Can(20)); // No Factory needed when Manufacturing exists.
        f.Has(15, 21); Equal(false, f.Can(19)); Equal(true, f.Can(20)); Equal(true, f.Can(29));
        f.Has(15, 20); Equal(false, f.Can(19)); Equal(false, f.Can(21)); Equal(true, f.Can(29));
        f.Has(15, 29); Equal(false, f.Can(19)); Equal(false, f.Can(20)); Equal(false, f.Can(21));
        f.Has(15); d.World.Globals.WonderCity[WonderIds.HooverDam] = 0;
        foreach (int plant in new[] { 19, 20, 21 }) Equal(false, f.Can(plant));
        Equal(true, f.Can(29));
    }

    private static void BuildingEligibilitySpaceshipsAndScenarioWonders()
    {
        var f = new AvailabilityFixture(); var d = f.Data;
        foreach (int part in new[] { 35, 36, 37 })
        {
            d.World.Globals.WonderCity[25] = -1; d.World.Globals.Year = 1; Equal(false, f.Can(part));
            d.World.Globals.WonderCity[25] = -2; Equal(true, f.Can(part));
            d.World.Globals.Year = 0; Equal(false, f.Can(part)); d.World.Globals.Status.Scenario = true; Equal(true, f.Can(part));
            d.World.Globals.Rules.Bloodlust = true; Equal(false, f.Can(part));
            Equal(true, BuildingAvailability.Improvement(d.World, d.Rules, 1, null, part));
            d.World.Globals.Rules.Bloodlust = false; d.World.Civilizations[1].SpaceshipFlags.Launched = true; Equal(false, f.Can(part));
            d.World.Civilizations[1].SpaceshipFlags.Launched = false; d.World.Globals.Status.Scenario = false;
        }
        d.World.Globals.Status.Scenario = true;
        Throws<InvalidOperationException>(() => f.Can(39));
        Equal(false, BuildingAvailability.Wonder(d.World, d.Rules, 1, 0, new(true, true)));
        Equal(true, BuildingAvailability.Wonder(d.World, d.Rules, 1, 23, new(true, true)));
        Equal(false, BuildingAvailability.Wonder(d.World, d.Rules, 1, 23, new(true, false)));
        Equal(true, BuildingAvailability.Wonder(d.World, d.Rules, 1, 0, new(false, false)));
        d.World.Globals.RevealMap = 1; Equal(true, f.Can(39));
        d.World.Globals.WonderCity[0] = -2; Equal(false, f.Can(39));
    }

    private static void ProductionOptionsOrderedSnapshotAndExpiration()
    {
        var f = new AvailabilityFixture(); var d = f.Data;
        for (int i = 0; i < d.Rules.Units.Length; ++i) d.Rules.Units[i] = d.Rules.Units[i] with { Prerequisite = AdvanceReference.Never };
        d.Rules.Units[2] = d.Rules.Units[2] with { Prerequisite = AdvanceReference.None, ObsoleteBy = AdvanceReference.Never };
        for (int i = 1; i < d.Rules.Improvements.Length; ++i)
            d.Rules.Improvements[i] = d.Rules.Improvements[i] with { Prerequisite = AdvanceReference.Never };
        foreach (int i in new[] { 4, 35, 38, 39 }) d.Rules.Improvements[i] = d.Rules.Improvements[i] with { Prerequisite = AdvanceReference.None };
        d.World.Globals.Year = 1; d.World.Globals.WonderCity[25] = -2;
        d.Rules.WonderObsolescence[0] = (AdvanceReference)7; d.World.Civilizations[2].Advances = default(AdvanceSet).With(7);
        d.City.ShieldBox = 101; d.Orders.SaveBuildState(new(0)); d.City.ShieldBox = 5;
        var options = d.Orders.Options(new(0)); Sequence(new[] { 2, -4, -35, -38, -39 }, options.Select(o => o.Item));
        Sequence(new[] { 101, 51, 51, 51, 51 }, options.Select(o => o.Stock));
        Equal(true, options.Last().ExpiredWonder); Equal(true, options.All(o => o.Turns == 1));
        Equal((short)5, d.City.ShieldBox); Equal(101, d.World.Production.SavedStock);
    }
}
