using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record TechnologySurvey(int ActorKnown, int OtherKnown, int BestScore, int SecondScore,
    int WantedScore, int? FirstKnown, int? BlockingWonder);
public sealed record TechnologyExchangeOffer(int Recipient, DiplomaticParty Counterpart, ResearchOption Wanted,
    ResearchOption Best, ResearchOption? Alternative, bool Friendly);
public sealed record TechnologySaleOffer(int Recipient, DiplomaticParty Counterpart, ResearchOption Advance, int Price, int Mode);
public enum TechnologyNegotiationNoticeKind { RefusedMedium, RefusedMad, WonderBlocked, NotNow, Unavailable, Gift, Petty }
public sealed record TechnologyNegotiationNotice(TechnologyNegotiationNoticeKind Kind, int Recipient,
    DiplomaticParty Counterpart, int Mode, int Reaction, ResearchOption? Technology = null, string? Wonder = null);
public abstract record TechnologyNegotiationDecision
{
    public sealed record Emissary(EmissaryDecision Decision) : TechnologyNegotiationDecision;
    public sealed record Exchange(TechnologyExchangeOffer Offer) : TechnologyNegotiationDecision;
    public sealed record Sale(TechnologySaleOffer Offer) : TechnologyNegotiationDecision;
    public sealed record Grant(ResearchGrantDecision Decision) : TechnologyNegotiationDecision;
    public sealed record Captured(CapturedTechnologyDecision Decision) : TechnologyNegotiationDecision;
}
public interface IDiplomaticTechnologyEffects
{
    void Notice(TechnologyNegotiationNotice notice);
    void RefreshStatus();
}

