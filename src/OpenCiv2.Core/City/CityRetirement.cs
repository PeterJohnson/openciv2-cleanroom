using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

/// <summary>Semantic presentation for C05 retirement; the adapter refreshes any displayed city.</summary>
public interface ICityRetirementEffects
{
    void SupportShortage(CitySlot city, UnitTarget selected, UnitType type);
    void UnitRemoved(CitySlot city, UnitTarget selected, bool shortage);
}

/// <summary>C05 unsupported-unit and affordable disorder retirement. The caller owns later recalculation.</summary>
public sealed class CityRetirement(GameState world, RuleTables rules, UnitDeletion deletion,
    UnitLoss loss, ICityRetirementEffects effects)
{
    private CityCalculationWorkspace Work => world.Calculation;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner & 31);

    public void Run(CitySlot slot)
    {
        MapKnowledge.RequireLocal(world);
        ref var city = ref world.Entities.Cities.At(slot);
        int owner = unchecked((sbyte)city.Owner.Value);
        var government = (Government)unchecked((byte)world.Civilizations[owner].Government);
        var support = new CitySupport(world, rules);
        Work.SupportedUnits = Work.ShieldSupport = 0;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || unchecked((sbyte)unit.Owner.Value) != owner
                || (unit.Home?.Value ?? 255) != slot.Value || unchecked((sbyte)rules.Units[unit.Type.Value].Role) >= 6)
            {
                continue;
            }
            support.Charge(slot, new((ushort)i), government);
            if (!(Work.Shields < Work.ShieldSupport || city.Flags.Disorder && (world.Globals.Turn + i) % 8 == 0
                && !Human(owner) && government > Government.Fundamentalism))
            {
                continue;
            }
            var (selected, distance) = Select(slot, Work.ShieldSupport <= Work.Shields);
            bool shortage = Work.Shields < Work.ShieldSupport;
            if (shortage)
            {
                if (distance < 0)
                {
                    continue;
                }
                UnitTarget target = new(new((ushort)selected), world.Entities.Units.Storage[selected].Token);
                if (Human(owner))
                {
                    effects.SupportShortage(slot, target, world.Entities.Units.Storage[selected].Type);
                }
                loss.DestroyAndRedraw(selected, true);
                effects.UnitRemoved(slot, target, true);
            }
            else
            {
                if (world.Globals.Status.AiCoordination
                    && world.Civilizations[world.Globals.BestHumanRank].NumAdvances > world.Civilizations[owner].NumAdvances)
                {
                    world.Civilizations[owner].GovernmentScore[(int)Government.Democracy] = -999;
                    world.Civilizations[owner].GovernmentScore[(int)Government.Republic] = -999;
                    continue;
                }
                if (distance <= 0 || rules.Units[world.Entities.Units.Storage[selected].Type.Value].Domain != RuleDomain.Land)
                {
                    continue;
                }
                if (CityQueries.At(world, world.Entities.Units.Storage[selected].Position) is { } destination)
                {
                    int cost = unchecked((sbyte)rules.Units[world.Entities.Units.Storage[selected].Type.Value].CostRows);
                    ref var receiving = ref world.Entities.Cities.At(destination);
                    receiving.ShieldBox = unchecked((short)(receiving.ShieldBox + (short)(unchecked(cost * Work.ShieldRows) / 2)));
                }
                UnitTarget target = new(new((ushort)selected), world.Entities.Units.Storage[selected].Token);
                deletion.Erase(selected, true);
                city.Flags.Disorder = city.Flags.SecondDisorderTurn = city.Flags.LongDisorder = false;
                effects.UnitRemoved(slot, target, false);
            }
            Work.SupportedUnits = Work.ShieldSupport = 0;
            i = -1;
        }
    }

    private (int Slot, int Distance) Select(CitySlot slot, bool withinSupport)
    {
        int best = -1, distance = -1;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied || (unit.Home?.Value ?? 255) != slot.Value)
            {
                continue;
            }
            int role = unchecked((sbyte)rules.Units[unit.Type.Value].Role);
            if (role >= 6 || withinSupport && (role > 4 || world.Map.CityOwner(unit.Position) is not null
                || ProtectedFortress(slot, unit.Position)))
            {
                continue;
            }
            int candidate = world.Map.Geometry.WeightedDistance(unit.Position, world.Entities.Cities.At(slot).Position,
                world.Globals.Rules.FlatWorld);
            if (candidate > distance)
            {
                best = i;
                distance = candidate;
            }
        }
        return (best, distance);
    }

    private bool ProtectedFortress(CitySlot slot, MapPoint point)
    {
        if ((world.Map.TileAt(point).Features & (MapFeatures.CitySite | MapFeatures.Fortification)) != MapFeatures.Fortification)
        {
            return false;
        }
        foreach (ref readonly var candidate in world.Entities.Cities.Scanned)
        {
            if (candidate.Token.Occupied && candidate.Owner == world.Entities.Cities.At(slot).Owner
                && world.Map.Geometry.StepDistance(candidate.Position, point, world.Globals.Rules.FlatWorld) < 4)
            {
                return true;
            }
        }
        return false;
    }
}
