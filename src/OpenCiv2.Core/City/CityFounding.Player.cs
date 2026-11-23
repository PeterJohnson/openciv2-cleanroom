using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed partial class CityFounding
{
    private IEnumerable<FoundingDecision?> Player(Operation operation, UnitSlot unit, string call)
    {
        operation.Present(effects.StopBlinking);
        var position = Unit(unit).Position;
        int owner = Unit(unit).Owner.Value;
        if (process.Rules.Definitions.Units[Unit(unit).Type.Value].Role != RuleRole.Settle)
        {
            effects.Rejected(new(owner, unit, FoundingRejection.RequiresSettler));
            operation.Result = FoundingOutcome.Unavailable;
            yield break;
        }
        if (world.Map.TileAt(position).Ocean)
        {
            effects.Rejected(new(owner, unit, FoundingRejection.Ocean));
            operation.Result = FoundingOutcome.Unavailable;
            yield break;
        }
        if (CityQueries.At(world, position) is { } existing)
        {
            operation.City = existing;
            int limit = process.Rules.Definitions.Cosmic.AqueductSize;
            if (unchecked((sbyte)City(existing).Size) >= limit)
            {
                effects.Rejected(new(owner, unit, FoundingRejection.PopulationLimit, existing, limit));
                operation.Result = FoundingOutcome.Unavailable;
                yield break;
            }
            City(existing).Size = unchecked((sbyte)(City(existing).Size + 1));
            foreach (var pending in Delete(operation, unit, call + "/join"))
            {
                yield return pending;
            }

            operation.Present(() => effects.RefreshCity(existing));
            operation.Present(() => effects.Redraw(position, world.Multiplayer.LocalHuman));
            operation.Result = FoundingOutcome.Joined;
            yield break;
        }
        foreach (var direction in MapNeighborhoods.Directions)
        {
            var p = MapNeighborhoods.Offset(world.Map.Geometry, position, direction, world.Globals.Rules.FlatWorld);
            if (world.Map.Geometry.InBounds(p) && world.Map.CityOwner(p) is not null)
            {
                effects.Rejected(new(owner, unit, FoundingRejection.AdjacentCity, CityQueries.At(world, p)));
                operation.Result = FoundingOutcome.Unavailable;
                yield break;
            }
        }
        if (creation.Create(new((byte)owner), position) is not { } city)
        { operation.Result = FoundingOutcome.Unavailable; yield break; }
        operation.City = city;
        string initial = City(city).Name;
        yield return new FoundingDecision.Name(owner, new(city, City(city).Token), initial);
        var answer = operation.TakeReply()!.Value;
        if (answer.Choice != 0)
        {
            using var child = lifecycle.BeginRemoveCity(removal, city.Value, call + "/cancel", incidents);
            while (child.Result is null)
            {
                var pending = child.Resume(operation.TakeReply()?.Choice);
                if (pending is not null)
                {
                    yield return new FoundingDecision.Lifecycle(pending);
                }
                else if (child.Result is null)
                {
                    yield return null;
                }
            }
            ref var leader = ref process.Rules.Leaders[world.Civilizations[owner].Tribe];
            leader.CityNameIndex = unchecked((byte)(leader.CityNameIndex - 1));
            operation.Result = FoundingOutcome.Cancelled;
            yield break;
        }
        foreach (var pending in Found(operation, unit, city, answer.Name ?? initial, call, false))
        {
            yield return pending;
        }

        var notice = new FoundedCity(owner, new(city, City(city).Token), City(city).Name,
            Calendar.Year(world.Globals.Turn, world.Globals, world.Scenario, calendar));
        operation.Present(effects.BuildSound);
        effects.Founded(notice);
        if (world.Globals.GameOptions.Tutorial && world.Civilizations[owner].CityCount == 1)
        { effects.FirstProduction(owner, city); }
        world.Calculation.Processing = true;
        yield return new FoundingDecision.Window(new(new(city, City(city).Token), owner, City(city).Name));
        operation.TakeReply();
        world.Calculation.Processing = false;
        operation.Result = FoundingOutcome.Founded;
    }
}
