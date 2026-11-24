using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum AudienceOfferKind { JoinWar, Tribute, Technology, OverABarrel }
public sealed record AudienceOffer(AudienceOfferKind Kind, int Recipient, DiplomaticParty Partner, int Reaction,
    int Variant = 0, int Gold = 0, ResearchOption? Advance = null, DiplomaticParty? Target = null,
    bool Aggressive = false, bool Allied = false);
public enum AudienceNoticeKind { OutaHere, OutaHereAlly, Begone, Provoke, Reject, Rid, AccursedWall, AccursedUN }
public sealed record AudienceNotice(AudienceNoticeKind Kind, int Recipient, DiplomaticParty Partner, int Reaction, int Variant = 0);
public abstract record DiplomaticAudienceDecision
{
    public sealed record Scenario(LifecycleDecision Value) : DiplomaticAudienceDecision;
    public sealed record Offer(AudienceOffer Value) : DiplomaticAudienceDecision;
    public sealed record Emissary(EmissaryDecision Value) : DiplomaticAudienceDecision;
    public sealed record Technology(TechnologyNegotiationDecision Value) : DiplomaticAudienceDecision;
    public sealed record Grant(ResearchGrantDecision Value) : DiplomaticAudienceDecision;
    public sealed record Treaty(OpeningTreatyDecision Value) : DiplomaticAudienceDecision;
    public sealed record Conversation(DiplomaticConversationDecision Value) : DiplomaticAudienceDecision;
}
public interface IDiplomaticAudienceEffects
{
    void Notice(AudienceNotice notice);
    void RefreshStatus();
}

