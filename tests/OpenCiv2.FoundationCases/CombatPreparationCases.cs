using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CombatFixture
    {
        public RevelationFixture Data { get; } = CargoFixture(0, 0, 0);
        public GameState World => Data.World;
        public CombatWorkspace State { get; } = new();
        public CombatStrength Strength { get; }
        public CombatPreparation Preparation { get; }
        public CombatFixture()
        {
            for (int i = 0; i < 3; ++i) Data.Unit(i).Owner = new(2);
            World.Map.TileAt(CargoPoint).Owner = 2;
            Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 2 };
            Data.Rules.Terrain[(int)Terrain.Forest] = new() { DefenseFactor = 4 };
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Attack = 2, Defense = 3, Firepower = 1 };
            Data.Rules.Units[10] = Data.Rules.Units[0] with { Attack = 10, Defense = 2 };
            Data.AddUnit(3, 1, 10, new(8, 10)); World.Globals.Difficulty = Difficulty.Prince;
            Strength = new(World, Data.Rules, Data.Stacks, State);
            Preparation = new(World, Data.Rules, Data.Stacks, State);
        }
        public ref Unit Attacker => ref Data.Unit(3);
        public PreparedCombat Prepare(bool execute = false) => Preparation.Prepare(new(3), 1, execute)!;
        public CitySlot City(int slot = 0)
        { Data.AddCity(slot, 2, CargoPoint); World.Civilizations[2].CityCount = 4; return new((ushort)slot); }
    }

    // U06 Strength primitives, including the sequential bonuses and retained terrain/city cache.
    private static void CombatStrengthAndCache()
    {
        var f = new CombatFixture(); f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Attack = 4 };
        Equal(32, f.Strength.Attack(new(3))); f.Attacker.Flags.Veteran = true; Equal(48, f.Strength.Attack(new(3)));
        f.Attacker.Flags.Paradropped = true; Equal(72, f.Strength.Attack(new(3)));
        f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Attack = 255 }; Equal(-18, f.Strength.Attack(new(3)));
        Equal(24, f.Strength.Defense(new(0), true, new(3))); Equal(2, f.State.DefenseFactor);
        f.Data.Unit(0).Orders = UnitOrder.Fortified; Equal(36, f.Strength.Defense(new(0), true, new(3)));
        f.World.Map.TileAt(CargoPoint).Features |= MapFeatures.Fortification; Equal(48, f.Strength.Defense(new(0), true, new(3)));
        f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Abilities = new() { IgnoreWalls = true } };
        Equal(36, f.Strength.Defense(new(0), true, new(3))); Equal(48, f.Strength.Defense(new(0), true, null));
        f.World.Map.TileAt(CargoPoint).Features &= ~MapFeatures.Fortification;
        var city = f.City(); BuildingQueries.SetBuilding(f.World, city, BuildingIds.CityWalls, true);
        Equal(72, f.Strength.Defense(new(0), true, null)); f.Data.Unit(0).Flags.Veteran = true;
        Equal(108, f.Strength.Defense(new(0), true, null)); Equal(6, f.State.DefenseFactor);
        f.Data.Unit(0).Flags.Veteran = false; f.Data.Unit(0).Orders = UnitOrder.None;
        f.State.Terrain = Terrain.Forest; f.World.Map.TileAt(CargoPoint).River = true;
        f.World.Map.TileAt(CargoPoint).Features &= ~MapFeatures.CitySite;
        Equal(180, f.Strength.Defense(new(0), false, null)); Equal(city, f.State.City);
        Equal(36, f.Strength.Defense(new(0), true, null)); Equal((CitySlot?)null, f.State.City); Equal(Terrain.Grassland, f.State.Terrain);
        f.Data.Rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 255 };
        f.World.Map.TileAt(CargoPoint).River = false; Equal(-12, f.Strength.Defense(new(0), true, null));
    }

    private static void CombatFighterAndDomainDefense()
    {
        var f = new CombatFixture(); var city = f.City();
        f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Air, Abilities = new() { AttackAir = true } };
        f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Domain = RuleDomain.Air };
        Equal(96, f.Strength.Defense(new(0), true, new(3))); Equal(8, f.State.DefenseFactor);
        f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Abilities = new() { AttackAir = true } };
        Equal(48, f.Strength.Defense(new(0), true, new(3)));
        f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Abilities = new() { DestroyedAfterAttack = true } };
        Equal(24, f.Strength.Defense(new(0), true, new(3))); Equal(2, f.State.DefenseFactor);
        BuildingQueries.SetBuilding(f.World, city, BuildingIds.CityWalls, true);
        f.Data.Unit(0).Orders = UnitOrder.Fortified; f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Sea };
        f.World.Map.TileAt(CargoPoint).River = true; Equal(36, f.Strength.Defense(new(0), true, null));
        var wall = new CombatFixture(); var wallCity = wall.City(); wall.World.Globals.WonderCity[WonderIds.GreatWall] = (short)wallCity.Value;
        Equal(72, wall.Strength.Defense(new(0), true, new(3)));
        wall.Data.Rules.WonderObsolescence[WonderIds.GreatWall] = (AdvanceReference)5; wall.World.Civilizations[4].Advances = default(AdvanceSet).With(5);
        Equal(24, wall.Strength.Defense(new(0), true, new(3)));
        wall.World.Map.TileAt(CargoPoint).Features |= MapFeatures.Fortification; // Airbase is neither a city nor a fortress.
        Equal(24, wall.Strength.Defense(new(0), true, null)); Equal((CitySlot?)null, wall.State.City);
    }

    private static void CombatDefenderSelection()
    {
        var f = new CombatFixture(); Equal(new UnitSlot(2), f.Strength.BestDefender(new(0), new(3)));
        f.Data.Unit(2).Damage = 9; Equal(new UnitSlot(1), f.Strength.BestDefender(new(0), new(3)));
        f.World.Globals.Rules.NormalCombat = false; Equal(new UnitSlot(2), f.Strength.BestDefender(new(0), new(3))); Equal((byte)9, f.Data.Unit(2).Damage);
        f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Ocean; Equal(new UnitSlot(1), f.Strength.BestDefender(new(1), new(3)));
        f.World.Map.TileAt(CargoPoint).Terrain = Terrain.Grassland; f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Defense = 255 };
        Equal(new UnitSlot(1), f.Strength.BestDefender(new(1), new(3))); Equal((UnitSlot?)null, f.Strength.BestDefender(null, new(3)));

        foreach (int variant in Enumerable.Range(0, 5))
        {
            var p = new CombatFixture(); p.Data.Rules.Units[11] = p.Data.Rules.Units[0] with
            { Abilities = variant == 0 ? new() { AntiHorseDefense = true } : variant < 4 ? new() { AntiAirDefense = true } : new() { AttackAir = true } };
            p.Data.Unit(0).Type = new(11);
            if (variant >= 2) p.Data.Rules.Units[10] = p.Data.Rules.Units[10] with { Domain = RuleDomain.Air, Abilities = new() { DestroyedAfterAttack = variant == 3, AttackAir = variant == 4 } };
            Equal(new UnitSlot(0), p.Strength.BestDefender(new(2), variant == 1 ? null : new(3)));
        }
        var sea = new CombatFixture(); var city = sea.City(); sea.Data.Rules.Units[11] = sea.Data.Rules.Units[0] with { Domain = RuleDomain.Sea };
        sea.Data.Unit(0).Type = new(11); Equal(new UnitSlot(2), sea.Strength.BestDefender(new(0), null));
        sea.Data.Rules.Units[10] = sea.Data.Rules.Units[10] with { Domain = RuleDomain.Air };
        Equal(new UnitSlot(0), sea.Strength.BestDefender(new(2), new(3)));
        BuildingQueries.SetBuilding(sea.World, city, BuildingIds.SamBattery, true); Equal(new UnitSlot(2), sea.Strength.BestDefender(new(0), new(3)));
    }

    private static void CombatPreviewAndExecutionPreparation()
    {
        var f = new CombatFixture(); f.Attacker.MovesUsed = 5;
        f.State.TreatyNotices[0] = f.State.TreatyNotices[1] = f.State.TreatyNotices[7] = true;
        f.State.AllyWarnings[0] = 9; f.State.AllyWarnings[7] = 2;
        Equal(26, f.Preparation.Preview(new(3), 1)); Equal(true, f.State.TreatyNotices[0]); Equal(false, f.State.TreatyNotices[1] || f.State.TreatyNotices[7]);
        Equal(9, f.State.AllyWarnings[0]); Equal(0, f.State.AllyWarnings[7]); Equal((byte)5, f.Attacker.MovesUsed);
        var execute = f.Prepare(true); Equal(26, execute.Attack); Equal(24, execute.Defense); Equal(new UnitSlot(2), execute.Defender);
        Equal(new MapPoint(8, 10), execute.Origin); Equal(CargoPoint, execute.Target); Equal((byte)5, f.Attacker.MovesUsed);
        f.State.City = new(17); f.State.Terrain = Terrain.Forest;
        Equal(999, f.Preparation.Preview(new(3), 0)); Equal(new CitySlot(17), f.State.City); Equal(Terrain.Forest, f.State.Terrain);
        f.Attacker.MovesUsed = 0; f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Attack = 255 };
        Equal(-2, f.Preparation.Preview(new(3), 1));
        f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Attack = 127 }; f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Defense = 0 };
        Equal(1024, f.Preparation.Preview(new(3), 1));
    }

    private static void CombatCityAndSpecialModifiers()
    {
        foreach (int citySlot in new[] { 0, 1 })
            foreach (bool execute in new[] { false, true })
            {
                var f = new CombatFixture(); var city = f.City(citySlot);
                foreach (int building in new[] { BuildingIds.SdiDefense, BuildingIds.SamBattery, BuildingIds.CoastalFortress }) BuildingQueries.SetBuilding(f.World, city, building, true);
                f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Domain = RuleDomain.Air, Abilities = new() { DestroyedAfterAttack = true } };
                var p = f.Prepare(execute); Equal(citySlot == 0 ? 24 : 96, p.Defense); Equal(citySlot > 0, p.SdiBonus);
                Equal(citySlot > 0 && execute ? BuildingIds.SamBattery : (int?)null, p.ImprovementNotice);
                f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Attack = 99 }; p = f.Prepare(execute);
                Equal(citySlot == 0 ? 24 : 48, p.Defense); Equal(false, p.SdiBonus);
                f.Data.Rules.Units[10] = f.Data.Rules.Units[10] with { Domain = RuleDomain.Sea };
                p = f.Prepare(execute); Equal(citySlot == 0 ? 24 : 48, p.Defense); Equal(1, p.AttackFirepower); Equal(1, p.DefenseFirepower);
                Equal(citySlot > 0 && execute, f.World.Entities.Cities.At(city).Flags.CoastalFortressUsed);
            }
        var horse = new CombatFixture(); horse.Data.Rules.Units[0] = horse.Data.Rules.Units[0] with { Abilities = new() { AntiHorseDefense = true } };
        Equal(36, horse.Prepare().Defense); horse.World.Multiplayer.Saved.DoubleMovement = true; Equal(24, horse.Prepare().Defense);
        horse.Data.Rules.Units[10] = horse.Data.Rules.Units[10] with { Movement = 12 }; Equal(36, horse.Prepare().Defense);
        var shore = new CombatFixture(); shore.Data.Rules.Units[0] = shore.Data.Rules.Units[0] with { Domain = RuleDomain.Sea, Firepower = 3 };
        shore.Data.Rules.Units[10] = shore.Data.Rules.Units[10] with { Firepower = 255 }; var result = shore.Prepare();
        Equal(true, result.ShoreBombard); Equal(-2, result.AttackFirepower); Equal(1, result.DefenseFirepower);
        var fighter = new CombatFixture(); fighter.City(1);
        fighter.Data.Rules.Units[0] = fighter.Data.Rules.Units[0] with { Domain = RuleDomain.Air, Range = 0, Abilities = new() { AttackAir = true, AntiAirDefense = true } };
        fighter.Data.Rules.Units[10] = fighter.Data.Rules.Units[10] with { Domain = RuleDomain.Air, Role = RuleRole.AirSuperiority };
        Equal(144, fighter.Prepare().Defense); Equal(false, fighter.Prepare().Scramble); Equal(true, fighter.Prepare(true).Scramble);
        var partisan = new CombatFixture(); partisan.Data.Rules.Units[9] = partisan.Data.Rules.Units[10]; partisan.Attacker.Type = new(9);
        partisan.Data.Rules.Units[0] = partisan.Data.Rules.Units[0] with { Attack = 0 }; Equal(640, partisan.Prepare().Attack);
    }

    private static void CombatBarbariansAndPreviewGoals()
    {
        var f = new CombatFixture(); f.Attacker.Owner = new(0); var city = f.City();
        Equal(40, f.Prepare().Attack); Equal(true, f.Prepare().DefenderReroll);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Globals.Difficulty = Difficulty.Chieftain; Equal(20, f.Prepare().Attack);
        BuildingQueries.SetBuilding(f.World, city, BuildingIds.Palace, true); Equal(10, f.Prepare().Attack);
        f.World.Globals.WonderCity[WonderIds.GreatWall] = (short)city.Value; Equal(5, f.Prepare().Attack);
        f.World.Civilizations[2].CityCount = 1; Equal(0, f.Prepare().Attack);
        foreach (int type in new[] { UnitTypeIds.Archers, UnitTypeIds.Legion })
        {
            var barb = new CombatFixture(); barb.Data.Rules.Units[type] = barb.Data.Rules.Units[0];
            for (int i = 0; i < 3; ++i) { barb.Data.Unit(i).Owner = new(0); barb.Data.Unit(i).Type = new((byte)type); }
            Equal(12, barb.Prepare().Defense); barb.World.Civilizations[1].Advances = default(AdvanceSet).With(AdvanceIds.IronWorking);
            Equal(type == UnitTypeIds.Archers ? 12 : 24, barb.Prepare().Defense);
            barb.Data.Rules.Units[type] = barb.Data.Rules.Units[type] with { Defense = 255 }; Equal(type == UnitTypeIds.Archers ? 1 : -8, barb.Prepare().Defense);
        }
        var sub = new CombatFixture(); sub.Data.Rules.Units[10] = sub.Data.Rules.Units[10] with { Abilities = new() { Submarine = true } };
        sub.World.Civilizations[2].StandingGoals[0] = new(new(11, 9), 18, 5);
        sub.World.Civilizations[2].StandingGoals[1] = new(new(30, 20), 18, 7);
        sub.World.Civilizations[2].StandingGoals[2] = new(new(11, 9), 2, 4);
        sub.Preparation.Preview(new(3), 1);
        Equal(new CivilizationGoal(CargoPoint, 18, 2), sub.World.Civilizations[2].StandingGoals[0]);
        Equal(new CivilizationGoal(new(30, 20), 18, 7), sub.World.Civilizations[2].StandingGoals[2]);
        Equal((byte)0, sub.Attacker.MovesUsed); Equal((byte)0, sub.Data.Unit(2).Damage);
    }

    private static void CombatPreparationFailures()
    {
        var hp = new CombatFixture(); hp.Data.Rules.Units[0] = hp.Data.Rules.Units[0] with { HitPoints = 0 }; hp.State.DefenseFactor = 99;
        Throws<DivideByZeroException>(() => hp.Prepare()); Equal(2, hp.State.DefenseFactor); Equal(Terrain.Grassland, hp.State.Terrain);
        var movement = new CombatFixture(); movement.Data.Rules.Cosmic = movement.Data.Rules.Cosmic with { RoadMovementMultiplier = 0 };
        movement.State.TreatyNotices[1] = true; Throws<DivideByZeroException>(() => movement.Prepare(true)); Equal(false, movement.State.TreatyNotices[1]);
        Equal((byte)0, movement.Attacker.MovesUsed);
        var remote = new CombatFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp; remote.State.TreatyNotices[1] = true;
        Throws<InvalidOperationException>(() => remote.Prepare()); Equal(true, remote.State.TreatyNotices[1]);
    }
}
