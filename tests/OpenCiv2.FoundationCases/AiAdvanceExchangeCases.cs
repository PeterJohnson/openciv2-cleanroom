using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // A08 §6 / V03 / V04 specification-derived exchanges using actual grants and valuation.
    private static Choice ExchangeDraw(int advance, bool reverse = false, int draw = 0, string call = "exchange") =>
        Choice.Create(new(reverse ? "A08.exchange.reverse" : "A08.exchange.forward", call, advance),
            new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(draw));

    private static void ExchangeKnowledge(GrantFixture f, int owner, int advance, sbyte value = 5)
    {
        f.Rules.Advances[advance] = f.Rules.Advances[advance] with
        { AiValue = value, Prerequisites = new([AdvanceReference.None, AdvanceReference.Never]), Enabled = false };
        f.World.Civilizations[owner].Advances = f.World.Civilizations[owner].Advances.With(advance);
        f.World.Globals.AdvanceKnownBy[advance] = f.World.Globals.AdvanceKnownBy[advance].With(owner);
    }

    private static AiAdvanceExchange ExchangeService(RetainedGrantFixture f) =>
        new(f.World, f.G.Data.Process.Rules, f.G.Random, f.Grants);

    private static void CompleteExchange(AiAdvanceExchange.Operation operation)
    {
        for (int i = 0; i < 50 && !operation.Completed; ++i) { Equal(null, operation.Resume()); }
        Equal(true, operation.Completed);
    }

    private static void AiExchangeSelectionAndActualGrants()
    {
        var f = new RetainedGrantFixture([ExchangeDraw(94), ExchangeDraw(95), ExchangeDraw(96, true, 1)]);
        ExchangeKnowledge(f.G, 2, 94); ExchangeKnowledge(f.G, 2, 95); ExchangeKnowledge(f.G, 1, 96, 4);
        f.World.Civilizations[2].Advances = f.World.Civilizations[2].Advances.With(97); // Both Never: no choice.
        f.World.Civilizations[1].Researching = 94; f.World.Civilizations[2].Researching = 96;
        using var op = ExchangeService(f).Begin(1, 2, "exchange"); CompleteExchange(op);
        Equal(true, op.Result); op.Resume();
        Equal(true, f.World.Civilizations[1].Advances.Contains(94));
        Equal(false, f.World.Civilizations[1].Advances.Contains(95));
        Equal(true, f.World.Civilizations[2].Advances.Contains(96));
        Equal((byte)2, f.World.Civilizations[1].AdvanceSource[94]); Equal((byte)1, f.World.Civilizations[2].AdvanceSource[96]);
        Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)1, f.World.Civilizations[2].NumAdvances);
        Equal((short)-1, f.World.Civilizations[1].Researching); Equal((short)-1, f.World.Civilizations[2].Researching);
        Equal(false, f.World.Civilizations[1].Treaties[2].Tribute);
        f.G.Random.RequireChoicesConsumed();
    }

    private static void AiExchangeOneSidedGates()
    {
        foreach (bool reverse in new[] { false, true })
            foreach (var (alliance, tribute, sourceCount, expected) in new[]
            { (true, false, 5, true), (true, false, 4, false), (false, false, 5, false), (true, true, 5, false) })
            {
                var f = new RetainedGrantFixture([ExchangeDraw(94, reverse)]);
                int recipient = reverse ? 2 : 1, source = reverse ? 1 : 2;
                ExchangeKnowledge(f.G, source, 94);
                f.World.Globals.Difficulty = Difficulty.Emperor;
                f.World.Civilizations[source].NumAdvances = (byte)sourceCount;
                f.World.Civilizations[recipient].Treaties[source].Alliance = alliance;
                f.World.Civilizations[recipient].Treaties[source].Tribute = tribute;
                using var op = ExchangeService(f).Begin(1, 2, "exchange"); CompleteExchange(op);
                Equal(expected, op.Result); Equal(expected, f.World.Civilizations[recipient].Advances.Contains(94));
                Equal(tribute || expected, f.World.Civilizations[recipient].Treaties[source].Tribute);
                Equal(expected, f.World.Civilizations[source].Treaties[recipient].Tribute);
                Equal((byte)(expected ? 1 : 0), f.World.Civilizations[recipient].NumAdvances);
                f.G.Random.RequireChoicesConsumed();
            }
    }

    private static void AiExchangeLeadingHumanOverride()
    {
        foreach (string gate in new[] { "pass", "human", "difficulty", "cities", "turn", "advances" })
        {
            var f = new RetainedGrantFixture([ExchangeDraw(94)]); ExchangeKnowledge(f.G, 2, 94);
            var w = f.World;
            w.Civilizations[1].NumAdvances = w.Civilizations[2].NumAdvances = 5;
            w.Civilizations[1].Treaties[2].Alliance = true;
            w.Globals.CivByRank[7] = 7; w.Globals.HumanPlayers = CivilizationSet.Only(7);
            w.Globals.CivsAlive = default; // The leader gate does not require a live slot.
            w.Globals.Difficulty = Difficulty.Emperor; w.Globals.Turn = 201;
            w.Civilizations[7].CityCount = 5; w.Civilizations[7].NumAdvances = 6;
            if (gate == "human") w.Globals.HumanPlayers = default;
            if (gate == "difficulty") w.Globals.Difficulty = Difficulty.Chieftain;
            if (gate == "cities") w.Civilizations[7].CityCount = 4;
            if (gate == "turn") w.Globals.Turn = 200;
            if (gate == "advances") w.Civilizations[7].NumAdvances = 5;
            using var op = ExchangeService(f).Begin(1, 2, "exchange"); CompleteExchange(op);
            Equal(gate == "pass", op.Result); f.G.Random.RequireChoicesConsumed();
        }
    }

    private static void AiExchangeScenarioAndAdmission()
    {
        foreach (bool scenario in new[] { false, true })
        {
            var f = new RetainedGrantFixture(scenario ? [] : [ExchangeDraw(94), ExchangeDraw(95, true)]);
            ExchangeKnowledge(f.G, 2, 94); ExchangeKnowledge(f.G, 1, 95);
            f.World.Globals.Status.Scenario = scenario; f.World.Scenario.Flags.NoConquestTechnology = true;
            using var op = ExchangeService(f).Begin(1, 2, "exchange"); CompleteExchange(op);
            Equal(!scenario, op.Result); f.G.Random.RequireChoicesConsumed();
        }
        var remote = new RetainedGrantFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = ExchangeService(remote).Begin(1, 2, "remote");
        Throws<InvalidOperationException>(() => rejected.Resume()); Equal((bool?)null, rejected.Result);
        Equal((byte)0, remote.World.Civilizations[1].NumAdvances);
        var empty = new RetainedGrantFixture(); using var none = ExchangeService(empty).Begin(1, 2, "empty");
        CompleteExchange(none); Equal(false, none.Result); empty.G.Random.RequireChoicesConsumed();
    }

    private static void AiExchangeRetainedGovernmentAndDisposal()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new RetainedGrantFixture([ExchangeDraw(AdvanceIds.Monarchy), ExchangeDraw(94, true)]);
            ExchangeKnowledge(f.G, 2, AdvanceIds.Monarchy); ExchangeKnowledge(f.G, 1, 94);
            using var op = ExchangeService(f).Begin(1, 2, "exchange");
            var pending = (ResearchGrantDecision.Government)op.Resume()!;
            Equal(1, pending.Choice.Civilization); Equal(pending, op.Resume()); Equal(false, op.Completed);
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            Equal(false, f.World.Civilizations[2].Advances.Contains(94));
            if (dispose)
            {
                op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume());
                Equal(false, f.World.Civilizations[2].Advances.Contains(94));
            }
            else
            {
                op.Resume(1); CompleteExchange(op);
                Equal(Government.Anarchy, f.World.Civilizations[1].Government);
                Equal(true, f.World.Civilizations[2].Advances.Contains(94));
                Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)1, f.World.Civilizations[2].NumAdvances);
            }
            f.G.Random.RequireChoicesConsumed();
        }
    }

    private static void AiExchangeRequiredFailurePrefixes()
    {
        var f = new RetainedGrantFixture([ExchangeDraw(94), ExchangeDraw(95, true)]);
        ExchangeKnowledge(f.G, 2, 94); ExchangeKnowledge(f.G, 1, 95);
        // Accepted inconsistent known-by summary reaches the real second grant's first-discovery draw.
        f.World.Globals.AdvanceKnownBy[95] = default;
        using var op = ExchangeService(f).Begin(1, 2, "exchange");
        Throws<ChoiceException>(() => op.Resume());
        Equal(true, f.World.Civilizations[1].Advances.Contains(94));
        Equal(true, f.World.Civilizations[2].Advances.Contains(95));
        Equal((byte)1, f.World.Civilizations[1].NumAdvances); Equal((byte)1, f.World.Civilizations[2].NumAdvances);
        Equal((bool?)null, op.Result); Throws<InvalidOperationException>(() => op.Resume());

        var early = new RetainedGrantFixture([ExchangeDraw(94)]);
        ExchangeKnowledge(early.G, 2, 94); ExchangeKnowledge(early.G, 1, 95);
        using var selection = ExchangeService(early).Begin(1, 2, "exchange");
        Throws<ChoiceException>(() => selection.Resume());
        Equal(false, early.World.Civilizations[1].Advances.Contains(94));
        Equal(false, early.World.Civilizations[1].Treaties[2].Tribute);

        var gift = new RetainedGrantFixture([ExchangeDraw(94, true)]);
        ExchangeKnowledge(gift.G, 1, 94); gift.World.Globals.AdvanceKnownBy[94] = default;
        gift.World.Globals.Difficulty = Difficulty.Emperor;
        gift.World.Civilizations[1].NumAdvances = 5; gift.World.Civilizations[2].Treaties[1].Alliance = true;
        using var tribute = ExchangeService(gift).Begin(1, 2, "exchange");
        Throws<ChoiceException>(() => tribute.Resume());
        Equal(true, gift.World.Civilizations[1].Treaties[2].Tribute);
        Equal(true, gift.World.Civilizations[2].Treaties[1].Tribute);
        Equal(true, gift.World.Civilizations[2].Advances.Contains(94));
    }

    private static void AiExchangeEmissaryDiagnostics()
    {
        var f = new RetainedGrantFixture([
            ExchangeDraw(94), ExchangeDraw(95, true),
            Choice.Create(new("V03.emissary", "exchange/grant/0"), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(0))
        ])
        { FailClose = true };
        ExchangeKnowledge(f.G, 3, 94); ExchangeKnowledge(f.G, 2, 95);
        f.World.Globals.AdvanceKnownBy[94] = default;
        using var op = ExchangeService(f).Begin(2, 3, "exchange");
        var pending = op.Resume();
        Equal(true, pending is ResearchGrantDecision.Emissary { Decision: EmissaryDecision.Receive });
        Equal(pending, op.Resume()); Equal(false, f.World.Civilizations[3].Advances.Contains(95));
        op.Resume(1); CompleteExchange(op);
        Equal(true, op.Result); Equal(1, op.PresentationFailures.Count); Equal(1, f.Closed);
        Equal(true, f.World.Civilizations[3].Advances.Contains(95));
        f.G.Random.RequireChoicesConsumed();
    }

    private static void DiplomacyTurnActualExchangeAndLibrary()
    {
        foreach (int turn in new[] { 8, 16 })
        {
            const string call = "turn/3/exchange";
            var choices = new List<Choice>
            {
                Choice.Create(new("V04.turn-random", "turn"), new SamplingLaw.PrimaryModulo(100), ChoiceCode.Primary(42)),
                Choice.Create(new("V04.turn-senate", "turn"), new SamplingLaw.PrimaryModulo(3), ChoiceCode.Primary(1)),
                ExchangeDraw(AdvanceIds.Monarchy, call: call), ExchangeDraw(94, true, call: call)
            };
            var f = new RetainedGrantFixture(choices); var w = f.World;
            ExchangeKnowledge(f.G, 3, AdvanceIds.Monarchy); ExchangeKnowledge(f.G, 2, 94);
            f.G.Data.AddCity(0, 1, new(10, 8)); w.Civilizations[1].CityCount = 1;
            w.Globals.WonderCity[WonderIds.GreatLibrary] = 0;
            w.Globals.Turn = (short)turn; w.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3);
            w.Civilizations[2].Treaties[3].Alliance = true;
            w.Civilizations[2].Treaties[4].ChangedThisTurn = true;
            var children = new DiplomacyTurnChildren();
            var service = new DiplomacyTurn(w, f.G.Rules, f.G.Random, ExchangeService(f), children);
            Throws<InvalidOperationException>(() => service.Run(2, "sync"));
            using var op = service.Begin(2, "turn");
            var pending = (DiplomacyTurnDecision.Research)op.Resume()!;
            Equal(1, ((ResearchGrantDecision.Government)pending.Decision).Choice.Civilization); Equal(pending, op.Resume());
            Equal((byte)254, w.Civilizations[1].AdvanceSource[AdvanceIds.Monarchy]);
            Equal(false, w.Civilizations[3].Advances.Contains(94));
            Equal(true, w.Civilizations[2].Treaties[4].ChangedThisTurn); Equal(0, children.Calls.Count);
            op.Resume(0);
            for (int i = 0; i < 50 && !op.Completed; ++i) { Equal(null, op.Resume()); }
            Equal(true, op.Completed); op.Resume();
            Equal(true, w.Civilizations[3].Advances.Contains(94));
            Equal(false, w.Civilizations[2].Treaties[4].ChangedThisTurn);
            Equal(false, children.Calls.Any(c => c.StartsWith("exchange/")));
            Equal(1, children.Calls.Count(c => c == "seek/2/1"));
            f.G.Random.RequireChoicesConsumed();
        }
    }
}
