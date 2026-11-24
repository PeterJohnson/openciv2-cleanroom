using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>Retained outcomes of V05 §6's opening. These are supplied by the audience owner.</summary>
public sealed record AudienceConclusion(bool TreatyMade, bool Paid, bool NoContact, bool Conceded,
    bool Hostile, bool EmissarySent, bool Ended);
public enum ConversationNoticeKind
{
    ContinueUN, ContinueHawks, SenateCease, WallPeace, UNPeace, WallCease, UNCease,
    NotoriousBig, NotoriousSmall, NotoriousHate, AttitudeAlly, AttitudePeace, Attitude,
    WelcomeAlly, WelcomePeace, Welcome, HowdyAlly, HowdyPeace, Howdy, DoodyAlly, Doody,
    PleaseCity, PleaseCities, AllyPlea, AllyBrag, PatienceAlly, Patience, CeaseExpire, Ambassadors, SchismTutorial
}
public enum DiplomaticAddress { Stern, Mild, Fearful }
public sealed record ConversationNotice(ConversationNoticeKind Kind, int Recipient, DiplomaticParty Partner,
    int Reaction = 0, DiplomaticParty? Third = null, CitySlot? City = null, string? CityName = null,
    DiplomaticAddress Address = DiplomaticAddress.Stern, UnitType? Unit = null, string? UnitName = null);
public abstract record DiplomaticConversationDecision
{
    public sealed record Emissary(EmissaryDecision Decision) : DiplomaticConversationDecision;
    public sealed record Menu(int Recipient, DiplomaticParty Partner, OwnedList<int> Options) : DiplomaticConversationDecision;
    public sealed record Proposal(DiplomaticProposalDecision Decision) : DiplomaticConversationDecision;
    public sealed record Favor(DiplomaticFavorDecision Decision) : DiplomaticConversationDecision;
    public sealed record Gift(DiplomaticGiftDecision Decision) : DiplomaticConversationDecision;
    public sealed record Intelligence(EmissaryIntelligence Visit) : DiplomaticConversationDecision;
}

