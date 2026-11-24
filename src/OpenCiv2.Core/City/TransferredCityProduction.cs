using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C09/V05 production repair after transfer. The first available defender ends the
/// scan even if its signed score is nonpositive. This does not update production counters.</summary>
public static class TransferredCityProduction
{
    public static void Repair(GameState world, RuleTables rules, CitySlot slot, int owner,
        WonderBuildRestrictions? scenario = null)
    {
        MapKnowledge.RequireLocal(world);
        ref var city = ref world.Entities.Cities.At(slot);
        bool eligible = city.Production >= 0
            ? UnitAvailability.CanBuild(world, rules, owner, slot, new((byte)city.Production))
            : BuildingAvailability.Improvement(world, rules, owner, slot, -city.Production, scenario);
        if (eligible)
        { return; }
        city.Production = UnitTypeIds.Settlers;
        // C09's declared nominal extra probe has role 0x19, not Defend; it is not a 63rd rule.
        for (int type = RuleLimits.UnitTypes - 1; type >= 0; --type)
        {
            var rule = rules.Units[type];
            if (rule.Role != RuleRole.Defend || !UnitAvailability.CanBuild(world, rules, owner, slot, new((byte)type)))
            { continue; }
            int score = unchecked((sbyte)rule.Defense * 8) / unchecked((sbyte)rule.CostRows);
            if (rule.Abilities.AntiHorseDefense)
            { ++score; }
            if (score > 0)
            { city.Production = (sbyte)type; }
            break;
        }
    }
}
