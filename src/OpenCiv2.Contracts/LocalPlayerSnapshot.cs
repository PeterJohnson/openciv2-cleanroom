namespace OpenCiv2.Contracts;

/// <summary>An immutable, observer-scoped capture at one published local session revision.</summary>
public sealed record LocalPlayerSnapshot(
    LocalPlayObservation Play, LiveRecords<OwnUnit> Units, LiveRecords<OwnCity> Cities, MapObservation Map)
{
    public static LocalPlayerSnapshot Capture(IPlayerClient client)
    {
        var play = client.ObserveLocalPlay() ?? throw new ArgumentException("A local player binding is required");
        var units = client.OwnUnits();
        var cities = client.OwnCities();
        var map = client.ObserveMap() ?? throw new InvalidOperationException("Map is unavailable");
        var stamp = play.Observation;
        if (units.Observation != stamp || cities.Observation != stamp || map.Revision != stamp.Revision
            || map.Identity.Runtime != stamp.Runtime || map.Identity.Session != stamp.Session
            || map.Identity.Observer != stamp.Observer)
        {
            throw new InvalidOperationException("Observation changed during capture");
        }

        return new(play, units, cities, map);
    }
}
