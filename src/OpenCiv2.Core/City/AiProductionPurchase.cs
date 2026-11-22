using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed record AlmostWonderNotice(CitySlot City, int Owner, int Wonder);

/// <summary>C06 AI purchase phase using retained F04 human wonder ranges and actual V07 race queries.
/// A02 reselection remains an owning child; this phase spends once without another completion pass.</summary>
public sealed class AiProductionPurchase(GameState world, RuleTables rules,
    Action<CitySlot> reconsiderProduction, Action<AlmostWonderNotice> almostWonder)
{
    public void Run(CitySlot slot, int owner, int netShields)
    {
        MapKnowledge.RequireLocal(world);
        unchecked
        {
            ref var city = ref world.Entities.Cities.At(slot);
            if (city.Flags.DefenderLost || world.Map.UnitOwner(city.Position) is null)
            {
                reconsiderProduction(slot);
            }

            int improvement = city.Production < 0 ? -city.Production : 0;
            int cost = improvement == 0 ? rules.Units[city.Production].CostRows : rules.Improvements[improvement].CostRows;
            int required = cost * world.Calculation.ShieldRows;
            ref var civ = ref world.Civilizations[owner];
            int policy = world.Map.LandRegion(city.Position) is { } region ? civ.ContinentPolicy[region] : 0;
            int budget = 0;
            if (policy is 0 or 1 or 4 && netShields != 0 && city.Production >= 0 && (sbyte)rules.Units[city.Production].Role == policy)
            {
                budget = civ.Gold >> 6;
            }

            if (improvement > 38)
            {
                int group = (improvement - 39) / 7;
                int count = civ.WondersByGroup[group];
                budget = count == 0 || count < world.HumanWonders.Minimum[group] ? civ.Gold >> 6
                    : count > world.HumanWonders.Maximum[group] + 1 ? civ.Gold >> 8 : civ.Gold >> 7;
                if (civ.Gold >= 2500)
                {
                    budget *= 2;
                }
            }
            if (improvement is >= 35 and <= 38)
            {
                for (int human = 1; human < EntityLimits.Civilizations; ++human)
                {
                    if (world.Globals.HumanPlayers.Contains(human) && world.Civilizations[human].SpaceshipFlags.Exists)
                    {
                        budget = civ.Gold >> 7;
                        break;
                    }
                }
                if (SpaceshipQueries.RaceBehind(world, rules, owner))
                {
                    budget *= 2;
                }
            }
            if (city.Flags.Disorder && improvement != 0 && city.ShieldBox != 0)
            {
                budget = civ.Gold >> 3;
            }

            if ((world.Map.UnitOwner(city.Position) is null || city.Flags.DefenderLost) && city.Production >= 0 && city.ShieldBox != 0)
            {
                budget = civ.Gold >> 3;
            }

            if (improvement == BuildingIds.Palace && city.ShieldBox != 0)
            {
                budget = civ.Gold >> 3;
            }

            if (city.Production == UnitTypeIds.NuclearMissile && civ.UnitsActive[UnitTypeIds.NuclearMissile] == 0 && city.ShieldBox != 0)
            {
                budget = civ.Gold >> 4;
            }

            if (civ.Gold > 2000)
            {
                budget += civ.Gold >> 9;
            }

            // C06 uses budget as the lower bound. Excess stock can produce negative spending.
            budget = Arithmetic.ClampAB(0, budget, required - city.ShieldBox);
            city.ShieldBox = (short)(city.ShieldBox + (short)budget);
            // A refund division fault retains the stock write, before any treasury charge.
            int refund = Arithmetic.Divide32((byte)world.Globals.Difficulty * budget, 10);
            refund = Arithmetic.Divide32(refund, 8 - world.Globals.PowerRank[world.Globals.BestHumanRank]);
            if (world.Civilizations[world.Globals.WorstHumanRank].Gold < civ.Gold)
            {
                if (civ.Gold >= 1000)
                {
                    refund = Arithmetic.Divide32(refund, 2);
                }

                if (civ.Gold >= 2000)
                {
                    refund = 0;
                }
            }
            civ.Gold -= budget - refund;
            int current = -city.Production;
            if (current > 38 && rules.Improvements[current].CostRows * world.Calculation.ShieldRows
                <= city.ShieldBox + world.Calculation.Shields - world.Calculation.ShieldSupport
                && world.Globals.WonderCity[current - 39] == BuildingQueries.Unbuilt && !world.Multiplayer.Saved.AutoplayActive)
            {
                almostWonder(new(slot, owner, current - 39));
            }
        }
    }
}
