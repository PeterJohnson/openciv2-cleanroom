using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

public sealed record GroundEntryResult(UnitTacticsContext? Context, GroundRecoveryResult? Recovery, int? Result);

/// <summary>A04 §2 ordinary-civilization entry through actual recovery and early completion.
/// The owning chooser must dispatch civilization zero to W06 before calling this operation.</summary>
public sealed class GroundTacticsEntry(GameState world, RuleTables rules, GroundRecovery recovery,
    GroundOrders orders, GameplayRandom random)
{
    public GroundEntryResult EnterOrdinary(UnitSlot slot, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        MapKnowledge.RequireLocal(world);
        ref var unit = ref world.Entities.Units.At(slot);
        if (unit.Owner.Value == 0)
        { throw new ArgumentException("Barbarian actors require the W06 chooser", nameof(slot)); }
        if (unit.Orders is >= UnitOrder.Fortress and <= UnitOrder.Clean)
        { return new(null, null, orders.Finish(slot, null)); }
        if (!world.Map.Geometry.InBounds(unit.Position))
        {
            UnitOrders.SetSleep(ref unit);
            return new(null, null, orders.Finish(slot, null));
        }
        var context = new UnitTacticsContext(world, rules, slot);
        Trespass(context, call);
        ForceCity(context);
        var next = recovery.Evaluate(context);
        int? completed = next.Route switch
        {
            GroundRecoveryRoute.Hold => orders.Direction(context, 8),
            GroundRecoveryRoute.Finish => orders.Finish(slot, context.Domain, context.StayHome),
            _ => null
        };
        return new(context, next, completed);
    }
    private void Trespass(UnitTacticsContext c, string call)
    {
        if (!c.EnemyZone || c.AdjacentForeignCivilization is not { } other || other == 0 || c.HomeCity != c.NearestCity
            || !world.Civilizations[c.Owner].Treaties[other].Peace)
        { return; }
        DiplomaticAttitudes.Adjust(world, c.Owner, other, 1);
        int Foreign() => c.AdjacentForeignCivilization ?? throw new InvalidOperationException("A04 trespass lost its adjacent civilization");
        if (world.Globals.HumanPlayers.Contains(Foreign()) && world.Globals.Difficulty > Difficulty.Prince
            && DiplomaticAttitudes.Get(world, c.Owner, Foreign()) > 60
            && world.Globals.PowerRank[c.Owner] < world.Globals.PowerRank[Foreign()]
            && random.SecondaryRange(new ChoiceKey("A04.trespass", call), 0, 3) == 0)
        { world.Civilizations[c.Owner].Treaties[Foreign()].HostileIntent = true; }
    }
    private void ForceCity(UnitTacticsContext c)
    {
        ref var unit = ref world.Entities.Units.At(c.Unit);
        if (c.NearestDistance != 1 || c.HomeCity == c.NearestCity || c.Domain != RuleDomain.Land
            || unchecked((sbyte)rules.Units[unit.Type.Value].Role) >= 6)
        { return; }
        var city = c.NearestCity ?? throw new InvalidOperationException("A04 adjacent-city distance requires a nearest city");
        if (world.Map.UnitOwner(world.Entities.Cities.At(city).Position) is not null)
        { return; }
        int other = world.Entities.Cities.At(city).Owner.Value;
        var treaty = world.Civilizations[c.Owner].Treaties[other];
        if (treaty.Ceasefire || treaty.Peace || treaty.Alliance)
        { return; }
        unit.Orders = UnitOrder.None;
        c.EnemyZone = c.ForceAttack = true;
        c.AdjacentForeignCivilization = other;
    }
}
