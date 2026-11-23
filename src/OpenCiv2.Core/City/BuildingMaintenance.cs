using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed record ImprovementLostNotice(CitySlot City, int Owner, int Building, string Name, int Credit);

/// <summary>C08 maintenance quote and city payment/forced-loss pass. Uses retained shield rows;
/// forced losses do not invoke manual-sale policy, recalculate the city or repair capital hints.</summary>
public sealed class BuildingMaintenance(GameState world, RuleTables rules, Action<ImprovementLostNotice> inHock)
{
    private bool Knows(int owner, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);

    public int Quote(int owner, int building)
    {
        int cost = rules.Improvements[building].Upkeep;
        if (building == BuildingIds.Barracks)
        {
            if (unchecked((byte)world.Globals.Difficulty) < (byte)Difficulty.Prince && cost != 0)
            {
                --cost;
            }

            if (Knows(owner, AdvanceIds.Gunpowder))
            {
                ++cost;
            }

            int advance = AdvanceIds.MobileWarfare;
            var visited = new HashSet<int>();
            while (advance >= 0 && !rules.Advances[advance].Enabled)
            {
                // The original walk does not terminate on a disabled cycle. Report a bounded stop
                // rather than returning a fabricated quote or searching the second prerequisite.
                if (!visited.Add(advance))
                {
                    throw new InvalidOperationException("Barracks upkeep reached a disabled prerequisite cycle");
                }

                advance = (int)rules.Advances[advance].Prerequisites[0];
            }
            if (Knows(owner, advance))
            {
                ++cost;
            }
        }
        if (cost == 1 && BuildingQueries.HasWonder(world, rules, owner, WonderIds.AdamSmith))
        {
            cost = 0;
        }

        if (cost != 0 && world.Civilizations[owner].Government == Government.Fundamentalism
            && building is BuildingIds.Temple or BuildingIds.Colosseum or BuildingIds.Cathedral)
        {
            cost = 0;
        }

        return cost;
    }

    public IReadOnlyList<Exception> Run(CitySlot slot)
    {
        MapKnowledge.RequireLocal(world);
        List<Exception> failures = [];
        int owner = unchecked((sbyte)world.Entities.Cities.At(slot).Owner.Value);
        if (world.Civilizations[owner].Government == Government.Anarchy)
        {
            return failures.AsReadOnly();
        }

        for (int building = BuildingIds.Palace; building <= BuildingIds.Capitalization; ++building)
        {
            if (!BuildingQueries.HasBuilding(world, slot, building))
            {
                continue;
            }

            world.Civilizations[owner].Gold = unchecked(world.Civilizations[owner].Gold - Quote(owner, building));
            if (world.Civilizations[owner].Gold >= 0)
            {
                continue;
            }

            world.Civilizations[owner].Gold = 0;
            BuildingQueries.SetBuilding(world, slot, building, false);
            if (!world.Multiplayer.Saved.AutoplayActive)
            {
                try
                {
                    inHock(new(slot, owner, building, rules.Improvements[building].Identity.Name,
                        unchecked(rules.Improvements[building].CostRows * world.Calculation.ShieldRows)));
                }
                catch (Exception error) { failures.Add(error); }
            }

            world.Civilizations[owner].Gold = unchecked(world.Civilizations[owner].Gold
                + rules.Improvements[building].CostRows * world.Calculation.ShieldRows);
        }
        return failures.AsReadOnly();
    }
}
