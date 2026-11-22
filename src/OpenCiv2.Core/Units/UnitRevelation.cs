using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>Real meeting and pre-meeting disclosure boundaries. Callers publish the final world separately.</summary>
public interface IUnitRevelationEffects
{
    void RevealOrigin(MapPoint position);

    void Meet(int actingCivilization, int otherCivilization, MapPoint position);
}

/// <summary>W03 §§3–5 local surroundings: terrain, adjacent interactions, then extended observers.
/// Presentation chooses its own repaint regions; network tile synchronization is a separate adapter.</summary>
public sealed class UnitRevelation
{
    private readonly GameState world;
    private readonly RuleTables rules;
    private readonly StackOperations stacks;
    private readonly SharedGoals goals;
    private readonly IUnitRevelationEffects effects;

    public UnitRevelation(GameState world, RuleTables rules, StackOperations stacks, IUnitRevelationEffects effects)
    {
        this.world = world;
        this.rules = rules;
        this.stacks = stacks;
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        goals = new(world, rules, new MovementGoalQueries(world, rules));
    }

    private ref Unit Unit(int slot) => ref world.Entities.Units.Storage[slot];

    private UnitRule Rule(int slot) => rules.Units[Unit(slot).Type.Value];

    private ref Relationship Relation(int from, int to) => ref world.Civilizations[from].Treaties[to];

    private void Goal(int owner, MapPoint position, RuleRole role, int priority) =>
        goals.AddCurrent(owner, position, (int)role, priority);

    private MapPoint Offset(MapPoint origin, int index) =>
        MapNeighborhoods.Offset(world.Map.Geometry, origin, MapNeighborhoods.Radius[index], world.Globals.Rules.FlatWorld);

    private static bool SeesMedium(RuleDomain domain, bool ocean) =>
        domain == RuleDomain.Air || ocean == (domain == RuleDomain.Sea);

