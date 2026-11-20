using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Query;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static EntityPools LiveQueryPools()
    {
        var pools = new EntityPools(); pools.Units.SetEnd(5); pools.Cities.SetEnd(4);
        for (int i = 0; i < 6; ++i) pools.Units.Storage[i] = new() { Token = new((uint)(100 + i)), Owner = new((byte)(i % 2 + 1)), Position = new((short)(i * 2), 0) };
        pools.Units.Storage[2].Token = new(0); pools.Units.Storage[5].Owner = new(1);
        for (int i = 0; i < 5; ++i) pools.Cities.Storage[i] = new() { Token = new((uint)(200 + i)), Owner = new((byte)(i % 2 + 1)), Name = ClassicText.DecodeField((new byte[] { (byte)('A' + i) }).ToArray()) };
        pools.Cities.Storage[2].Token = new(0);
        return pools;
    }
    private static void LiveQueryOwnEntities()
    {
        var pools = LiveQueryPools(); var observation = new LiveObservation(new(7), new(9), 11, new(1));
        var units = LiveProjection.OwnUnits(pools, observation); var cities = LiveProjection.OwnCities(pools, observation);
        Equal(observation, units.Observation); Equal(observation, cities.Observation);
        Equal(new OwnedList<UnitSlot>([new(0), new(4)]), new(units.Records.Select(u => u.Slot))); Equal(new UnitToken(104), units.Records[1].Token);
        Equal(1, cities.Records.Count); Equal(new CitySlot(0), cities.Records[0].Slot); Equal("A", cities.Records[0].Name);
        var other = observation with { Observer = new(2) };
        Equal(new OwnedList<UnitSlot>([new(1), new(3)]), new(LiveProjection.OwnUnits(pools, other).Records.Select(u => u.Slot)));
        Equal(new OwnedList<CitySlot>([new(1), new(3)]), new(LiveProjection.OwnCities(pools, other).Records.Select(c => c.Slot)));
        Equal(0, LiveProjection.OwnUnits(pools, observation with { Observer = new(0) }).Records.Count);
        Equal(0, LiveProjection.OwnCities(pools, observation with { Observer = new(7) }).Records.Count);
    }
    private static void LiveQueryFieldDomains()
    {
        var pools = new EntityPools(); pools.Units.SetEnd(1); pools.Cities.SetEnd(1);
        pools.Units.Storage[0] = new() { Token = new(uint.MaxValue), Owner = new(0), Type = new(255), MovesUsed = 255, Damage = 254, Orders = (UnitOrder)(-128), Home = new(255), Position = new(short.MinValue, short.MaxValue) };
        pools.Cities.Storage[0] = new() { Token = new(0x80000000), Owner = new(0), Name = "\u00ff\u20acA\u0000cX", Size = -128, Production = 127, FoodBox = short.MinValue, ShieldBox = short.MaxValue, Position = new(short.MaxValue, short.MinValue) };
        var observation = new LiveObservation(new(ulong.MaxValue), new(ulong.MaxValue), ulong.MaxValue, new(0));
        Equal(new OwnUnit(new(0), new(uint.MaxValue), new(short.MinValue, short.MaxValue), new(255), 255, 254, (UnitOrder)(-128), new(255)), LiveProjection.OwnUnits(pools, observation).Records[0]);
        Equal(new OwnCity(new(0), new(0x80000000), new(short.MaxValue, short.MinValue), "\u00ff\u20acA\0cX", -128, 127, short.MinValue, short.MaxValue), LiveProjection.OwnCities(pools, observation).Records[0]);
        pools.Units.Storage[0].Home = null; Equal(true, LiveProjection.OwnUnits(pools, observation).Records[0].Home is null);
        pools.Cities.Storage[0].Name = ""; Equal(0, LiveProjection.OwnCities(pools, observation).Records[0].Name.Length);
        pools.Cities.Storage[0].Name = ClassicText.DecodeWindows1252((Enumerable.Repeat((byte)81, 100)).ToArray()); Equal(100, LiveProjection.OwnCities(pools, observation).Records[0].Name.Length);
    }
    private static void LiveQueryOwnedSnapshots()
    {
        var pools = LiveQueryPools(); var observation = new LiveObservation(new(1), new(1), 0, new(1));
        var units = LiveProjection.OwnUnits(pools, observation); var cities = LiveProjection.OwnCities(pools, observation);
        pools.Units.Storage[0].Damage = 9; pools.Units.Storage[0].Token = new(777); pools.Cities.Storage[0].Name = "Z"; pools.Cities.Storage[0].FoodBox = 123;
        Equal((byte)0, units.Records[0].Damage); Equal(new UnitToken(100), units.Records[0].Token); Equal("A", cities.Records[0].Name); Equal((short)0, cities.Records[0].FoodBox);
        var updated = LiveProjection.OwnUnits(pools, observation with { Revision = 1 }); Equal((byte)9, updated.Records[0].Damage); Equal(0UL, units.Observation.Revision); Equal(1UL, updated.Observation.Revision);
        var copied = cities.Records[0].Name.ToArray(); copied[0] = '\0'; Equal('A', cities.Records[0].Name[0]);
        pools.InvalidateForNewSession(); Equal(2, units.Records.Count); Equal(1, cities.Records.Count); Equal(0, LiveProjection.OwnUnits(pools, observation).Records.Count);
    }
    private static void LiveQueryAdmission()
    {
        var pools = LiveQueryPools(); var observation = new LiveObservation(new(1), new(1), 0, new(1));
        foreach (var invalid in new[] { observation with { Runtime = new(0) }, observation with { Session = new(0) }, observation with { Observer = new(8) }, observation with { Observer = new(255) }, observation with { Provenance = (LiveProvenance)1 } })
        { Throws<ArgumentException>(() => LiveProjection.OwnUnits(pools, invalid)); Throws<ArgumentException>(() => LiveProjection.OwnCities(pools, invalid)); }
        Equal(2, LiveProjection.OwnUnits(pools, observation).Records.Count); Equal(1, LiveProjection.OwnCities(pools, observation).Records.Count);
        pools.Units.SetEnd(EntityLimits.Units); pools.Cities.SetEnd(EntityLimits.Cities);
        pools.Units.Storage[^1] = new() { Token = new(999), Owner = new(1) }; pools.Cities.Storage[^1] = new() { Token = new(999), Owner = new(1) };
        Equal(new UnitSlot(2047), LiveProjection.OwnUnits(pools, observation).Records[^1].Slot); Equal(new CitySlot(255), LiveProjection.OwnCities(pools, observation).Records[^1].Slot);
    }
    private static void LiveQueryNoSideEffects()
    {
        var world = WriterWorld(); world.Entities.Units.SetEnd(1); world.Entities.Cities.SetEnd(1);
        world.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(1), Flags = StateConversion.DecodeUnitStatus(0xabcd), Visibility = StateConversion.DecodeCivilizations(255), Previous = new UnitSlot(9), Next = (UnitSlot?)null };
        world.Entities.Cities.Storage[0] = new() { Token = new(1), Owner = new(1), Name = "A\u0000c", KnownTo = StateConversion.DecodeCivilizations(255), Flags = StateConversion.DecodeCityStatus(uint.MaxValue) };
        world.Civilizations[1].MilitaryCount = -77; world.Mood.Happy = 123;
        var beforeUnits = new OwnedList<Unit>(world.Entities.Units.Storage.ToArray()); var beforeCities = new OwnedList<City>(world.Entities.Cities.Storage.ToArray()); var beforeGlobals = world.Globals;
        var observation = new LiveObservation(new(1), new(1), 5, new(1));
        Equal(LiveProjection.OwnUnits(world.Entities, observation), LiveProjection.OwnUnits(world.Entities, observation));
        Equal(LiveProjection.OwnCities(world.Entities, observation), LiveProjection.OwnCities(world.Entities, observation));
        Equal(beforeUnits, new(world.Entities.Units.Storage.ToArray())); Equal(beforeCities, new(world.Entities.Cities.Storage.ToArray())); Equal(beforeGlobals, world.Globals);
        Equal((short)-77, world.Civilizations[1].MilitaryCount); Equal(123, world.Mood.Happy);
    }
}
