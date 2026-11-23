using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public enum CombatTreatyNoticeKind { SneakAttack, BreakCeasefire }
/// <summary>Owner-addressed notice. The session must route it to the human defender.</summary>
public sealed record CombatTreatyNotice(CombatTreatyNoticeKind Kind, int Recipient, int Attacker, UnitTarget Unit, string AttackerName);
/// <summary>Cancelled returns the U06 early movement result 1. Otherwise Combat carries execution
/// strength adjustments; ForceWar is retained for the later allies-join-war stage.</summary>
public sealed record CombatEntryResult(PreparedCombat Combat, bool Cancelled, bool ForceWar);

/// <summary>U06 execution entry through diplomacy/difficulty adjustment. Uses actual V04 confirmation
/// and war, A08 choice and V02 government transitions. Viewer/start/nuclear/round stages follow this one.</summary>
public sealed class CombatEntry(GameState world, RuleTables rules, CombatWorkspace state, GameplayRandom random,
    AttackConfirmation confirmation, DiplomaticWar war, GovernmentPolicy government, Action<CombatTreatyNotice> notice)
{
    private GameState World => world;
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    public Operation Begin(PreparedCombat combat, MovementOptions options, string call) => new(this, combat, options, call);

    public sealed class Operation : IDisposable
    {
        private readonly CombatEntry parent;
        private readonly PreparedCombat combat;
        private readonly MovementOptions options;
        private readonly string call;
        private AttackConfirmation.Operation? child;
        private bool started, faulted, disposed;
        public CombatEntryResult? Result { get; private set; }
        public bool Completed => Result is not null;
        internal Operation(CombatEntry parent, PreparedCombat combat, MovementOptions options, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            this.combat = combat;
            this.options = options;
            this.call = call;
        }
        public AttackWarning? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed combat entry cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (!started)
                {
                    started = true;
                    parent.Charge(combat.Attacker, options.Execute);
                    if (parent.Human(combat.AttackerOwner))
                    { child = parent.Confirm(combat); }
                    reply = null;
                }
                if (child is not null)
                {
                    if (child.Resume(reply) is { } pending)
                    { return pending; }
                    bool cancelled = child.Blocked || parent.World.Civilizations[combat.AttackerOwner].Treaties[combat.DefenderOwner].Alliance;
                    child.Dispose();
                    child = null;
                    if (cancelled)
                    { Result = new(combat, true, false); return null; }
                }
                Result = parent.Admit(combat, call);
                return null;
            }
            catch { faulted = true; child?.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; child?.Dispose(); }
    }
    private void Charge(UnitSlot attacker, bool execute)
    {
        if (execute)
        { Unit(attacker).MovesUsed = unchecked((byte)(Unit(attacker).MovesUsed + rules.Cosmic.RoadMovementMultiplier)); }
    }
    private AttackConfirmation.Operation Confirm(PreparedCombat combat) =>
        confirmation.Begin(combat.AttackerOwner, combat.DefenderOwner, new() { Contact = true, Peace = true, Alliance = true });

    private CombatEntryResult Admit(PreparedCombat combat, string call)
    {
        int c = combat.AttackerOwner, e = combat.DefenderOwner;
        if (c != 0 && e != 0)
        { world.Globals.PeaceTurns = 0; }
        DiplomaticRelations.Set(world, c, e, new() { Fighting = true });
        int attack = TreatyBreak(combat, call);
        bool forceWar = war.Force(c, e);
        if (c != 0 && world.Globals.Difficulty < Difficulty.Prince && Human(e))
        { attack >>= 1; }
        if (world.Globals.Difficulty == Difficulty.Chieftain && Human(c))
        { attack = unchecked(attack * 2); }
        return new(combat with { Attack = attack }, false, forceWar);
    }
    private int TreatyBreak(PreparedCombat combat, string call)
    {
        int c = combat.AttackerOwner, e = combat.DefenderOwner, attack = combat.Attack;
        var reverse = world.Civilizations[e].Treaties[c];
        bool sneak = Human(e) && (reverse.Ceasefire || reverse.Peace
            || (reverse.Contact || world.Civilizations[c].Treaties[e].BrokenTreaty) && !reverse.War);
        if (sneak)
        {
            var regime = world.Civilizations[c].Government;
            if (regime > Government.Fundamentalism)
            {
                int roll = random.PrimaryModulo(new ChoiceKey("U06.sneak-government", call), 100);
                if (regime == Government.Republic || BuildingQueries.HasWonder(world, rules, c, WonderIds.UnitedNations))
                { roll -= 50; }
                if (unchecked(roll - 10 * world.Civilizations[e].Reputation) >= 0 && !Human(c))
                { government.Choose(c, true, call + "/government"); }
            }
            if (!world.Multiplayer.Saved.AutoplayActive && Human(e))
            {
                var live = world.Civilizations[e].Treaties[c];
                bool ceasefire = live.Ceasefire && !live.Peace;
                notice(new(ceasefire ? CombatTreatyNoticeKind.BreakCeasefire : CombatTreatyNoticeKind.SneakAttack,
                    e, c, new(combat.Attacker, Unit(combat.Attacker).Token), ceasefire ? world.Names[c].Tribe : world.Names[c].Adjective));
            }
            attack = unchecked(attack * 2);
            world.Civilizations[e].LastContactTurn[c] = world.Globals.Turn;
        }
        else if (world.Civilizations[c].Treaties[e].Peace || world.Civilizations[e].Treaties[c].Peace)
        {
            if (Human(c) || Human(e))
            { state.AggregateTreatyNotice = true; }
            else
            {
                int observer = world.Multiplayer.LocalHuman;
                ArgumentOutOfRangeException.ThrowIfNegative(observer);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(observer, EntityLimits.Civilizations);
                if (world.Civilizations[observer].Treaties[e].Alliance)
                { state.AggregateAllyWarning = 1; }
                else if (world.Civilizations[observer].Treaties[c].Alliance)
                { state.AggregateAllyWarning = 2; }
                else if (world.Globals.RevealMap != 0)
                { state.AggregateTreatyNotice = true; }
            }
        }
        return attack;
    }
}
