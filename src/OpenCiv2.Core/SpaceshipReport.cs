using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>V07 §7 owned text-report values after the actual support-inclusive calculation.</summary>
public sealed record SpaceshipReport(int Civilization, OwnedList<short> Parts, OwnedList<int> Limits,
    bool OverCapacity, int Population, int Support, int Energy, int Mass, int Fuel, bool Fusion,
    int FlightTime, int Success, bool Launched, int ArrivalYear, bool Arrived, bool CanLaunch)
{
    public static SpaceshipReport Prepare(GameState world, RuleTables rules, SpaceshipValues values,
        CalendarValues calendar, int owner, bool allowLaunch)
    {
        if (owner < 0 || owner >= EntityLimits.Civilizations)
        { throw new ArgumentOutOfRangeException(nameof(owner)); }
        new SpaceshipPerformance(world, rules, values, calendar).Calculate(owner, true);
        ref var civ = ref world.Civilizations[owner];
        var parts = new short[6];
        var limits = new int[6];
        bool over = false;
        for (int part = 0; part < 6; ++part)
        {
            parts[part] = civ.SpaceshipParts[part];
            limits[part] = SpaceshipQueries.PartLimit(world, values, owner, part);
            over |= parts[part] > limits[part];
        }
        var work = world.Spaceship;
        bool launched = civ.SpaceshipFlags.Launched;
        return new(owner, new(parts), new(limits), over, parts[3], work.Support, work.Energy,
            work.Mass, work.Fuel, civ.SpaceshipFlags.FusionAtLaunch, work.FlightTime, work.Success,
            launched, civ.SpaceshipArrivalYear, launched && civ.SpaceshipArrivalYear < world.Globals.Year,
            !launched && world.Globals.HumanPlayers.Contains(owner) && allowLaunch && parts[3] != 0 && work.Success != 0);
    }
}
