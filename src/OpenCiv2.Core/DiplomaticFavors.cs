using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record MercenaryOffer(int Recipient, DiplomaticParty Partner, DiplomaticParty Target,
    int? Gold, ResearchOption? First, ResearchOption? Second);
public abstract record DiplomaticFavorDecision
{
    public sealed record Menu(int Recipient, DiplomaticParty Partner) : DiplomaticFavorDecision;
    public sealed record Target(int Recipient, OwnedList<DiplomaticParty> Options) : DiplomaticFavorDecision;
    public sealed record War(MercenaryOffer Offer) : DiplomaticFavorDecision;
    public sealed record Technology(TechnologyNegotiationDecision Decision) : DiplomaticFavorDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : DiplomaticFavorDecision;
}
public enum DiplomaticFavorNoticeKind { MapNo, NoContact, AtWar, RefusedWar, EternalAllies, Unaffordable, Declare, BetrayAlly, BetrayPeace }
public sealed record DiplomaticFavorNotice(DiplomaticFavorNoticeKind Kind, int Recipient,
    DiplomaticParty Partner, DiplomaticParty? Target, int Reaction);
public interface IDiplomaticFavorEffects
{
    void Notice(DiplomaticFavorNotice notice);
    void RefreshStatus();
    void Publish();
}

