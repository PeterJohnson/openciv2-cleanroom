using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using RuntimeCity = OpenCiv2.Core.State.City;
using RuntimeUnit = OpenCiv2.Core.State.Unit;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F02 §§2–3,9–10 / F05 entity reset portions / F06 lifetime renewal.
    private static void EntityTokenSequences()
    {
        var process = new ProcessTokens(); var peer = new ProcessTokens();
        Equal(new UnitToken(1), process.AssignUnit()); Equal(new UnitToken(2), process.AssignUnit()); Equal(new CityToken(1), process.AssignCity());
        Equal(1U, peer.NextUnit); Equal(1U, peer.NextCity); process.NextUnit = uint.MaxValue;
        Equal(new UnitToken(uint.MaxValue), process.AssignUnit()); Equal(0U, process.NextUnit); Equal(new UnitToken(0), process.AssignUnit()); Equal(new UnitToken(1), process.AssignUnit());
        Equal(2U, process.NextCity); process.NextCity = uint.MaxValue; Equal(new CityToken(uint.MaxValue), process.AssignCity()); Equal(new CityToken(0), process.AssignCity());
        Equal(1U, process.NextCity); Equal(2U, process.NextUnit); Equal(false, new UnitToken(0).Occupied); Equal(false, new CityToken(0).Occupied); Equal(true, new UnitToken(0x80000000).Occupied); Equal(true, new CityToken(uint.MaxValue).Occupied);
    }
    private static void EntityReservationFirstHole()
    {
        var tokens = Enumerable.Repeat(new UnitToken(0x80000000), EntityLimits.Units).ToArray(); tokens[7] = new(0); tokens[9] = new(0); var before = new OwnedList<UnitToken>(tokens); uint end = 2040;
        for (int i = 0; i < 2; ++i) { Equal(new UnitSlot(7), EntityAllocation.ReserveUnit(tokens.AsSpan(0, (int)end), ref end, new(1), 2000, StateConversion.DecodeCivilizations(0))!.Value); Equal(2040U, end); Equal(before, new(tokens)); }
        Array.Fill(tokens, new UnitToken(1)); Equal(true, EntityAllocation.ReserveUnit(tokens.AsSpan(0, (int)end), ref end, new(1), 2000, StateConversion.DecodeCivilizations(0)) is null); Equal(2040U, end);
        end = 2048; tokens[2047] = new(0); Equal(new UnitSlot(2047), EntityAllocation.ReserveUnit(tokens, ref end, new(7), short.MaxValue, StateConversion.DecodeCivilizations(0))!.Value); Equal(2048U, end);
    }
    private static void EntityReservationThresholds()
    {
        var tokens = Enumerable.Repeat(new UnitToken(1), EntityLimits.Units).ToArray(); uint end = 2039;
        Equal(new UnitSlot(2039), EntityAllocation.ReserveUnit(tokens.AsSpan(0, (int)end), ref end, new(1), 1948, StateConversion.DecodeCivilizations(0))!.Value); Equal(2040U, end);
        Equal(true, EntityAllocation.ReserveUnit(tokens.AsSpan(0, (int)end), ref end, new(1), 1948, StateConversion.DecodeCivilizations(0)) is null); Equal(2040U, end);
        end = 2039; Equal(true, EntityAllocation.ReserveUnit(tokens.AsSpan(0, (int)end), ref end, new(1), 1949, StateConversion.DecodeCivilizations(255)) is null); Equal(2039U, end);
        end = 2047; Equal(new UnitSlot(2047), EntityAllocation.ReserveUnit(tokens.AsSpan(0, (int)end), ref end, new(7), 1948, StateConversion.DecodeCivilizations(128))!.Value); Equal(2048U, end);
        Equal(true, EntityAllocation.ReserveUnit(tokens, ref end, new(7), 0, StateConversion.DecodeCivilizations(255)) is null); Equal(2048U, end);
        end = 2040; Equal(true, EntityAllocation.ReserveUnit(tokens.AsSpan(0, (int)end), ref end, new(6), 0, StateConversion.DecodeCivilizations(128)) is null);
        Equal(new UnitSlot(2040), EntityAllocation.ReserveUnit(tokens.AsSpan(0, (int)end), ref end, new(0), short.MinValue, StateConversion.DecodeCivilizations(1))!.Value); Equal(2041U, end);
    }
    private static void EntityReservationAdmission()
    {
        UnitToken[] tokens = [new(1), new(0)]; uint end = 2;
        Throws<ArgumentException>(() => EntityAllocation.ReserveUnit(tokens, ref end, new(8), 0, StateConversion.DecodeCivilizations(0))); Equal(2U, end);
        Throws<ArgumentException>(() => EntityAllocation.ReserveUnit(tokens.AsSpan(0, 1), ref end, new(1), 0, StateConversion.DecodeCivilizations(2))); Equal(2U, end);
        end = 1; Throws<ArgumentException>(() => EntityAllocation.ReserveUnit(tokens, ref end, new(1), 0, StateConversion.DecodeCivilizations(2))); Equal(1U, end);
        end = 2049; Throws<ArgumentException>(() => EntityAllocation.ReserveUnit(tokens, ref end, new(1), 0, StateConversion.DecodeCivilizations(2))); Equal(2049U, end); Equal(new OwnedList<UnitToken>([new(1), new(0)]), new(tokens));
    }
    private static void EntityReservationRetainedScan()
    {
        var tokens = Enumerable.Repeat(new UnitToken(99), EntityLimits.Units).ToArray(); var process = new ProcessTokens { NextUnit = 100, NextCity = 200 }; uint end = 0;
        Equal(new UnitSlot(0), EntityAllocation.ReserveUnit(tokens.AsSpan(0, 0), ref end, new(1), 0, StateConversion.DecodeCivilizations(2))!.Value); Equal(1U, end); Equal(new UnitToken(99), tokens[0]); tokens[0] = process.AssignUnit();
        end = 0; Equal(new UnitSlot(0), EntityAllocation.ReserveUnit(tokens.AsSpan(0, 0), ref end, new(1), 0, StateConversion.DecodeCivilizations(2))!.Value); Equal(new UnitToken(100), tokens[0]); tokens[0] = process.AssignUnit();
        Equal(new UnitToken(101), tokens[0]); Equal(new UnitToken(99), tokens[1]); Equal(200U, process.NextCity);
    }
    private static void EntityPoolDeletion()
    {
        var pool = new UnitPool(); pool.SetEnd(5); uint[] tokens = [11, 0, 13, 0, 15];
        for (int i = 0; i < tokens.Length; ++i) { pool.Storage[i].Token = new(tokens[i]); pool.Storage[i].Damage = 37; }
        ref var record = ref pool.At(new(4)); Equal(true, pool.FinishDeletion(4)); Equal(4, pool.End); Equal(new UnitToken(0), record.Token); Equal((byte)37, record.Damage);
        Equal(false, pool.FinishDeletion(3)); Equal(4, pool.End); Equal(false, pool.FinishDeletion(-1)); Throws<ArgumentOutOfRangeException>(() => pool.FinishDeletion(4));
        Throws<ArgumentOutOfRangeException>(() => pool.SetEnd(2049)); Throws<ArgumentOutOfRangeException>(() => pool.SetEnd(-1)); Equal(4, pool.End);
        pool.Storage[100].Token = new(91); Equal(false, pool.Live(new(100))); pool.SetEnd(101); Equal(true, pool.Live(new(100))); record.Damage = 79; Equal((byte)79, pool.At(new(4)).Damage);
        Throws<ArgumentOutOfRangeException>(() => pool.At(new(2048))); Equal(false, pool.Live(new(65535))); Equal(101, pool.Scanned.Length);
        var cities = new CityPool(); cities.SetEnd(256); cities.At(new(255)).Token = new(0x80000000); cities.At(new(255)).Size = -128;
        Equal(true, cities.FinishDeletion(255)); Equal(255, cities.End); Equal((sbyte)-128, cities.At(new(255)).Size); Equal(false, cities.FinishDeletion(254)); Equal(255, cities.End);
        Throws<ArgumentOutOfRangeException>(() => cities.At(new(256))); Equal(false, cities.Live(new(65535)));
    }
    private static void EntityPoolReservation()
    {
        var pool = new UnitPool(); pool.SetEnd(2040); foreach (ref var unit in pool.Scanned) unit.Token = new(1);
        pool.Storage[7].Token = new(0); pool.Storage[7].Damage = 10; pool.Storage[7].Orders = UnitOrder.Sleeping;
        Equal(new UnitSlot(7), EntityAllocation.ReserveUnit(pool, new(2), 2000, StateConversion.DecodeCivilizations(0))!.Value); Equal(2040, pool.End); Equal((byte)10, pool.Storage[7].Damage); Equal((sbyte)UnitOrder.Sleeping, (sbyte)pool.Storage[7].Orders); Equal(new UnitToken(0), pool.Storage[7].Token);
        pool.Storage[7].Token = new(1); Equal(true, EntityAllocation.ReserveUnit(pool, new(2), 1948, StateConversion.DecodeCivilizations(0)) is null); Equal(new UnitSlot(2040), EntityAllocation.ReserveUnit(pool, new(2), 1948, StateConversion.DecodeCivilizations(4))!.Value); Equal(2041, pool.End);
        Throws<ArgumentException>(() => EntityAllocation.ReserveUnit(pool, new(8), 0, StateConversion.DecodeCivilizations(255))); Equal(2041, pool.End);
    }
    private static void EntityPoolResets()
    {
        var pools = new EntityPools(); var process = new ProcessTokens { NextUnit = 100, NextCity = 200 }; pools.Units.SetEnd(2); pools.Cities.SetEnd(1);
        ref var unit = ref pools.Units.At(new(1)); unit.Token = process.AssignUnit(); unit.Previous = new UnitSlot(0); unit.Next = new UnitSlot(2); unit.Damage = 77; unit.Destination = new(32, 0); unit.Carrier = new UnitSlot(32); unit.Orders = UnitOrder.Sleeping; pools.Units.At(new(2047)) = unit;
        ref var city = ref pools.Cities.At(new(255)); city.Token = process.AssignCity(); city.Size = -128; city.FoodBox = short.MinValue; UnitSlot? selected = null;
        pools.ResetScans(ref selected); Equal(0, pools.Units.End); Equal(0, pools.Cities.End); Equal(new UnitSlot(0), selected!.Value); Equal(false, pools.Units.Live(selected.Value)); Equal(new UnitToken(100), unit.Token); Equal((short)0, (unit.Previous?.Value ?? -1));
        pools.InvalidateForNewSession();
        foreach (var item in pools.Units.Storage) { Equal(new UnitToken(0), item.Token); Equal((short)-1, (item.Previous?.Value ?? -1)); Equal((short)-1, (item.Next?.Value ?? -1)); }
        Equal((byte)77, unit.Damage); Equal((short)32, unit.Destination.X); Equal((sbyte)UnitOrder.Sleeping, (sbyte)unit.Orders); Equal((byte)77, pools.Units.At(new(2047)).Damage);
        Equal(new CityToken(0), city.Token); Equal((sbyte)-128, city.Size); Equal(short.MinValue, city.FoodBox); Equal(101U, process.NextUnit); Equal(201U, process.NextCity);
        var fresh = new EntityPools(); Equal((byte)0, fresh.Units.At(new(1)).Damage); Equal((sbyte)0, fresh.Cities.At(new(255)).Size);
    }
    private static void EntityPoolRenewal()
    {
        var pools = new EntityPools(); pools.Units.SetEnd(3); pools.Cities.SetEnd(2);
        pools.Units.Storage[0].Token = new(50); pools.Units.Storage[2].Token = new(90); pools.Units.Storage[0].Home = new(200); pools.Units.Storage[0].Next = new UnitSlot(2); pools.Units.Storage[1].Damage = 123;
        pools.Cities.Storage[0].Token = new(7); pools.Cities.Storage[1].Token = new(9); var process = new ProcessTokens { NextUnit = 100, NextCity = 200 };
        pools.RenewLoadedLifetimes(process, false); Equal(new OwnedList<UnitToken>([new(100), new(0), new(101)]), new(pools.Units.Scanned.ToArray().Select(u => u.Token)));
        Equal(new CitySlot(200), pools.Units.Storage[0].Home!.Value); Equal(new UnitSlot(2), pools.Units.Storage[0].Next!.Value); Equal((byte)123, pools.Units.Storage[1].Damage);
        Equal(new CityToken(200), pools.Cities.Storage[0].Token); Equal(new CityToken(201), pools.Cities.Storage[1].Token); Equal(102U, process.NextUnit); Equal(202U, process.NextCity);
        process.NextUnit = uint.MaxValue; pools.RenewLoadedLifetimes(process, false); Equal(new UnitToken(uint.MaxValue), pools.Units.Storage[0].Token); Equal(new UnitToken(0), pools.Units.Storage[2].Token); Equal(1U, process.NextUnit);
        pools.RenewLoadedLifetimes(process, true); Equal(new UnitToken(2), pools.Units.Storage[1].Token); Equal(new UnitToken(3), pools.Units.Storage[2].Token);
    }
    private static void EntityRecordCopies()
    {
        var unit = new RuntimeUnit(); Equal((sbyte)-1, (sbyte)unit.Orders); Equal((short)-1, (unit.Previous?.Value ?? -1)); Equal((short)-1, (unit.Next?.Value ?? -1)); Equal(true, unit.Home is null);
        foreach (short link in new short[] { short.MinValue, -2, -1 })
        {
            Equal<UnitSlot?>(null, StateConversion.DecodeUnitReference(link));
            Equal<CitySlot?>(null, StateConversion.DecodeCityReference(link));
        }
        Equal<UnitSlot?>(new(32767), StateConversion.DecodeUnitReference(short.MaxValue));
        Equal<CitySlot?>(new(0), StateConversion.DecodeCityReference(0));
        Equal((short)-1, StateConversion.EncodeUnitReference(null));
        Equal((short)-1, StateConversion.EncodeCityReference(null));
        Throws<OverflowException>(() => StateConversion.EncodeUnitReference(new(ushort.MaxValue)));
        Throws<OverflowException>(() => StateConversion.EncodeCityReference(new(ushort.MaxValue)));
        var city = new RuntimeCity(); for (int i = 0; i < 3; ++i) Equal((short)-1, (city.RoutePartners[i]?.Value ?? -1)); Equal(0, city.Name.Length);
        city.RememberedSize[7] = 255; city.Improvements = city.Improvements.With(39); city.Supplies[2] = -128; city.RoutePartners[2] = new CitySlot(254); city.Name = "\u00e9\u0000";
        var copy = city; var snapshot = new OwnedList<RuntimeCity>([city]); copy.RememberedSize[7] = 1; copy.Improvements = copy.Improvements.With(39, false).With(33); copy.Supplies[2] = 3; copy.RoutePartners[2] = (CitySlot?)null; copy.Name = "\u0001";
        Equal(city, snapshot[0]); Equal(false, city == copy); Equal((byte)255, city.RememberedSize[7]); Equal(true, city.Improvements.Contains(39)); Equal((sbyte)-128, city.Supplies[2]); Equal((short)254, (city.RoutePartners[2]?.Value ?? -1)); Equal("\u00e9\u0000", city.Name);
        Equal(city.GetHashCode(), snapshot[0].GetHashCode()); var pool = new CityPool(); Equal(new RuntimeCity(), pool.At(new(255)));
    }
    private static void EntityMoodDispatch()
    {
        var workspace = new CityMoodWorkspace { City = new(4), Happy = 5, Unhappy = 2 };
        Equal(3, workspace.Calculate(new(9), false)); Equal(0, workspace.Calculate(null, true, () => throw new InvalidOperationException("Must not run"))); Equal(new CitySlot(4), workspace.City!.Value);
        Equal(6, workspace.Calculate(new(0), true, () => { Equal(new CitySlot(0), workspace.City!.Value); workspace.Happy = 8; return 6; })); Equal(8, workspace.Happy);
        workspace.Happy = int.MaxValue; workspace.Unhappy = -1; Equal(int.MinValue, workspace.Calculate(new(9), false));
        var failure = new InvalidOperationException("authored child failure");
        try { workspace.Calculate(new(5), true, () => { workspace.Happy = 17; throw failure; }); throw new Exception("Expected child failure"); }
        catch (InvalidOperationException caught) { Equal(true, ReferenceEquals(failure, caught)); }
        Equal(new CitySlot(5), workspace.City!.Value); Equal(17, workspace.Happy);
        Throws<ArgumentException>(() => workspace.Calculate(new(7), true)); Equal(new CitySlot(7), workspace.City!.Value);
    }
}
