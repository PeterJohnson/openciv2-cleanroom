using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

[Flags]
public enum PeaceReasons { None = 0, Ordinary = 1, DangerousEnemy = 2, ExistingAlliedTarget = 4, Wonder = 8 }
public abstract record TreatyNegotiationDecision
{
    public sealed record Technology(int Recipient, DiplomaticParty Partner, bool Alliance, ResearchOption Advance, int Variant) : TreatyNegotiationDecision;
    public sealed record Gold(int Recipient, DiplomaticParty Partner, bool Alliance, int Price, int Variant) : TreatyNegotiationDecision;
    public sealed record Solidarity(int Recipient, DiplomaticParty Partner, bool Alliance, DiplomaticParty Target) : TreatyNegotiationDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : TreatyNegotiationDecision;
}
public enum TreatyNegotiationNoticeKind { NoSmall, NoThanks, NoPatience, NoDislike, NoWinning, NoBetrayWeak, NoBetray, ThanksAnyway, Goodbye }
public sealed record TreatyNegotiationNotice(TreatyNegotiationNoticeKind Kind, int Recipient, DiplomaticParty Partner,
    bool Alliance, int Reaction);
public interface ITreatyNegotiationEffects
{
    void Notice(TreatyNegotiationNotice notice);
    void RefreshStatus();
}

