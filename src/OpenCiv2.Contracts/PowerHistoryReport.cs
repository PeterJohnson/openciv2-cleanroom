using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

/// <summary>P07 history values prepared once for a report. Sample dates use the game's calendar.</summary>
public sealed record PowerHistoryDate(int Turn, int Year);
public sealed record PowerHistorySeries(int Civilization, string Name, OwnedList<byte> Values);
public sealed record PowerHistoryReport(int Interval, int Maximum, OwnedList<PowerHistoryDate> Dates,
    OwnedList<PowerHistorySeries> Series)
{
    public bool Monthly { get; init; }
}
