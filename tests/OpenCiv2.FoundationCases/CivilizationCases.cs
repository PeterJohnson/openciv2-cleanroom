using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F02 §4 / F06 §6 stage 17; authored vectors and cleanroom census regression cases.
    private static Civilization[] IncomingCivilizations() => Enumerable.Range(0, 8).Select(_ =>
        StateConversion.ToLive(ClassicRecords.DecodeStoredCivilization(Enumerable.Repeat((byte)0xa5, 1428).ToArray()))).ToArray();

    private static void CivilizationCensusCounts()
    {
        var civs = IncomingCivilizations(); var prior = civs[1]; var pools = new EntityPools();
        pools.Units.SetEnd(260);
        foreach (ref var unit in pools.Units.Scanned) { unit.Token = new(1); unit.Owner = new(1); unit.Type = new(61); }
        pools.Units.Storage[0].Token = new(0); pools.Units.Storage[0].Type = new(255);
        pools.Units.Storage[1].Owner = new(9); pools.Units.Storage[1].Type = new(255);
        pools.Units.Storage[260].Token = new(1); pools.Units.Storage[260].Type = new(255);
        pools.Cities.SetEnd(256);
        foreach (ref var city in pools.Cities.Scanned) { city.Token = new(1); city.Owner = new(1); city.Production = 61; }
        CivilizationCensus.RebuildLoadedUnits(civs, pools);
        Equal((byte)2, civs[1].UnitsActive[61]); Equal((byte)0, civs[1].UnitsInProduction[61]);
        var expected = prior; expected.UnitsActive = default; expected.UnitsActive[61] = 2; expected.UnitsInProduction = default;
        Equal(expected, civs[1]); Equal((byte)0, civs[0].UnitsActive[0]); Equal((byte)0, civs[7].UnitsInProduction[61]); Equal((byte)0xa5, civs[7].UnitsLost[61]);
        pools.Cities.Storage[0].Token = new(0); pools.Cities.Storage[0].Production = 127;
        pools.Cities.Storage[1].Production = -1; pools.Cities.Storage[2].Production = -128;
        CivilizationCensus.RebuildLoadedUnits(civs, pools); Equal((byte)253, civs[1].UnitsInProduction[61]);
    }
    private static void CivilizationCensusFailures()
    {
        var civs = IncomingCivilizations(); var pools = new EntityPools(); var later = civs[3];
        pools.Units.SetEnd(2);
        foreach (ref var unit in pools.Units.Scanned) { unit.Token = new(1); unit.Owner = new(2); unit.Type = new(0); }
        pools.Units.Storage[1].Type = new(62);
        Throws<ArgumentOutOfRangeException>(() => CivilizationCensus.RebuildLoadedUnits(civs, pools));
        Equal((byte)0, civs[1].UnitsActive[0]); Equal((byte)1, civs[2].UnitsActive[0]); Equal((byte)0, civs[2].UnitsInProduction[0]); Equal(later, civs[3]);
        pools.Units.Storage[1].Type = new(0); pools.Cities.SetEnd(2);
        foreach (ref var city in pools.Cities.Scanned) { city.Token = new(1); city.Owner = new(2); city.Production = 0; }
        pools.Cities.Storage[1].Production = 62;
        Throws<ArgumentOutOfRangeException>(() => CivilizationCensus.RebuildLoadedUnits(civs, pools));
        Equal((byte)2, civs[2].UnitsActive[0]); Equal((byte)1, civs[2].UnitsInProduction[0]); Equal(later, civs[3]);
        var before = new OwnedList<Civilization>(civs);
        Throws<ArgumentException>(() => CivilizationCensus.RebuildLoadedUnits(civs.AsSpan(0, 7), pools)); Equal(before, new(civs));
    }
    private static void CivilizationValueCopies()
    {
        var first = IncomingCivilizations()[0]; var copy = first; var owned = new OwnedList<Civilization>([first]);
        copy.Treaties[7] = StateConversion.DecodeRelationship(0x12345678); copy.Goals[47] = new(new(1, 2), -3, -4); copy.StandingGoals[15] = new(new(-5, -6), 7, 8);
        copy.ContinentMilitary[63] = 0xffff; copy.AdvanceSource[99] = 0; copy.SpaceshipParts[5] = short.MinValue;
        Equal(first, owned[0]); Equal(first.GetHashCode(), owned[0].GetHashCode()); Equal(false, first == copy);
        Equal(StateConversion.DecodeRelationship(0xa5a5a5a5U), first.Treaties[7]); Equal((byte)0xa5, first.AdvanceSource[99]);
        var globals = StateConversion.ToLive(ClassicRecords.DecodeStoredGlobals(Pattern(330))); var other = globals;
        other.WonderCity[27] = -2; other.FirstDiscoverer[99] = 0; other.PowerRank[7] = 0;
        Equal(false, globals == other); Equal((byte)0x99, globals.FirstDiscoverer[99]);
        Equal((short)-1, (new GlobalValues().ActiveUnit?.Value ?? -1)); Equal((short)-1, (new GlobalValues().MovingUnit?.Value ?? -1));
        // Directed diplomacy is retained independently, not mirrored by construction or copy.
        var civs = new Civilization[8]; civs[1].Treaties[2] = StateConversion.DecodeRelationship(0x80000001); Equal(StateConversion.DecodeRelationship(0U), civs[2].Treaties[1]);
    }
    private static void UnitLiveConversion()
    {
        var stored = ClassicRecords.DecodeStoredUnit(Pattern(32)); var live = StateConversion.ToLive(stored);
        Equal(stored with { RetainedByte = 0, RetainedWord = 0 }, StateConversion.ToStored(live));
        Equal(stored, StateConversion.PreserveOpaque(StateConversion.ToStored(live), stored));
        live.Token = new(uint.MaxValue); live.Previous = (UnitSlot?)null; live.Next = (UnitSlot?)null; live.Home = null;
        var exported = StateConversion.PreserveOpaque(StateConversion.ToStored(live), stored);
        Equal(new UnitToken(uint.MaxValue), exported.Token); Equal((short)-1, exported.Previous); Equal((short)-1, exported.Next); Equal(true, exported.Home is null);
        Equal(live, StateConversion.ToLive(ClassicRecords.DecodeStoredUnit(ClassicRecords.Encode(exported))));
    }
    private static void CityLiveConversion()
    {
        var stored = ClassicRecords.DecodeStoredCity(Pattern(88)); var live = StateConversion.ToLive(stored);
        Equal(stored with { RetainedByte = 0, Flags = stored.Flags & StateConversion.CityStatusMask, WorkedTiles = stored.WorkedTiles & 0xfc1fffff }, StateConversion.ToStored(live)); Equal(16, live.Name.Length);
        Equal(stored, StateConversion.PreserveOpaque(StateConversion.ToStored(live), stored));
        byte[] name = Enumerable.Repeat((byte)0xff, 16).ToArray(); name[0] = 0xe9; name[1] = 0;
        var shortName = StateConversion.ToLive(stored with { Name = new(name) }); Equal("\u00e9", shortName.Name);
        var export = StateConversion.ToStored(shortName); Equal((byte)0xe9, export.Name[0]); Equal(true, export.Name.Skip(1).All(b => b == 0));
        shortName.Name = ClassicText.DecodeWindows1252((new byte[17]).ToArray()); Throws<ArgumentException>(() => StateConversion.ToStored(shortName));
        shortName.Name = "\u0001\u0000\u0002"; Throws<ArgumentException>(() => StateConversion.ToStored(shortName));
        shortName.Name = ""; Equal(true, StateConversion.ToStored(shortName).Name.All(b => b == 0));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { RoutePartners = new([0, 1]) }));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { Name = new([1]) }));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { Supplies = new([1, 2, 3, 4]) }));
        live.RoutePartners[0] = (CitySlot?)null; Equal((short)-1, StateConversion.ToStored(live).RoutePartners[0]);
    }
    private static void CivilizationLiveConversion()
    {
        var stored = ClassicRecords.DecodeStoredCivilization(Pattern(1428)); var live = StateConversion.ToLive(stored);
        Equal((ushort)(stored.Flags & StateConversion.CivilizationStatusMask), StateConversion.ToStored(live).Flags);
        Equal(stored, StateConversion.PreserveOpaque(StateConversion.ToStored(live), stored));
        var snapshot = StateConversion.ToStored(live); live.UnitsLost[61] = 255; live.Goals[47] = new(new(-1, 2), -3, 4);
        Equal(stored.UnitsLost[61], snapshot.UnitsLost[61]); Equal(stored.Goals[47], snapshot.Goals[47]);
        live.Government = (Government)(-1); Equal((byte)255, StateConversion.ToStored(live).Government);
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { Goals = OwnedList<CivilizationGoal>.Empty }));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { UnitsActive = new(new byte[63]) }));
        var exported = StateConversion.ToStored(live); Equal(live, StateConversion.ToLive(exported) with { Government = live.Government });
    }
    private static void GlobalsLiveConversion()
    {
        var stored = ClassicRecords.DecodeStoredGlobals(Pattern(330)); var live = StateConversion.ToLive(stored);
        Equal((ushort)(stored.GameFlags & 0xff), StateConversion.ToStored(live).GameFlags);
        Equal(live, StateConversion.ToLive(StateConversion.ToStored(live)));
        Equal(stored, StateConversion.PreserveOpaque(StateConversion.ToStored(live), stored));
        live.Multiplayer = (GameMode)511; live.Difficulty = (Difficulty)(-1); live.ActiveUnit = (UnitSlot?)null; live.MovingUnit = (UnitSlot?)null;
        var exported = StateConversion.ToStored(live); Equal((byte)255, exported.Multiplayer); Equal((byte)255, exported.Difficulty); Equal((short)-1, exported.ActiveUnit); Equal((short)-1, exported.MovingUnit);
        var snapshot = exported; live.FirstDiscoverer[99] = 0; live.AdvanceKnownBy[99] = StateConversion.DecodeCivilizations(0); live.WonderCity[27] = -1;
        Equal(stored.FirstDiscoverer[99], snapshot.FirstDiscoverer[99]); Equal(stored.AdvanceKnownBy[99], snapshot.AdvanceKnownBy[99]);
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { WonderCity = OwnedList<short>.Empty }));
        Throws<ArgumentException>(() => StateConversion.ToLive(stored with { PowerRank = new(new byte[9]) }));
    }
}
