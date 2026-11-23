using OpenCiv2.Contracts;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>P07 §14. Read retained A01 history, including extinct civilizations. No census or RNG.</summary>
public static class PowerHistory
{
    public static PowerHistoryReport Prepare(GameState world, ProcessContext process, CalendarValues calendar)
    {
        MapKnowledge.RequireLocal(world);
        int interval = world.Globals.Status.Scenario && world.Scenario.Flags.ObjectiveVictory ? 2 : 4;
        int count = Math.Clamp(world.Globals.Turn / interval, 0, GameState.PowerHistorySamples);
        int maximum = 50;
        var series = new List<PowerHistorySeries>();
        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        {
            var samples = new byte[count];
            for (int i = 0; i < count; ++i)
            {
                samples[i] = world.PowerHistory[i][c];
                maximum = Math.Max(maximum, samples[i]);
            }
            if (world.Globals.CivsAlive.Contains(c) || world.Globals.CivsAtStart.Contains(c))
            {
                var name = process.Rules.Leaders[world.Civilizations[c].Tribe].Plural;
                series.Add(new(c, name.Custom ? world.Names[c].Tribe : name.Text, new(samples)));
            }
        }
        return new(interval, maximum, new(Enumerable.Range(0, count).Select(i => new PowerHistoryDate(
            i * interval + 1, Calendar.Year(i * interval + 1, world.Globals, world.Scenario, calendar)))), new(series))
        { Monthly = world.Globals.Status.Scenario && world.Scenario.YearIncrement < 0 };
    }
}
