using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>V06 city dispatch domain, separate from unit-mission and payment replies.</summary>
public enum CityMission { Embassy, Investigate, Steal, Sabotage, Poison, PlantNuclear, Incite, Chat }
public sealed class EspionageWorkspace { public CityMission? LastMission; }
public enum EspionageIncidentKind { Terror, War, Allied, Leniency, SenateScandal }
public sealed record EspionageIncidentNotice(EspionageIncidentKind Kind, DiplomaticParty Actor,
    DiplomaticParty Victim, int? AttitudeCategory = null);
public sealed record EspionagePretext(DiplomaticParty Actor, DiplomaticParty Victim, bool Allied) : LifecycleDecision;

/// <summary>V06 §11 local incident service. Real V04 war, A08 stance and V02 government children
/// execute here; the human victim's pretext reply is an explicit continuation.</summary>
public sealed class EspionageIncidents
{
    private readonly GameState world;
    private readonly GameplayRandom random;
    private readonly DiplomaticWar war;
    private readonly DiplomaticStance stance;
    private readonly GovernmentChanges governments;
    private readonly Action<EspionageIncidentNotice> notice;

    public EspionageIncidents(GameState world, GameplayRandom random, DiplomaticWar war,
        DiplomaticStance stance, GovernmentChanges governments, Action<EspionageIncidentNotice> notice)
    {
        this.world = world;
        this.random = random;
        this.war = war;
        this.stance = stance;
        this.governments = governments;
        this.notice = notice;
    }

    public Operation Begin(int actor, int victim, string call) => new(this, actor, victim, call);
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private DiplomaticParty Party(int owner) => new(owner, world.Names[owner].Tribe, world.Names[owner].Adjective);
    private void Show(EspionageIncidentKind kind, int actor, int victim, int? category = null) => notice(new(kind, Party(actor), Party(victim), category));

    public sealed class Operation
    {
        private readonly EspionageIncidents parent;
        private readonly int actor, victim;
        private readonly string call;
        private bool started;
        private EspionagePretext? decision;
        public bool Completed { get; private set; }
        internal Operation(EspionageIncidents parent, int actor, int victim, string call)
        { this.parent = parent; this.actor = actor; this.victim = victim; this.call = call; }

        /// <summary>Only reply1 invokes the offered response. Faults terminate the operation with prior effects retained.</summary>
        public EspionagePretext? Resume(int? reply = null)
        {
            if (Completed)
            { return null; }
            try
            { return Advance(reply); }
            catch { Completed = true; throw; }
        }

        private EspionagePretext? Advance(int? reply)
        {
            var world = parent.world;
            MapKnowledge.RequireLocal(world);
            if (!started)
            {
                ArgumentException.ThrowIfNullOrEmpty(call);
                started = true;
                var relation = world.Civilizations[victim].Treaties[actor];
                if (relation.War)
                { Completed = true; return null; }
                if (!relation.Ceasefire && !relation.Peace && !relation.Alliance)
                {
                    DiplomaticAttitudes.Adjust(world, victim, actor, 20);
                    Completed = true;
                    return null;
                }
                if (parent.Human(victim))
                {
                    if (world.Multiplayer.Saved.AutoplayActive || world.Civilizations[actor].Government == Government.Fundamentalism)
                    { Completed = true; return null; }
                    decision = new(parent.Party(actor), parent.Party(victim), relation.Alliance);
                    return decision;
                }
            }
            else if (decision is { } pretext)
            {
                if (reply is null)
                { return decision; }
                if (reply == 1)
                {
                    if (pretext.Allied)
                    { DiplomaticRelations.Clear(world, actor, victim, new() { Alliance = true }); }
                    else
                    { DiplomaticRelations.Set(world, actor, victim, new() { War = true }); }
                    world.Civilizations[victim].LastContactTurn[actor] = world.Globals.Turn;
                }
                decision = null;
            }

            if (parent.Human(actor) && !parent.Human(victim))
            {
                if (!world.Civilizations[actor].Treaties[victim].Alliance)
                {
                    if (world.Civilizations[actor].Government == Government.Fundamentalism)
                    {
                        world.Civilizations[victim].Treaties[actor].Vendetta = true;
                        DiplomaticAttitudes.Adjust(world, victim, actor, 10);
                        if (!world.Multiplayer.Saved.AutoplayActive)
                        { parent.Show(EspionageIncidentKind.Terror, actor, victim); }
                    }
                    else
                    {
                        if (!world.Multiplayer.Saved.AutoplayActive)
                        { parent.Show(EspionageIncidentKind.War, actor, victim); }
                        parent.war.Declare(actor, victim);
                    }
                }
                else if (world.Espionage.LastMission == CityMission.Steal
                    && world.Civilizations[actor].NumAdvances < world.Civilizations[victim].NumAdvances)
                {
                    if (!world.Multiplayer.Saved.AutoplayActive)
                    {
                        parent.stance.Evaluate(actor, victim, call + "/stance");
                        int category = DiplomaticAttitudes.Category(world.Diplomacy.Attitude);
                        int delta = parent.random.PrimaryModulo(new ChoiceKey("V06.incident-leniency", call), 15) + 5;
                        DiplomaticAttitudes.Adjust(world, victim, actor, delta);
                        parent.Show(EspionageIncidentKind.Leniency, actor, victim, category);
                    }
                    Completed = true;
                    return null;
                }
                else
                {
                    if (!world.Multiplayer.Saved.AutoplayActive)
                    { parent.Show(EspionageIncidentKind.Allied, actor, victim); }
                    parent.war.Declare(actor, victim);
                }
            }
            if (parent.Human(actor))
            {
                bool senate = world.Civilizations[actor].Government == Government.Democracy;
                if (!senate)
                {
                    int roll = parent.random.PrimaryModulo(new ChoiceKey("V06.incident-senate", call), 2);
                    senate = roll != 0 && world.Civilizations[actor].Government == Government.Republic;
                }
                if (senate && !(world.Globals.Status.Scenario && world.Scenario.Flags.RevolutionRestriction))
                {
                    if (!world.Multiplayer.Saved.AutoplayActive)
                    { parent.Show(EspionageIncidentKind.SenateScandal, actor, victim); }
                    parent.governments.Change(actor, (int)Government.Anarchy);
                }
            }
            Completed = true;
            return null;
        }
    }
}
