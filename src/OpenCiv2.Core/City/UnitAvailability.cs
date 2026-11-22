using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

/// <summary>C06 ordered unit eligibility. Alternatives use their prerequisites, not recursive availability.</summary>
public static class UnitAvailability
{
    public static bool CanBuild(GameState world, RuleTables rules, int civilization, CitySlot? city, UnitType type)
    {
        var rule = rules.Units[type.Value];
        bool Knows(int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, civilization, advance);
        int prerequisite = (int)rule.Prerequisite;
        if (prerequisite <= (int)AdvanceReference.Never || prerequisite != (int)AdvanceReference.None && !Knows(prerequisite))
        {
            return false;
        }
        if ((int)rule.ObsoleteBy >= 0 && Knows((int)rule.ObsoleteBy))
        {
            return false;
        }
        if (rule.Role == RuleRole.Defend && unchecked((sbyte)rule.Defense) < unchecked((sbyte)rules.Units[UnitTypeIds.Musketeers].Defense)
            && Knows(AdvanceIds.Gunpowder))
        {
            return false;
        }
        int multiplier = rules.Cosmic.RoadMovementMultiplier;
        if (rule.Role == RuleRole.Attack && unchecked((sbyte)rule.Movement) == 2 * multiplier
            && unchecked((sbyte)rule.Attack) <= unchecked((sbyte)rules.Units[UnitTypeIds.Knights].Attack)
            && unchecked((sbyte)rule.Defense) < unchecked((sbyte)rules.Units[UnitTypeIds.Knights].Defense)
            && Knows((int)rules.Units[UnitTypeIds.Knights].Prerequisite))
        {
            return false;
        }
        if (unchecked((sbyte)rule.Attack) >= 99
            && (world.Globals.WonderCity[WonderIds.Manhattan] == -1 || !Knows(AdvanceIds.NuclearFission)))
        {
            return false;
        }
        if (city is { } supplied && rule.Domain == RuleDomain.Sea)
        {
            var flags = world.Entities.Cities.At(supplied).Flags;
            if (!flags.OceanAccess || !flags.Coastal)
            {
                return false;
            }
        }
        if (!world.Globals.HumanPlayers.Contains(civilization) && rule.Domain == RuleDomain.Land
            && unchecked((sbyte)rule.Movement) > multiplier)
        {
            foreach (var alternative in rules.Units)
            {
                if (alternative.Role == rule.Role && alternative.Domain == rule.Domain
                    && unchecked((sbyte)alternative.Movement) > multiplier && Knows((int)alternative.Prerequisite)
                    && unchecked((sbyte)alternative.Attack) >= unchecked((sbyte)rule.Attack)
                    && unchecked((sbyte)alternative.Defense) >= unchecked((sbyte)rule.Defense)
                    && (unchecked((sbyte)alternative.Attack) > unchecked((sbyte)rule.Attack)
                        || unchecked((sbyte)alternative.Defense) > unchecked((sbyte)rule.Defense)))
                {
                    return false;
                }
            }
        }
        return type.Value != UnitTypeIds.Fanatics || world.Civilizations[civilization].Government == Government.Fundamentalism;
    }
}
