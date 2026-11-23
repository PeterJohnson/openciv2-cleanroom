using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum AttackWarningKind { Peace, TributeCeasefire, Ceasefire, Annoy }
public sealed record AttackWarning(AttackWarningKind Kind, DiplomaticParty Actor, DiplomaticParty Victim);
public enum AttackNoticeKind { Allied, Overruled, UnitedNations, Hawks, Aggressor }
public sealed record AttackNotice(AttackNoticeKind Kind, DiplomaticParty Actor, DiplomaticParty Victim, Government Government);

/// <summary>V04 §4 local senate query and attack permission. Neither operation declares war,
/// changes treaties/reputation nor moves units. The warning's only affirmative reply is one.</summary>
public sealed class AttackConfirmation(GameState world, RuleTables rules, Action<AttackNotice> notice)
{
    private GameState World => world;
    private RuleTables Rules => rules;
    public bool SenateBlocks(int actor, int victim)
    {
        var government = world.Civilizations[actor].Government;
        if (government < Government.Republic || world.Globals.Status.Scenario && world.Scenario.Flags.RevolutionRestriction)
        { return false; }
        int bonus = world.Civilizations[victim].Treaties[actor].SneakAttack ? 25 : 0;
        if (BuildingQueries.HasWonder(world, rules, actor, WonderIds.UnitedNations))
        { bonus = 50; }
        int threshold = Math.Clamp(bonus + 15 * world.Civilizations[victim].Reputation, 0, 75);
        if (world.Civilizations[actor].DiploRandom < threshold)
        { return false; }
        return government >= Government.Democracy || world.Civilizations[actor].Flags.SenateActive;
    }
    private DiplomaticParty Party(int owner) => new(owner, world.Names[owner].Tribe, world.Names[owner].Adjective);
    private void Show(AttackNoticeKind kind, int actor, int victim) => notice(new(kind, Party(actor), Party(victim), world.Civilizations[actor].Government));
    public Operation Begin(int actor, int victim, Relationship protectedFlags) => new(this, actor, victim, protectedFlags);

    public sealed class Operation : IDisposable
    {
        private readonly AttackConfirmation parent;
        private readonly int actor, victim;
        private readonly Relationship protectedFlags;
        private bool started, disposed, faulted;
        public bool Completed { get; private set; }
        public bool Blocked { get; private set; } = true;
        public AttackWarning? Pending { get; private set; }
        internal Operation(AttackConfirmation parent, int actor, int victim, Relationship protectedFlags)
        { this.parent = parent; this.actor = actor; this.victim = victim; this.protectedFlags = protectedFlags; }
        public AttackWarning? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed attack confirmation cannot resume"); }
            if (Completed)
            { return null; }
            try
            { return Advance(reply); }
            catch { faulted = true; throw; }
        }
        private AttackWarning? Advance(int? reply)
        {
            var world = parent.World;
            MapKnowledge.RequireLocal(world);
            if (!started)
            {
                started = true;
                var relation = world.Civilizations[actor].Treaties[victim];
                if (relation.War || !DiplomaticRelations.Intersects(relation, protectedFlags) || !world.Globals.CivsAlive.Contains(victim))
                { Blocked = false; Completed = true; return null; }
                if (relation.Alliance)
                { parent.Show(AttackNoticeKind.Allied, actor, victim); Completed = true; return null; }
                var kind = relation.Peace ? AttackWarningKind.Peace : relation.Ceasefire
                    ? relation.Tribute ? AttackWarningKind.TributeCeasefire : AttackWarningKind.Ceasefire : AttackWarningKind.Annoy;
                return Pending = new(kind, parent.Party(actor), parent.Party(victim));
            }
            if (reply is null)
            { return Pending; }
            Pending = null;
            if (reply != 1)
            { Completed = true; return null; }
            if (parent.SenateBlocks(actor, victim))
            { parent.Show(AttackNoticeKind.Overruled, actor, victim); Completed = true; return null; }
            if (world.Civilizations[actor].Government > Government.Fundamentalism)
            {
                var kind = BuildingQueries.HasWonder(world, parent.Rules, victim, WonderIds.UnitedNations) ? AttackNoticeKind.UnitedNations
                    : !world.Civilizations[victim].Treaties[actor].SneakAttack && world.Civilizations[victim].Reputation == 0
                        ? AttackNoticeKind.Hawks : AttackNoticeKind.Aggressor;
                parent.Show(kind, actor, victim);
            }
            Blocked = false;
            Completed = true;
            return null;
        }
        public void Dispose() => disposed = true;
    }
}
