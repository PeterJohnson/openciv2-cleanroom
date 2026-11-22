using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>C05 box factors, food requirements and ordered support/military counts. Payment,
/// starvation, growth and unit retirement remain separate turn operations.</summary>
public sealed class CitySupport(GameState world, RuleTables rules)
{
    private CityCalculationWorkspace Work => world.Calculation;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner & 31);

    public int AiBoxFactor(bool food, int boxRule)
    {
        int difficulty = unchecked((byte)world.Globals.Difficulty);
        int rows = 13 - difficulty + (difficulty < (int)Difficulty.King ? 1 : 0) + (difficulty == 0 ? 1 : 0);
        if ((food ? world.Globals.Turn > 200 : world.Globals.Turn >= 200)
            && difficulty > (int)Difficulty.Warlord && Human(world.Globals.CivByRank[7]))
        {
            int p = 2;
            if (world.Globals.WonderCity[WonderIds.Manhattan] != BuildingQueries.Unbuilt && NonhumanKnows(AdvanceIds.Rocketry))
            {
                p = 1;
                if (!HumanKnows(AdvanceIds.Rocketry) || !HumanKnows(AdvanceIds.NuclearFission))
                {
                    p = 0;
                }
            }
            int a = p;
            if (world.Globals.WonderCity[WonderIds.Apollo] != BuildingQueries.Unbuilt && NonhumanKnows(AdvanceIds.SpaceFlight))
            {
                a = HumanKnows(AdvanceIds.SpaceFlight) ? p - 1 : p - 2;
            }

            rows -= Arithmetic.ClampAB(a, 0, 2);
        }
        if (boxRule != 10)
        {
            rows = unchecked(boxRule * rows) / 10;
            if ((rows & 1) != 0)
            {
                ++rows;
            }
        }
        return rows;
    }

    private bool HumanKnows(int advance) => world.Globals.AdvanceKnownBy[advance].Intersects(world.Globals.HumanPlayers);
    private bool NonhumanKnows(int advance) => world.Globals.AdvanceKnownBy[advance].Contains(7)
        || world.Globals.AdvanceKnownBy[advance].Except(world.Globals.HumanPlayers).Count != 0;

    public void FoodFactor(int owner)
    {
        Work.SettlerFood = unchecked((byte)world.Civilizations[owner].Government) <= (int)Government.Monarchy
            ? rules.Cosmic.SettlerFoodLowGovernment : rules.Cosmic.SettlerFoodHighGovernment;
        Work.FoodRows = rules.Cosmic.FoodBoxRows;
        if (!Human(owner))
        {
            Work.FoodRows = AiBoxFactor(true, Work.FoodRows);
        }
    }

    public int Charge(CitySlot city, UnitSlot unit, Government government)
    {
        Work.SupportedUnits = unchecked(Work.SupportedUnits + 1);
        bool charge = unchecked((byte)government) switch
        {
            (byte)Government.Anarchy or (byte)Government.Despotism => Work.SupportedUnits > world.Entities.Cities.At(city).Size,
            (byte)Government.Monarchy => Work.SupportedUnits > rules.Cosmic.MonarchyFreeSupport,
            (byte)Government.Communism => Work.SupportedUnits > rules.Cosmic.CommunismFreeSupport,
            (byte)Government.Fundamentalism => !rules.Units[world.Entities.Units.At(unit).Type.Value].Abilities.FundamentalismFreeSupport
                && Work.SupportedUnits > rules.Cosmic.FundamentalismFreeSupport,
            _ => true
        };
        if (!charge)
        {
            return 0;
        }

        return Work.ShieldSupport = unchecked(Work.ShieldSupport + 1);
    }

    public void Calculate(CitySlot slot)
    {
        MapKnowledge.RequireLocal(world);
        ref var city = ref world.Entities.Cities.At(slot);
        int owner = unchecked((sbyte)city.Owner.Value);
        var government = world.Civilizations[owner].Government;
        FoodFactor(owner);
        Work.ShieldRows = Human(owner) ? rules.Cosmic.ShieldBoxRows : AiBoxFactor(false, rules.Cosmic.ShieldBoxRows);
        Work.ShieldSupport = Work.SupportedUnits = Work.SupportedSettlers = Work.AggressiveUnits = Work.HomeAttackUnits = 0;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || unchecked((sbyte)unit.Owner.Value) != owner || (unit.Home?.Value ?? 255) != slot.Value)
            {
                continue;
            }

            unit.Flags.Aggressive = unit.Flags.CostsShield = false;
            var rule = rules.Units[unit.Type.Value];
            int role = unchecked((sbyte)rule.Role);
            if (role >= 6)
            {
                continue;
            }

            unit.Flags.CostsShield = Charge(slot, new((ushort)i), government) != 0;
            int before = Work.AggressiveUnits;
            if (rule.Attack != 0)
            {
                if (rule.Domain == RuleDomain.Air)
                {
                    if (role != (int)RuleRole.AirSuperiority)
                    {
                        ++Work.AggressiveUnits;
                    }

                    if (unit.Position.X == city.Position.X || unit.Position.Y == city.Position.Y)
                    {
                        ++Work.HomeAttackUnits;
                    }
                }
                else if (unit.Position == city.Position)
                {
                    ++Work.HomeAttackUnits;
                }
                else if (world.Map.CityOwner(unit.Position) is null)
                {
                    ++Work.AggressiveUnits;
                    if ((world.Map.TileAt(unit.Position).Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification)
                    {
                        foreach (ref readonly var candidate in world.Entities.Cities.Scanned)
                        {
                            if (candidate.Token.Occupied && candidate.Owner.Value == owner
                                && world.Map.Geometry.StepDistance(candidate.Position, unit.Position, world.Globals.Rules.FlatWorld) < 4)
                            { --Work.AggressiveUnits; break; }
                        }
                    }
                }
            }
            unit.Flags.Aggressive = Work.AggressiveUnits != before;
            if (role == (int)RuleRole.Settle)
            {
                ++Work.SupportedSettlers;
            }
        }
    }

    public int? GrowthFacilityNeeded(CitySlot slot)
    {
        if (rules.Cosmic.AqueductSize <= world.Entities.Cities.At(slot).Size && !BuildingQueries.HasBuilding(world, slot, BuildingIds.Aqueduct))
        {
            return BuildingIds.Aqueduct;
        }

        if (rules.Cosmic.SewerSize <= world.Entities.Cities.At(slot).Size && !BuildingQueries.HasBuilding(world, slot, BuildingIds.SewerSystem))
        {
            return BuildingIds.SewerSystem;
        }

        return null;
    }
}
