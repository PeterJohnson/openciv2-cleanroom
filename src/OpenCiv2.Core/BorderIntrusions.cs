using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public readonly record struct BorderIntrusionSummary(CitySlot? SelectedCity, int Cities, int Units, int Weight, int RepeatOffenses);

/// <summary>V04 §9 retained border warnings and mutating measurement for A08 relation review.
/// Each city's first qualifying occupied tile ends its tile scan, even when its stack counts nothing.</summary>
public sealed class BorderIntrusions(GameState world, RuleTables rules, StackOperations stacks)
{
    public int Count(int owner, int intruder)
    {
        MapKnowledge.RequireLocal(world);
        ref var summary = ref world.Diplomacy.Intrusions;
        summary = default;
        if (world.Civilizations[owner].Treaties[intruder].Alliance)
        { return 0; }
        for (int cityIndex = 0; cityIndex < world.Entities.Cities.End; ++cityIndex)
        {
            ref var city = ref world.Entities.Cities.Storage[cityIndex];
            if (!city.Token.Occupied || city.Owner.Value != owner)
            { continue; }
            var citySlot = new CitySlot((ushort)cityIndex);
            int localCount = 0;
            for (int position = 0; position < 20; ++position)
            {
                var point = MapNeighborhoods.Offset(world.Map.Geometry, city.Position, MapNeighborhoods.City[position], world.Globals.Rules.FlatWorld);
                if (!world.Map.Geometry.InBounds(point) || world.Map.TileAt(point).Ocean || world.Map.CityOwner(point) is not null
                    || world.Map.UnitOwner(point) != intruder)
                { continue; }
                var features = world.Map.TileAt(point).Features;
                for (int slot = stacks.FirstAt(point); slot >= 0; slot = stacks.Next(slot))
                {
                    ref var unit = ref world.Entities.Units.Storage[slot];
                    if (unchecked((sbyte)rules.Units[unit.Type.Value].Role) >= 5 || unit.Flags.BorderIntruder)
                    { continue; }
                    if (summary.SelectedCity != citySlot)
                    { summary = summary with { SelectedCity = citySlot, Cities = summary.Cities + 1 }; }
                    if (unit.Flags.BorderWarned)
                    { summary = summary with { RepeatOffenses = summary.RepeatOffenses + 1 }; }
                    else
                    { unit.Flags.BorderWarned = true; }
                    unit.Flags.BorderIntruder = true;
                    ++localCount;
                    int weight = 1 + (features.HasFlag(MapFeatures.Road) ? 1 : 0) + (features.HasFlag(MapFeatures.Railroad) ? 1 : 0)
                        + (features.HasFlag(MapFeatures.Mine) ? 1 : 0) + (features.HasFlag(MapFeatures.Irrigation) ? 1 : 0)
                        + (features.HasFlag(MapFeatures.Fortification) ? 2 : 0);
                    summary = summary with { Units = summary.Units + 1, Weight = summary.Weight + weight };
                }
                break;
            }
            if (localCount > 3)
            { summary = summary with { RepeatOffenses = summary.RepeatOffenses + 1 }; }
        }
        return summary.Weight;
    }
}
