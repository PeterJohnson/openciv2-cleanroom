using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using GameCalendar = OpenCiv2.Core.Calendar;

namespace OpenCiv2.Session;

public static class SetupCalendar
{
    /// <summary>F05 §4: clear the first-launch marker and compute F04 year magnitudes for the
    /// selected calendar at turns 0/21/41. Retains scenario inputs, stored year and random contexts.</summary>
    public static AccelerationYears PrepareAccelerationYears(SessionRuntime runtime, CalendarValues calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        var world = runtime.World;
        world.Globals.FirstLaunchTurn = -1;
        uint Magnitude(int turn)
        {
            int year = GameCalendar.Year(turn, world.Globals, world.Scenario, calendar);
            return year < 0 ? unchecked(0U - (uint)year) : (uint)year;
        }
        var years = new AccelerationYears(Magnitude(0), Magnitude(21), Magnitude(41));
        if (runtime.Active)
        {
            runtime.Publish();
        }

        return years;
    }
}