/// <summary>V05 §6 local AI/player audience from entry through common cleanup. Composes the
/// actual A08 evaluation, V05 exchanges/offers/conversation and V03/V04 gameplay children.
/// Loaded scenario actions can retain actual S02 child decisions before audience initialization.</summary>
public sealed class DiplomaticAudience(GameState world, RuleState rules, GameplayRandom random,
    DiplomaticStance stance, EmissaryReception emissaries, DiplomaticTechnology technology, ResearchGrants grants,
    DiplomaticWar war, OpeningTreaties treaties, DiplomaticConversation conversation,
    Action<Event> executeEvent, IDiplomaticAudienceEffects effects)
{
    private readonly ScenarioPermissionActions? retainedEvents;
    public DiplomaticAudience(GameState world, RuleState rules, GameplayRandom random,
        DiplomaticStance stance, EmissaryReception emissaries, DiplomaticTechnology technology, ResearchGrants grants,
        DiplomaticWar war, OpeningTreaties treaties, DiplomaticConversation conversation,
        ScenarioPermissionActions events, IDiplomaticAudienceEffects effects)
        : this(world, rules, random, stance, emissaries, technology, grants, war, treaties, conversation,
            _ => throw new InvalidOperationException("Retained scenario events require their continuation"), effects)
    { retainedEvents = events; }
    private GameState World => world;
    private DiplomacyWorkspace E => world.Diplomacy;
    private Relationship Relation(int a, int b) => world.Civilizations[a].Treaties[b];
    private int Rank(int c) => world.Globals.PowerRank[c];
    private int Strength(int c) => world.Civilizations[c].MilitaryStrength;
    private int Distrust(int a, int b) => world.Civilizations[a].Reputation - world.Civilizations[b].ReputationCredit[a];
    private bool Historical => world.Globals.Status.Scenario && world.Scenario.Flags.HistoricalAi;
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules.Definitions, c, w);
    private bool Knows(int c, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, advance);
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private int Category => DiplomaticAttitudes.Category(E.Attitude);
    private static int Required(int? value) => value ?? throw new InvalidOperationException("The audience operand is absent");
    private void Request(int b, int delta) => world.Civilizations[b].DiploRequestCount = unchecked((sbyte)(world.Civilizations[b].DiploRequestCount + delta));
    private void Show(AudienceNoticeKind kind, int a, int b, int reaction = 4, int variant = 0) => effects.Notice(new(kind, a, Party(b), reaction, variant));
    private DiplomaticAudienceDecision Offer(AudienceOfferKind kind, int a, int b, int variant = 0, int? advance = null,
        int gold = 0, int? target = null) => new DiplomaticAudienceDecision.Offer(new(kind, a, Party(b), kind == AudienceOfferKind.JoinWar ? 0 : 4,
            variant, gold, advance is { } t ? new(t, rules.Definitions.Advances[t].Identity.Name) : null,
            target is { } c ? Party(c) : null, E.Aggressive, Relation(b, a).Alliance));

    public bool TechnologyGap(int a, int b)
    {
        bool gap;
        if (Knows(a, AdvanceIds.MobileWarfare) || Knows(a, AdvanceIds.AdvancedFlight))
        { gap = !Knows(b, AdvanceIds.MobileWarfare) && !Knows(b, AdvanceIds.AdvancedFlight); }
        else if (Knows(a, AdvanceIds.Conscription) || Knows(a, AdvanceIds.Tactics))
        { gap = !Knows(b, AdvanceIds.Conscription) && !Knows(b, AdvanceIds.Tactics); }
        else
        { gap = Knows(a, AdvanceIds.Gunpowder) && !Knows(b, AdvanceIds.Gunpowder); }
        return world.Civilizations[a].NumAdvances <= world.Civilizations[b].NumAdvances && Knows(b, AdvanceIds.Conscription) ? false : gap;
    }
    public Operation Begin(int actor, int other, bool playerInitiated, MapPoint focus, string call) => new(this, actor, other, playerInitiated, focus, call);
    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticAudience parent;
        private readonly IEnumerator<DiplomaticAudienceDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        internal bool DeclaredWar, Paid, Propose, Hostile, TechGap, NoContact, EmissarySent, Ended, Stop, Accepted;
        public bool Completed { get; private set; }
        public bool ScenarioDenied { get; internal set; }
        public DiplomaticAudienceDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticAudience parent, int a, int b, bool player, MapPoint focus, string call)
        { ArgumentException.ThrowIfNullOrEmpty(call); this.parent = parent; steps = parent.Run(this, a, b, player, focus, call).GetEnumerator(); }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal int Answer() => TakeReply()!.Value.Selected;
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public DiplomaticAudienceDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed audience cannot resume"); }
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
    private IEnumerable<DiplomaticAudienceDecision?> Run(Operation op, int a, int b, bool player, MapPoint focus, string call)
    {
        E.AiMeetingActive = true;
        if (world.Events.Loaded)
        {
            bool allowed;
            if (retainedEvents is null)
            { allowed = ScenarioPermissions.AllowNegotiation(world, a, b, executeEvent); }
            else
            {
                using var permission = retainedEvents.BeginNegotiation(a, b, call + "/permission");
                while (permission.Allowed is null)
                {
                    var pending = permission.Resume(op.TakeReply());
                    if (permission.Allowed is null)
                    { yield return pending is null ? null : new DiplomaticAudienceDecision.Scenario(pending); }
                }
                op.Report(permission.PresentationFailures);
                allowed = permission.Allowed.Value;
            }
            if (!allowed)
            { E.AiMeetingActive = false; op.ScenarioDenied = true; yield break; }
        }
        E.SelectedPair = (b, a);
        E.GiftsThisMeeting = 0;
        if (!player)
        { world.Civilizations[b].DiploRequestCount = 0; }
        E.InitialEvaluationDone = false;
        E.OpeningEvaluationActive = true;
        stance.Evaluate(a, b, call + "/stance");
        E.InitialEvaluationDone = true;
        E.OpeningEvaluationActive = false;
        E.HadCeasefire = Relation(b, a).Ceasefire;
        op.NoContact = !Relation(b, a).Contact;
        if (!player)
        {
            world.Civilizations[a].LastContactTurn[b] = world.Globals.Turn;
            DiplomaticRelations.Clear(world, a, b, new() { Ceasefire = true, Tribute = true });
        }
        else
        {
            if (world.Civilizations[b].DiploRequestCount < 0)
            { world.Civilizations[b].DiploRequestCount = 0; }
            if (Relation(a, b).Aggressor)
            { world.Civilizations[b].DiploRequestCount = unchecked((sbyte)stance.Patience(a, b)); }
            if (Relation(a, b).Incident)
            { world.Civilizations[b].DiploRequestCount = 0; DiplomaticRelations.Clear(world, a, b, new() { WarThisTurn = true }); }
            else if (world.Globals.Difficulty > Difficulty.King && world.Civilizations[a].SpaceshipFlags.Launched
                && world.Civilizations[b].SpaceshipFlags.Launched && world.Civilizations[a].SpaceshipArrivalYear < world.Civilizations[b].SpaceshipArrivalYear
                && Relation(b, a).War)
            { DiplomaticRelations.Set(world, a, b, new() { WarThisTurn = true }); }
        }
        E.EmissaryResponse = player ? 1 : 0;
        int requests = world.Civilizations[b].DiploRequestCount;
        if (player && (Relation(b, a).WarThisTurn || requests >= stance.Patience(a, b) && Relation(a, b).MetThisTurn))
        {
            bool allied = !Relation(b, a).WarThisTurn && Relation(b, a).Alliance;
            Show(allied ? AudienceNoticeKind.OutaHereAlly : AudienceNoticeKind.OutaHere, a, b, allied ? 3 : 4);
            op.Ended = op.Stop = true;
        }
        else
        {
            foreach (var d in Ask(op, a, b, focus, call + "/emissary"))
            { yield return d; }
            DiplomaticRelations.Set(world, a, b, new() { MetThisTurn = true });
            op.EmissarySent = true;
            op.Hostile = Relation(b, a).CityCaptured;
            DiplomaticRelations.Clear(world, a, b, new() { CityCaptured = true });
            if (Historical && (a == 3 && b is 6 or 1 || b == 3 && a is 6 or 1))
            {
                E.WarTarget = 0;
                E.BestTechnology = null;
                op.Hostile = false;
                if (a == 6 || b == 6)
                { E.Demand = 0; }
            }
            using (var exchange = technology.Begin(a, b, 1, focus, call + "/exchange"))
            {
                while (!exchange.Completed)
                {
                    var d = exchange.Resume(op.TakeReply());
                    if (!exchange.Completed)
                    { yield return d is null ? null : new DiplomaticAudienceDecision.Technology(d); }
                }
                op.Report(exchange.PresentationFailures);
            }
            if (!E.Aggressive && !player && (Relation(b, a).Ceasefire || Relation(b, a).Peace) && E.Fear > 2)
            { op.Stop = true; }
            else
            {
                if (E.WarTarget is > 0 and < EntityLimits.Civilizations and var target && !world.Globals.CivsAlive.Contains(target))
                { E.WarTarget = null; }
                if (Relation(b, a).Alliance && E.RaceThreat && E.OutranksTarget)
                { E.WarTarget = null; }
                foreach (var d in Requests(op, a, b, focus, call))
                { yield return d; }
                foreach (var d in Threats(op, a, b, player, focus, call))
                { yield return d; }
            }
        }
        OpeningTreatyResult result = new(false, false, false, false);
        if (!op.Stop)
        {
            using var opening = treaties.Begin(a, b, focus, new(op.DeclaredWar, op.Paid, op.NoContact, op.Hostile), call + "/treaties");
            while (!opening.Completed)
            {
                var d = opening.Resume(op.TakeReply());
                if (!opening.Completed)
                { yield return d is null ? null : new DiplomaticAudienceDecision.Treaty(d); }
            }
            op.Report(opening.PresentationFailures);
            result = opening.Result!;
            op.Stop = result.EndsAudience;
        }
        var conclusion = new AudienceConclusion(result.TreatyMade, op.Paid, op.NoContact, result.Conceded, op.Hostile, op.EmissarySent, op.Ended);
        using var end = op.Stop ? conversation.BeginCleanup(a, b, player, conclusion, call + "/conversation")
            : conversation.Begin(a, b, player, focus, conclusion, call + "/conversation");
        while (!end.Completed)
        {
            var d = end.Resume(op.TakeReply());
            if (!end.Completed)
            { yield return d is null ? null : new DiplomaticAudienceDecision.Conversation(d); }
        }
        op.Report(end.PresentationFailures);
    }
    private IEnumerable<DiplomaticAudienceDecision?> Requests(Operation op, int a, int b, MapPoint focus, string call)
    {
        if (E.WarTarget is > 0 && (E.RequestMode != 0 || Relation(b, a).Alliance))
        {
            foreach (var d in Ask(op, a, b, focus, call + "/war-emissary"))
            { yield return d; }
            if (op.Accepted)
            {
                int t = Required(E.WarTarget);
                var relation = Relation(a, t);
                yield return Offer(AudienceOfferKind.JoinWar, a, b, relation.Alliance ? 0 : relation.Peace ? 1 : 2, target: t);
                if (op.Answer() == 0)
                {
                    if (!Relation(b, a).Alliance)
                    { DiplomaticAttitudes.Adjust(world, b, a, 5); }
                    else
                    { if (E.RequestMode != 0) { DiplomaticAttitudes.Adjust(world, b, a, 20); } op.Propose = true; }
                    Request(b, 1);
                }
                else
                {
                    war.Declare(a, Required(E.WarTarget), b);
                    op.DeclaredWar = true;
                    E.Aggressive = false;
                    E.StrongestEnemy = E.WarTarget = null;
                    if (!Relation(b, a).Alliance)
                    {
                        if (random.PrimaryModulo(new ChoiceKey("V05.audience-war-payment", call), 2) != 0)
                        { E.Demand = 0; RestoreCeasefire(a, b); }
                    }
                    else
                    { E.Demand = 0; op.Paid = true; }
                }
            }
        }
        if (Relation(a, b).Alliance)
        {
            if (Distrust(a, b) > 4 && !op.Paid)
            { op.Propose = true; }
            if (E.BestTechnology is >= 0)
            {
                bool fewer = world.Civilizations[b].NumAdvances < world.Civilizations[a].NumAdvances;
                bool poorer = world.Civilizations[a].Gold < world.Civilizations[b].Gold;
                if (Rank(b) < Rank(a) ? fewer || poorer : fewer && poorer)
                { E.Demand = 0; }
            }
            if (E.RaceThreat && E.OutranksTarget)
            { op.Propose = true; E.Demand = 0; }
            if (Historical && b == 6 && a == 7)
            { E.BestTechnology = null; op.Propose = true; }
        }
        op.TechGap = TechnologyGap(a, b);
        if (E.Demand > 0 && (E.RequestMode == 2 || op.Propose) && E.Demand <= world.Civilizations[a].Gold)
        {
            foreach (var d in Ask(op, a, b, focus, call + "/tribute-emissary"))
            { yield return d; }
            if (op.Accepted)
            {
                if (Relation(b, a).Alliance)
                { int cap = Math.Max(world.Civilizations[a].Gold / 2, 100); if (cap <= E.Demand) { E.Demand = cap; } }
                int variant = random.SecondaryRange(new ChoiceKey("V05.audience-tribute", call), 0, 3);
                if (variant == 3 && Strength(b) < Strength(a))
                { variant = 2; }
                if (variant == 2 && world.Globals.FirstDiscoverer[AdvanceIds.Invention] == 0)
                { variant = 1; }
                if (op.TechGap)
                { variant = 8; }
                if (Relation(b, a).Alliance)
                { variant = 9; }
                yield return Offer(AudienceOfferKind.Tribute, a, b, variant, gold: E.Demand);
                if (op.Answer() == 1)
                {
                    world.Civilizations[a].Gold = unchecked(world.Civilizations[a].Gold - E.Demand);
                    world.Civilizations[b].Gold = unchecked(world.Civilizations[b].Gold + E.Demand);
                    E.Aggressive = false;
                    op.Paid = true;
                    op.Present(effects.RefreshStatus);
                    if (world.Civilizations[b].DiploRequestCount != 0)
                    { Request(b, -1); }
                    DiplomaticAttitudes.Adjust(world, b, a, unchecked(-DiplomaticGifts.GoldValue(E.Demand)));
                    RestoreCeasefire(a, b);
                }
                E.Demand = 999;
            }
        }
        if (Relation(b, a).Alliance && Category > 3)
        { op.Propose = true; }
        if ((E.RequestMode != 0 || op.Propose) && E.BestTechnology is >= 0 && E.Demand != 999)
        {
            foreach (var d in Ask(op, a, b, focus, call + "/technology-emissary"))
            { yield return d; }
            if (op.Accepted)
            {
                int variant = random.SecondaryRange(new ChoiceKey("V05.audience-technology", call), 0, 2);
                if (variant < 2 && op.TechGap)
                { variant = 8; }
                if (Relation(b, a).Alliance)
                { variant = 9; }
                yield return Offer(AudienceOfferKind.Technology, a, b, variant, E.BestTechnology);
                if (op.Answer() == 1)
                {
                    foreach (var d in Grant(op, b, Required(E.BestTechnology), a, call + "/technology"))
                    { yield return d; }
                    ValueGift(a, b);
                    E.BestTechnology = E.SecondTechnology;
                    E.SecondTechnology = null;
                    if (E.NearbyUnits == 0 || !E.Aggressive || E.BestTechnology is null)
                    { op.Paid = true; }
                    else
                    {
                        yield return Offer(AudienceOfferKind.OverABarrel, a, b, advance: E.BestTechnology);
                        if (op.Answer() == 1)
                        {
                            foreach (var d in Grant(op, b, Required(E.BestTechnology), a, call + "/second-technology"))
                            { yield return d; }
                            ValueGift(a, b);
                            E.BestTechnology = null;
                            op.Paid = true;
                        }
                    }
                    if (op.Paid)
                    { E.Aggressive = false; RestoreCeasefire(a, b); if (world.Civilizations[b].DiploRequestCount != 0) { Request(b, -1); } }
                }
                else if (Relation(b, a).Alliance)
                { DiplomaticAttitudes.Adjust(world, b, a, unchecked(Distrust(a, b) + 2)); Request(b, 1); }
                E.Demand = 999;
            }
        }
    }
    private IEnumerable<DiplomaticAudienceDecision?> Threats(Operation op, int a, int b, bool player, MapPoint focus, string call)
    {
        if (Relation(b, a).Alliance && !op.Paid && (E.Aggressive || E.Attitude > 49 || E.RaceThreat && E.OutranksTarget))
        {
            foreach (var d in Ask(op, a, b, focus, call + "/dismiss-emissary"))
            { yield return d; }
            Show(AudienceNoticeKind.Begone, a, b, variant: E.Demand == 999 ? 0 : 1);
            war.CancelAlliance(b, a);
            world.Civilizations[a].LastContactTurn[b] = world.Globals.Turn;
            if (E.RaceThreat && world.Globals.Difficulty > Difficulty.King)
            { E.DeclareWar = true; }
            op.Stop = true;
            yield break;
        }
        op.Hostile = op.Hostile && E.Aggressive;
        if (op.Hostile)
        { E.Aggressive = false; }
        if (op.NoContact && !op.Paid && E.Demand == 999 && !E.Aggressive && Category > (E.InitiallyAggressive ? 2 : 4)
            && !Wonder(a, WonderIds.GreatWall) && !Wonder(a, WonderIds.UnitedNations))
        {
            int roll = random.PrimaryModulo(new ChoiceKey("V05.audience-reconsider", call), 2);
            if (unchecked((byte)world.Civilizations[b].Government) < 6 - roll)
            { E.Aggressive = true; }
        }
        AudienceNoticeKind? threat = null;
        int variant = 0;
        if (E.Aggressive && E.Demand > 50 && (Relation(b, a).Peace || !DiplomaticAttitudes.Hostile(world, b, a) && E.LastContact > 0 && !Relation(b, a).Incident))
        {
            foreach (var d in Ask(op, a, b, focus, call + "/provoke-emissary"))
            { yield return d; }
            threat = AudienceNoticeKind.Provoke;
            variant = random.SecondaryRange(new ChoiceKey("V05.audience-provoke", call), 0, 2);
        }
        else if (E.Aggressive && E.Demand == 999)
        { threat = AudienceNoticeKind.Reject; }
        else if (E.Aggressive && (E.LastContact < 0 || Relation(b, a).Incident || player))
        {
            foreach (var d in Ask(op, a, b, focus, call + "/rid-emissary"))
            { yield return d; }
            threat = AudienceNoticeKind.Rid;
        }
        if (threat is { } kind)
        {
            DiplomaticRelations.Set(world, a, b, new() { War = true });
            if (DiplomaticAttitudes.Get(world, b, a) < 75)
            { DiplomaticAttitudes.Set(world, b, a, 75); }
            Show(kind, a, b, variant: variant);
            world.Civilizations[a].LastContactTurn[b] = world.Globals.Turn;
            op.Stop = true;
            yield break;
        }
        if (E.Aggressive)
        { op.Stop = true; yield break; }
        if (E.SuppressedAggression && !Relation(b, a).Alliance)
        {
            foreach (var d in Ask(op, a, b, focus, call + "/suppressed-emissary"))
            { yield return d; }
            if (op.Accepted)
            { Show(Wonder(a, WonderIds.GreatWall) ? AudienceNoticeKind.AccursedWall : AudienceNoticeKind.AccursedUN, a, b); }
        }
    }
    private void RestoreCeasefire(int a, int b) { if (E.HadCeasefire) { DiplomaticRelations.Set(world, a, b, new() { Ceasefire = true }); } }
    private void ValueGift(int a, int b) => DiplomaticAttitudes.Adjust(world, b, a,
        unchecked(-(AdvanceValuation.Value(world, rules, b, Required(E.BestTechnology)) * 3 / 2)));
    private IEnumerable<DiplomaticAudienceDecision?> Ask(Operation op, int a, int b, MapPoint focus, string call)
    {
        using var child = emissaries.Begin(a, b, focus, call);
        while (child.Accepted is null)
        { var d = child.Resume(op.TakeReply()?.Selected); if (d is not null) { yield return new DiplomaticAudienceDecision.Emissary(d); } }
        op.Accepted = child.Accepted.Value;
        op.Report(child.PresentationFailures);
    }
    private IEnumerable<DiplomaticAudienceDecision?> Grant(Operation op, int c, int advance, int source, string call)
    {
        using var child = grants.Begin(c, advance, source, false, call);
        while (!child.Completed)
        { var d = child.Resume(op.TakeReply()?.Selected); if (!child.Completed) { yield return d is null ? null : new DiplomaticAudienceDecision.Grant(d); } }
        op.Report(child.PresentationFailures);
    }
}
