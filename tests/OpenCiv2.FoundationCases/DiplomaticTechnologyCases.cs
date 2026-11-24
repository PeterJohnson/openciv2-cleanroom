using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class TechnologyFixture : IDiplomaticTechnologyEffects, IEmissaryEffects
    {
        public TheftFixture T { get; }
        public GameState World => T.World;
        public DiplomaticTechnology Service { get; }
        public List<TechnologyNegotiationNotice> Notices { get; } = [];
        public List<EmissaryNotice> Greetings { get; } = [];
        public int Refreshes, Closed;
        public bool FailRefresh, FailNotice;
        public TechnologyFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            T = new(choices, data, true);
            var w = World;
            for (int c = 0; c < EntityLimits.Civilizations; ++c)
            { w.Civilizations[c].Advances = default; w.Civilizations[c].NumAdvances = 0; w.Civilizations[c].Researching = -1; }
            w.Globals.AdvanceKnownBy = default;
            for (int a = 0; a < RuleLimits.Advances; ++a)
            { T.Rules.Advances[a] = new() { Identity = new("advance-" + a, "Advance " + a), AiValue = 5 }; }
            w.Globals.Turn = 100; w.Globals.Difficulty = Difficulty.Prince;
            w.Globals.PowerRank[1] = w.Globals.PowerRank[2] = 3;
            w.Globals.HumanPlayers = CivilizationSet.Only(1); w.Multiplayer.LocalHuman = 1;
            w.Civilizations[1].Tribe = 1; w.Civilizations[2].Tribe = 2;
            w.Civilizations[1].Gold = 5000; w.Civilizations[2].Gold = 777;
            w.Civilizations[1].Treaties[2] = w.Civilizations[2].Treaties[1] = new() { Contact = true, Peace = true };
            w.Civilizations[2].Attitude[1] = 50; w.Diplomacy.Attitude = 50; w.Diplomacy.SelectedPair = (2, 1);
            var government = new GovernmentChanges(w, T.Data.Process.Rules, T.Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected selection"), _ => throw new InvalidOperationException("Unexpected rates"), _ => { });
            var stance = new DiplomaticStance(w, T.Data.Process.Rules, new(w, T.Data.Process.Rules, government, T.Random));
            var emissaries = new EmissaryReception(w, T.Rules, T.Random, stance, this);
            Service = new(w, T.Data.Process.Rules, T.Random, emissaries, T.Research, T.Captured, this);
        }
        public void Know(int owner, int advance, sbyte value = 5)
        { ExchangeKnowledge(T.Grant, owner, advance, value); ++World.Civilizations[owner].NumAdvances; }
        public void Pair() { Know(1, 94); Know(1, 95); Know(2, 90); Know(2, 91); }
        public DiplomaticTechnology.Operation Begin(int mode = 0, bool selection = false) => Service.Begin(1, 2, mode, new(10, 8), "trade", selection);
        public TechnologyExchangeOffer Offer(DiplomaticTechnology.Operation op)
        {
            Equal(true, op.Resume() is TechnologyNegotiationDecision.Emissary);
            return ((TechnologyNegotiationDecision.Exchange)op.Resume(new(0))!).Offer;
        }
        public void Notice(TechnologyNegotiationNotice notice)
        { Notices.Add(notice); if (FailNotice) { throw new InvalidOperationException("Missing negotiation notice"); } }
        public void RefreshStatus() { ++Refreshes; if (FailRefresh) { throw new InvalidOperationException("Missing status view"); } }
        public void Notice(EmissaryNotice notice) => Greetings.Add(notice);
        public void OpenLeader(int counterpart) { }
        public void CloseLeader() => ++Closed;
        public void EnsureVisible(MapPoint point, int observer) { }
    }
    private static Choice TradeGreeting() => GreetingChoice("trade/round-0/emissary/greet");

    private static void DiplomaticTechnologySurveyAndBlocking()
    {
        var f = new TechnologyFixture(); f.Pair(); var w = f.World;
        w.Civilizations[1].Advances = w.Civilizations[1].Advances.With(99); // Both Never: not counted.
        var survey = f.Service.Survey(1, 2);
        Equal(2, survey.ActorKnown); Equal(2, survey.OtherKnown); Equal(94, survey.FirstKnown);
        Equal(95, w.Diplomacy.BestTechnology); Equal(94, w.Diplomacy.SecondTechnology); Equal(91, w.Diplomacy.WantedTechnology);
        Equal(5, survey.BestScore); Equal(5, survey.SecondScore); Equal(5, survey.WantedScore);
        f.T.Rules.Advances[94] = f.T.Rules.Advances[94] with { AiValue = 9 };
        f.Service.Survey(1, 2); Equal(94, w.Diplomacy.BestTechnology); Equal(95, w.Diplomacy.SecondTechnology);
        w.Civilizations[1].WondersByGroup[0] = w.Civilizations[1].WondersByGroup[1] = 1;
        f.T.Rules.Improvements[39] = f.T.Rules.Improvements[39] with { Prerequisite = (AdvanceReference)90 };
        f.T.Rules.Improvements[47] = f.T.Rules.Improvements[47] with { Prerequisite = (AdvanceReference)91 };
        w.Entities.Cities.Storage[0].Production = -39; f.T.Data.AddCity(1, 2, new(20, 8)); w.Entities.Cities.Storage[1].Production = -47;
        Equal(8, f.Service.Survey(1, 2).BlockingWonder);
        w.Globals.WonderCity[8] = -2; Equal(0, f.Service.Survey(1, 2).BlockingWonder);
        w.Globals.Difficulty = Difficulty.Emperor;
        using var only = f.Begin(selection: true); Equal(null, only.Resume()); Equal(0, only.Result); Equal(0, f.Notices.Count);
        using var blocked = f.Begin(); Equal(null, blocked.Resume()); Equal(TechnologyNegotiationNoticeKind.WonderBlocked, f.Notices.Single().Kind);
        f.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticTechnologyExchangeAndCapturedChoice()
    {
        foreach (int reply in new[] { 1, 2 })
        {
            var f = new TechnologyFixture([TradeGreeting()]); f.Pair(); var w = f.World;
            using var op = f.Begin(); var offer = f.Offer(op);
            Equal(95, offer.Best.Identity); Equal(94, offer.Alternative!.Identity); Equal(91, offer.Wanted.Identity);
            Equal(offer, ((TechnologyNegotiationDecision.Exchange)op.Resume()!).Offer);
            var pending = (TechnologyNegotiationDecision.Captured)op.Resume(new(reply))!;
            var choice = (CapturedTechnologyChoice)pending.Decision;
            Sequence(new[] { 90, 91 }, choice.Options.Select(a => a.Identity));
            Equal(true, w.Civilizations[2].Advances.Contains(reply == 1 ? 95 : 94));
            Equal(false, w.Civilizations[1].Flags.TradedTechnology); // Tail waits for the actual chooser/grant.
            op.Resume(new(90)); Equal(true, op.Completed); Equal(1, op.Result);
            Equal(true, w.Civilizations[1].Advances.Contains(90)); Equal(false, w.Civilizations[1].Advances.Contains(91));
            Equal((byte)(reply == 1 ? 40 : 45), w.Civilizations[2].Attitude[1]);
            Equal(reply == 1 ? 94 : 95, w.Diplomacy.BestTechnology); Equal((int?)null, w.Diplomacy.SecondTechnology);
            Equal(true, w.Civilizations[1].Flags.TradedTechnology && w.Civilizations[2].Flags.TradedTechnology);
            Equal(false, w.Diplomacy.Aggressive); Equal(0, f.Closed); op.Resume(); f.T.Random.RequireChoicesConsumed();
        }
    }

    private static void DiplomaticTechnologyRepeatedExchange()
    {
        var f = new TechnologyFixture([TradeGreeting()]); f.Pair();
        using var op = f.Begin(1); f.Offer(op);
        Equal(true, op.Resume(new(1)) is TechnologyNegotiationDecision.Captured);
        var second = (TechnologyNegotiationDecision.Exchange)op.Resume(new(90))!;
        Equal(94, second.Offer.Best.Identity); Equal(91, second.Offer.Wanted.Identity); Equal(null, second.Offer.Alternative);
        Equal(true, op.Resume(new(1)) is TechnologyNegotiationDecision.Captured);
        op.Resume(new(91)); Equal(true, op.Completed); Equal(0, op.Result);
        foreach (int a in new[] { 90, 91, 94, 95 })
        { Equal(true, f.World.Civilizations[1].Advances.Contains(a) && f.World.Civilizations[2].Advances.Contains(a)); }
        Equal(1, f.Greetings.Count); Equal(0, f.Closed); f.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticTechnologySalesAndPricing()
    {
        foreach (var (gold, price) in new[] { (999, 0), (1500, 1000), (1501, 1500), (3000, 1500), (3001, 2250) })
        {
            var f = new TechnologyFixture(); f.Know(2, 90); f.World.Diplomacy.WantedTechnology = 90;
            f.World.Civilizations[1].Gold = gold;
            using var op = f.Service.BeginSale(1, 2, 0, "sale"); var pending = op.Resume();
            if (price == 0) { Equal(null, pending); Equal(0, op.Result); }
            else { Equal(price, ((TechnologyNegotiationDecision.Sale)pending!).Offer.Price); op.Resume(new(0)); Equal(1, op.Result); }
            Equal(gold, f.World.Civilizations[1].Gold); Equal(777, f.World.Civilizations[2].Gold); f.T.Random.RequireChoicesConsumed();
        }
        var trade = new TechnologyFixture(); trade.Know(2, 90); trade.World.Diplomacy.WantedTechnology = 90;
        trade.World.Civilizations[1].Gold = 1500; trade.FailRefresh = true;
        using var sale = trade.Service.BeginSale(1, 2, 2, "sale"); sale.Resume(); sale.Resume(new(1));
        Equal(500, trade.World.Civilizations[1].Gold); Equal(777, trade.World.Civilizations[2].Gold);
        Equal(true, trade.World.Civilizations[1].Advances.Contains(90)); Equal(1, sale.PresentationFailures.Count); Equal(1, sale.Result);
        trade = new(); trade.Know(2, 90); trade.World.Diplomacy.WantedTechnology = 90;
        trade.World.Civilizations[1].Gold = 10000; trade.World.Globals.AdvanceKnownBy[90] = CivilizationSet.Only(2).With(3).With(4);
        trade.World.Civilizations[1].Treaties[2].Alliance = true; trade.World.Civilizations[2].NumAdvances = 5;
        using var discount = trade.Service.BeginSale(1, 2, 0, "discount");
        Equal(281, ((TechnologyNegotiationDecision.Sale)discount.Resume()!).Offer.Price); discount.Resume(new(0));
        foreach (var (attitude, price) in new[] { (2000, 90000), (int.MaxValue, 30000) })
        {
            trade = new(); trade.Know(2, 90); trade.World.Diplomacy.WantedTechnology = 90;
            trade.World.Civilizations[1].Gold = 100000; trade.World.Diplomacy.Attitude = attitude;
            using var extreme = trade.Service.BeginSale(1, 2, 0, "extreme");
            Equal(price, ((TechnologyNegotiationDecision.Sale)extreme.Resume()!).Offer.Price); extreme.Resume(new(0));
        }
    }

    private static void DiplomaticTechnologyRefusalAndGifts()
    {
        Action<TechnologyFixture>[] conditions = [
            f => f.World.Civilizations[2].Treaties[1].Vendetta = true,
            f => f.World.Civilizations[1].Treaties[2].SneakAttack = true,
            f => f.World.Diplomacy.CapturedCities = 1,
            f => f.World.Civilizations[2].Treaties[1].Fighting = true,
            f => { f.World.Diplomacy.Aggressive = true; for (int a = 80; a < 86; ++a) { f.Know(2, a); } },
            f => f.World.Diplomacy.Attitude = 76
        ];
        foreach (var condition in conditions)
        {
            var f = new TechnologyFixture([Choice.Create(new("V05.exchange-refusal", "trade/round-0"),
                new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0))]); condition(f);
            using var op = f.Begin(); Equal(null, op.Resume()); Equal(0, op.Result);
            Equal(TechnologyNegotiationNoticeKind.RefusedMedium, f.Notices.Single().Kind); f.T.Random.RequireChoicesConsumed();
        }
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            var f = new TechnologyFixture(); f.Know(2, 90);
            f.World.Civilizations[2].Treaties[1].Alliance = true; f.World.Civilizations[2].NumAdvances = 20;
            f.World.Civilizations[2].DiploRequestCount = (sbyte)(mode == 3 ? 127 : 0);
            using var op = f.Begin(mode); op.Resume(); Equal(true, op.Completed);
            Equal(mode != 1, f.World.Civilizations[1].Advances.Contains(90)); Equal(mode > 1 ? 1 : 0, op.Result);
            Equal((sbyte)(mode == 0 ? 2 : mode == 3 ? 127 : 0), f.World.Civilizations[2].DiploRequestCount);
            Equal(mode == 1 ? 0 : 1, f.Notices.Count); f.T.Random.RequireChoicesConsumed();
        }
        var quiet = new TechnologyFixture(); quiet.Pair();
        using var demand = quiet.Begin(2); Equal(null, demand.Resume()); Equal(0, demand.Result); quiet.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticTechnologyPettyFailureAndGovernment()
    {
        var f = new TechnologyFixture([TradeGreeting()]); f.Pair(); f.World.Globals.PowerRank[1] = 7; f.World.Civilizations[1].CityCount = 5;
        f.World.Civilizations[2].Treaties[1].Alliance = true;
        using var petty = f.Begin(); f.Offer(petty); petty.Resume(new(2));
        Equal(TechnologyNegotiationNoticeKind.Petty, f.Notices.Single().Kind); Equal((sbyte)1, f.World.Civilizations[2].DiploRequestCount);
        Equal(false, f.World.Civilizations[2].Advances.Contains(94)); Equal(0, petty.Result); f.T.Random.RequireChoicesConsumed();
        f = new([TradeGreeting()]); f.Pair(); f.T.Rules.Advances[94] = f.T.Rules.Advances[94] with { AiValue = 9 };
        f.World.Civilizations[2].Attitude[1] = 70; f.World.Diplomacy.Attitude = 70; f.FailNotice = true;
        using var failedNotice = f.Begin(); f.Offer(failedNotice);
        Throws<InvalidOperationException>(() => failedNotice.Resume(new(2)));
        Equal((sbyte)0, f.World.Civilizations[2].DiploRequestCount); Equal(false, f.World.Civilizations[2].Advances.Contains(95));
        foreach (bool failure in new[] { false, true })
        {
            f = new(); f.Know(2, AdvanceIds.Monarchy); f.World.Diplomacy.WantedTechnology = AdvanceIds.Monarchy;
            f.World.Civilizations[1].Gold = 10000; f.T.Retained!.FailRevolution = failure;
            using var op = f.Service.BeginSale(1, 2, 0, "government");
            int price = ((TechnologyNegotiationDecision.Sale)op.Resume()!).Offer.Price;
            Equal(true, op.Resume(new(1)) is TechnologyNegotiationDecision.Grant { Decision: ResearchGrantDecision.Government });
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); Equal(10000 - price, f.World.Civilizations[1].Gold);
            if (failure)
            { Throws<InvalidOperationException>(() => op.Resume(new(1))); Throws<InvalidOperationException>(() => op.Resume(new(0))); }
            else { op.Resume(new(1)); Equal(true, op.Completed); Equal(Government.Anarchy, f.World.Civilizations[1].Government); }
        }
        f = new([TradeGreeting()]); f.Pair();
        using var stopped = f.Begin(); f.Offer(stopped); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume(new(1))); Equal(false, f.World.Civilizations[2].Advances.Contains(95));
    }

    private static void DiplomaticTechnologySessionChoices()
    {
        using var runtime = new SessionRuntime(new(), new(251));
        var f = new TechnologyFixture([TradeGreeting()], CreationFixture(new(runtime.Process, runtime.World))); f.Pair();
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.exchange", "Authored diplomatic exchange", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new DiplomaticTechnologyOperation(f.Begin(), _ => recipient,
                _ => throw new InvalidOperationException("Unexpected intelligence"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.exchange", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(player, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, actor.Reply(prompt, 0).Accepted); Equal(true, player.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(player, command); Equal(true, player.Reply(prompt, 1).Accepted); runtime.AdvanceOne();
        prompt = RequiredInteraction(player, command); Equal(false, player.Cancel(command).Accepted);
        Equal(true, player.Reply(prompt, 90).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(true, f.World.Civilizations[1].Advances.Contains(90)); Equal(false, f.World.Civilizations[1].Advances.Contains(91)); f.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticTechnologyLeaderAndSaleModes()
    {
        foreach (int unit in new[] { UnitTypeIds.Armor, UnitTypeIds.Cavalry, UnitTypeIds.Artillery, UnitTypeIds.Howitzer,
            UnitTypeIds.Riflemen, UnitTypeIds.Musketeers, -1 })
        {
            var f = new TechnologyFixture(); int advance = unit < 0 ? AdvanceIds.GuerrillaWarfare : 94;
            f.Know(1, advance); f.Know(2, 90);
            f.World.Globals.PowerRank[1] = 7; f.World.Civilizations[1].CityCount = 5; f.World.Globals.Turn = 201;
            if (unit >= 0) { f.T.Rules.Units[unit] = f.T.Rules.Units[unit] with { Prerequisite = (AdvanceReference)advance }; }
            using var op = f.Begin(); Equal(true, op.Resume() is TechnologyNegotiationDecision.Emissary);
            op.Resume(new(1)); Equal(true, op.Completed); Equal(0, f.Notices.Count); f.T.Random.RequireChoicesConsumed();
        }
        var refused = new TechnologyFixture(); refused.Know(1, 94); refused.Know(2, 90);
        refused.World.Globals.PowerRank[1] = 7; refused.World.Civilizations[1].CityCount = 5;
        refused.World.Globals.Turn = 201; refused.World.Civilizations[1].Gold = 0;
        using var blocked = refused.Begin(); Equal(null, blocked.Resume()); Equal(TechnologyNegotiationNoticeKind.NotNow, refused.Notices.Single().Kind);
        foreach (int mode in new[] { 0, 2 })
        {
            var f = new TechnologyFixture(); f.Know(2, 90); f.World.Civilizations[2].Treaties[1].Peace = false;
            f.World.Diplomacy.Attitude = 70;
            using var op = f.Begin(mode); Equal(true, op.Resume() is TechnologyNegotiationDecision.Sale);
            op.Resume(new(0)); Equal(mode, op.Result); Equal(false, f.World.Civilizations[1].Advances.Contains(90));
            Equal(0, f.Notices.Count); f.T.Random.RequireChoicesConsumed();
        }
        var floor = new TechnologyFixture(); floor.Know(2, 90, 1); floor.World.Diplomacy.WantedTechnology = 90;
        for (int c = 0; c < 8; ++c) { floor.World.Globals.AdvanceKnownBy[90] = floor.World.Globals.AdvanceKnownBy[90].With(c); }
        floor.World.Civilizations[1].Gold = 100;
        using var minimum = floor.Service.BeginSale(1, 2, 0, "minimum");
        Equal(100, ((TechnologyNegotiationDecision.Sale)minimum.Resume()!).Offer.Price); minimum.Resume(new(0));
    }

    private static void DiplomaticTechnologySessionCancellation()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
            for (int stage = 0; stage < 3; ++stage)
            {
                var f = new TechnologyFixture(stage == 1 ? [TradeGreeting()] : []);
                if (stage == 2) { f.Know(2, 90); f.World.Diplomacy.WantedTechnology = 90; }
                else { f.Pair(); }
                var core = stage == 2 ? f.Service.BeginSale(1, 2, 0, "sale") : f.Begin();
                var context = new OperationContext(f.World, f.T.Data.Process, f.T.Random, new PresentationRandom(1, "technology"),
                    new(default, new(1)), default, (_, _) => { }, () => { });
                using var operation = new DiplomaticTechnologyOperation(core, c => new((ulong)c),
                    _ => throw new InvalidOperationException("Unexpected intelligence"));
                Equal(CommandStatus.AwaitingInteraction, operation.Resume(context, new()).Status);
                if (stage == 1) { Equal(CommandStatus.AwaitingInteraction, operation.Resume(context, new(OperationInputKind.Choice, 0)).Status); }
                Equal(CommandStatus.Completed, operation.Resume(context, new(input)).Status);
                Equal(5000, f.World.Civilizations[1].Gold); Equal(false, f.World.Civilizations[1].Flags.TradedTechnology);
                Equal(false, f.World.Civilizations[2].Advances.Contains(95)); f.T.Random.RequireChoicesConsumed();
            }
    }
}
