using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A03 §§6–7 ordered settler and role-21 choices. Assigning an order does
/// not execute work; A04 completion and U04/W04 remain actual subsequent phases.</summary>
public sealed partial class TerrainWorkerTactics(GameState world, RuleState installed, StackOperations stacks, SharedGoals goals)
{
    private RuleTables Rules => installed.Definitions;
    private ref Unit Actor(UnitTacticsContext c) => ref world.Entities.Units.At(c.Unit);
    private ref Civilization Civ(UnitTacticsContext c) => ref world.Civilizations[c.Owner];
    private ref State.City Home(UnitTacticsContext c) => ref world.Entities.Cities.At(c.HomeCity
        ?? throw new InvalidOperationException("A03 reached a home-city read without a captured city"));
    private bool Knows(UnitTacticsContext c, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c.Owner, advance);
    private bool RoadAllowed(UnitTacticsContext c, MapPoint p) => !world.Map.TileAt(p).River || Knows(c, AdvanceIds.BridgeBuilding);
    private bool Resource(MapPoint p) => world.Map.SpecialResource(p) != 0;
    private BaseTerrainWork Work(Terrain terrain) => Rules.Terrain[(int)terrain].Work
        ?? throw new InvalidOperationException("A03 requires terrain work definitions");
    private bool SizeAllows(UnitTacticsContext c, MapPoint p, Terrain terrain) =>
        3 * (unchecked((sbyte)Work(terrain).Irrigation.WorkTurns) / 5) - 3 <= Home(c).Size || Resource(p);
    private MapPoint Offset(MapPoint p, MapPoint delta) => MapNeighborhoods.Offset(world.Map.Geometry, p, delta, world.Globals.Rules.FlatWorld);
    private int Distance(MapPoint a, MapPoint b) => world.Map.Geometry.WeightedDistance(a, b, world.Globals.Rules.FlatWorld);
    private SpecialUnitChoice Order(UnitTacticsContext c, UnitOrder order, byte? task = null)
    { Actor(c).Orders = order; if (task is { } value) { Actor(c).AiTask = value; } return new(SpecialUnitRoute.Finish); }
    private SpecialUnitChoice Goto(UnitTacticsContext c, MapPoint point, byte task)
    { UnitOrders.SetGoto(ref Actor(c), task, point); return new(SpecialUnitRoute.Finish); }

    public SpecialUnitChoice Choose(UnitTacticsContext c)
    {
        MapKnowledge.RequireLocal(world);
        if (c.SettlerKind && Actor(c).Orders == UnitOrder.GoTo && world.Map.CityOwner(Actor(c).Destination) is not null)
        { c.GotoCity = true; }
        if (c.SettlerType && !c.ForceAttack && !c.GotoCity && c.Terrain != Terrain.Ocean && world.Map.CityOwner(c.Position) is null)
        {
            var result = Settler(c);
            if (result.Route != SpecialUnitRoute.Continue)
            {
                return result;
            }
        }
        if (c.Role == 21 && Actor(c).Orders != UnitOrder.GoTo && !c.ForceAttack && !c.GotoCity && c.HomeDistance != 0 && c.Terrain != Terrain.Ocean)
        { return Colonist(c); }
        return new(SpecialUnitRoute.Continue);
    }