/// <summary>V05 §6 main conversation and common cleanup, using actual negotiated responses.
/// Entry/opening and S02 admission remain with the enclosing audience controller.</summary>
public sealed class DiplomaticConversation(GameState world, RuleTables rules, DiplomaticStance stance,
    EmissaryReception emissaries, AttackConfirmation senate, BorderIntrusions borders,
    DiplomaticProposals proposals, DiplomaticFavors favors, DiplomaticGifts gifts, Action<ConversationNotice> notice)
{
    private GameState World => world;
    private DiplomacyWorkspace E => world.Diplomacy;
    private Relationship Relation(int a, int b) => world.Civilizations[a].Treaties[b];
    private int Rank(int c) => world.Globals.PowerRank[c];
    private int Strength(int c) => world.Civilizations[c].MilitaryStrength;
    private int Distrust(int a, int b) => world.Civilizations[a].Reputation - world.Civilizations[b].ReputationCredit[a];
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules, c, w);
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private void Request(int c, int delta) => world.Civilizations[c].DiploRequestCount = unchecked((sbyte)(world.Civilizations[c].DiploRequestCount + delta));
    private void Contact(int a, int b, int offset = 0) => world.Civilizations[a].LastContactTurn[b] = unchecked((short)(world.Globals.Turn + offset));
    private void Show(ConversationNoticeKind kind, int a, int b, int reaction = 0, int? third = null,
        CitySlot? city = null, DiplomaticAddress address = DiplomaticAddress.Stern) =>
        notice(new(kind, a, Party(b), reaction, third is { } c ? Party(c) : null, city,
            city is { } slot ? world.Entities.Cities.At(slot).Name : null, address,
            kind == ConversationNoticeKind.Ambassadors ? new UnitType(UnitTypeIds.Diplomat) : null,
            kind == ConversationNoticeKind.Ambassadors ? rules.Units[UnitTypeIds.Diplomat].Identity.Name : null));

    public Operation Begin(int actor, int other, bool playerInitiated, MapPoint focus, AudienceConclusion opening, string call) =>
        new(this, actor, other, playerInitiated, focus, opening, call, false);
    public Operation BeginCleanup(int actor, int other, bool playerInitiated, AudienceConclusion opening, string call) =>
        new(this, actor, other, playerInitiated, null, opening, call, true);

    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticConversation parent;
        private readonly IEnumerator<DiplomaticConversationDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        internal bool Ended;
        public bool Completed { get; private set; }
        public DiplomaticConversationDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticConversation parent, int a, int b, bool player, MapPoint? focus,
            AudienceConclusion opening, string call, bool cleanupOnly)
        {
            ArgumentNullException.ThrowIfNull(opening);
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            Ended = opening.Ended;
            steps = parent.Run(this, a, b, player, focus, opening, call, cleanupOnly).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Report(Exception? error) { if (error is not null) { failures.Add(error); } }
        public DiplomaticConversationDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed conversation cannot resume"); }
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

    private IEnumerable<DiplomaticConversationDecision?> Run(Operation op, int a, int b, bool player,
        MapPoint? focus, AudienceConclusion opening, string call, bool cleanupOnly)
    {
        if (!cleanupOnly && (E.AskedEmissary || player || !Relation(b, a).Peace
            && (E.Fear != 0 || Relation(b, a).War || !Relation(b, a).Contact)))
        {
            if (Distrust(a, b) > 6)
            {
                int allies = 0;
                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                { if (Relation(b, c).Alliance) { ++allies; } }
                if (Relation(b, a).Vendetta && (allies != 0 || E.TargetIsLeader || Strength(a) < Strength(b)))
                { E.DeclareWar = true; }
            }
            if (opening.NoContact)
            {
                using var reception = emissaries.Begin(a, b, focus, call + "/emissary");
                while (reception.Accepted is null)
                {
                    var decision = reception.Resume(op.TakeReply()?.Selected);
                    if (decision is not null)
                    { yield return new DiplomaticConversationDecision.Emissary(decision); }
                }
                op.Report(reception.PresentationFailures);
            }
            if (!player && (!E.AskedEmissary || E.EmissaryResponse == -1))
            { ForcedTreaty(a, b); }
            else
            {
                if (opening.TreatyMade)
                { Denounce(a, b, player); }
                else
                { Greet(a, b, player, opening.Paid); }
                BorderAndAllies(a, b);
                int round = 0;
                while (true)
                {
                    if (world.Civilizations[b].DiploRequestCount < 0)
                    { world.Civilizations[b].DiploRequestCount = 0; }
                    yield return new DiplomaticConversationDecision.Menu(a, Party(b), proposals.Options(a, b));
                    int choice = op.TakeReply()!.Value.Selected;
                    if (choice == 0)
                    { op.Ended = true; break; }
                    string childCall = call + "/round-" + round++;
                    if (choice == 7)
                    {
                        using var favor = favors.Begin(a, b, focus ?? throw new InvalidOperationException("The conversation focus is absent"), childCall + "/favor");
                        while (!favor.Completed)
                        {
                            var decision = favor.Resume(op.TakeReply());
                            if (!favor.Completed)
                            { yield return decision is null ? null : new DiplomaticConversationDecision.Favor(decision); }
                        }
                        op.Report(favor.PresentationFailures);
                    }
                    else if (choice == 8)
                    {
                        using var gift = gifts.Begin(a, b, childCall + "/gift");
                        while (!gift.Completed)
                        {
                            var decision = gift.Resume(op.TakeReply());
                            if (!gift.Completed)
                            { yield return decision is null ? null : new DiplomaticConversationDecision.Gift(decision); }
                        }
                        op.Report(gift.PresentationFailures);
                    }
                    else
                    {
                        using var proposal = proposals.BeginResponse(a, b, choice, childCall + "/proposal");
                        while (!proposal.Completed)
                        {
                            var decision = proposal.Resume(op.TakeReply());
                            if (!proposal.Completed)
                            { yield return decision is null ? null : new DiplomaticConversationDecision.Proposal(decision); }
                        }
                        op.Report(proposal.PresentationFailures);
                        if (proposal.EndsAudience)
                        { break; }
                    }
                    Request(b, 1);
                    int requests = world.Civilizations[b].DiploRequestCount;
                    if (requests < stance.Patience(a, b))
                    { continue; }
                    Show(Relation(b, a).Alliance ? ConversationNoticeKind.PatienceAlly : ConversationNoticeKind.Patience, a, b, 3);
                    break;
                }
            }
        }
        foreach (var decision in Cleanup(op, a, b, player, opening))
        { yield return decision; }
    }

    private void Hawks(int a, int b)
    {
        if (world.Civilizations[a].Government > Government.Fundamentalism
            && !(world.Globals.Status.Scenario && world.Scenario.Flags.RevolutionRestriction) && !world.Multiplayer.Saved.AutoplayActive)
        { Show(Wonder(a, WonderIds.UnitedNations) ? ConversationNoticeKind.ContinueUN : ConversationNoticeKind.ContinueHawks, a, b); }
    }
    private void ForcedTreaty(int a, int b)
    {
        if (E.DeclareWar)
        { return; }
        if (!Wonder(b, WonderIds.GreatWall) && !Wonder(b, WonderIds.UnitedNations))
        {
            if (!senate.SenateBlocks(a, b))
            { Hawks(a, b); }
            else
            {
                if (!Relation(b, a).Peace && E.Fear != 0)
                { DiplomaticRelations.Set(world, a, b, new() { Peace = true }); }
                Contact(a, b, 16);
            }
            if (Relation(b, a).War)
            { Show(ConversationNoticeKind.SenateCease, a, b); DiplomaticRelations.Set(world, a, b, new() { Ceasefire = true }); Contact(a, b); }
        }
        else if (!Relation(b, a).Peace)
        {
            if (E.Fear != 0)
            {
                Show(Wonder(b, WonderIds.GreatWall) ? ConversationNoticeKind.WallPeace : ConversationNoticeKind.UNPeace, a, b);
                DiplomaticRelations.Set(world, a, b, new() { Peace = true });
            }
            Contact(a, b, 16);
            if (Relation(b, a).War)
            {
                Show(Wonder(b, WonderIds.GreatWall) ? ConversationNoticeKind.WallCease : ConversationNoticeKind.UNCease, a, b);
                DiplomaticRelations.Set(world, a, b, new() { Ceasefire = true });
                Contact(a, b);
            }
        }
    }
    private void Denounce(int a, int b, bool player)
    {
        if (player && (Relation(b, a).Vendetta || Relation(b, a).Aggressor))
        { return; }
        int? target = null;
        int lowest = -1;
        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        {
            if (c == a || c == b || Relation(b, c).War)
            { continue; }
            int credit = world.Civilizations[a].ReputationCredit[c];
            if (credit <= lowest)
            { target = c; lowest = credit; }
        }
        if (target is not { } t)
        { return; }
        world.Civilizations[a].ReputationCredit[t] = unchecked((sbyte)(world.Civilizations[a].ReputationCredit[t] + 1));
        Show(Relation(b, t).War ? ConversationNoticeKind.NotoriousHate
            : Strength(a) < Strength(b) ? ConversationNoticeKind.NotoriousBig : ConversationNoticeKind.NotoriousSmall,
            a, b, Relation(b, t).War ? 4 : 3, t);
    }
    private void Greet(int a, int b, bool player, bool paid)
    {
        var relation = Relation(b, a);
        bool allied = relation.Alliance, peace = relation.Peace || relation.Ceasefire;
        if (E.Demand == 999 && !paid)
        { Show(allied ? ConversationNoticeKind.AttitudeAlly : peace ? ConversationNoticeKind.AttitudePeace : ConversationNoticeKind.Attitude, a, b, 4); }
        else if (!player)
        {
            var address = DiplomaticAddress.Stern;
            if (!relation.Vendetta)
            {
                if (Strength(b) < Strength(a) || DiplomaticAttitudes.Category(E.Attitude) < 4 && Distrust(a, b) < 3)
                { address = DiplomaticAddress.Mild; }
                if (E.Fear != 0)
                { address = DiplomaticAddress.Fearful; }
            }
            Show(allied ? ConversationNoticeKind.WelcomeAlly : peace ? ConversationNoticeKind.WelcomePeace : ConversationNoticeKind.Welcome, a, b, 2, address: address);
        }
        else if (world.Civilizations[b].DiploRequestCount < 2)
        { Show(allied ? ConversationNoticeKind.HowdyAlly : peace ? ConversationNoticeKind.HowdyPeace : ConversationNoticeKind.Howdy, a, b, 2); }
        else
        { Show(allied ? ConversationNoticeKind.DoodyAlly : ConversationNoticeKind.Doody, a, b, allied ? 2 : 3); }
    }
    private void BorderAndAllies(int a, int b)
    {
        if (!Relation(b, a).Peace && !Relation(b, a).Alliance && borders.Count(b, a) != 0)
        {
            var city = E.Intrusions.SelectedCity ?? throw new InvalidOperationException("A counted border intrusion requires its selected city");
            Show(city.Value < 2 ? ConversationNoticeKind.PleaseCity : ConversationNoticeKind.PleaseCities, a, b, city: city);
        }
        if (!Relation(a, b).Alliance)
        { return; }
        if (E.StrongestEnemy is > 0 && Regions(b, E.StrongestEnemy.Value))
        { Show(ConversationNoticeKind.AllyPlea, a, b, 2); }
        else if (E.WeakestEnemy is > 0 && Regions(E.WeakestEnemy.Value, b))
        { Show(ConversationNoticeKind.AllyBrag, a, b, 3); }
    }
    private bool Regions(int defender, int attacker)
    {
        int count = 0;
        for (int r = 1; r < MapLimits.Regions - 1; ++r)
        {
            if ((world.Civilizations[defender].CityCount > 1 ? 1 : 0) < world.Civilizations[defender].ContinentCities[r]
                && world.Civilizations[defender].ContinentMilitary[r] < world.Civilizations[attacker].ContinentAttack[r])
            { ++count; }
        }
        return count != 0;
    }
    private IEnumerable<DiplomaticConversationDecision?> Cleanup(Operation op, int a, int b, bool player, AudienceConclusion opening)
    {
        DiplomaticRelations.Clear(world, a, b, new() { Incident = true });
        var relation = Relation(a, b);
        world.Civilizations[b].Treaties[a].HostileIntent = E.DeclareWar && !relation.Alliance && !relation.War;
        if (!op.Ended)
        { Request(b, 1); }
        if (opening.Conceded || opening.Hostile)
        {
            int steps = 0;
            while (world.Civilizations[b].DiploRequestCount < unchecked((opening.Hostile ? 1 : 0) + stance.Patience(a, b)))
            {
                Request(b, 1);
                if (++steps == 64)
                { steps = 0; yield return null; }
            }
        }
        op.Report(emissaries.Reset());
        relation = Relation(a, b);
        if (E.HadCeasefire && !Relation(b, a).Ceasefire && !relation.Peace && !relation.Alliance && !relation.War)
        { Show(ConversationNoticeKind.CeaseExpire, a, b); }
        int local = world.Multiplayer.LocalHuman;
        if (world.Globals.Difficulty == Difficulty.Chieftain && !Relation(b, a).War && !Relation(local, b).Embassy
            && !Wonder(local, WonderIds.UnitedNations) && !Wonder(local, WonderIds.MarcoPolo)
            && CivilizationKnowledge.KnowsAdvance(world.Civilizations, b, AdvanceIds.Writing))
        {
            Show(ConversationNoticeKind.Ambassadors, a, b);
            world.Civilizations[a].Treaties[b].Embassy = true;
            yield return new DiplomaticConversationDecision.Intelligence(new(a, b));
            op.TakeReply();
        }
        if (world.Globals.GameOptions.Tutorial && opening.EmissarySent && (E.AskedEmissary || player)
            && E.EmissaryResponse != -1 && world.Civilizations[b].CityCount > 4 && Rank(a) + 1 < Rank(b) && Relation(b, a).War)
        { Show(ConversationNoticeKind.SchismTutorial, a, b); }
        E.AiMeetingActive = false;
    }
}
