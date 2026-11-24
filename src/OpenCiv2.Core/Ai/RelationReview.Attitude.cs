using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;

namespace OpenCiv2.Core.Ai;

public sealed partial class RelationReview
{
    private int Strength(int c) => world.Civilizations[c].MilitaryStrength;
    private int Advances(int c) => world.Civilizations[c].NumAdvances;
    private int Rank(int c) => world.Globals.PowerRank[c];
    private bool Nuclear(int c) => world.Civilizations[c].UnitsActive[UnitTypeIds.NuclearMissile] != 0;
    private bool Wonder(int c, int wonder) => BuildingQueries.HasWonder(world, rules.Definitions, c, wonder);

    // A08 §7: null propagates an unknown incoming value. Every independent query
    // is still evaluated; the final no-Contact overwrite can establish zero.
    private int? AdjustedDelta(int c, int a, int? delta)
    {
        unchecked
        {
            int ahead = 0, hostile = 0, pressure = 0;
            for (int k = 1; k < EntityLimits.Civilizations; ++k)
            {
                if (Advances(k) > Advances(a))
                { ++ahead; }
                if (DiplomaticAttitudes.Hostile(world, c, k))
                { ++hostile; }
                if (world.Civilizations[k].Treaties[a].War)
                { ++pressure; if (Strength(k) > Strength(a)) { ++pressure; } }
            }
            var relation = world.Civilizations[c].Treaties[a];
            int distrust = world.Civilizations[a].Reputation - world.Civilizations[c].ReputationCredit[a];
            var leader = rules.Leaders[world.Civilizations[c].Tribe];
            if (relation.Alliance)
            {
                if (hostile == 0)
                {
                    if (world.Civilizations[c].Gold < world.Civilizations[a].Gold)
                    { ++delta; }
                    if (Advances(c) < Advances(a) && world.Globals.Difficulty != Difficulty.Chieftain)
                    { ++delta; }
                    if (Advances(a) < Advances(c))
                    { --delta; }
                    if (Strength(c) < Strength(a))
                    { ++delta; }
                    if (Strength(a) < Strength(c))
                    { --delta; }
                }
                else if (leader.Attack < 1 || hostile > 1)
                { delta -= Math.Max(1, hostile - leader.Attack - 1); }
                if (Advances(c) + 8 < Advances(a))
                { ++delta; }
            }
            if (relation.Peace)
            { delta -= 2; }
            if (!relation.War)
            { --delta; }
            if (Rank(a) < 7 && world.Globals.Turn > 200)
            {
                int adjustment = 7 - Rank(a);
                if (SpaceshipQueries.RaceActive(world))
                { adjustment = 1; }
                if (world.Globals.Turn < 400)
                { adjustment = (adjustment + 1) >> 1; }
                delta -= adjustment;
            }
            if (Rank(a) == 7 && world.Civilizations[a].CityCount > 3 && world.Globals.Turn > 200
                && world.Globals.Difficulty != Difficulty.Chieftain)
            {
                int adjustment = (int)world.Globals.Difficulty / 3 + 1;
                if (distrust <= pressure && !relation.Vendetta)
                { adjustment >>= 1; }
                delta += adjustment;
            }
            if (Nuclear(a))
            { --delta; }
            if (!Nuclear(c) && delta > 0)
            { --delta; }
            int personality = 3 * leader.Attack + 2 * leader.Expand;
            if (personality < -1)
            { personality = -2; }
            int difference = Rank(a) - Rank(c);
            if (difference < 0 || !relation.Alliance)
            { difference /= 2; }
            delta += personality;
            delta += difference;
            if (!world.Globals.Status.AiCoordination || distrust <= pressure && !relation.Vendetta)
            {
                if (4 * Strength(c) < Strength(a))
                { --delta; }
                if (2 * Strength(c) < Strength(a))
                { --delta; }
                if (3 * Strength(c) / 2 < Strength(a))
                { --delta; }
            }
            if (!relation.Alliance)
            {
                if (Strength(a) < Strength(c))
                { ++delta; }
                if (2 * Strength(a) < Strength(c))
                { ++delta; }
            }
            if (Wonder(a, WonderIds.GreatWall) || Wonder(a, WonderIds.UnitedNations))
            { if (delta < 1) { --delta; } else { delta >>= 1; } }
            if (ahead == 0)
            { ++delta; }
            if (Advances(c) < Advances(a))
            { delta += 1 - leader.Civilize; }
            if (relation.War && delta < 1)
            { delta = 0; }
            if (Wonder(c, WonderIds.EiffelTower))
            { ++delta; }
            if (Wonder(a, WonderIds.EiffelTower))
            {
                if (delta > 0)
                { delta >>= 1; }
                --delta;
                if (delta >= 0)
                { --delta; }
            }
            if (!world.Civilizations[c].Treaties[a].Contact)
            { delta = 0; }
            return delta;
        }
    }
}