    public void Reveal(int slot, bool allowContact)
    {
        MapKnowledge.RequireLocal(world);
        var origin = Unit(slot).Position;
        int owner = unchecked((sbyte)Unit(slot).Owner.Value);
        var entryRule = Rule(slot);
        bool extended = entryRule.Abilities.SeeTwo, hidden = entryRule.Abilities.Submarine;
        var domain = entryRule.Domain;
        bool originOcean = world.Map.TileAt(origin).Ocean;
        bool fullMap = world.Globals.RevealMap != 0, originPublished = false;
        if (owner == 0)
        {
            Unit(slot).Visibility = Unit(slot).Visibility.Union(world.Map.TileAt(origin).RevealedTo);
        }

        for (int i = 0; i < 25; ++i)
        {
            var point = Offset(origin, i);
            if (!world.Map.Geometry.InBounds(point)
                || !(i < MapLimits.Directions || i == 20 || extended && SeesMedium(domain, world.Map.TileAt(point).Ocean)))
            {
                continue;
            }
            MapKnowledge.Remember(world, owner, point);
            MapKnowledge.Explore(world, owner, point);
        }

        for (int i = 0; i < MapLimits.Directions; ++i)
        {
            var point = Offset(origin, i);
            if (!world.Map.Geometry.InBounds(point))
            {
                continue;
            }
            var city = CityQueries.At(world, point);
            if (city is { } citySlot)
            {
                int cityOwner = unchecked((sbyte)world.Entities.Cities.At(citySlot).Owner.Value);
                if (cityOwner != owner)
                {
                    MapKnowledge.RevealCity(world, owner, citySlot);
                    if (!hidden)
                    {
                        stacks.MarkSeen(slot, cityOwner);
                    }
                    if (domain == RuleDomain.Land && !Relation(cityOwner, owner).Peace)
                    {
                        Goal(cityOwner, point, RuleRole.Attack, 4);
                        Goal(cityOwner, point, RuleRole.Defend, 2);
                    }
                }
            }

            bool neighborOcean = world.Map.TileAt(point).Ocean;
            if (hidden && !neighborOcean)
            {
                continue;
            }
            int other = stacks.FirstAt(point);
            if (other < 0)
            {
                if (!world.Map.TileAt(origin).Ocean && world.Map.CityOwner(point) is { } cityOwner
                    && cityOwner != owner && !Relation(owner, cityOwner).Contact)
                {
                    PublishOrigin();
                    if (allowContact)
                    {
                        effects.Meet(owner, cityOwner, point);
                    }
                }
                continue;
            }

            int otherOwner = unchecked((sbyte)Unit(other).Owner.Value), otherType = Unit(other).Type.Value;
            if (otherOwner == owner)
            {
                continue;
            }

            if (!hidden || rules.Units[otherType].Abilities.DetectSubmarines)
            {
                if (world.Map.CityOwner(origin) is null)
                {
                    stacks.MarkSeen(slot, otherOwner);
                }
                if (!Relation(owner, otherOwner).Alliance)
                {
                    UnitOrders.WakeSentries(world, rules, stacks, other);
                }
                if (world.Globals.HumanPlayers.Contains(otherOwner)
                    ? neighborOcean == originOcean : neighborOcean || !originOcean)
                {
                    UnitOrders.InterruptGoto(ref Unit(other), rules);
                }
            }

            if (!rules.Units[otherType].Abilities.Submarine || stacks.Count(other) > 1 || Rule(slot).Abilities.DetectSubmarines)
            {
                if (city is null)
                {
                    stacks.MarkSeen(other, owner);
                }
                if (domain == RuleDomain.Air || (world.Globals.HumanPlayers.Contains(owner)
                    ? neighborOcean == originOcean : originOcean || !neighborOcean))
                {
                    UnitOrders.InterruptGoto(ref Unit(slot), rules);
                }
                if (neighborOcean && otherOwner != 0 && !Relation(owner, otherOwner).Peace)
                {
                    Goal(owner, point, RuleRole.NavalSuperiority, rules.Units[otherType].Abilities.Submarine ? 3 : 2);
                }
            }

            if (Rule(slot).Domain == RuleDomain.Land && !originOcean && !neighborOcean
                && (rules.Units[otherType].Domain != RuleDomain.Air || city is not null))
            {
                if (!fullMap)
                {
                    PublishOrigin();
                }
                if (allowContact && Unit(slot).Type.Value != UnitTypeIds.Partisans)
                {
                    effects.Meet(owner, otherOwner, point);
                }
                if (unchecked((sbyte)Rule(slot).Role) < 6)
                {
                    Goal(otherOwner, point, RuleRole.Defend, owner == 0 ? 1 : 2);
                }
                if (!Relation(owner, otherOwner).Peace)
                {
                    int priority = otherOwner != 0 && rules.Units[otherType].Role != RuleRole.Diplomacy
                        && !(Unit(other).Orders == UnitOrder.Fortified && rules.Units[otherType].Role == RuleRole.Defend) ? 3 : 1;
                    Goal(owner, point, RuleRole.Attack, priority);
                }
            }
        }

        for (int i = MapLimits.Directions; i < 25; ++i)
        {
            if (i == 20)
            {
                continue;
            }
            var point = Offset(origin, i);
            if (!world.Map.Geometry.InBounds(point))
            {
                continue;
            }
            int other = stacks.FirstAt(point);
            int otherOwner = world.Map.CityOrUnitOwner(point) is { } tileOwner ? tileOwner : -1;
            var city = CityQueries.At(world, point);
            if (otherOwner == owner)
            {
                continue;
            }
            if (extended && SeesMedium(domain, world.Map.TileAt(point).Ocean))
            {
                if (city is { } citySlot)
                {
                    MapKnowledge.RevealCity(world, owner, citySlot);
                }
                if (other >= 0 && !Rule(other).Abilities.Submarine)
                {
                    if (city is null)
                    {
                        stacks.MarkSeen(other, owner);
                    }
                    if (world.Map.TileAt(point).Ocean && otherOwner != 0 && !Relation(owner, otherOwner).Peace)
                    {
                        Goal(owner, point, RuleRole.NavalSuperiority, 2);
                    }
                }
            }
            if (other >= 0 && !hidden && Rule(other).Abilities.SeeTwo
                && SeesMedium(Rule(other).Domain, world.Map.TileAt(origin).Ocean))
            {
                UnitOrders.InterruptGoto(ref Unit(other), rules);
                UnitOrders.WakeSentries(world, rules, stacks, other);
                if (world.Map.CityOwner(origin) is null)
                {
                    stacks.MarkSeen(slot, otherOwner);
                }
                if (!world.Map.TileAt(point).Ocean && otherOwner != 0 && !Relation(otherOwner, owner).Peace)
                {
                    Goal(otherOwner, origin, RuleRole.NavalSuperiority, 2);
                }
            }
            if (city is not null && world.Map.TileAt(origin).RevealedTo.Contains(otherOwner)
                && Rule(slot).Attack != 0 && !hidden)
            {
                stacks.MarkSeen(slot, otherOwner);
            }
        }

        void PublishOrigin()
        {
            if (!originPublished)
            {
                effects.RevealOrigin(origin);
                originPublished = true;
            }
        }
    }
}