/// <summary>V05 §5 alliance/peace bargaining, real A08 willingness, V03 payments and V04 treaties/war.</summary>
public sealed class TreatyNegotiation(GameState world, RuleState rules, GameplayRandom random,
    DiplomaticStance stance, ResearchGrants grants, NegotiatedTreaties treaties, DiplomaticWar war, ITreatyNegotiationEffects effects)
{
    private GameState World => world;
    private DiplomacyWorkspace E => world.Diplomacy;
    private int Distrust(int a, int b) => world.Civilizations[a].Reputation - world.Civilizations[b].ReputationCredit[a];
    private int Rank(int c) => world.Globals.PowerRank[c];
    private int Strength(int c) => world.Civilizations[c].MilitaryStrength;
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules.Definitions, c, w);
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private ResearchOption Advance(int a) => new(a, rules.Definitions.Advances[a].Identity.Name);
    private static int Required(int? a) => a ?? throw new InvalidOperationException("The treaty negotiation choice is absent");
    private void Request(int b) => world.Civilizations[b].DiploRequestCount = unchecked((sbyte)(world.Civilizations[b].DiploRequestCount + 1));
    private void Notice(TreatyNegotiationNoticeKind kind, int a, int b, bool alliance, int reaction = 3) =>
        effects.Notice(new(kind, a, Party(b), alliance, reaction));

    public PeaceReasons PeaceWillingness(int a, int b)
    {
        MapKnowledge.RequireLocal(world);
        PeaceReasons reasons = !E.TargetIsLeader && E.Attitude < 51
            && Distrust(a, b) <= 4 + (world.Globals.Difficulty == Difficulty.Chieftain ? 2 : 0) ? PeaceReasons.Ordinary : PeaceReasons.None;
        if (E.WarTarget is > 0 and var t && !DiplomaticAttitudes.Hostile(world, a, t) && world.Civilizations[a].Treaties[t].Contact)
        {
            uint threshold = world.Civilizations[a].Treaties[t].Peace ? (uint)(3 * Strength(b) / 2) : (uint)(2 * Strength(b));
            if (world.Civilizations[b].Treaties[t].Fighting && (uint)Strength(t) >= threshold)
            { reasons = PeaceReasons.Ordinary | PeaceReasons.DangerousEnemy; }
        }
        if (E.WarTarget is > 0 and var allied && world.Civilizations[a].Treaties[allied].Alliance)
        { reasons |= PeaceReasons.ExistingAlliedTarget; }
        if (world.Civilizations[b].Treaties[a].Vendetta && Distrust(a, b) > 2 - rules.Leaders[world.Civilizations[b].Tribe].Attack
            || Distrust(a, b) > 6 || E.Attitude > 75)
        { reasons = PeaceReasons.None; }
        if (Wonder(a, WonderIds.GreatWall) || Wonder(a, WonderIds.UnitedNations))
        { reasons |= PeaceReasons.Wonder; }
        return reasons;
    }

    public int GoldPrice(int a, int b, bool alliance)
    {
        unchecked
        {
            int q = ((world.Civilizations[a].Gold / 50) * Strength(b)) / (Strength(b) + Strength(a) + 1)
                + world.Civilizations[a].Reputation * (alliance ? 2 : 1);
            int price = q * (Strength(b) < Strength(a) ? 100 : 50);
            while (price > world.Civilizations[a].Gold)
            { price -= 50; }
            return price;
        }
    }
    private bool WantsPayment(int a, int b, bool alliance) => world.Civilizations[b].DiploRequestCount > 1
        || E.Attitude > (alliance ? 25 : 50) || (alliance ? world.Civilizations[a].Reputation != 0 || E.DeclareWar : Distrust(a, b) > 1);
    public Operation Begin(int actor, int other, bool alliance, string call) => new(this, actor, other, alliance, call);

    public sealed class Operation : IDisposable
    {
        private readonly TreatyNegotiation parent;
        private readonly IEnumerator<TreatyNegotiationDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? reply;
        private bool disposed, faulted;
        public bool Completed { get; private set; }
        public bool EndsAudience { get; internal set; }
        public TreatyNegotiationDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(TreatyNegotiation parent, int a, int b, bool alliance, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Run(this, a, b, alliance, call).GetEnumerator();
        }
        internal ResearchReply? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        public TreatyNegotiationDecision? Resume(ResearchReply? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed treaty negotiation cannot resume"); }
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

    private IEnumerable<TreatyNegotiationDecision?> Run(Operation op, int a, int b, bool alliance, string call)
    {
        AllianceReasons allianceReasons = AllianceReasons.None;
        PeaceReasons peaceReasons = PeaceReasons.None;
        bool willing = alliance ? stance.AllianceWilling(a, b, ref allianceReasons) : (peaceReasons = PeaceWillingness(a, b)) != PeaceReasons.None;
        if (!willing)
        { Refuse(a, b, alliance); yield break; }
        bool dangerous = alliance ? allianceReasons.HasFlag(AllianceReasons.DangerousEnemy) : peaceReasons.HasFlag(PeaceReasons.DangerousEnemy);
        bool allied = alliance ? allianceReasons.HasFlag(AllianceReasons.ExistingAlliedTarget) : peaceReasons.HasFlag(PeaceReasons.ExistingAlliedTarget);
        bool paid = false;
        if (WantsPayment(a, b, alliance) && ((!dangerous && !allied) || alliance && E.DeclareWar) && E.BestTechnology is > 0)
        {
            int variant = random.SecondaryRange(new ChoiceKey("V05.treaty-secret", call), 0, 1);
            yield return new TreatyNegotiationDecision.Technology(a, Party(b), alliance, Advance(Required(E.BestTechnology)), variant);
            if (op.TakeReply()!.Value.Selected == 0)
            { Reject(); }
            else
            {
                paid = true;
                foreach (var decision in Grant(op, b, Required(E.BestTechnology), a, call + "/technology"))
                { yield return decision; }
                int benefit = unchecked(2 * AdvanceValuation.Value(world, rules, b, Required(E.BestTechnology)));
                DiplomaticAttitudes.Adjust(world, b, a, unchecked(-benefit));
                E.BestTechnology = E.SecondTechnology;
                E.SecondTechnology = null;
            }
        }
        if (WantsPayment(a, b, alliance) && (alliance ? !dangerous || E.DeclareWar : !allied) && willing && !paid)
        {
            int price = GoldPrice(a, b, alliance);
            if (price > 0)
            {
                int variant = random.SecondaryRange(new ChoiceKey("V05.treaty-gold", call), 0, 1);
                yield return new TreatyNegotiationDecision.Gold(a, Party(b), alliance, price, variant);
                if (op.TakeReply()!.Value.Selected == 0)
                { Reject(); }
                else
                {
                    world.Civilizations[a].Gold = unchecked(world.Civilizations[a].Gold - price);
                    world.Civilizations[b].Gold = unchecked(world.Civilizations[b].Gold + price);
                    paid = true;
                    DiplomaticAttitudes.Adjust(world, b, a, unchecked(-DiplomaticGifts.GoldValue(price)));
                    op.Present(effects.RefreshStatus);
                }
            }
        }
        if (!willing)
        { Notice(TreatyNegotiationNoticeKind.NoPatience, a, b, alliance); yield break; }
        if (E.WarTarget is > 0 and var target)
        {
            yield return new TreatyNegotiationDecision.Solidarity(a, Party(b), alliance, Party(target));
            if (op.TakeReply()!.Value.Selected == 0)
            {
                bool abandon = alliance ? allianceReasons != AllianceReasons.None || Rank(a) == 7 || Distrust(a, b) > 2 || E.DeclareWar
                    : dangerous || allied || Rank(a) == 7 || Distrust(a, b) > 3;
                if (abandon)
                {
                    Request(b);
                    Notice(paid ? TreatyNegotiationNoticeKind.ThanksAnyway : TreatyNegotiationNoticeKind.Goodbye, a, b, alliance);
                    op.EndsAudience = !paid;
                    yield break;
                }
            }
            else
            {
                war.Declare(a, Required(E.WarTarget), b);
                if (alliance)
                {
                    if (allianceReasons.HasFlag(AllianceReasons.NewCommonTarget))
                    {
                        DiplomaticRelations.Set(world, b, Required(E.WarTarget), new() { Contact = true, War = true });
                        DiplomaticRelations.Set(world, a, Required(E.WarTarget), new() { Contact = true });
                    }
                    world.Civilizations[b].Treaties[Required(E.WarTarget)].Aggressor = true;
                    world.Civilizations[b].Treaties[Required(E.WarTarget)].ExtendedAggressor = true;
                }
                E.WarTarget = null;
            }
        }
        if (alliance)
        { treaties.FormAlliance(a, b, call + "/alliance"); }
        else
        { treaties.SignPeace(a, b, call + "/peace"); }

        void Reject()
        {
            if (alliance)
            { willing = false; }
            else
            { peaceReasons &= PeaceReasons.Wonder; willing = peaceReasons != PeaceReasons.None; dangerous = allied = false; }
        }
    }

    private void Refuse(int a, int b, bool alliance)
    {
        bool vendetta = world.Civilizations[b].Treaties[a].Vendetta;
        int distrust = Distrust(a, b), category = DiplomaticAttitudes.Category(E.Attitude);
        if (!vendetta && distrust < (alliance ? 3 : 5) && (alliance ? E.CapturedCities == 0 : E.CapturedCities < 2))
        {
            TreatyNegotiationNoticeKind kind;
            if (alliance)
            {
                if (world.Civilizations[a].CityCount < 4 || Rank(a) != 7 && (Rank(a) - Rank(b) < 3 || Rank(a) < 5))
                {
                    kind = category >= 4 ? TreatyNegotiationNoticeKind.NoDislike : Rank(a) < Rank(b) ? TreatyNegotiationNoticeKind.NoSmall
                    : world.Civilizations[b].DiploRequestCount < 3 ? TreatyNegotiationNoticeKind.NoThanks : TreatyNegotiationNoticeKind.NoPatience;
                }
                else
                { kind = TreatyNegotiationNoticeKind.NoWinning; }
            }
            else
            {
                kind = E.TargetIsLeader || Rank(a) > Rank(b) + 2 ? TreatyNegotiationNoticeKind.NoWinning
                : E.Attitude < 51 && (Rank(b) <= Rank(a) || E.Attitude < 26) ? TreatyNegotiationNoticeKind.NoPatience : TreatyNegotiationNoticeKind.NoDislike;
            }
            Notice(kind, a, b, alliance, kind is TreatyNegotiationNoticeKind.NoDislike or TreatyNegotiationNoticeKind.NoWinning ? 4 : 3);
            return;
        }
        Request(b);
        // category(Boolean) is 1 for both inputs, independently of the full attitude category.
        bool weak = !vendetta && distrust > (alliance ? 2 : 4)
            && (E.Fear != 0 || DiplomaticAttitudes.Category(E.Attitude < 2 ? 1 : 0) != 0);
        Notice(weak ? TreatyNegotiationNoticeKind.NoBetrayWeak : TreatyNegotiationNoticeKind.NoBetray, a, b, alliance, weak ? 3 : 4);
    }
    private IEnumerable<TreatyNegotiationDecision?> Grant(Operation op, int c, int a, int source, string call)
    {
        using var grant = grants.Begin(c, a, source, false, call);
        while (!grant.Completed)
        {
            var decision = grant.Resume(op.TakeReply()?.Selected);
            if (!grant.Completed)
            { yield return decision is null ? null : new TreatyNegotiationDecision.Grant(decision); }
        }
        op.Report(grant.PresentationFailures);
    }
}
