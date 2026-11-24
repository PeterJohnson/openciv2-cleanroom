using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A08 §8's contradictory admission gate. On a stable map the two
/// coordinate queries cannot admit a unit, so this policy never transfers one.</summary>
public static class MilitaryAid
{
    public static void Run(GameState world, RuleTables rules, int owner)
    {
        MapKnowledge.RequireLocal(world);
        int wars = 0;
        for (int k = 1; k < EntityLimits.Civilizations; ++k)
        { if (k != owner && world.Civilizations[owner].Treaties[k].War) { ++wars; } }
        for (int ally = 1; ally < EntityLimits.Civilizations; ++ally)
        {
            if (ally == owner || !world.Civilizations[owner].Treaties[ally].Alliance
                || wars != 0 && world.Civilizations[ally].MilitaryStrength > world.Civilizations[owner].MilitaryStrength)
            { continue; }
            for (int enemy = 1; enemy < EntityLimits.Civilizations; ++enemy)
            {
                var relation = world.Civilizations[ally].Treaties[enemy];
                if (enemy == owner || enemy == ally || !relation.War || !relation.Fighting
                    || world.Civilizations[ally].MilitaryStrength > world.Civilizations[enemy].MilitaryStrength
                    || world.Globals.PowerRank[ally] > world.Globals.PowerRank[enemy])
                { continue; }
                foreach (ref readonly var unit in world.Entities.Units.Scanned)
                {
                    if (!unit.Token.Occupied || unit.Owner.Value != owner)
                    { continue; }
                    var rule = rules.Units[unit.Type.Value];
                    if (unchecked((sbyte)rule.Role) >= 2 || rule.Domain != RuleDomain.Land)
                    { continue; }
                    var position = unit.Position;
                    if (world.Map.Geometry.InBounds(position))
                    { continue; }
                    // CityOwner's own bounds gate always returns absent here. Preserve
                    // that reached query without inventing the unreachable gift path.
                    _ = world.Map.CityOwner(position);
                }
            }
        }
    }
}
