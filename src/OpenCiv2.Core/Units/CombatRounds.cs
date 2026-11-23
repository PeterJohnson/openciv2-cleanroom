using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U06 signed decrement gate. Only the most-negative divisor reaches the negative
/// remainder path; record its primary sample directly without changing the sampling law.</summary>
public static class CombatSampling
{
    public static int RollBelow(GameplayRandom random, ChoiceKey key, int strength)
    {
        if (unchecked(strength - 1) <= 0)
        { return 0; }
        return strength > 0 ? random.PrimaryModulo(key, strength) : Arithmetic.Remainder32(random.PrimaryCode(key), strength);
    }
}
public interface ICombatRoundEffects
{
    void Publish();
    void Explosion(MapPoint point, int kind);
    void Redraw(MapPoint origin, MapPoint target);
}

/// <summary>U06 local damage rounds and hit presentation, consuming captured preparation strengths
/// and firepowers. The parent owns the both-zero shortcut, result presentation and casualties.</summary>
public sealed class CombatRounds(GameState world, RuleTables rules, GameplayRandom random, CombatWorkspace state, ICombatRoundEffects effects)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private int Hp(UnitSlot slot) => UnitMovement.RemainingHitPoints(world, rules, ref Unit(slot));
    public Operation Begin(PreparedCombat combat, string call, int roundBudget = 64) => new(this, combat, call, roundBudget);

    public sealed class Operation : IDisposable
    {
        private readonly CombatRounds parent;
        private readonly PreparedCombat combat;
        private readonly string call;
        private readonly int roundBudget;
        private bool started, disposed, faulted;
        public int Rounds { get; private set; }
        public int? Result { get; private set; }
        public bool Completed => Result is not null;
        internal Operation(CombatRounds parent, PreparedCombat combat, string call, int roundBudget)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roundBudget);
            this.parent = parent;
            this.combat = combat;
            this.call = call;
            this.roundBudget = roundBudget;
        }
        /// <summary>Null yields after a finite batch, retaining damage and choice ordinals. A result
        /// is zero for attacker loss; normal combat may return remaining attacker HP on victory.</summary>
        public int? Advance()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed combat round operation cannot resume"); }
            if (Completed)
            { return Result; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                { started = true; parent.Initialize(combat); }
                for (int i = 0; i < roundBudget; ++i)
                {
                    if (parent.Hp(combat.Attacker) == 0 || parent.Hp(combat.Defender) == 0)
                    { return Result = parent.Hp(combat.Attacker); }
                    bool won = parent.Round(combat, call, Rounds);
                    Rounds = checked(Rounds + 1);
                    if (!parent.World.Globals.Rules.NormalCombat)
                    { return Result = won ? 1 : 0; }
                }
                return null;
            }
            catch { faulted = true; throw; }
        }
        public void Dispose() => disposed = true;
    }

    private void Initialize(PreparedCombat combat)
    {
        state.AnimationInterval = UnitMovement.MaximumHitPoints(rules, Unit(combat.Attacker)) < 30
            && UnitMovement.MaximumHitPoints(rules, Unit(combat.Defender)) < 30 ? 5 : 10;
        if (!rules.Units[Unit(combat.Attacker).Type.Value].Abilities.DestroyedAfterAttack)
        { return; }
        state.AnimationInterval = 0;
        state.ExplosionShown = false;
        for (int viewer = 1; viewer < EntityLimits.Civilizations; ++viewer)
        {
            if (state.Seen[viewer] && viewer == world.Multiplayer.LocalHuman)
            { effects.Explosion(combat.Target, state.ExplosionKind); }
        }
    }
    private bool Round(PreparedCombat combat, string call, int round)
    {
        int attack = CombatSampling.RollBelow(random, new("U06.round.attack", call, round), combat.Attack);
        int defense = CombatSampling.RollBelow(random, new("U06.round.defense", call, round), combat.Defense);
        bool won = attack > defense;
        if (won && combat.DefenderReroll)
        {
            attack = CombatSampling.RollBelow(random, new("U06.reroll.attack", call, round), combat.Attack);
            defense = CombatSampling.RollBelow(random, new("U06.reroll.defense", call, round), combat.Defense);
            if (attack < defense)
            { won = false; }
        }
        Hit(won ? combat.Defender : combat.Attacker, won ? combat.AttackFirepower : combat.DefenseFirepower,
            won ? combat.Target : combat.Origin, combat.Origin, combat.Target);
        return won;
    }

    /// <summary>U06 applying one hit, including byte wrap and post-explosion HP query effects.</summary>
    public void Hit(UnitSlot victim, int firepower, MapPoint location, MapPoint origin, MapPoint target)
    {
        MapKnowledge.RequireLocal(world);
        int old = Unit(victim).Damage;
        Unit(victim).Damage = unchecked((byte)(old + firepower));
        int next = Unit(victim).Damage;
        if (!state.AnyViewer || state.AnimationInterval == 0 || next / state.AnimationInterval == old / state.AnimationInterval)
        { return; }
        state.ExplosionShown = true;
        effects.Publish();
        for (int viewer = 1; viewer < EntityLimits.Civilizations; ++viewer)
        {
            if (!state.Seen[viewer] || viewer != world.Multiplayer.LocalHuman)
            { continue; }
            effects.Explosion(location, state.ExplosionKind);
            if (Hp(victim) != 0)
            { effects.Redraw(origin, target); }
        }
    }
}

public sealed record CombatPromotionNotice(UnitTarget Unit, UnitType Type, int Owner, string Name);

/// <summary>U06 promotion bookkeeping after result presentation. Aggression uses captured owners;
/// the promotion/wonder query uses the winner's live record. No casualty is deleted here.</summary>
public sealed class CombatPromotion(GameState world, RuleTables rules, GameplayRandom random, Action<CombatPromotionNotice> notice)
{
    public void Apply(PreparedCombat combat, bool attackerWon, string call)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        ref byte aggression = ref world.Civilizations[combat.AttackerOwner].AggressionCounter[combat.DefenderOwner];
        aggression = attackerWon ? (byte)0 : unchecked((byte)(aggression + 1));
        var winner = attackerWon ? combat.Attacker : combat.Defender;
        int roll = CombatSampling.RollBelow(random, new("U06.promotion", call), unchecked(combat.Attack + combat.Defense));
        if (roll <= (attackerWon ? combat.Defense : combat.Attack)
            || BuildingQueries.HasWonder(world, rules, world.Entities.Units.At(winner).Owner.Value, WonderIds.SunTzu))
        { Promote(winner); }
    }
    public void Promote(UnitSlot slot)
    {
        MapKnowledge.RequireLocal(world);
        ref var unit = ref world.Entities.Units.At(slot);
        if (unit.Flags.Veteran || rules.Units[unit.Type.Value].Abilities.DestroyedAfterAttack)
        { return; }
        unit.Flags.Veteran = true;
        if (world.Globals.HumanPlayers.Contains(unit.Owner.Value) && !world.Multiplayer.Saved.AutoplayActive)
        { notice(new(new(slot, unit.Token), unit.Type, unit.Owner.Value, rules.Units[unit.Type.Value].Identity.Name)); }
    }
}