    private SpecialUnitChoice Settler(UnitTacticsContext c)
    {
        int count = stacks.Count(c.Unit.Value);
        bool company = count > 1;
        if (!company)
        {
            for (int d = 0; d < 8; ++d)
            {
                var p = Offset(c.Position, MapNeighborhoods.Directions[d]);
                if (world.Map.Geometry.InBounds(p) && world.Map.UnitOwner(p) is not null)
                { company = true; break; }
            }
        }
        if (!company)
        {
            goals.AddCurrent(c.Owner, c.Position, (int)RuleRole.Defend, 2);
        }

        goals.ClearStandingNear(c.Owner, 21, c.Position, 0);
        if (world.Map.TileAt(c.Position).Features.HasFlag(MapFeatures.Pollution))
        {
            return Order(c, UnitOrder.Clean, AiTaskIds.LocalCleanup);
        }

        bool human = world.Globals.HumanPlayers.Contains(c.Owner);
        if (human && Actor(c).Orders == UnitOrder.GoTo && world.Map.Geometry.InBounds(Actor(c).Destination)
            && world.Map.TileAt(Actor(c).Destination).Features.HasFlag(MapFeatures.Pollution))
        {
            return new(SpecialUnitRoute.Continue);
        }

        bool SafeFortress() => !human && (SpatialControl.AdjacentNonalliedUnit(world, c.Position, c.Owner) is null || count > 1);
        if (c.FortressNeed == 2 && SafeFortress())
        {
            return Order(c, UnitOrder.Fortress, AiTaskIds.FortressWork);
        }

        bool goal = c.SettlerKind && Actor(c).Flags.GoalReached || SharedGoals.PriorityAt(Civ(c), c.Position, 21) != 0;
        if (c.HomeDistance <= 2 && Home(c).Flags.SettlerNearby && c.SettlerKind && Actor(c).Orders == UnitOrder.GoTo)
        { return new(SpecialUnitRoute.Continue); }
        bool local = false;
        if (c.HomeDistance is > 0 and < 3 && SizeAllows(c, c.Position, c.Terrain))
        { local = TerrainImprovementChoice.Choose(world, Rules, c.Owner, c.Position) != 0 || c.Policy == 4; }
        if (local)
        {
            int improvement = TerrainImprovementChoice.Choose(world, Rules, c.Owner, c.Position);
            if (improvement == 1)
            {
                return Order(c, UnitOrder.Irrigate, AiTaskIds.LocalIrrigation);
            }

            if (improvement == 2)
            {
                return Order(c, UnitOrder.Mine, AiTaskIds.LocalMine);
            }
        }
        if (goal || local)
        {
            var features = world.Map.TileAt(c.Position).Features;
            if (features.HasFlag(MapFeatures.Road) && !features.HasFlag(MapFeatures.Railroad) && Knows(c, AdvanceIds.Railroad))
            {
                local = Resource(c.Position) || features.HasFlag(MapFeatures.Mine) || unchecked((sbyte)Rules.Terrain[(int)c.Terrain].Shields) > 1;
                if (goal || local)
                {
                    return Order(c, UnitOrder.Road, AiTaskIds.RailroadWork);
                }
            }
            else if (!features.HasFlag(MapFeatures.Road) && RoadAllowed(c, c.Position))
            {
                local = (features & (MapFeatures.Irrigation | MapFeatures.Mine)) != 0 && (world.Map.TileAt(c.Position).River || (int)c.Terrain < 3);
                if (goal || local)
                {
                    return Order(c, UnitOrder.Road, AiTaskIds.RoadWork);
                }
            }
        }
        if (c.FortressNeed != 0 && SafeFortress())
        {
            return Order(c, UnitOrder.Fortress, AiTaskIds.FortressWork);
        }

        c.CitySelector = c.HomeCity;
        if (Actor(c).Orders == UnitOrder.GoTo && !Actor(c).Flags.Colonist
            && unchecked(16 * Civ(c).ContinentCities[c.Region]) < unchecked((installed.Leaders[Civ(c).Tribe].Expand + 1) * world.Map.Regions[c.Region].Fertile))
        { return new(SpecialUnitRoute.Finish); }
        if (c.HomeDistance < 2)
        {
            for (int i = 0; i < 8; ++i)
            {
                int direction = (world.Globals.Turn + i) & 7;
                var p = Offset(c.Position, MapNeighborhoods.Directions[direction]);
                if (!world.Map.Geometry.InBounds(p) || world.Map.CityOwner(p) is not null)
                {
                    continue;
                }

                int first = stacks.FirstAt(p);
                if (first >= 0 && (world.Entities.Units.Storage[first].Owner.Value != c.Owner || stacks.CountRole(first, (int)RuleRole.Settle, Rules) != 0))
                {
                    continue;
                }

                if (TerrainImprovementChoice.Choose(world, Rules, c.Owner, p) == 0 || !SizeAllows(c, p, world.Map.TileAt(p).Terrain) || Distance(c.Position, p) >= 3)
                {
                    continue;
                }

                Actor(c).AiTask = AiTaskIds.MoveToWork;
                return new(SpecialUnitRoute.Direction, direction);
            }
        }
        if (!world.Map.TileAt(c.Position).Features.HasFlag(MapFeatures.Road) && RoadAllowed(c, c.Position) && c.HomeDistance < 3 && (int)c.Terrain < 3)
        { Actor(c).AiTask = AiTaskIds.RoadWork; return Order(c, UnitOrder.Road); }
        return new(SpecialUnitRoute.Continue);
    }
}
