using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class GuardFixture
    {
        public RevelationFixture Data { get; } = new();
        public CombatWorkspace Combat { get; } = new();
        public GroundGuard Guard { get; }
        public GuardFixture()
        {
            Data.World.Map.Allocate(new(100, 24));
            foreach (ref var tile in Data.World.Map.Tiles) tile.Terrain = Terrain.Grassland;
            Data.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            Data.Rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1, DefenseFactor = 2 };
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Role = RuleRole.Defend, Attack = 1, Defense = 2 };
            Data.Rules.Units[1] = Data.Rules.Units[0] with { Defense = 3 };
            Data.Rules.Units[2] = Data.Rules.Units[0] with { Role = RuleRole.Attack, Defense = 1 };
            Data.AddUnit(0, 1, 0, new(10, 10)); Data.Unit(0).AiTask = 19;
            Data.AddCity(0, 1, new(10, 10)); Data.World.Civilizations[1].CityCount = 1;
            Guard = new(Data.World, Data.Rules, Data.Stacks, new(Data.World, Data.Rules, Data.Stacks, Combat),
                new(Data.World, Data.Rules, new MovementGoalQueries(Data.World, Data.Rules)));
        }
        public UnitTacticsContext Context() => new(Data.World, Data.Rules, new(0));
        public void Other(int slot = 1, int type = 1) => Data.AddUnit(slot, 1, type, new(10, 10));
        public int Goal => SharedGoals.PriorityAt(Data.World.Civilizations[1], new(10, 10), (sbyte)RuleRole.Defend);
        public void ForeignFort(int homeX = 14)
        {
            Data.World.Map.TileAt(new(10, 10)).Features &= ~MapFeatures.CitySite;
            Data.World.Map.TileAt(new(10, 10)).Features |= MapFeatures.Fortification;
            Data.World.Map.TileAt(new(10, 10)).Claimant = 2;
            Data.World.Entities.Cities.At(new(0)).Position = new(homeX, 10);
            Data.World.Map.TileAt(new(homeX, 10)).Features |= MapFeatures.CitySite;
            Data.World.Map.TileAt(new(homeX, 10)).Owner = 1;
        }
    }

    private static void GuardScenarioAndFerry()
    {
        foreach (int variant in new[] { 0, 1, 2, 3, 4 })
        {
            var f = new GuardFixture(); f.Data.World.Entities.Cities.At(new(0)).Flags.FerryPort = true;
            f.Data.World.Globals.Status.Scenario = variant != 1; f.Data.World.Globals.Status.ScenarioExport = variant == 1;
            f.Data.World.Scenario.Flags.HistoricalAi = true; f.Data.World.Globals.Turn = (short)(variant == 2 ? 6 : 5);
            f.Data.World.Civilizations[1].Treaties[3].War = variant == 3;
            var context = f.Context(); if (variant == 4) f.Data.Stacks.Relocate(0, new(80, 10), false);
            Equal(variant == 0 ? GroundGuardRoute.Hold : GroundGuardRoute.PartisanFortress, f.Guard.Scenario(context));
            Equal(variant != 0, context.StayHome); // Scenario gate uses live x, ferry routing retains captured dh/H.
        }
        foreach (bool garrison in new[] { false, true })
        {
            var f = new GuardFixture(); f.Data.World.Entities.Cities.At(new(0)).Flags.FerryPort = true;
            f.Data.Unit(0).Flags.Garrison = garrison; var context = f.Context();
            Equal(garrison ? GroundGuardRoute.Specialized : GroundGuardRoute.PartisanFortress, f.Guard.Scenario(context));
            if (!garrison)
            {
                Equal(GroundGuardRoute.CityGuard, f.Guard.ForeignFortress(context));
                Equal(GroundGuardRoute.Hold, f.Guard.City(context)); Equal(false, context.StayHome);
            }
        }
    }

    private static void GuardForeignFortress()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new GuardFixture(); f.ForeignFort(variant >= 2 ? 12 : 14);
            f.Data.World.Civilizations[1].Treaties[2].Peace = true;
            f.Data.World.Civilizations[2].Reputation = (byte)(variant == 1 ? 7 : 6);
            f.Data.World.Civilizations[1].Treaties[2].Alliance = variant == 3;
            var context = f.Context(); bool admitted = variant is 0 or 3;
            Equal(admitted ? GroundGuardRoute.CityGuard : GroundGuardRoute.PlanMove, f.Guard.ForeignFortress(context));
            Equal(!admitted, context.Hold); Equal((byte)(admitted ? 19 : 70), f.Data.Unit(0).AiTask);
        }
        var stacked = new GuardFixture(); stacked.ForeignFort(); stacked.Other(); stacked.Data.Unit(0).Orders = UnitOrder.GoTo;
        Equal(GroundGuardRoute.CityGuard, stacked.Guard.ForeignFortress(stacked.Context()));
        foreach (bool companion in new[] { false, true })
        {
            var f = new GuardFixture(); f.ForeignFort(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Attack, ObsoleteBy = (AdvanceReference)5 };
            f.Data.World.Globals.AdvanceKnownBy[5] = CivilizationSet.Only(3); f.Data.World.Civilizations[3].Advances = default(AdvanceSet).With(5);
            if (companion) f.Other(); var context = f.Context();
            Equal(companion ? GroundGuardRoute.CityGuard : GroundGuardRoute.PlanMove, f.Guard.ForeignFortress(context));
            Equal(!companion, context.Hold);
        }
    }

    private static void GuardDefenderQuotaAndGoals()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new GuardFixture(); f.Other(); f.Data.World.Entities.Cities.At(new(0)).Size = (sbyte)(variant == 3 ? 1 : 8);
            f.Data.World.Civilizations[1].ContinentPolicy[0] = 5;
            if (variant is 1 or 2)
            {
                f.Data.AddUnit(2, 2, 2, new(12, 10));
                if (variant == 2) f.Data.World.Map.TileAt(new(12, 10)).Terrain = Terrain.Ocean;
            }
            if (variant == 3)
            { BuildingQueries.SetBuilding(f.Data.World, new(0), BuildingIds.Palace, true); f.Data.World.Civilizations[1].CityCount = 12; }
            var context = f.Context(); Equal(GroundGuardRoute.Hold, f.Guard.City(context)); Equal(true, context.Guard);
            Equal(variant is 1 or 3 ? 3 : 0, f.Goal);
            Equal(new CitySlot(0), f.Data.Unit(0).Home); Equal((byte)70, f.Data.Unit(0).AiTask);
        }
        var strongest = new GuardFixture(); strongest.Other(); strongest.Data.World.Entities.Cities.At(new(0)).Size = 20;
        strongest.Data.Rules.Units[0] = strongest.Data.Rules.Units[0] with { Defense = 4 };
        Equal(GroundGuardRoute.Hold, strongest.Guard.City(strongest.Context())); Equal(0, strongest.Goal);
    }

    private static void GuardTemporaryFortificationAndReserve()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new GuardFixture(); f.Other(); f.Data.World.Entities.Cities.At(new(0)).Size = 1;
            f.Data.Unit(0).Orders = UnitOrder.Sleeping;
            if (variant == 1) f.Data.Stacks.Relocate(0, new(10, 10), false); // Later equal defender now wins against the actor.
            if (variant is 2 or 3) f.Other(2);
            if (variant == 3) f.Data.World.Globals.Turn = 1;
            var context = f.Context(); var result = f.Guard.City(context);
            Equal(variant == 0 ? GroundGuardRoute.Hold : GroundGuardRoute.TransportWork, result);
            Equal(variant == 0, context.Guard);
            Equal(variant is 0 or 3 ? UnitOrder.Sleeping : UnitOrder.None, f.Data.Unit(0).Orders);
            Equal(variant == 1 ? 2 : 3, f.Combat.DefenseFactor); // Actual second defender scan saw temporary Fortified.
        }
        var reserve = new GuardFixture(); reserve.Other(); reserve.Other(2, 2);
        Equal(2, reserve.Data.Stacks.CountReserveDefenders(0, reserve.Data.Rules));
        reserve.Data.Stacks.Relocate(0, new(10, 10), false);
        Equal(1, reserve.Data.Stacks.CountReserveDefenders(0, reserve.Data.Rules));
        var negative = new GuardFixture(); negative.Other(); negative.Data.World.Entities.Cities.At(new(0)).Size = -8;
        negative.Data.World.Entities.Cities.At(new(0)).Flags.FerryPort = true;
        negative.Data.Unit(0).Orders = UnitOrder.Sleeping; var retained = negative.Context();
        Equal(GroundGuardRoute.PartisanFortress, negative.Guard.Scenario(retained));
        Equal(GroundGuardRoute.PlanMove, negative.Guard.City(retained)); Equal(true, retained.Hold);
        Equal(UnitOrder.None, negative.Data.Unit(0).Orders); Equal(false, retained.Guard);
    }

    private static void GuardSettlerPhaseState()
    {
        foreach (int variant in new[] { 0, 1, 2 })
        {
            var f = new GuardFixture(); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
            if (variant == 2) f.Data.AddUnit(1, 2, 2, new(12, 10));
            var context = f.Context(); context.SettlerType = variant != 0;
            Equal(variant == 1 ? GroundGuardRoute.TransportWork : GroundGuardRoute.Hold, f.Guard.City(context));
            Equal(5, f.Goal); Equal(variant != 1, context.Guard);
        }
    }

    private static void GuardWallsAndLiveStrength()
    {
        foreach (var (attack, defense, walls, garrison, hold) in new[]
        { (2, 2, false, false, false), (2, 2, false, true, true), (3, 2, true, false, true), (4, 2, true, true, false), (0, 255, true, false, false) })
        {
            var f = new GuardFixture(); var context = f.Context();
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = (byte)attack, Defense = (byte)defense };
            f.Data.Unit(0).Flags.Garrison = garrison; BuildingQueries.SetBuilding(f.Data.World, new(0), BuildingIds.CityWalls, walls);
            Equal(hold ? GroundGuardRoute.Hold : GroundGuardRoute.PlanMove, f.Guard.City(context));
            Equal(hold ? new CitySlot(0) : (CitySlot?)null, f.Data.Unit(0).Home);
        }
    }

    private static void GuardMissingHomeBoundary()
    {
        var f = new GuardFixture(); f.Data.World.Entities.Cities.At(new(0)).Owner = new(2);
        f.Data.World.Map.TileAt(new(10, 10)).Owner = 2; f.Data.Unit(0).Damage = 3;
        var context = f.Context(); Equal(null, context.HomeCity);
        var recovery = new GroundRecovery(f.Data.World, f.Data.Rules, f.Data.Stacks, new(f.Data.World, f.Data.Rules));
        Equal(GroundRecoveryRoute.CityGuard, recovery.Evaluate(context).Route);
        Throws<InvalidOperationException>(() => f.Guard.City(context));
        Equal(true, context.Hold); Equal(true, context.Guard); Equal(null, f.Data.Unit(0).Home);
        Equal((byte)19, f.Data.Unit(0).AiTask); Equal((byte)0, f.Data.Unit(0).MovesUsed);
    }
}
