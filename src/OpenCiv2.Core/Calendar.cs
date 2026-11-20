using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

public static class Calendar
{
    /// <summary>F04 §5: pure signed-32 year from selected validated calendar operands, with no implicit content
    /// fallback.</summary>
    public static int Year(
        int inputTurn,
        in GlobalValues globals,
        in ScenarioParameters scenario,
        CalendarValues calendar)
    {
        unchecked
        {
            int level = (int)globals.Difficulty;
            if (globals.Status.LargeMap)
            {
                --level;
            }

            if (globals.Status.SmallMap)
            {
                ++level;
            }

            int profile = Math.Clamp(level - 1, 0, 3);
            int extra = globals.FirstLaunchTurn >= 0 ? Math.Max(inputTurn - globals.FirstLaunchTurn, 0) : 0;
            int working = inputTurn - extra;
            if (globals.Status.Scenario && scenario.YearIncrement != 0 && scenario.StartYear != 0)
            {
                return scenario.StartYear + working * Math.Abs((int)scenario.YearIncrement) - 1 + extra;
            }

            int remaining = working == 0 ? 0 : working - 1;
            var periods = calendar.Profiles[profile];
            int index = 0;
            while (index < periods.Count && remaining > periods[index].TurnCount)
            {
                remaining -= periods[index].TurnCount;
                ++index;
            }
            int year;
            if (index < periods.Count)
            {
                year = periods[index].StartYear + periods[index].YearStep * remaining;
            }
            else
            {
                var last = periods[^1];
                year = last.StartYear + last.YearStep * last.TurnCount + remaining;
            }
            if (year == 0)
            {
                year = 1;
            }

            return year + extra;
        }
    }
}
public static class CivilizationKnowledge
{
    public const int FutureTechnology = 89;

    /// <summary>V03 §2: sentinel branches precede civilization access; no alive/human/prerequisite filtering.</summary>
    public static bool KnowsAdvance(ReadOnlySpan<Civilization> civilizations, int civilization, int advance)
    {
        if (advance == -2)
        {
            return false;
        }

        if (advance < 0)
        {
            return true;
        }

        if (advance == FutureTechnology || advance >= RuleLimits.Advances || civilization < 1)
        {
            return false;
        }

        if (civilization >= civilizations.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(civilization), "Advance query has no civilization backing");
        }

        return civilizations[civilization].Advances.Contains(advance);
    }
}