/// <summary>V05 §3 local survey, sale and exchange using actual A08 valuation,
/// V05 reception, V03 grants and the captured-technology chooser.</summary>
public sealed class DiplomaticTechnology(GameState world, RuleState rules, GameplayRandom random,
    EmissaryReception emissaries, ResearchGrants grants, CapturedTechnology captured, IDiplomaticTechnologyEffects effects)
{
    private GameState World => world;
    private DiplomacyWorkspace E => world.Diplomacy;
    private bool Knows(int c, int advance) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, advance);
    private int Value(int c, int advance) => AdvanceValuation.Value(world, rules, c, advance);
    private ResearchOption Technology(int advance) => new(advance, rules.Definitions.Advances[advance].Identity.Name);
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    private int Category => DiplomaticAttitudes.Category(E.Attitude);
    private static int Required(int? choice) => choice ?? throw new InvalidOperationException("The diplomatic technology choice is absent");

    public TechnologySurvey Survey(int actor, int other)
    {
        MapKnowledge.RequireLocal(world);
        E.BestTechnology = E.SecondTechnology = E.WantedTechnology = null;
        int actorKnown = 0, otherKnown = 0, best = 0, second = 0, wanted = 0;
        int? first = null, blocking = null;
        for (int a = 0; a < RuleLimits.Advances; ++a)
        {
            var rule = rules.Definitions.Advances[a];
            if (rule.Prerequisites[0] == AdvanceReference.Never && rule.Prerequisites[1] == AdvanceReference.Never)
            { continue; }
            bool actorHas = Knows(actor, a), otherHas = Knows(other, a);
            if (actorHas)
            { ++actorKnown; first ??= a; }
            if (otherHas)
            { ++otherKnown; }
            if (otherHas && !actorHas)
            {
                for (int w = 0; w < RuleLimits.Wonders; ++w)
                {
                    int improvement = BuildingIds.Capitalization + 1 + w;
                    if ((int)rules.Definitions.Improvements[improvement].Prerequisite != a
                        || world.Globals.WonderCity[w] != BuildingQueries.Unbuilt
                        || world.Civilizations[other].WondersByGroup[w / 7] >= world.Civilizations[actor].WondersByGroup[w / 7])
                    { continue; }
                    foreach (ref readonly var city in world.Entities.Cities.Scanned)
                    {
                        if (city.Token.Occupied && city.Owner.Value == other && city.Production == -improvement)
                        { blocking = w; break; }
                    }
                }
                int score = Value(actor, a);
                if (score >= wanted)
                { wanted = score; E.WantedTechnology = a; }
            }
            if (actorHas && !otherHas)
            {
                int score = Value(other, a);
                if (score < best)
                { if (score >= second) { second = score; E.SecondTechnology = a; } }
                else
                {
                    if (E.BestTechnology is not null)
                    { second = best; E.SecondTechnology = E.BestTechnology; }
                    best = score;
                    E.BestTechnology = a;
                }
            }
        }
        return new(actorKnown, otherKnown, best, second, wanted, first, blocking);
    }

    public Operation Begin(int actor, int other, int mode, MapPoint focus, string call, bool selectionOnly = false) =>
        new(this, actor, other, mode, focus, call, selectionOnly, false);
    public Operation BeginSale(int actor, int other, int mode, string call) => new(this, actor, other, mode, default, call, false, true);

    public sealed class Operation : IDisposable
    {
        private readonly DiplomaticTechnology parent;
        private readonly IEnumerator<TechnologyNegotiationDecision?> steps;
        private readonly List<Exception> failures = [];
        private ResearchReply? answer;
        private bool faulted, disposed;
        public bool Completed { get; private set; }
        public int? Result { get; internal set; }
        public TechnologyNegotiationDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(DiplomaticTechnology parent, int actor, int other, int mode, MapPoint focus,
            string call, bool selectionOnly, bool sale)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = (sale ? parent.Sell(this, actor, other, mode, call)
                : parent.Exchange(this, actor, other, mode, focus, call, selectionOnly)).GetEnumerator();
        }
        internal ResearchReply? TakeAnswer() { var result = answer; answer = null; return result; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public TechnologyNegotiationDecision? Resume(ResearchReply? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed diplomatic exchange cannot resume"); }
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

    private void Notice(TechnologyNegotiationNoticeKind kind, int actor, int other, int mode, int reaction = 0,
        int? technology = null, int? wonder = null) => effects.Notice(new(kind, actor, Party(other), mode, reaction,
            technology is { } a ? Technology(a) : null,
            wonder is { } w ? rules.Definitions.Improvements[BuildingIds.Capitalization + 1 + w].Identity.Name : null));

    private IEnumerable<TechnologyNegotiationDecision?> Sell(Operation operation, int actor, int other, int mode, string call)
    {
        operation.Result = 0;
        if (E.WantedTechnology is not { } wanted)
        { yield break; }
        int price;
        unchecked
        {
            price = Value(other, wanted) * 20;
            if (E.Attitude > 50)
            { price = E.Attitude * price / 50; }
            if (world.Globals.PowerRank[actor] == 7 && world.Civilizations[actor].CityCount > 4
                && world.Globals.Turn > 200 && world.Globals.Difficulty != Difficulty.Chieftain)
            { price *= 2; }
            int divisor = world.Globals.AdvanceKnownBy[wanted].Count - 1;
            if (divisor < 2)
            { divisor = 1; }
            int q = price / divisor;
            price = q * 10;
            if (world.Civilizations[actor].Gold > 1500)
            { price = q * 30 / 2; }
            if (world.Civilizations[actor].Gold > 3000)
            { price = price * 3 / 2; }
            if (world.Civilizations[actor].Treaties[other].Alliance)
            {
                if (world.Civilizations[actor].NumAdvances < world.Civilizations[other].NumAdvances)
                { price /= 2; }
                else
                { price -= price / 4; }
                if (world.Civilizations[actor].NumAdvances + 4 < world.Civilizations[other].NumAdvances)
                { price /= 2; }
            }
            if (price < 0)
            { price = 30000; }
            if (price < 100)
            { price = 100; }
        }
        if (world.Civilizations[actor].Gold < price)
        { yield break; }
        yield return new TechnologyNegotiationDecision.Sale(new(actor, Party(other), Technology(wanted), price, mode));
        if (operation.TakeAnswer()!.Value.Selected == 1)
        {
            world.Civilizations[actor].Gold = unchecked(world.Civilizations[actor].Gold - price);
            operation.Present(effects.RefreshStatus);
            foreach (var decision in Grant(operation, actor, Required(E.WantedTechnology), other, call + "/grant"))
            { yield return decision; }
        }
        operation.Result = 1; // An affordable offer was presented, even when declined.
    }

    private IEnumerable<TechnologyNegotiationDecision?> Exchange(Operation operation, int actor, int other,
        int mode, MapPoint focus, string call, bool selectionOnly)
    {
        bool allied = world.Civilizations[other].Treaties[actor].Alliance;
        for (int iteration = 0; ; ++iteration)
        {
            string round = call + "/round-" + iteration;
            var survey = Survey(actor, other);
            operation.Result = 0;
            if (selectionOnly)
            { yield break; }
            var relation = world.Civilizations[other].Treaties[actor];
            if (!allied && (relation.Vendetta || world.Civilizations[actor].Treaties[other].SneakAttack
                || E.CapturedCities != 0 || relation.Fighting || E.Aggressive && survey.OtherKnown > 5 || Category > 5))
            {
                if (mode == 0)
                {
                    bool medium = random.SecondaryRange(new ChoiceKey("V05.exchange-refusal", round), 0, 2) == 0;
                    Notice(medium ? TechnologyNegotiationNoticeKind.RefusedMedium : TechnologyNegotiationNoticeKind.RefusedMad,
                        actor, other, mode, medium ? 3 : 4);
                }
                yield break;
            }
            if (survey.BlockingWonder is { } blocked && world.Globals.Difficulty > Difficulty.King)
            {
                if (mode == 0)
                { Notice(TechnologyNegotiationNoticeKind.WonderBlocked, actor, other, mode, wonder: blocked); }
                yield break;
            }
            if (survey.ActorKnown < survey.OtherKnown)
            {
                if (mode == 1)
                { yield break; }
                if (Category > 5 - (world.Civilizations[other].Treaties[actor].Peace ? 0 : 1) && !allied)
                {
                    foreach (var decision in Sell(operation, actor, other, mode, round + "/sale"))
                    { yield return decision; }
                    if (operation.Result == 1)
                    { operation.Result = mode; yield break; }
                    if (mode == 0)
                    { Notice(TechnologyNegotiationNoticeKind.RefusedMedium, actor, other, mode, 3); }
                    yield break;
                }
            }
            if (world.Globals.PowerRank[actor] == 7 && world.Civilizations[actor].CityCount > 4 && !allied
                && Category > (world.Globals.Turn < 201 ? 4 : 3) - (world.Civilizations[other].Treaties[actor].Peace ? 0 : 1)
                && !MilitaryPrerequisite(E.BestTechnology))
            {
                if (mode != 1)
                {
                    foreach (var decision in Sell(operation, actor, other, mode, round + "/sale"))
                    { yield return decision; }
                    if (operation.Result == 1)
                    { operation.Result = mode; yield break; }
                }
                if (mode == 0)
                { Notice(TechnologyNegotiationNoticeKind.NotNow, actor, other, mode); }
                yield break;
            }
            if (E.BestTechnology is null && E.WantedTechnology is not null && world.Civilizations[other].Treaties[actor].Alliance
                && world.Civilizations[actor].NumAdvances + 6 - rules.Leaders[world.Civilizations[other].Tribe].Civilize
                    - Arithmetic.ClampAB(world.Civilizations[other].NumAdvances / 12, 0, 5) < world.Civilizations[other].NumAdvances
                && (world.Civilizations[other].DiploRequestCount < 2 || mode == 3))
            {
                Notice(TechnologyNegotiationNoticeKind.Gift, actor, other, mode, 3, E.WantedTechnology);
                foreach (var decision in Grant(operation, actor, Required(E.WantedTechnology), other, round + "/gift"))
                { yield return decision; }
                E.WantedTechnology = null;
                if (mode < 2)
                { world.Civilizations[other].DiploRequestCount = unchecked((sbyte)(world.Civilizations[other].DiploRequestCount + 2)); }
                else
                { operation.Result = 1; }
                yield break;
            }
            if (E.WantedTechnology is null || E.BestTechnology is null)
            {
                if (mode != 1)
                {
                    foreach (var decision in Sell(operation, actor, other, mode, round + "/sale"))
                    { yield return decision; }
                    if (operation.Result == 1)
                    { operation.Result = mode; yield break; }
                }
                if (mode == 0)
                { Notice(TechnologyNegotiationNoticeKind.Unavailable, actor, other, mode); }
                yield break;
            }
            if (mode > 1)
            { yield break; }
            using var reception = emissaries.Begin(actor, other, focus, round + "/emissary");
            while (reception.Accepted is null)
            {
                if (reception.Resume(operation.TakeAnswer()?.Selected) is { } prompt)
                { yield return new TechnologyNegotiationDecision.Emissary(prompt); }
            }
            operation.Report(reception.PresentationFailures);
            if (reception.Accepted == false)
            { yield break; }
            yield return new TechnologyNegotiationDecision.Exchange(new(actor, Party(other), Technology(Required(E.WantedTechnology)),
                Technology(Required(E.BestTechnology)), E.SecondTechnology is { } second && second != E.BestTechnology ? Technology(second) : null,
                mode == 0 && !E.Aggressive && Category < 5));
            int reply = operation.TakeAnswer()!.Value.Selected;
            if (reply == 0)
            { yield break; }
            int benefit = unchecked(2 * survey.BestScore);
            if (reply == 2)
            {
                if (Knows(other, Required(E.SecondTechnology)) || BuildingQueries.HasWonder(world, rules.Definitions, other, WonderIds.MarcoPolo)
                    || world.Globals.PowerRank[actor] == 7 && world.Civilizations[actor].CityCount > 4
                    || survey.SecondScore < survey.BestScore && (survey.ActorKnown < survey.OtherKnown || Category > 4))
                {
                    Notice(TechnologyNegotiationNoticeKind.Petty, actor, other, mode, 3);
                    world.Civilizations[other].DiploRequestCount = unchecked((sbyte)(world.Civilizations[other].DiploRequestCount + 1));
                    yield break;
                }
                (E.BestTechnology, E.SecondTechnology) = (E.SecondTechnology, E.BestTechnology);
                benefit = survey.SecondScore;
            }
            foreach (var decision in Grant(operation, other, Required(E.BestTechnology), actor, round + "/give"))
            { yield return decision; }
            using var take = captured.Begin(actor, other, round + "/take");
            while (!take.Finished)
            {
                var decision = take.Resume(operation.TakeAnswer());
                if (!take.Finished)
                { yield return decision is null ? null : new TechnologyNegotiationDecision.Captured(decision); }
            }
            operation.Report(take.PresentationFailures);
            E.BestTechnology = E.SecondTechnology;
            E.SecondTechnology = null;
            DiplomaticAttitudes.Adjust(world, other, actor, unchecked(-benefit));
            world.Civilizations[actor].Flags.TradedTechnology = world.Civilizations[other].Flags.TradedTechnology = true;
            E.Aggressive = false;
            if (mode == 0)
            { operation.Result = 1; yield break; }
        }
    }

    private bool MilitaryPrerequisite(int? advance)
    {
        int comparison = advance ?? -1; // The original absent choice can match a None prerequisite.
        foreach (int unit in new[] { UnitTypeIds.Armor, UnitTypeIds.Cavalry, UnitTypeIds.Artillery, UnitTypeIds.Howitzer, UnitTypeIds.Riflemen, UnitTypeIds.Musketeers })
        { if ((int)rules.Definitions.Units[unit].Prerequisite == comparison) { return true; } }
        return advance == AdvanceIds.GuerrillaWarfare;
    }
    private IEnumerable<TechnologyNegotiationDecision?> Grant(Operation operation, int actor, int advance, int source, string call)
    {
        using var grant = grants.Begin(actor, advance, source, false, call);
        while (!grant.Completed)
        {
            var decision = grant.Resume(operation.TakeAnswer()?.Selected);
            if (!grant.Completed)
            { yield return decision is null ? null : new TechnologyNegotiationDecision.Grant(decision); }
        }
        operation.Report(grant.PresentationFailures);
    }
}
