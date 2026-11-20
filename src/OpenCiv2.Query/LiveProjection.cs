using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Query;

/// <summary>Engine projections, not authority issuers. Session validates a private client lease
/// before supplying the observer. Own entities only; enemy/remembered views require W03.</summary>
public static class LiveProjection
{
    public static LiveRecords<OwnUnit> OwnUnits(EntityPools pools, LiveObservation observation)
    {
        Admit(observation);
        var result = new List<OwnUnit>();
        var units = pools.Units.Scanned;
        for (int i = 0; i < units.Length; ++i)
        {
            ref readonly var unit = ref units[i];
            if (unit.Token.Occupied && unit.Owner == observation.Observer)
            {
                result.Add(
                    new(
                        new((ushort)i),
                        unit.Token,
                        unit.X,
                        unit.Y,
                        unit.Type,
                        unit.MovesUsed,
                        unit.Damage,
                        unit.Orders,
                        unit.Home));
            }
        }
        return new(observation, new(result));
    }

    public static LiveRecords<OwnCity> OwnCities(EntityPools pools, LiveObservation observation)
    {
        Admit(observation);
        var result = new List<OwnCity>();
        var cities = pools.Cities.Scanned;
        for (int i = 0; i < cities.Length; ++i)
        {
            ref readonly var city = ref cities[i];
            if (city.Token.Occupied && city.Owner == observation.Observer)
            {
                result.Add(
                    new(
                        new((ushort)i),
                        city.Token,
                        city.X,
                        city.Y,
                        new OwnedList<byte>(city.Name.TakeWhile(b => b != 0)),
                        city.Size,
                        city.Production,
                        city.FoodBox,
                        city.ShieldBox));
            }
        }
        return new(observation, new(result));
    }

    private static void Admit(LiveObservation observation)
    {
        if (observation.Runtime.Value == 0 || observation.Session.Value == 0
            || observation.Observer.Value >= EntityLimits.Civilizations
            || observation.Provenance != LiveProvenance.OwnedCurrent)
        {
            throw new ArgumentException("Own-entity projection requires an admitted observer", nameof(observation));
        }
    }
}
