using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record OpeningTreatyInputs(bool DeclaredWar, bool Paid, bool NoContact, bool Hostile);
public sealed record OpeningTreatyResult(bool TreatyMade, bool Conceded, bool SenateBlocked, bool EndsAudience);
public enum OpeningTreatyOfferKind { Alliance, Solidarity, GrantCease, WallCease, Ceasefire, Technology, Gold, Grovel, Peace, GoldForPeace }
public sealed record OpeningTreatyOffer(OpeningTreatyOfferKind Kind, int Recipient, DiplomaticParty Partner,
    int Reaction = 0, DiplomaticParty? Target = null, ResearchOption? Advance = null, int Gold = 0);
public abstract record OpeningTreatyDecision
{
    public sealed record Offer(OpeningTreatyOffer Value) : OpeningTreatyDecision;
    public sealed record Emissary(EmissaryDecision Value) : OpeningTreatyDecision;
    public sealed record Grant(ResearchGrantDecision Value) : OpeningTreatyDecision;
}
public enum OpeningTreatyNoticeKind
{
    DidNotProve, ContinueUN, ContinueHawks, OverruleCease, WallOverCease, UNOverCease,
    ContinueWar, Worthless, OverrulePeace, WallOverPeace, UNOverPeace, NoPeace
}
public sealed record OpeningTreatyNotice(OpeningTreatyNoticeKind Kind, int Recipient, DiplomaticParty Partner, int Reaction);
public interface IOpeningTreatyEffects
{
    void Notice(OpeningTreatyNotice notice);
    void RefreshStatus();
}

