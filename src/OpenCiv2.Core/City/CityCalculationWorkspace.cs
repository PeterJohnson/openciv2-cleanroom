using OpenCiv2.Contracts;

namespace OpenCiv2.Core.City;

public enum CityYield { Food, Shields, Trade }

[Flags]
public enum TileAvailability : byte
{
    None = 0, Unusable = 1, HostileUnit = 4, CityPresent = 8, OtherCityWorked = 16, HumanBlocker = 32
}

/// <summary>F02/C02/C03 shared calculation state. It belongs to one world, not one city;
/// callers own processing/reallocation/job resets and the support phase before worker allocation.</summary>
public sealed class CityCalculationWorkspace
{
    public TileAvailability[] Availability { get; } = new TileAvailability[25];
    public int[] TileYield { get; } = new int[3];
    public int[] Totals { get; } = new int[3];
    public int Food { get => Totals[0]; set => Totals[0] = value; }
    public int Shields { get => Totals[1]; set => Totals[1] = value; }
    public int Trade { get => Totals[2]; set => Totals[2] = value; }
    public int Unassigned, SpecialistCount;
    public bool WorkersOverflow;
    public int SupportedSettlers, SettlerFood, ShieldSupport;
    public bool Processing, Reallocating, ApplyYield;
    public bool JobFound;
    public int JobPriority;
    public MapPoint JobPosition;
    public int FoodRows, ShieldRows, SupportedUnits, AggressiveUnits, HomeAttackUnits;
    public CitySlot? CapitalCity, FoodPartner;
    public int CapitalDistance, CapitalLink, FoodOut, FoodIn;
    public int PollutionDivisor, PopulationFactor, Pollution, ShieldWaste, TradeCorruption;
    public int[] RouteTrade { get; } = new int[3];
    public int Luxury, Science, Tax, BuildingContent, ExcessUnhappy, MartialLaw;
    public byte[] HistoryHappy { get; } = new byte[5];
    public byte[] HistoryUnhappy { get; } = new byte[5];
    public byte[] HistoryExcess { get; } = new byte[5];
}
