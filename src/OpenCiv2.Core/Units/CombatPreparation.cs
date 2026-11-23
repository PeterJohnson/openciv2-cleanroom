using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U06 retained cache and per-civilization notice inputs. The owning combat operation
/// carries this state through preparation and subsequent children; entry zero is retained.</summary>
public sealed class CombatWorkspace
{
    public Terrain Terrain;
    public CitySlot? City;
    public int DefenseFactor;
    public CivilizationArray<bool> TreatyNotices;
    public CivilizationArray<int> AllyWarnings;
    public CivilizationArray<bool> Seen;
    public bool AnyViewer, ExplosionShown;
    public int AnimationInterval, ExplosionKind, KillCount;
    public UnitSlot? AnimationDefender;
    public bool ActiveUnitLost, AggregateTreatyNotice;
    public int AggregateAllyWarning;
    public CombatSound? LastSound;
    public int? SoundArgument;
    public MapPoint? AnimationTarget;
}

/// <summary>U06 strength primitives and validated defender selection, including their cache effects.</summary>
public sealed class CombatStrength(GameState world, RuleTables rules, StackOperations stacks, CombatWorkspace state)
{
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private bool Ability(UnitSlot? slot, Func<UnitAbilities, bool> predicate) => slot is { } unit && predicate(Rule(unit).Abilities);

    public int Attack(UnitSlot slot)
    {
        int attack = unchecked((sbyte)Rule(slot).Attack * 8);
        if (Unit(slot).Flags.Veteran)
        { attack = unchecked(attack + (attack >> 1)); }
        if (Unit(slot).Flags.Paradropped)
        { attack = unchecked(attack + (attack >> 1)); }
        return attack;
    }

    private void Refresh(MapPoint point)
    { state.Terrain = world.Map.TileAt(point).Terrain; state.City = CityQueries.At(world, point); }

    public int Defense(UnitSlot defender, bool refresh, UnitSlot? attacker)
    {
        var point = Unit(defender).Position;
        if (refresh)
        { Refresh(point); }
        int defense = unchecked((sbyte)Rule(defender).Defense * 4);
        var surface = world.Map.TileAt(point);
        defense = unchecked(defense * (unchecked((sbyte)rules.Terrain[(int)state.Terrain].DefenseFactor) + (surface.River ? 1 : 0)));
        ref int factor = ref state.DefenseFactor;
        factor = 2;
        if (Unit(defender).Orders == UnitOrder.Fortified && Rule(defender).Domain == RuleDomain.Land)
        { factor = 3; }
        if ((surface.Features & (MapFeatures.Fortification | MapFeatures.CitySite)) == MapFeatures.Fortification
            && (attacker is not { } attacking || Rule(attacking).Domain != RuleDomain.Air && !Rule(attacking).Abilities.IgnoreWalls))
        { factor = 4; }
        bool fighter = state.City is not null && Rule(defender).Domain == RuleDomain.Air && Rule(defender).Abilities.AttackAir
            && attacker is { } air && Rule(air).Domain == RuleDomain.Air && !Rule(air).Abilities.DestroyedAfterAttack;
        if (fighter)
        { factor *= Ability(attacker, a => a.AttackAir) ? 2 : 4; }
        else
        {
            if (state.City is { } city
                && (BuildingQueries.HasBuilding(world, city, BuildingIds.CityWalls)
                    || BuildingQueries.HasWonder(world, rules, Unit(defender).Owner.Value, WonderIds.GreatWall))
                && Rule(defender).Domain == RuleDomain.Land
                && (attacker is not { } attackingCity || Rule(attackingCity).Domain == RuleDomain.Land && !Rule(attackingCity).Abilities.IgnoreWalls))
            { factor = 6; }
            if (Rule(defender).Domain != RuleDomain.Land)
            { factor = 2; }
        }
        if (factor != 2)
        { defense = unchecked(defense * factor) >> 1; }
        if (Unit(defender).Flags.Veteran)
        { defense = unchecked(defense + (defense >> 1)); }
        return defense;
    }

