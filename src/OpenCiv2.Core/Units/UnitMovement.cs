using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U02 movement allowance, hit points and exhaustion. These are engine operations on backed
/// records and rules; they do not validate a player's command or execute a step.</summary>
public static class UnitMovement
{
    public static int MaximumHitPoints(RuleTables rules, in Unit unit) =>
        unchecked((sbyte)rules.Units[unit.Type.Value].HitPoints);

    /// <summary>U02 standalone remaining-HP query clears stored damage when normal combat is disabled.</summary>
    public static int RemainingHitPoints(GameState world, RuleTables rules, ref Unit unit)
    {
        if (!world.Globals.Rules.NormalCombat)
        {
            unit.Damage = 0;
        }

        return Math.Max(0, MaximumHitPoints(rules, unit) - unit.Damage);
    }

    /// <summary>U02 signed movement with sea technology/wonder bonuses and domain-specific damage rounding.
    /// Stable-state queries do not mutate damage or consume random choices.</summary>
    public static int Allowance(GameState world, RuleTables rules, in Unit unit)
    {
        var rule = rules.Units[unit.Type.Value];
        int moves = unchecked((sbyte)rule.Movement);
        if (moves == 0)
        {
            return 0;
        }

        int road = rules.Cosmic.RoadMovementMultiplier;
        if (rule.Domain == RuleDomain.Sea)
        {
            int owner = unit.Owner.Value;
            if (CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, AdvanceIds.NuclearPower))
            {
                moves += road;
            }

            if (BuildingQueries.HasWonder(world, rules, owner, WonderIds.Magellan))
            {
                moves += 2 * road;
            }

            if (BuildingQueries.HasWonder(world, rules, owner, WonderIds.Lighthouse) && !rule.Abilities.CoastBound)
            {
                moves += road;
            }
        }

        if (unit.Damage == 0 || !world.Globals.Rules.NormalCombat || rule.Domain == RuleDomain.Air)
        {
            return moves;
        }

        int hp = MaximumHitPoints(rules, unit);
        int remaining = Math.Max(0, hp - unit.Damage);
        int scaled = unchecked(remaining * moves) / (hp < 2 ? 1 : hp);
        int remainder = scaled % road;
        if (remainder != 0)
        {
            scaled += road - remainder;
        }

        return Math.Max(rule.Domain == RuleDomain.Sea ? 2 * road : road, scaled);
    }

    public static int Left(GameState world, RuleTables rules, in Unit unit) =>
        Math.Max(0, unchecked(Allowance(world, rules, unit) - unit.MovesUsed));

    /// <summary>Replaces spent movement with the allowance's low byte, even if that reduces the stored value.</summary>
    public static void Exhaust(GameState world, RuleTables rules, ref Unit unit) =>
        unit.MovesUsed = unchecked((byte)Allowance(world, rules, unit));
}