/// <summary>V05 §4 local favors, real technology/map exchanges and mercenary-war policy.</summary>
public sealed class DiplomaticFavors(GameState world, RuleState rules, GameplayRandom random,
    DiplomaticStance stance, DiplomaticTechnology technology, ResearchGrants grants,
    NegotiatedMaps maps, DiplomaticWar war, IDiplomaticFavorEffects effects)
{
    private GameState World => world;
    private DiplomacyWorkspace E => world.Diplomacy;
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private ResearchOption Advance(int a) => new(a, rules.Definitions.Advances[a].Identity.Name);
    private bool Knows(int c, int a) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, a);
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules.Definitions, c, w);
    private static int Required(int? choice) => choice ?? throw new InvalidOperationException("The mercenary technology is absent");
    private void Notice(DiplomaticFavorNoticeKind kind, int a, int b, int? t = null, int reaction = 3) =>
        effects.Notice(new(kind, a, Party(b), t is { } target ? Party(target) : null, reaction));

    public int WarPrice(int a, int b, int t)
    {
        unchecked
        {
            int price = Arithmetic.ClampAB(((world.Civilizations[a].Reputation + world.Civilizations[b].Gold / 100
                - world.Civilizations[t].Reputation - world.Civilizations[b].ReputationCredit[a])
                * (world.Civilizations[t].MilitaryStrength >> 1)) / 50, 2, 500) * 50;
            var target = world.Civilizations[b].Treaties[t];
            if (!target.Ceasefire && !target.Peace)
            { price -= price / 3; }
            if (world.Civilizations[b].Gold < world.Civilizations[a].Gold)
            { price += price / 3; }
            if (target.Alliance)
            { price = Arithmetic.ClampAB(Math.Min(price, 7000) * 3, 500, 25000); }
            if (world.Globals.PowerRank[t] == 7 && world.Civilizations[t].CityCount > 4)
            { price /= 2; }
            if (!world.Civilizations[a].Treaties[t].War)
            { price += price / 3; }
            if (world.Civilizations[b].Treaties[a].Alliance)
            {
                bool fighting = false;
                for (int c = 1; c < EntityLimits.Civilizations; ++c)
                { if (world.Civilizations[b].Treaties[c].War) { fighting = true; break; } }
                if (!fighting)
                { price -= price / 4; }
            }
            if (world.Civilizations[t].Treaties[a].SneakAttack)
            { price -= price / 4; }
            if (world.Globals.PowerRank[a] < world.Globals.PowerRank[b])
            { price -= price / 3; }
            if (world.Civilizations[t].Treaties[a].Nuked)
            {
                if (!world.Civilizations[a].Treaties[t].Nuked)
                { price /= 2; }
                if (world.Globals.PowerRank[a] < world.Globals.PowerRank[t])
                { price /= 2; }
            }
            return Arithmetic.ClampAB(price / 100, 1, 500) * 50;
        }
    }
    public int WarDistrust(int a, int b, int t)
    {
        int distrust = world.Civilizations[a].Reputation - world.Civilizations[b].ReputationCredit[a]
            - rules.Leaders[world.Civilizations[b].Tribe].Attack;
        if (world.Civilizations[b].Treaties[a].Vendetta)
        { distrust += 2; }
        if (!world.Civilizations[a].Treaties[t].War)
        { ++distrust; }
        if (world.Globals.PowerRank[t] < world.Globals.PowerRank[a])
        { ++distrust; }
        if (world.Globals.PowerRank[b] < world.Globals.PowerRank[a])
        { ++distrust; }
        return world.Civilizations[b].Treaties[a].Alliance ? distrust / 2 : distrust;
    }

    public Operation Begin(int actor, int partner, MapPoint focus, string call) => new(this, actor, partner, focus, call);
    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticFavors parent;
        private readonly IEnumerator<DiplomaticFavorDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? answer;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public DiplomaticFavorDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticFavors parent, int a, int b, MapPoint focus, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Run(this, a, b, focus, call).GetEnumerator();
        }
        internal ResearchReply? TakeAnswer() { var result = answer; answer = null; return result; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Report(Exception? error) { if (error is not null) { failures.Add(error); } }
        internal void Present(Action action) { try { action(); } catch (Exception error) { Report(error); } }
        public DiplomaticFavorDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed favor cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && reply is null)
                { return Pending; }
                answer = Pending is null ? null : reply;
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

    private IEnumerable<DiplomaticFavorDecision?> Run(Operation op, int a, int b, MapPoint focus, string call)
    {
        for (int iteration = 0; ; ++iteration)
        {
            string round = call + "/round-" + iteration;
            yield return new DiplomaticFavorDecision.Menu(a, Party(b));
            int reply = op.TakeAnswer()!.Value.Selected;
            if (reply == 0)
            { world.Civilizations[b].DiploRequestCount = unchecked((sbyte)(world.Civilizations[b].DiploRequestCount - 1)); yield break; }
            if (reply == 1)
            {
                using var exchange = technology.Begin(a, b, 0, focus, round + "/exchange");
                while (!exchange.Completed)
                {
                    var decision = exchange.Resume(op.TakeAnswer());
                    if (!exchange.Completed)
                    { yield return decision is null ? null : new DiplomaticFavorDecision.Technology(decision); }
                }
                op.Report(exchange.PresentationFailures);
                if (exchange.Result != 0)
                {
                    int captured = world.Civilizations[b].DiploRequestCount;
                    if (captured + 1 < stance.Patience(a, b))
                    { world.Civilizations[b].DiploRequestCount = unchecked((sbyte)(world.Civilizations[b].DiploRequestCount + 1)); continue; }
                }
                effects.Publish();
                yield break;
            }
            if (reply == 3)
            {
                if ((!world.Civilizations[a].Treaties[b].Alliance && E.Attitude > 0 && !Wonder(a, WonderIds.MarcoPolo) && !Wonder(a, WonderIds.Magellan))
                    || E.Attitude > 25 || !Knows(b, AdvanceIds.MapMaking) || !Knows(a, AdvanceIds.MapMaking))
                { Notice(DiplomaticFavorNoticeKind.MapNo, a, b); }
                else
                { op.Report(maps.Exchange(a, b)); }
                yield break;
            }
            if (reply != 2)
            { yield break; }
            List<DiplomaticParty> targets = [];
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            { if (c != a && c != b && world.Globals.CivsAlive.Contains(c)) { targets.Add(Party(c)); } }
            yield return new DiplomaticFavorDecision.Target(a, new(targets));
            int t = op.TakeAnswer()!.Value.Selected;
            if (t == 0)
            { continue; }
            if (t < 0 || t >= EntityLimits.Civilizations)
            { throw new ArgumentOutOfRangeException(nameof(t)); }
            if (!world.Civilizations[b].Treaties[t].Contact)
            { Notice(DiplomaticFavorNoticeKind.NoContact, a, b, t); yield break; }
            if (world.Civilizations[b].Treaties[t].War)
            { Notice(DiplomaticFavorNoticeKind.AtWar, a, b, t); yield break; }
            int price = WarPrice(a, b, t), distrust = WarDistrust(a, b, t);
            if (distrust >= 7 && (world.Civilizations[b].Treaties[t].Alliance || world.Civilizations[b].Treaties[a].Vendetta
                || !world.Civilizations[a].Treaties[t].War || world.Globals.PowerRank[a] > world.Globals.PowerRank[t]))
            { Notice(DiplomaticFavorNoticeKind.RefusedWar, a, b, t); yield break; }
            if (world.Civilizations[b].Treaties[t].Alliance)
            { Notice(DiplomaticFavorNoticeKind.EternalAllies, a, b, t, 4); yield break; }
            bool money = world.Civilizations[b].Gold < 1000 || world.Civilizations[b].Gold <= world.Civilizations[a].Gold
                || E.BestTechnology is null || E.SecondTechnology is null;
            yield return new DiplomaticFavorDecision.War(new(a, Party(b), Party(t), money ? price : null,
                money ? null : Advance(Required(E.BestTechnology)), money ? null : Advance(Required(E.SecondTechnology))));
            if (op.TakeAnswer()!.Value.Selected == 0)
            { yield break; }
            if (!money)
            {
                foreach (var decision in Grant(op, b, Required(E.BestTechnology), a, round + "/first"))
                { yield return decision; }
                foreach (var decision in Grant(op, b, Required(E.SecondTechnology), a, round + "/second"))
                { yield return decision; }
                E.BestTechnology = E.SecondTechnology = null;
            }
            else
            {
                if (world.Civilizations[a].Gold < price)
                { Notice(DiplomaticFavorNoticeKind.Unaffordable, a, b, t); yield break; }
                world.Civilizations[a].Gold = unchecked(world.Civilizations[a].Gold - price);
                world.Civilizations[b].Gold = unchecked(world.Civilizations[b].Gold + price);
            }
            Mercenary(op, a, b, t, round);
            yield break;
        }
    }

    private void Mercenary(Operation op, int a, int b, int t, string call)
    {
        op.Present(effects.RefreshStatus);
        DiplomaticRelations.Set(world, b, t, new() { Contact = true, RecentContact = true, War = true });
        world.Civilizations[b].Treaties[t].Aggressor = world.Civilizations[b].Treaties[t].ExtendedAggressor = true;
        world.Civilizations[b].Treaties[a].Mercenary = true;
        effects.Publish();
        Notice(DiplomaticFavorNoticeKind.Declare, a, b, t, 0);
        if (world.Civilizations[a].Treaties[t].Alliance)
        {
            if (random.PrimaryModulo(new ChoiceKey("V05.mercenary-betray-alliance", call), 2) == 0)
            {
                DiplomaticAttitudes.Adjust(world, t, a, 25);
                Notice(DiplomaticFavorNoticeKind.BetrayAlly, a, b, t, 4);
                war.CancelAlliance(a, t);
            }
        }
        else if (world.Civilizations[a].Treaties[t].Peace
            && random.PrimaryModulo(new ChoiceKey("V05.mercenary-betray-peace", call), 2) == 0)
        {
            DiplomaticAttitudes.Adjust(world, t, a, 25);
            Notice(DiplomaticFavorNoticeKind.BetrayPeace, a, b, t, 4);
            DiplomaticRelations.Set(world, a, t, new() { War = true });
            world.Civilizations[t].Treaties[a].Vendetta = true;
        }
        effects.Publish();
    }
    private IEnumerable<DiplomaticFavorDecision?> Grant(Operation op, int c, int a, int source, string call)
    {
        using var grant = grants.Begin(c, a, source, false, call);
        while (!grant.Completed)
        {
            var decision = grant.Resume(op.TakeAnswer()?.Selected);
            if (!grant.Completed)
            { yield return decision is null ? null : new DiplomaticFavorDecision.Grant(decision); }
        }
        op.Report(grant.PresentationFailures);
    }
}
