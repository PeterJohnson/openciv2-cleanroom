using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Content;
using GameCalendar = OpenCiv2.Core.Calendar;
using StateConversion = OpenCiv2.Formats.StateConversion;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static GlobalValues CalendarGlobals(int difficulty = 2, ushort flags = 0) => new() { Difficulty = (Difficulty)difficulty, Status = StateConversion.DecodeGameStatus(flags), FirstLaunchTurn = -1 };
    private static CalendarValues ClassicCalendar => GameplayContent.Builtin.Values.Calendar;
    private static void CalendarProfiles()
    {
        int[][] expected = [[1240, 1240, 1240, 1240], [1240, 1240, 1620, 1240], [1620, 1240, 1798, 1620], [1798, 1620, 1874, 1798], [1874, 1798, 1874, 1874], [1874, 1874, 1874, 1874]];
        for (int difficulty = 0; difficulty < 6; ++difficulty) for (int classification = 0; classification < 4; ++classification)
            Equal(expected[difficulty][classification], GameCalendar.Year(275, CalendarGlobals(difficulty, (ushort)(classification * 4)), new(), ClassicCalendar));
        Equal(1874, GameCalendar.Year(275, CalendarGlobals(255, 0xfff3), new(), ClassicCalendar));
    }
    private static void CalendarPeriods()
    {
        (int Difficulty, int Turn, int Year)[] points = [
            (0,0,-4000),(0,1,-4000),(0,2,-3980),(0,200,-20),(0,201,1),(0,202,20),(0,251,1000),(0,252,1010),(0,301,1500),(0,302,1505),
            (0,351,1750),(0,352,1752),(0,401,1850),(0,402,1851),(0,451,1900),(0,452,1901),(0,551,2000),(0,552,2001),
            (2,0,-4000),(2,1,-4000),(2,2,-3950),(2,61,-1000),(2,62,-975),(2,100,-25),(2,101,1),(2,102,10),(2,251,1500),(2,252,1505),
            (2,301,1750),(2,302,1752),(2,351,1850),(2,352,1851),(2,401,1900),(2,402,1901),
            (3,61,-1000),(3,62,-975),(3,101,1),(3,102,20),(3,151,1000),(3,152,1010),(3,201,1500),(3,202,1505),(3,251,1750),(3,252,1752),(3,301,1850),(3,302,1851),
            (4,61,-1000),(4,62,-975),(4,101,1),(4,102,20),(4,176,1500),(4,177,1510),(4,201,1750),(4,202,1752),(4,251,1850),(4,252,1851),(4,301,1900),(4,302,1901)];
        foreach (var point in points) Equal(point.Year, GameCalendar.Year(point.Turn, CalendarGlobals(point.Difficulty), new(), ClassicCalendar));
    }
    private static void CalendarSignedTurns()
    {
        foreach (var (turn, year) in new (int, int)[] { (-1, -4040), (-32768, -659380), (32767, 34216), (int.MaxValue, -2147482200), (int.MinValue, -2147482199) })
            Equal(year, GameCalendar.Year(turn, CalendarGlobals(0), new(), ClassicCalendar));
        Equal(-1642450, GameCalendar.Year(-32768, CalendarGlobals(5), new(), ClassicCalendar));
    }
    private static void CalendarLaunch()
    {
        var globals = CalendarGlobals(0); globals.FirstLaunchTurn = 21;
        foreach (var (turn, year) in new (int, int)[] { (0, -4000), (20, -3620), (21, -3600), (41, -3580) }) Equal(year, GameCalendar.Year(turn, globals, new(), ClassicCalendar));
        globals.FirstLaunchTurn = 201; Equal(3, GameCalendar.Year(203, globals, new(), ClassicCalendar)); globals = CalendarGlobals(4); globals.FirstLaunchTurn = 100; Equal(0, GameCalendar.Year(125, globals, new(), ClassicCalendar));
        globals.FirstLaunchTurn = 0; Equal(-3999, GameCalendar.Year(1, globals, new(), ClassicCalendar)); Equal(-3959, GameCalendar.Year(41, globals, new(), ClassicCalendar));
        globals = CalendarGlobals(0); globals.FirstLaunchTurn = 10; Equal(2147479818, GameCalendar.Year(int.MinValue, globals, new(), ClassicCalendar));
        globals.FirstLaunchTurn = 0; Equal(-2147482199, GameCalendar.Year(int.MinValue, globals, new(), ClassicCalendar)); globals.FirstLaunchTurn = short.MinValue; Equal(-3200, GameCalendar.Year(41, globals, new(), ClassicCalendar)); globals.FirstLaunchTurn = short.MaxValue; Equal(34217, GameCalendar.Year(32768, globals, new(), ClassicCalendar));
    }
    private static void CalendarScenario()
    {
        var globals = CalendarGlobals(2, 0x80); var scenario = new ScenarioParameters { YearIncrement = 5, StartYear = 100 };
        foreach (var (turn, year) in new (int, int)[] { (0, 99), (1, 104), (21, 204), (-2, 89) }) Equal(year, GameCalendar.Year(turn, globals, scenario, ClassicCalendar));
        scenario.YearIncrement = -5; Equal(204, GameCalendar.Year(21, globals, scenario, ClassicCalendar)); globals.Status = new GameStatus(); Equal(-3000, GameCalendar.Year(21, globals, scenario, ClassicCalendar));
        globals.Status = new GameStatus { Scenario = true }; scenario.StartYear = 0; Equal(-3000, GameCalendar.Year(21, globals, scenario, ClassicCalendar)); scenario.StartYear = 100; scenario.YearIncrement = 0; Equal(-3000, GameCalendar.Year(21, globals, scenario, ClassicCalendar));
        globals = CalendarGlobals(5, 0x8c); scenario.StartYear = 1; scenario.YearIncrement = -1; Equal(0, GameCalendar.Year(0, globals, scenario, ClassicCalendar)); globals.FirstLaunchTurn = 0; Equal(41, GameCalendar.Year(41, globals, scenario, ClassicCalendar));
        scenario.StartYear = -100; scenario.YearIncrement = -12; globals.FirstLaunchTurn = 21; Equal(171, GameCalendar.Year(41, globals, scenario, ClassicCalendar));
        globals = CalendarGlobals(0, 0x80); scenario.StartYear = 1; scenario.YearIncrement = short.MinValue; Equal(int.MinValue, GameCalendar.Year(65536, globals, scenario, ClassicCalendar)); Equal(0, GameCalendar.Year(131072, globals, scenario, ClassicCalendar));
        scenario.StartYear = short.MinValue; scenario.YearIncrement = 1; Equal(2147450879, GameCalendar.Year(int.MinValue, globals, scenario, ClassicCalendar));
    }
    private static CalendarValues CustomCalendar(CalendarPeriod[] periods)
    {
        var input = new BundleInput(); input.Custom(); input.Edit("calendar", root =>
        {
            foreach (string name in GameplayNames.CalendarProfiles) for (int i = 0; i < periods.Length; ++i)
            {
                var row = root["profiles"]![name]!["periods"]![i]!; row["startYear"] = periods[i].StartYear; row["turnCount"] = periods[i].TurnCount; row["yearStep"] = periods[i].YearStep;
            }
        }); return input.Load().Values.Calendar;
    }
    private static void CalendarCustom()
    {
        var calendar = CustomCalendar([new(-12, 2, 3), new(-6, 2, 3), new(0, 2, 5), new(10, 2, 7), new(24, 2, 11), new(46, 2, 13)]);
        int[] years = [-12, -12, -9, -6, -3, 1, 5, 10, 17, 24, 35, 46, 59, 72, 73, 74]; for (int i = 0; i < years.Length; ++i) Equal(years[i], GameCalendar.Year(i, CalendarGlobals(), new(), calendar));
        Equal(-3350, GameCalendar.Year(14, CalendarGlobals(), new(), ClassicCalendar)); var globals = CalendarGlobals(); globals.FirstLaunchTurn = 5; Equal(3, GameCalendar.Year(7, globals, new(), calendar));
        Equal(-12, GameCalendar.Year(0, CalendarGlobals(0), new(), calendar)); Equal(-12, GameCalendar.Year(0, CalendarGlobals(5), new(), calendar));
        calendar = CustomCalendar([new(int.MinValue, 1, int.MaxValue), new(-1, 1, 1), new(0, 1, 1), new(1, 1, 1), new(2, 1, 1), new(3, 1, 1)]);
        foreach (var (turn, year) in new (int, int)[] { (-2, 3), (-1, -2147483646), (1, int.MinValue), (2, -1), (3, 1), (int.MaxValue, 2147483644) }) Equal(year, GameCalendar.Year(turn, CalendarGlobals(), new(), calendar));
    }
    private static void CalendarObservation()
    {
        var globals = CalendarGlobals(3, 0xfffc); globals.Turn = 123; globals.Year = 456; globals.FirstLaunchTurn = 21;
        var scenario = new ScenarioParameters { Flags = StateConversion.DecodeScenarioOptions(0xffff), StartYear = -400, YearIncrement = -12, MaximumTurns = 90 }; var before = globals; var beforeScenario = scenario; var calendar = ClassicCalendar;
        Equal(-129, GameCalendar.Year(41, globals, scenario, calendar)); Equal(before, globals); Equal(beforeScenario, scenario); Equal(calendar, ClassicCalendar);
    }
    private static void KnowledgeSentinels()
    {
        var civs = new Civilization[8]; for (int i = 0; i < 100; ++i) civs[1].Advances = civs[1].Advances.With(i);
        foreach (int civ in new[] { -1, 0, 1, 8, int.MaxValue })
        {
            Equal(false, CivilizationKnowledge.KnowsAdvance(civs, civ, -2)); Equal(true, CivilizationKnowledge.KnowsAdvance(civs, civ, -1)); Equal(true, CivilizationKnowledge.KnowsAdvance(civs, civ, int.MinValue));
            Equal(false, CivilizationKnowledge.KnowsAdvance(civs, civ, 89)); Equal(false, CivilizationKnowledge.KnowsAdvance(civs, civ, 100));
        }
        var before = new OwnedList<Civilization>(civs);
        for (int advance = 0; advance < 100; ++advance) { Equal(advance != 89, CivilizationKnowledge.KnowsAdvance(civs, 1, advance)); Equal(false, CivilizationKnowledge.KnowsAdvance(civs, 0, advance)); Equal(false, CivilizationKnowledge.KnowsAdvance(civs, 2, advance)); }
        Throws<ArgumentOutOfRangeException>(() => CivilizationKnowledge.KnowsAdvance(civs, 8, 0)); Equal(before, new(civs));
        Equal(true, CivilizationKnowledge.KnowsAdvance([], int.MaxValue, -1)); Equal(false, CivilizationKnowledge.KnowsAdvance([], int.MaxValue, 89));
    }
}
