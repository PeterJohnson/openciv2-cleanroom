using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed record LeonardoUpgradeNotice(UnitSlot Unit, int Owner, UnitType OldType, UnitType NewType,
    string OldName, string NewName, string WonderName);

/// <summary>C08 local Eiffel acquisition and Leonardo's ascending upgrade pass. These children do
/// not acquire a wonder, rebuild counters or implement research/production completion themselves.</summary>
public sealed class WonderEffects(GameState world, RuleTables rules, Action<LeonardoUpgradeNotice> upgraded, Action<MapPoint> redraw)
{
    public void ApplyEiffel(int owner)
    {
        MapKnowledge.RequireLocal(world);
        if (world.Civilizations[owner].Flags.EiffelApplied)
        {
            return;
        }

        world.Civilizations[owner].Reputation /= 2;
        world.Civilizations[owner].Flags.EiffelApplied = true;
        for (int other = 1; other < EntityLimits.Civilizations; ++other)
        {
            if (other == owner)
            {
                continue;
            }

            DiplomaticAttitudes.Adjust(world, other, owner, -25);
            world.Civilizations[other].Treaties[owner].Vendetta = false;
        }
    }

    public void UpgradeForLeonardo(int owner)
    {
        MapKnowledge.RequireLocal(world);
        if (!BuildingQueries.HasWonder(world, rules, owner, WonderIds.Leonardo))
        {
            return;
        }

        var notified = new bool[RuleLimits.UnitTypes];
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || unchecked((sbyte)unit.Owner.Value) != owner)
            {
                continue;
            }

            var rule = rules.Units[unit.Type.Value];
            int advance = (int)rule.ObsoleteBy;
            if (rule.Role == RuleRole.Defend && unchecked((sbyte)rule.Defense) < unchecked((sbyte)rules.Units[UnitTypeIds.Musketeers].Defense)
                && CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, AdvanceIds.Gunpowder))
            {
                advance = AdvanceIds.Gunpowder;
            }

            if (advance < 0 || !CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance))
            {
                continue;
            }

            int replacement = -1;
            for (int candidate = 0; candidate < RuleLimits.UnitTypes; ++candidate)
            {
                if ((int)rules.Units[candidate].Prerequisite == advance && rules.Units[candidate].Role == rules.Units[unit.Type.Value].Role
                    && unchecked((sbyte)rules.Units[candidate].Capacity) >= unchecked((sbyte)rules.Units[unit.Type.Value].Capacity))
                {
                    replacement = candidate;
                }
            }

            if (replacement < 0)
            {
                continue;
            }

            int old = unit.Type.Value;
            if (!notified[old] && world.Globals.HumanPlayers.Contains(owner & 31))
            {
                notified[old] = true;
                upgraded(new(new((ushort)i), owner, new((byte)old), new((byte)replacement), rules.Units[old].Identity.Name,
                    rules.Units[replacement].Identity.Name, rules.Improvements[39 + WonderIds.Leonardo].Identity.Name));
            }
            unit.Type = new((byte)replacement);
            unit.Flags.Veteran = false;
            redraw(unit.Position);
        }
    }
}
