using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // U02 allowance: signed rules, sea bonuses, domain-specific damage, pure allowance versus mutating HP/exhaustion.
    private static void UnitMovementAllowances()
    {
        var (world, installed) = ResearchWorld();
        var rules = installed.Definitions;
        rules.Cosmic = rules.Cosmic with { RoadMovementMultiplier = 3 };
        rules.Units[0] = new() { Movement = 6, HitPoints = 10, Domain = RuleDomain.Land };
        world.Globals.Rules.NormalCombat = true;
        var unit = new Unit { Owner = new(1) };
        foreach (var (damage, expected) in new[] { (0, 6), (1, 6), (5, 3), (10, 3) })
        {
            unit.Damage = (byte)damage;
            Equal(expected, UnitMovement.Allowance(world, rules, unit));
            Equal((byte)damage, unit.Damage);
        }

        rules.Units[0] = rules.Units[0] with { Domain = RuleDomain.Sea };
        world.Civilizations[1].Advances = default(AdvanceSet).With(59);
        world.Globals.WonderCity[3] = world.Globals.WonderCity[12] = 0;
        world.Entities.Cities.Storage[0].Owner = new(1);
        unit.Damage = 0;
        Equal(18, UnitMovement.Allowance(world, rules, unit));
        unit.Damage = 5;
        Equal(9, UnitMovement.Allowance(world, rules, unit));
        unit.Damage = 10;
        Equal(6, UnitMovement.Allowance(world, rules, unit));
        unit.Damage = 0;
        rules.Units[0] = rules.Units[0] with { Abilities = new() { CoastBound = true } };
        Equal(15, UnitMovement.Allowance(world, rules, unit));
        rules.WonderObsolescence[12] = (AdvanceReference)4;
        world.Civilizations[7].Advances = default(AdvanceSet).With(4);
        Equal(9, UnitMovement.Allowance(world, rules, unit));
        rules.Units[0] = rules.Units[0] with { Domain = RuleDomain.Air };
        unit.Damage = 9;
        Equal(6, UnitMovement.Allowance(world, rules, unit));

        rules.Units[0] = rules.Units[0] with { Domain = RuleDomain.Land };
        world.Globals.Rules.NormalCombat = false;
        Equal(6, UnitMovement.Allowance(world, rules, unit));
        Equal((byte)9, unit.Damage);
        Equal(10, UnitMovement.RemainingHitPoints(world, rules, ref unit));
        Equal((byte)0, unit.Damage);
        unit.MovesUsed = 9;
        UnitMovement.Exhaust(world, rules, ref unit);
        Equal((byte)6, unit.MovesUsed);
        Equal(0, UnitMovement.Left(world, rules, unit));

        rules.Units[0] = rules.Units[0] with { Movement = 255, HitPoints = 128 };
        Equal(-1, UnitMovement.Allowance(world, rules, unit));
        UnitMovement.Exhaust(world, rules, ref unit);
        Equal((byte)255, unit.MovesUsed);
        Equal(-128, UnitMovement.MaximumHitPoints(rules, unit));
        Equal(0, UnitMovement.RemainingHitPoints(world, rules, ref unit));
        rules.Units[0] = rules.Units[0] with { Movement = 0, Domain = RuleDomain.Sea };
        unit.Owner = new(255);
        Equal(0, UnitMovement.Allowance(world, rules, unit));
    }

    private static (GameState World, RuleTables Rules) ActivationWorld()
    {
        var (world, installed) = ResearchWorld();
        world.Map.Allocate(new(40, 24));
        world.Globals.Rules.FlatWorld = true;
        world.Globals.TurnCiv = 1;
        var rules = installed.Definitions;
        rules.Cosmic = rules.Cosmic with { RoadMovementMultiplier = 3 };
        rules.Units[0] = new() { Movement = 6, HitPoints = 10 };
        rules.Units[1] = rules.Units[0] with { Domain = RuleDomain.Sea };
        world.Entities.Units.SetEnd(3);
        world.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(1), Position = new(8, 8) };
        world.Entities.Units.Storage[1] = new() { Token = new(2), Owner = new(1), Position = new(12, 8) };
        world.Entities.Units.Storage[2] = new() { Token = new(3), Owner = new(2), Position = new(10, 8) };
        return (world, rules);
    }

    // U04 distinct predicates: rectangular bounds and ownership are absent from awakeWithMoves.
    private static void UnitActivationPredicates()
    {
        var (world, rules) = ActivationWorld();
        Equal(false, UnitActivation.Selectable(world, rules, -1));
        Equal(false, UnitActivation.AwakeWithMoves(world, rules, 3));
        ref var unit = ref world.Entities.Units.Storage[0];
        Equal(true, UnitActivation.Selectable(world, rules, 0));
        unit.Flags.Waiting = unit.Flags.Automated = true;
        unit.Orders = UnitOrder.GoTo;
        Equal(true, UnitActivation.Selectable(world, rules, 0));
        unit.Orders = UnitOrder.Fortified;
        Equal(false, UnitActivation.Selectable(world, rules, 0));
        Equal(true, UnitActivation.AwakeWithMoves(world, rules, 0));
        unit.Orders = UnitOrder.None;
        unit.Flags.SpyMission = true;
        Equal(false, UnitActivation.Selectable(world, rules, 0));
        Equal(true, UnitActivation.AwakeWithMoves(world, rules, 0));
        unit.Flags.SpyMission = false;
        unit.Position = new(1, 0);
        Equal(true, UnitActivation.Selectable(world, rules, 0));
        unit.Position = new(50, -1);
        Equal(false, UnitActivation.Selectable(world, rules, 0));
        Equal(true, UnitActivation.AwakeWithMoves(world, rules, 0));
        unit.Position = new(-1, 0);
        Equal(false, UnitActivation.AwakeWithMoves(world, rules, 0));
        Equal(false, UnitActivation.Selectable(world, rules, 2));
        Equal(true, UnitActivation.AwakeWithMoves(world, rules, 2));
        unit.Position = new(8, 8);
        unit.Orders = UnitOrder.Sleeping;
        Equal(false, UnitActivation.AwakeWithMoves(world, rules, 0));
        unit.Orders = UnitOrder.None;
        unit.MovesUsed = 6;
        Equal(false, UnitActivation.AwakeWithMoves(world, rules, 0));
        Equal(false, UnitActivation.Selectable(world, rules, 0));
    }

    // U04 next-unit search: active ties, sea penalty and the protected active slot need all three passes.
    private static void UnitActivationWaitingAndTies()
    {
        var (world, rules) = ActivationWorld();
        MapPoint cursor = new(10, 8);
        UnitSlot? Next(UnitSlot? prior = null, int mode = 0, int observer = 1) =>
            UnitActivation.NextSelectable(world, rules, prior, cursor, mode, observer);
        var pool = world.Entities.Units;
        Equal(new UnitSlot(0), Next()!.Value);
        world.Globals.ActiveUnit = new(1);
        Equal(new UnitSlot(1), Next()!.Value);
        world.Globals.ActiveUnit = new(0);
        pool.Storage[0].Type = new(1);
        Equal(new UnitSlot(1), Next()!.Value);
        pool.Storage[0].Type = new(0);
        foreach (ref var unit in pool.Scanned)
        {
            unit.Flags.Waiting = true;
        }
        Equal(new UnitSlot(1), Next(new(0))!.Value);
        Equal(true, pool.Storage[0].Flags.Waiting);
        Equal(false, pool.Storage[2].Flags.Waiting);
        pool.Storage[1].MovesUsed = 6;
        pool.Storage[1].Flags.Waiting = true;
        Equal(new UnitSlot(0), Next(new(0))!.Value);
        Equal(false, pool.Storage[0].Flags.Waiting);
        foreach (ref var unit in pool.Scanned)
        {
            unit.Flags.Waiting = true;
        }
        Equal((UnitSlot?)null, Next(mode: 1, observer: 2));
        Equal(true, pool.Scanned.ToArray().All(u => !u.Flags.Waiting));

        // Protection captures shared active, not the previous argument.
        pool.Storage[1].MovesUsed = 0;
        world.Globals.ActiveUnit = new(1);
        pool.Storage[0].Flags.Waiting = pool.Storage[1].Flags.Waiting = true;
        Equal(new UnitSlot(0), Next(new(0))!.Value);
        Equal(true, pool.Storage[1].Flags.Waiting);
    }
}