    public UnitSlot? BestDefender(UnitSlot? supplied, UnitSlot? attacker)
    {
        if (supplied is not { } initial)
        { return null; }
        int best = 0;
        UnitSlot selected = initial;
        Refresh(Unit(initial).Position);
        for (int current = stacks.First(initial.Value); current >= 0; current = stacks.Next(current))
        {
            UnitSlot candidate = new((ushort)current);
            if (state.Terrain == Terrain.Ocean && Rule(candidate).Domain == RuleDomain.Land)
            { continue; }
            int score = Defense(candidate, false, attacker);
            if (world.Globals.Rules.NormalCombat)
            {
                int remaining = UnitMovement.RemainingHitPoints(world, rules, ref Unit(candidate));
                score = Arithmetic.Divide32(unchecked(remaining * score), UnitMovement.MaximumHitPoints(rules, Unit(candidate)));
            }
            if (Rule(candidate).Abilities.AntiHorseDefense)
            { score = unchecked(score + 1); }
            if (Rule(candidate).Abilities.AntiAirDefense)
            {
                if (attacker is null)
                { score = unchecked(score + 1); }
                else if (Rule(attacker.Value).Domain == RuleDomain.Air)
                { score = unchecked(score * (Rule(attacker.Value).Abilities.DestroyedAfterAttack ? 5 : 3)); }
            }
            if (Rule(candidate).Abilities.AttackAir && attacker is { } air && Rule(air).Domain == RuleDomain.Air && Rule(air).Abilities.AttackAir)
            { score = unchecked(score * 2); }
            if (Rule(candidate).Domain == RuleDomain.Sea && state.City is { } city)
            {
                score = attacker is { } airAttacker && Rule(airAttacker).Domain == RuleDomain.Air && !BuildingQueries.HasBuilding(world, city, BuildingIds.SamBattery)
                    ? unchecked(score * 2) : score / 2;
            }
            if (score >= best)
            { best = score; selected = candidate; }
        }
        return selected;
    }
}

public sealed record PreparedCombat(UnitSlot Attacker, UnitSlot Defender, int AttackerOwner, int DefenderOwner,
    MapPoint Origin, MapPoint Target, int Attack, int Defense, int AttackFirepower, int DefenseFirepower,
    bool DefenderReroll, bool ShoreBombard, bool SdiBonus, bool Scramble, int? ImprovementNotice);

