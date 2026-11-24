using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum ContactNoticeKind { UNForce, WallForce, AllyMakesPeace, SignPeace, CancelAllied, SignNato, SignAllied, AllyMakesWar, DeclareWar, CancelPeace }
public sealed record ContactNotice(ContactNoticeKind Kind, DiplomaticParty First, DiplomaticParty Second,
    DiplomaticParty? Target = null, CitySlot? City = null, string? CityName = null);
public abstract record DiplomaticContactDecision
{
    public sealed record Audience(DiplomaticAudienceDecision Value) : DiplomaticContactDecision;
    public sealed record Grant(ResearchGrantDecision Value) : DiplomaticContactDecision;
    public sealed record Ally(AllyHelpDecision Value) : DiplomaticContactDecision;
}

/// <summary>V04 §10 local contact dispatch and AI peace/alliance/war transitions, composed
/// with actual V05 audiences/ally help, A08 security/exchange and V04 obligations.
/// Network forwarding and two-human network parley belong to the network session.</summary>
public sealed class DiplomaticContact(GameState world, RuleState rules, DiplomaticStance stance,
    DiplomaticAudience audiences, AiAdvanceExchange exchange, DiplomaticWar war, AllyHelp help,
    Action<Event> executeEvent, Action<ContactNotice> notice)
{
    private GameState World => world;
    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c);
    private Relationship Relation(int a, int b) => world.Civilizations[a].Treaties[b];
    private int Rank(int c) => world.Globals.PowerRank[c];
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules.Definitions, c, w);
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private bool Historical => world.Globals.Status.Scenario && world.Scenario.Flags.HistoricalAi;
    private bool Observed(int a, int b)
    {
        int local = world.Multiplayer.LocalHuman;
        return Relation(local, a).Embassy || Wonder(local, WonderIds.UnitedNations) || Wonder(local, WonderIds.MarcoPolo)
            || Relation(local, b).Embassy || Wonder(local, WonderIds.UnitedNations) || Wonder(local, WonderIds.MarcoPolo)
            || world.Globals.RevealMap != 0;
    }
    private void Show(ContactNoticeKind kind, int a, int b, int? target = null, CitySlot? city = null) =>
        notice(new(kind, Party(a), Party(b), target is { } t ? Party(t) : null, city,
            city is { } c ? world.Entities.Cities.At(c).Name : null));
    public bool NeedsAudience(int a, int b) => !Relation(a, b).Contact || Relation(a, b).Incident
        || world.Civilizations[a].LastContactTurn[b] < 0 || world.Globals.Turn - world.Civilizations[a].LastContactTurn[b] > 15;
    private bool Peaceful(int a, int b) => !stance.FeelsSecure(a, b) || Wonder(b, WonderIds.GreatWall)
        || Wonder(b, WonderIds.UnitedNations) || world.Civilizations[a].Government > Government.Fundamentalism && !Relation(a, b).Aggressor;
    public Operation Begin(int first, int second, MapPoint focus, string call) => new(this, first, second, focus, call);

    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticContact parent;
        private readonly IEnumerator<DiplomaticContactDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public DiplomaticContactDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticContact parent, int a, int b, MapPoint focus, string call)
        { ArgumentException.ThrowIfNullOrEmpty(call); this.parent = parent; steps = parent.Meet(this, a, b, focus, call).GetEnumerator(); }
        internal ResearchReply? TakeReply() { var result = reply; reply = null; return result; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public DiplomaticContactDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed contact cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<DiplomaticContactDecision?> Meet(Operation op, int a, int b, MapPoint focus, string call)
    {
        if (a == 0 || b == 0 || world.Globals.Turn == 0)
        { yield break; }
        if (world.Events.Loaded && !ScenarioPermissions.AllowNegotiation(world, a, b, executeEvent))
        { yield break; }
        // Both local dispatch branches (single-player/either AI and hotseat/email two-human)
        // perform these ordered tests. The second observes the first audience's live state.
        if (Human(a) && a == world.Multiplayer.LocalHuman && NeedsAudience(a, b))
        { foreach (var d in Audience(op, a, b, focus, call + "/first-audience")) { yield return d; } }
        if (Human(b) && b == world.Multiplayer.LocalHuman && NeedsAudience(b, a))
        { foreach (var d in Audience(op, b, a, focus, call + "/second-audience")) { yield return d; } }
        if (!Relation(a, b).Contact)
        { DiplomaticRelations.Set(world, a, b, new() { ChangedThisTurn = true }); }
        bool firstContact = !Relation(a, b).Contact;
        DiplomaticRelations.Set(world, a, b, new() { Contact = true, RecentContact = true });
        if (Human(a) || Human(b) || (unchecked(world.Globals.Turn + a + b) & 3) != 0 && !firstContact)
        { yield break; }
        if (Historical && (a == 1 && b == 3 || a == 3 && b == 1) && world.Globals.Turn <= 4)
        { yield break; }
        if (Peaceful(a, b) && Peaceful(b, a))
        {
            using var trade = exchange.Begin(a, b, call + "/exchange");
            while (!trade.Completed)
            {
                var d = trade.Resume(op.TakeReply()?.Selected);
                if (!trade.Completed)
                { yield return d is null ? null : new DiplomaticContactDecision.Grant(d); }
            }
            op.Report(trade.PresentationFailures);
            if (!Relation(a, b).Peace)
            { Peace(a, b); }
            else
            { Alliance(a, b, focus, call); }
        }
        else if (!Relation(a, b).War || firstContact)
        { foreach (var d in War(op, a, b, call)) { yield return d; } }
    }
    private void Peace(int a, int b)
    {
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            if (Relation(a, b).Aggressor)
            {
                if (Wonder(b, WonderIds.UnitedNations))
                { Show(ContactNoticeKind.UNForce, a, b); }
                else if (Wonder(b, WonderIds.GreatWall))
                { Show(ContactNoticeKind.WallForce, a, b); }
            }
            if (Relation(b, a).Aggressor)
            {
                if (Wonder(a, WonderIds.UnitedNations))
                { Show(ContactNoticeKind.UNForce, b, a); }
                else if (Wonder(a, WonderIds.GreatWall))
                { Show(ContactNoticeKind.WallForce, b, a); }
            }
            int local = world.Multiplayer.LocalHuman;
            if (Relation(local, a).Alliance)
            { Show(ContactNoticeKind.AllyMakesPeace, a, b); }
            else if (Relation(local, b).Alliance)
            { Show(ContactNoticeKind.AllyMakesPeace, b, a); }
            else if (Observed(a, b))
            { Show(ContactNoticeKind.SignPeace, a, b); }
        }
        DiplomaticRelations.Set(world, a, b, new() { Peace = true });
    }
    private void Alliance(int a, int b, MapPoint focus, string call)
    {
        bool wasAllied = Relation(a, b).Alliance, useExisting = wasAllied, leaderGap = false;
        if (Rank(a) == 7 && Rank(b) > 3)
        { useExisting = false; leaderGap = true; }
        if (Rank(b) == 7 && Rank(a) > 3)
        { useExisting = false; leaderGap = true; }
        int alliedThirds = 0;
        int? target = null;
        bool targetLeader = false;
        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        {
            if (c == a || c == b)
            { continue; }
            if (Relation(a, c).Alliance || Relation(b, c).Alliance)
            { ++alliedThirds; }
            else
            {
                targetLeader = Human(c) && Rank(c) == 7 && world.Globals.Difficulty != Difficulty.Chieftain
                    && world.Civilizations[c].CityCount >= 5 && world.Globals.Turn >= 201;
                if (targetLeader)
                { target = c; break; }
                if (world.Globals.Difficulty != Difficulty.Chieftain && (useExisting
                    ? !Relation(a, c).Peace && !Relation(b, c).Peace
                    : !leaderGap && DiplomaticAttitudes.Hostile(world, a, c) && Rank(a) <= Rank(c)
                        && DiplomaticAttitudes.Hostile(world, b, c) && Rank(b) <= Rank(c)))
                { target = c; break; }
            }
        }
        if (Historical && (a == 6 && b == 7 || a == 7 && b == 6) && world.Globals.Turn > 1)
        { target = 0; }
        if (target is null or < 1 || alliedThirds != 0 && !useExisting
            && (!targetLeader || world.Civilizations[world.Globals.PlayerCiv].Reputation < alliedThirds))
        {
            if (wasAllied && !world.Multiplayer.Saved.AutoplayActive && Observed(a, b))
            { Show(ContactNoticeKind.CancelAllied, a, b); }
            DiplomaticRelations.Clear(world, a, b, new() { Alliance = true });
            return;
        }
        int t = target.Value;
        if (!wasAllied)
        {
            bool joined = false;
            if (Observed(a, b) || Human(t) && (Relation(world.Multiplayer.LocalHuman, a).Contact || Relation(world.Multiplayer.LocalHuman, b).Contact))
            {
                if (!world.Multiplayer.Saved.AutoplayActive)
                {
                    if (targetLeader)
                    { Show(ContactNoticeKind.SignNato, a, b, t, CityQueries.Nearest(world, focus, a, b).Slot); }
                    else
                    { Show(ContactNoticeKind.SignAllied, a, b, t); }
                }
                joined = war.JoinAllyWar(a, t, b, call + "/first-ally");
                joined |= war.JoinAllyWar(b, t, a, call + "/second-ally");
            }
            if (Human(t) && !world.Civilizations[t].SpaceshipFlags.Launched && !joined)
            {
                if (!Relation(a, t).War)
                { DiplomaticRelations.Set(world, a, t, new() { Incident = true }); }
                if (!Relation(a, t).War)
                { DiplomaticRelations.Set(world, b, t, new() { Incident = true }); }
            }
        }
        DiplomaticRelations.Set(world, a, b, new() { Alliance = true });
    }
    private IEnumerable<DiplomaticContactDecision?> War(Operation op, int a, int b, string call)
    {
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            int local = world.Multiplayer.LocalHuman;
            int? ally = Relation(local, a).Alliance ? a : Relation(local, b).Alliance ? b : null;
            if (ally is { } side)
            {
                int enemy = side == a ? b : a;
                if (!stance.FeelsSecure(side, enemy))
                {
                    using var demand = help.Begin(side, enemy, call + "/ally-help");
                    while (!demand.Completed)
                    {
                        var d = demand.Resume(op.TakeReply()?.Selected);
                        if (!demand.Completed)
                        { yield return d is null ? null : new DiplomaticContactDecision.Ally(d); }
                    }
                    op.Report(demand.PresentationFailures);
                }
                Show(ContactNoticeKind.AllyMakesWar, side, enemy);
            }
            else if (Observed(a, b))
            { Show(Relation(a, b).Peace && !Relation(a, b).Alliance ? ContactNoticeKind.CancelPeace : ContactNoticeKind.DeclareWar, a, b); }
        }
        DiplomaticRelations.Clear(world, a, b, new() { Peace = true });
        DiplomaticRelations.Set(world, a, b, new() { War = true });
        if (Relation(a, b).Vendetta)
        { DiplomaticRelations.Clear(world, a, b, new() { Vendetta = true }); world.Civilizations[a].Treaties[b].Aggressor = true; }
    }
    private IEnumerable<DiplomaticContactDecision?> Audience(Operation op, int a, int b, MapPoint focus, string call)
    {
        using var audience = audiences.Begin(a, b, false, focus, call);
        while (!audience.Completed)
        {
            var d = audience.Resume(op.TakeReply());
            if (!audience.Completed)
            { yield return d is null ? null : new DiplomaticContactDecision.Audience(d); }
        }
        op.Report(audience.PresentationFailures);
    }
}