/// <summary>V05 §6 opening alliance, ceasefire and peace offers. Distinct from player proposal
/// bargaining; actual V03 grants and V04 war/treaty children retain their decisions and effects.</summary>
public sealed class OpeningTreaties(GameState world, RuleTables rules, GameplayRandom random,
    EmissaryReception emissaries, AttackConfirmation senate, ResearchGrants grants,
    DiplomaticWar war, NegotiatedTreaties treaties, IOpeningTreatyEffects effects)
{
    private GameState World => world;
    private DiplomacyWorkspace E => world.Diplomacy;
    private Relationship Relation(int a, int b) => world.Civilizations[a].Treaties[b];
    private int Rank(int c) => world.Globals.PowerRank[c];
    private int Distrust(int a, int b) => world.Civilizations[a].Reputation - world.Civilizations[b].ReputationCredit[a];
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules, c, w);
    private bool Knows(int c, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, advance);
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private static int Required(int? value) => value ?? throw new InvalidOperationException("The live opening treaty operand is absent");
    private void Show(OpeningTreatyNoticeKind kind, int a, int b, int reaction = 0) => effects.Notice(new(kind, a, Party(b), reaction));
    private OpeningTreatyDecision Offer(OpeningTreatyOfferKind kind, int a, int b, int reaction = 0,
        int? target = null, int? advance = null, int gold = 0) => new OpeningTreatyDecision.Offer(new(kind, a, Party(b), reaction,
            target is { } c ? Party(c) : null, advance is { } t ? new(t, rules.Advances[t].Identity.Name) : null, gold));

    public int PeaceThreshold(int a, int b)
    {
        uint own = world.Civilizations[a].MilitaryStrength, other = world.Civilizations[b].MilitaryStrength;
        int threshold = 3;
        if (unchecked(2 * own) < other)
        { threshold = 2; }
        if (unchecked(3 * own) / 2 < other)
        { --threshold; }
        if (other < own)
        { ++threshold; }
        if (unchecked(2 * other) < own)
        { ++threshold; }
        return threshold;
    }
    public int ConcessionGold(int b)
    {
        int gold = unchecked(Arithmetic.ClampAB(unchecked(E.Fear * 2 - 4), 0, world.Civilizations[b].Gold / 50) * 50);
        return Wonder(b, WonderIds.GreatWall) || Wonder(b, WonderIds.UnitedNations) ? gold / 2 : gold;
    }
    private bool ConcessionRoll(int a, int b, string phase, string call)
    {
        int d = unchecked(Distrust(a, b) + 1);
        if (Relation(b, a).Vendetta)
        { d = unchecked(d * 2); }
        return d == 1 || unchecked(d - 1) < 0
            || random.PrimaryModulo(new ChoiceKey("V05.opening-" + phase + "-concession", call), d) == 0;
    }
    private void Hawks(int a, int b)
    {
        if (world.Civilizations[a].Government > Government.Fundamentalism
            && !(world.Globals.Status.Scenario && world.Scenario.Flags.RevolutionRestriction) && !world.Multiplayer.Saved.AutoplayActive)
        { Show(Wonder(a, WonderIds.UnitedNations) ? OpeningTreatyNoticeKind.ContinueUN : OpeningTreatyNoticeKind.ContinueHawks, a, b); }
    }
    public Operation Begin(int actor, int other, MapPoint? focus, OpeningTreatyInputs inputs, string call) => new(this, actor, other, focus, inputs, call);
    public sealed class Operation : IDisposable
    {
        private readonly OpeningTreaties parent;
        private readonly IEnumerator<OpeningTreatyDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        internal bool TreatyMade, Conceded, SenateBlocked, EndsAudience, AcceptedEmissary;
        public bool Completed { get; private set; }
        public OpeningTreatyResult? Result { get; private set; }
        public OpeningTreatyDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(OpeningTreaties parent, int a, int b, MapPoint? focus, OpeningTreatyInputs inputs, string call)
        {
            ArgumentNullException.ThrowIfNull(inputs);
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Run(this, a, b, focus, inputs, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var value = reply; reply = null; return value; }
        internal int Answer() => TakeReply()!.Value.Selected;
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public OpeningTreatyDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed opening treaty cannot resume"); }
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
                Result = new(TreatyMade, Conceded, SenateBlocked, EndsAudience);
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<OpeningTreatyDecision?> Run(Operation op, int a, int b, MapPoint? focus, OpeningTreatyInputs inputs, string call)
    {
        foreach (var decision in Alliance(op, a, b, focus, inputs, call))
        { yield return decision; }
        foreach (var decision in Ceasefire(op, a, b, focus, inputs, call))
        { yield return decision; }
        if (op.EndsAudience)
        { yield break; }
        foreach (var decision in Peace(op, a, b, focus, inputs, call))
        { yield return decision; }
    }
    private IEnumerable<OpeningTreatyDecision?> Alliance(Operation op, int a, int b, MapPoint? focus, OpeningTreatyInputs inputs, string call)
    {
        if (!Relation(b, a).Peace || Relation(b, a).Alliance || E.TargetIsLeader)
        { yield break; }
        int allies = 0;
        for (int c = 1; c < EntityLimits.Civilizations; ++c)
        { if (Relation(a, c).Alliance) { ++allies; } }
        bool propose = Rank(a) < 6 && Rank(b) < 6;
        if (propose && (E.WarTarget ?? -1) < 1 && world.Globals.Turn > 200)
        {
            int? lowest = null;
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                if (c == a || c == b || Rank(c) < Rank(a) || Rank(c) < Rank(b)
                    || !Relation(a, c).Contact || Relation(a, c).War || Relation(b, c).Alliance || Relation(b, c).War)
                { continue; }
                if (lowest is null || Rank(c) <= lowest.Value)
                { lowest = Rank(c); E.WarTarget = c; }
            }
        }
        bool gap = E.WarTarget is > 0 and var t && (Relation(a, t).Ceasefire || Relation(a, t).Peace || Relation(a, t).Alliance);
        if (!(E.Fear != 0 || propose || gap) || !(E.PeaceInclination > 1 || propose) || allies != 0
            || !(E.Attitude < 25 || E.Attitude < 50 && gap) || !(gap || inputs.DeclaredWar || propose)
            || Distrust(a, b) >= 4 || Relation(b, a).Ceasefire || E.CapturedCities >= 2 || !(Rank(a) < 6 || Rank(b) < 5))
        { yield break; }
        foreach (var d in Ask(op, a, b, focus, call + "/alliance-emissary"))
        { yield return d; }
        if (!op.AcceptedEmissary)
        { yield break; }
        yield return Offer(OpeningTreatyOfferKind.Alliance, a, b);
        if (op.Answer() != 0)
        { yield break; }
        propose = true;
        if (E.WarTarget is >= 0)
        {
            propose = false;
            yield return Offer(OpeningTreatyOfferKind.Solidarity, a, b, 3, target: E.WarTarget);
            if (op.Answer() == 1)
            {
                war.Declare(a, Required(E.WarTarget), b);
                world.Civilizations[b].Treaties[Required(E.WarTarget)].Aggressor = true;
                world.Civilizations[b].Treaties[Required(E.WarTarget)].ExtendedAggressor = true;
                E.WarTarget = null;
                propose = true;
            }
            else
            {
                DiplomaticAttitudes.Adjust(world, b, a, 10);
                if (E.Fear == 0 || Distrust(a, b) > 2 || E.Attitude > 25)
                { Show(OpeningTreatyNoticeKind.DidNotProve, a, b, 4); }
            }
        }
        if (propose)
        { treaties.FormAlliance(a, b, call + "/alliance"); }
        op.TreatyMade = true;
    }
    private IEnumerable<OpeningTreatyDecision?> Ceasefire(Operation op, int a, int b, MapPoint? focus, OpeningTreatyInputs inputs, string call)
    {
        if (!Relation(b, a).War)
        { yield break; }
        foreach (var d in Ask(op, a, b, focus, call + "/ceasefire-emissary"))
        { yield return d; }
        if (!op.AcceptedEmissary)
        { yield break; }
        yield return Offer(inputs.Paid ? OpeningTreatyOfferKind.GrantCease : E.SuppressedAggression || inputs.Hostile
            ? OpeningTreatyOfferKind.WallCease : OpeningTreatyOfferKind.Ceasefire, a, b);
        int choice = op.Answer();
        if (choice != 0)
        {
            if (!Wonder(b, WonderIds.GreatWall) && !Wonder(b, WonderIds.UnitedNations))
            {
                if (!senate.SenateBlocks(a, b))
                { Hawks(a, b); }
                else
                { Show(OpeningTreatyNoticeKind.OverruleCease, a, b); choice = 0; op.SenateBlocked = true; }
            }
            else
            {
                Show(Wonder(b, WonderIds.GreatWall) ? OpeningTreatyNoticeKind.WallOverCease : OpeningTreatyNoticeKind.UNOverCease, a, b);
                choice = 0;
                op.SenateBlocked = true;
            }
        }
        if (choice != 0 && ConcessionRoll(a, b, "ceasefire", call))
        {
            if (E.Fear > 3 && E.WantedTechnology is >= 0)
            {
                yield return Offer(OpeningTreatyOfferKind.Technology, a, b, 4, advance: E.WantedTechnology);
                if (op.Answer() != 0)
                {
                    choice = 0;
                    foreach (var d in Grant(op, a, Required(E.WantedTechnology), b, call + "/concession"))
                    { yield return d; }
                    op.Conceded = true;
                }
            }
            if (choice != 0)
            {
                int gold = ConcessionGold(b);
                if (gold != 0)
                {
                    yield return Offer(OpeningTreatyOfferKind.Gold, a, b, 4, gold: gold);
                    if (op.Answer() != 0)
                    { Pay(op, a, b, gold); choice = 0; }
                }
            }
            if (choice != 0 && E.Fear != 0 && world.Civilizations[b].CityCount < 2
                && !Wonder(b, WonderIds.GreatWall) && !Wonder(b, WonderIds.UnitedNations))
            {
                yield return Offer(OpeningTreatyOfferKind.Grovel, a, b, 4, gold: world.Civilizations[b].Gold);
                if (op.Answer() != 0)
                {
                    world.Civilizations[a].Gold = unchecked(world.Civilizations[a].Gold + world.Civilizations[b].Gold);
                    world.Civilizations[b].Gold = 0;
                    choice = 0;
                    op.Present(effects.RefreshStatus);
                    op.Conceded = true;
                    for (int advance = 0; advance < RuleLimits.Advances; ++advance)
                    {
                        if (!Knows(b, advance) || Knows(a, advance))
                        { continue; }
                        var rule = rules.Advances[advance];
                        if (rule.Prerequisites[0] == AdvanceReference.Never && rule.Prerequisites[1] == AdvanceReference.Never)
                        { continue; }
                        foreach (var d in Grant(op, a, advance, b, call + "/grovel/" + advance))
                        { yield return d; }
                    }
                }
            }
            if (choice == 0)
            { E.LastContact = world.Globals.Turn; }
        }
        if (choice != 0)
        {
            Show(OpeningTreatyNoticeKind.ContinueWar, a, b, 4);
            world.Civilizations[a].LastContactTurn[b] = world.Globals.Turn;
            op.EndsAudience = true;
            yield break;
        }
        op.TreatyMade = true;
        treaties.AgreeCeasefire(a, b, call + "/ceasefire");
        if (Distrust(a, b) > 6)
        { Show(OpeningTreatyNoticeKind.Worthless, a, b, 4); }
    }
    private IEnumerable<OpeningTreatyDecision?> Peace(Operation op, int a, int b, MapPoint? focus, OpeningTreatyInputs inputs, string call)
    {
        if (Relation(b, a).Peace)
        { yield break; }
        if (!(E.SuppressedAggression || (inputs.Paid || inputs.NoContact) && !Relation(b, a).Ceasefire
            || (E.Fear != 0 || DiplomaticAttitudes.Category(E.Attitude) <= PeaceThreshold(a, b)) && !E.TargetIsLeader
                && unchecked(Distrust(a, b) + E.CapturedCities) < unchecked(E.Fear + 4)))
        { yield break; }
        foreach (var d in Ask(op, a, b, focus, call + "/peace-emissary"))
        { yield return d; }
        if (!op.AcceptedEmissary)
        { yield break; }
        yield return Offer(OpeningTreatyOfferKind.Peace, a, b);
        int choice = op.Answer();
        if (choice != 0)
        {
            bool noWonder = !Wonder(b, WonderIds.GreatWall) && !Wonder(b, WonderIds.UnitedNations);
            if (noWonder || op.SenateBlocked && random.PrimaryModulo(new ChoiceKey("V05.opening-peace-wonder", call), 2) == 0)
            {
                if (!senate.SenateBlocks(a, b) || op.SenateBlocked && random.PrimaryModulo(new ChoiceKey("V05.opening-peace-senate", call), 2) == 0)
                { if (!Relation(b, a).ChangedThisTurn) { Hawks(a, b); } }
                else
                { Show(OpeningTreatyNoticeKind.OverrulePeace, a, b); choice = 0; }
            }
            else
            { Show(Wonder(b, WonderIds.GreatWall) ? OpeningTreatyNoticeKind.WallOverPeace : OpeningTreatyNoticeKind.UNOverPeace, a, b); choice = 0; }
        }
        if (choice != 0 && ConcessionRoll(a, b, "peace", call))
        {
            int gold = ConcessionGold(b);
            if (gold != 0)
            {
                yield return Offer(OpeningTreatyOfferKind.GoldForPeace, a, b, gold: gold);
                if (op.Answer() != 0)
                { Pay(op, a, b, gold); choice = 0; }
            }
        }
        op.TreatyMade = true;
        if (choice == 0)
        { treaties.SignPeace(a, b, call + "/peace"); }
        else
        { Show(OpeningTreatyNoticeKind.NoPeace, a, b, 4); }
    }
    private void Pay(Operation op, int a, int b, int gold)
    {
        world.Civilizations[a].Gold = unchecked(world.Civilizations[a].Gold + gold);
        world.Civilizations[b].Gold = unchecked(world.Civilizations[b].Gold - gold);
        op.Present(effects.RefreshStatus);
        op.Conceded = true;
    }
    private IEnumerable<OpeningTreatyDecision?> Ask(Operation op, int a, int b, MapPoint? focus, string call)
    {
        using var child = emissaries.Begin(a, b, focus, call);
        while (child.Accepted is null)
        {
            var decision = child.Resume(op.TakeReply()?.Selected);
            if (decision is not null)
            { yield return new OpeningTreatyDecision.Emissary(decision); }
        }
        op.AcceptedEmissary = child.Accepted.Value;
        op.Report(child.PresentationFailures);
    }
    private IEnumerable<OpeningTreatyDecision?> Grant(Operation op, int c, int advance, int source, string call)
    {
        using var child = grants.Begin(c, advance, source, false, call);
        while (!child.Completed)
        {
            var decision = child.Resume(op.TakeReply()?.Selected);
            if (!child.Completed)
            { yield return decision is null ? null : new OpeningTreatyDecision.Grant(decision); }
        }
        op.Report(child.PresentationFailures);
    }
}