/// <summary>U06 preparation shared by preview and execution. Execution=true only prepares/scales
/// strengths and city notices; the combat parent still owns charge, diplomacy, rounds and aftermath.</summary>
public sealed class CombatPreparation
{
    private readonly GameState world;
    private readonly RuleTables rules;
    private readonly StackOperations stacks;
    private readonly CombatWorkspace state;
    private readonly CombatStrength strength;
    private readonly SharedGoals goals;
    public CombatPreparation(GameState world, RuleTables rules, StackOperations stacks, CombatWorkspace state)
    {
        this.world = world;
        this.rules = rules;
        this.stacks = stacks;
        this.state = state;
        strength = new(world, rules, stacks, state);
        goals = new(world, rules, new MovementGoalQueries(world, rules));
    }
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    /// <summary>Null is U06's no-target result (999), after entry clears and movement queries.
    /// A returned context still requires the execution parent to resolve the fight.</summary>
    public PreparedCombat? Prepare(UnitSlot attacker, int direction, bool execute)
    {
        MapKnowledge.RequireLocal(world);
        state.AggregateTreatyNotice = false;
        state.AggregateAllyWarning = 0;
        state.ExplosionKind = 0;
        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        { state.TreatyNotices[c] = false; state.AllyWarnings[c] = 0; }
        int owner = Unit(attacker).Owner.Value;
        var origin = Unit(attacker).Position;
        int attack = strength.Attack(attacker), attackFirepower = unchecked((sbyte)Rule(attacker).Firepower);
        int movement = UnitMovement.Left(world, rules, Unit(attacker)) < rules.Cosmic.RoadMovementMultiplier
            ? UnitMovement.Left(world, rules, Unit(attacker)) : rules.Cosmic.RoadMovementMultiplier;
        if (execute)
        { attack = Arithmetic.Divide32(unchecked(attack * movement), rules.Cosmic.RoadMovementMultiplier); }
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, MapLimits.Directions);
        var target = MapNeighborhoods.Offset(world.Map.Geometry, origin, MapNeighborhoods.Directions[direction], world.Globals.Rules.FlatWorld);
        int first = stacks.FirstAt(target);
        if (first < 0)
        { return null; }
        UnitSlot defender = strength.BestDefender(new((ushort)first), attacker)!.Value;
        int enemy = Unit(defender).Owner.Value, defense = strength.Defense(defender, false, attacker);
        int defenseFirepower = unchecked((sbyte)Rule(defender).Firepower);
        bool reroll = false, shore = false, sdi = false, scramble = false;
        int? improvement = null;
        if (Rule(attacker).Role == RuleRole.AirSuperiority && Rule(defender).Domain == RuleDomain.Air && Rule(defender).Range == 0)
        { defense = unchecked(defense - (defense >> 1)); defenseFirepower = 1; }
        if (Rule(defender).Abilities.AntiHorseDefense)
        {
            int allowance = UnitMovement.Allowance(world, rules, Unit(attacker));
            if (world.Multiplayer.Saved.DoubleMovement)
            { allowance = unchecked(allowance - unchecked((sbyte)Rule(attacker).Movement) / 2); }
            if (allowance == 2 * rules.Cosmic.RoadMovementMultiplier && Rule(attacker).Domain == RuleDomain.Land && UnitMovement.MaximumHitPoints(rules, Unit(attacker)) == 10)
            { defense = unchecked(defense + (defense >> 1)); }
        }
        if (Rule(defender).Abilities.AntiAirDefense && Rule(attacker).Domain == RuleDomain.Air)
        { defense = unchecked(defense * (Rule(attacker).Abilities.DestroyedAfterAttack ? 5 : 3)); }
        if (state.City is { Value: > 0 } city)
        {
            if (Rule(attacker).Domain == RuleDomain.Sea && Rule(defender).Domain != RuleDomain.Sea && BuildingQueries.HasBuilding(world, city, BuildingIds.CoastalFortress))
            {
                defense = unchecked(defense * 2);
                if (execute)
                { improvement = BuildingIds.CoastalFortress; world.Entities.Cities.At(city).Flags.CoastalFortressUsed = true; }
            }
            if (Rule(attacker).Domain == RuleDomain.Air)
            {
                if (!Rule(defender).Abilities.AttackAir || Rule(attacker).Abilities.DestroyedAfterAttack)
                {
                    if (BuildingQueries.HasBuilding(world, city, BuildingIds.SdiDefense) && Rule(attacker).Abilities.DestroyedAfterAttack && unchecked((sbyte)Rule(attacker).Attack) < 99)
                    { defense = unchecked(defense * 2); sdi = true; if (execute) { improvement = BuildingIds.SdiDefense; } }
                    if (BuildingQueries.HasBuilding(world, city, BuildingIds.SamBattery))
                    { defense = unchecked(defense * 2); if (execute) { improvement = BuildingIds.SamBattery; } }
                }
                else if (execute)
                { scramble = true; }
            }
        }
        if (Unit(attacker).Type.Value == UnitTypeIds.Partisans && Rule(defender).Attack == 0)
        { attack = unchecked(attack * 8); }
        if (Rule(attacker).Domain == RuleDomain.Sea && Rule(defender).Domain == RuleDomain.Land)
        { attackFirepower = defenseFirepower = 1; }
        if (Rule(defender).Abilities.Submarine)
        { defenseFirepower = 1; }
        if (Rule(attacker).Abilities.Submarine && !Human(enemy))
        { goals.ClearStandingNear(enemy, 18, target, 3); goals.AddStanding(enemy, target, 18, 2); }
        if (Rule(defender).Domain == RuleDomain.Sea && state.Terrain != Terrain.Ocean && Rule(attacker).Domain != RuleDomain.Sea)
        { defenseFirepower = 1; shore = true; attackFirepower = unchecked(attackFirepower * 2); }
        if (owner == 0)
        {
            attack = Human(enemy) ? Arithmetic.Divide32(unchecked(((int)world.Globals.Difficulty + 1) * attack), 4) : attack / 2;
            if (state.City is { } defendingCity)
            {
                if (world.Civilizations[enemy].CityCount < 2)
                { attack = 0; }
                if (BuildingQueries.HasBuilding(world, defendingCity, BuildingIds.Palace))
                { reroll = true; attack >>= 1; }
                if (world.Civilizations[enemy].CityCount < 8)
                { reroll = true; }
            }
            if (BuildingQueries.HasWonder(world, rules, enemy, WonderIds.GreatWall))
            { attack /= 2; }
        }
        if (enemy == 0)
        {
            bool iron = false;
            for (int c = 0; c < EntityLimits.Civilizations; ++c)
            { if (Human(c) && CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, AdvanceIds.IronWorking)) { iron = true; break; } }
            if (Unit(defender).Type.Value == UnitTypeIds.Archers || Unit(defender).Type.Value == UnitTypeIds.Legion && !iron)
            { defense >>= 1; if (defense < 2) { defense = 1; } }
            if (BuildingQueries.HasWonder(world, rules, owner, WonderIds.GreatWall))
            { attack = unchecked(attack * 2); }
        }
        return new(attacker, defender, owner, enemy, origin, target, attack, defense, attackFirepower, defenseFirepower, reroll, shore, sdi, scramble, improvement);
    }
    public int Preview(UnitSlot attacker, int direction)
    {
        var prepared = Prepare(attacker, direction, false);
        if (prepared is null)
        { return 999; }
        int attack = prepared.Attack, defense = prepared.Defense;
        if (world.Globals.Rules.NormalCombat)
        {
            attack = unchecked((UnitMovement.RemainingHitPoints(world, rules, ref Unit(attacker)) + 8 * prepared.AttackFirepower) * attack);
            defense = unchecked((UnitMovement.RemainingHitPoints(world, rules, ref Unit(prepared.Defender)) + 8 * prepared.DefenseFirepower) * defense);
        }
        while (attack > 3999)
        { attack >>= 1; defense >>= 1; }
        int score = Arithmetic.Divide32(unchecked(attack * 8), unchecked(defense + 1));
        return Math.Min(1024, score);
    }
}
