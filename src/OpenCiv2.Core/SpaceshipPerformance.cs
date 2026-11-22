using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>V07 calculation scratch, shared by all ships within one world and retained across ship loss.</summary>
public sealed class SpaceshipWorkspace
{
    public int Mass, Support, Energy, Fuel, FlightTime, Success, ProjectedArrival;
}

/// <summary>V07 §§3–4 calculation and production-group choice, using declared gameplay values and
/// actual F04 calendar. Preview years are mutations; no draw or implicit current-year update occurs.</summary>
public sealed class SpaceshipPerformance(GameState world, RuleTables rules, SpaceshipValues values, CalendarValues calendar)
{
    private SpaceshipWorkspace Work => world.Spaceship;
    private int Count(int owner, int part) => SpaceshipQueries.PartCount(world, values, owner, part);
    private int Limit(int owner, int part) => SpaceshipQueries.PartLimit(world, values, owner, part);
    private bool CanBuild(int owner, int group) => SpaceshipQueries.GroupCanBuild(world, rules, values, owner, group);
    private bool BelowLimit(int owner, int group) => SpaceshipQueries.GroupCountClamped(world, values, owner, group)
        < SpaceshipQueries.GroupLimit(world, values, owner, group);
    private int Year() => Calendar.Year(world.Globals.Turn, world.Globals, world.Scenario, calendar);

    public void Calculate(int owner, bool includeSupport)
    {
        MapKnowledge.RequireLocal(world);
        unchecked
        {
            ref var civ = ref world.Civilizations[owner];
            if (!civ.SpaceshipFlags.Launched && CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, AdvanceIds.FusionPower))
            {
                civ.SpaceshipFlags.FusionAtLaunch = true;
            }

            var flags = civ.SpaceshipFlags;
            Work.Mass = 0;
            for (int p = 0; p < 6; ++p)
            {
                Work.Mass += civ.SpaceshipParts[p] * values.Parts[p].MassHundredsOfTons;
            }

            int fullMass = Work.Mass;
            for (int p = 0; p < 6; ++p)
            {
                if (civ.SpaceshipParts[p] == 0)
                {
                    Work.Mass += values.Parts[p].MassHundredsOfTons;
                }
            }

            Work.Success = 100;
            int life = Count(owner, 4), habitation = Count(owner, 3);
            Work.Support = Arithmetic.Divide32(100 * life, Arithmetic.ClampAB(habitation, 1, 99));
            if (includeSupport)
            {
                Work.Success = Arithmetic.Divide32(Arithmetic.ClampAB(Work.Support, 0, 100) * Work.Success, 100);
            }

            int solar = Count(owner, 5);
            habitation = Count(owner, 3);
            life = Count(owner, 4);
            Work.Energy = Arithmetic.Divide32(200 * solar, Arithmetic.ClampAB(habitation + life, 1, 99));
            Work.Success = Arithmetic.Divide32(Arithmetic.ClampAB(Work.Energy, 0, 100) * Work.Success, 100);
            int fuel = Count(owner, 2), propulsion = Count(owner, 1);
            Work.Fuel = Arithmetic.Divide32(100 * fuel, Arithmetic.ClampAB(propulsion, 1, 99));
            int paradigm = rules.Cosmic.SpaceshipMassThrustParadigm;
            if (flags.FusionAtLaunch)
            {
                paradigm = (paradigm * 3) >> 2;
            }

            int scale = 1;
            while (paradigm > 100)
            { scale *= 2; paradigm >>= 1; }
            Work.FlightTime = Estimate(owner, Work.Mass, paradigm, false);
            int arrival = Estimate(owner, fullMass, paradigm, false);
            int projected = Estimate(owner, fullMass, paradigm, true);
            if (scale > 1)
            { Work.FlightTime *= scale; arrival *= scale; projected *= scale; }
            if (Work.FlightTime > 150)
            {
                Work.Success -= (Work.FlightTime - 150) / 10;
            }

            Work.Success = Arithmetic.ClampAB(Work.Success, 0, 100);
            if (!civ.SpaceshipFlags.Launched)
            {
                civ.SpaceshipPopulation = (short)Count(owner, 3);
                civ.SpaceshipLaunchYear = (short)Year();
                civ.SpaceshipArrivalYear = (short)(Year() + arrival / 10);
                Work.ProjectedArrival = Year() + projected / 10;
            }
        }
    }

    private int Estimate(int owner, int mass, int paradigm, bool raw)
    {
        unchecked
        {
            int numerator = mass * paradigm;
            int fuel = SpaceshipQueries.Thrust(raw ? world.Civilizations[owner].SpaceshipParts[2] : Count(owner, 2));
            int propulsion = SpaceshipQueries.Thrust(raw ? world.Civilizations[owner].SpaceshipParts[1] : Count(owner, 1));
            return Arithmetic.Divide32(numerator, Arithmetic.ClampAB(10 * propulsion, 0, 10 * fuel) + 1);
        }
    }

    public int ChooseGroup(int owner, int group)
    {
        Calculate(owner, false);
        if (world.Civilizations[owner].SpaceshipFlags.Launched)
        {
            return -1;
        }

        int best = 9999;
        for (int human = 1; human < EntityLimits.Civilizations; ++human)
        {
            if (world.Globals.HumanPlayers.Contains(human)
                && world.Civilizations[human].SpaceshipArrivalYear > 0 && world.Civilizations[human].SpaceshipArrivalYear < best)
            {
                best = world.Civilizations[human].SpaceshipArrivalYear;
            }
        }

        int arrival = world.Civilizations[owner].SpaceshipArrivalYear;
        bool raceClose = arrival != 0 && unchecked(arrival - world.Globals.Year) <= 14 && best > arrival;
        bool preferOther = group == 0 && (BelowLimit(owner, 2) || (!raceClose && BelowLimit(owner, 1)));
        if (preferOther || (group == 1 && raceClose) || !BelowLimit(owner, group) || !CanBuild(owner, group))
        {
            int lowest = group == 0 ? 1 : 0;
            for (int candidate = 2; candidate >= lowest; --candidate)
            {
                if (CanBuild(owner, candidate) && (candidate != 1 || !raceClose) && BelowLimit(owner, candidate))
                {
                    group = candidate;
                    break;
                }
            }
        }
        if (group == 0)
        {
            for (int human = 1; human < EntityLimits.Civilizations; ++human)
            {
                if (!world.Globals.HumanPlayers.Contains(human) || !world.Civilizations[human].SpaceshipFlags.Launched
                    || world.Civilizations[human].SpaceshipArrivalYear > Work.ProjectedArrival
                    || SpaceshipQueries.GroupFull(world, values, owner, 1)
                    || !CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, (int)rules.Improvements[BuildingIds.Component].Prerequisite))
                {
                    continue;
                }

                if (world.Civilizations[owner].SpaceshipParts[1] <= Limit(owner, 1))
                {
                    return 1;
                }

                if (world.Civilizations[owner].SpaceshipParts[2] <= Limit(owner, 2))
                {
                    return 1;
                }
            }
        }
        return CanBuild(owner, group) ? group : -2;
    }
}
