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
    private sealed class DemandFixture : IDiplomaticDemandEffects, IDiplomaticProposalEffects
    {
        public TreatyFixture F { get; }
        public TechnologyFixture T => F.T;
        public RevelationFixture Data => F.Data;
        public GameState World => F.World;
        public DiplomaticDemands Demands { get; }
        public DiplomaticProposals Proposals { get; }
        public List<DiplomaticDemandNotice> Notices { get; } = [];
        public List<MapPoint> Redraws { get; } = [];
        public int BlinkClears, Cancellations, Refreshes;
        public bool FailNotice, FailRedraw, FailRefresh, FailCancel;
        public DemandFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            F = new(choices, data); World.Globals.Turn = 100;
            var government = new GovernmentChanges(World, Data.Process.Rules, Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government selector"), _ => throw new InvalidOperationException("Unexpected rates"), _ => { });
            var stance = new DiplomaticStance(World, Data.Process.Rules, new(World, Data.Process.Rules, government, T.T.Random));
            var war = War(Data, F.WarEffects, T.T.Random);
            Demands = new(World, Data.Process.Rules, T.T.Random, stance, T.Service, T.T.Research, Data.Stacks, war, this);
            Proposals = new(World, stance, F.Negotiation, Demands, war, this);
        }
        public DiplomaticDemands.Operation Begin(int proposal = 4) => Demands.Begin(1, 2, proposal, "demand");
        public void Peace(bool peace) => World.Civilizations[1].Treaties[2].Peace = World.Civilizations[2].Treaties[1].Peace = peace;
        public void Alliance() { World.Civilizations[1].Treaties[2].Alliance = World.Civilizations[2].Treaties[1].Alliance = true; }
        public void Intruder()
        {
            Data.AddCity(0, 1, new(8, 8)); Data.AddCity(1, 2, new(20, 8));
            Data.Unit(0).Owner = new(2); Data.Unit(0).Type = new(0); Data.Stacks.Relocate(0, new(10, 8), false);
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Role = RuleRole.Attack };
            Data.Unit(0).Visibility = CivilizationSet.Only(1); Data.Unit(0).Orders = UnitOrder.Fortified; Data.Unit(0).MovesUsed = 7;
        }
        public void Notice(DiplomaticDemandNotice notice)
        { Notices.Add(notice); if (FailNotice) { throw new InvalidOperationException("Missing demand notice"); } }
        public void RefreshStatus() { ++Refreshes; if (FailRefresh) { throw new InvalidOperationException("Missing status"); } }
        public void Redraw(MapPoint position) { Redraws.Add(position); if (FailRedraw) { throw new InvalidOperationException("Missing map view"); } }
        public void ClearActiveUnitBlink() => ++BlinkClears;
        public void CancelledAlliance(int actor, DiplomaticParty other)
        { ++Cancellations; if (FailCancel) { throw new InvalidOperationException("Missing cancellation notice"); } }
    }
    private static Choice DemandPrimary(string site, int bound, int roll, string call = "demand") =>
        Choice.Create(new("V05." + site, call), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(roll));
    private static Choice DemandProvoke(bool initial, string call = "demand") =>
        Choice.Create(new(initial ? "V05.demand-provoke-initial" : "V05.demand-provoke", call), new SamplingLaw.SecondaryRange(0, 2), ChoiceCode.Secondary(uint.MaxValue));

    private static void DiplomaticProposalOptionsAndDispatch()
    {
        var f = new DemandFixture(); Sequence(new[] { 0, 1, 4, 5, 7, 8 }, f.Proposals.Options(1, 2));
        f.World.Civilizations[1].Government = Government.Republic; f.World.Civilizations[2].Treaties[1].ChangedThisTurn = true;
        Sequence(new[] { 0, 1, 7, 8 }, f.Proposals.Options(1, 2));
        f.Peace(false); Sequence(new[] { 0, 2, 7, 8 }, f.Proposals.Options(1, 2));
        f.World.Civilizations[1].Government = (Government)257; Sequence(new[] { 0, 2, 4, 7, 8 }, f.Proposals.Options(1, 2));
        f.Alliance(); Sequence(new[] { 0, 3, 6, 7, 8 }, f.Proposals.Options(1, 2));
        foreach (int choice in new[] { 0, 7, 8, 99 })
        { using var ignored = f.Proposals.BeginResponse(1, 2, choice, "proposal"); ignored.Resume(); Equal(true, ignored.Completed); Equal(false, ignored.EndsAudience); }
        Equal(4, f.BlinkClears); Equal(0, f.Notices.Count);
        foreach (int proposal in new[] { 1, 2 })
        {
            f = new(); using var op = f.Proposals.BeginResponse(1, 2, proposal, "proposal"); op.Resume();
            Equal(true, op.Completed); Equal(proposal == 1 ? NegotiatedTreatyKind.Alliance : NegotiatedTreatyKind.Peace, f.F.Agreements.Single().Kind);
            Equal(1, f.BlinkClears); f.T.T.Random.RequireChoicesConsumed();
        }
    }

    private static void DiplomaticDemandProvocationTiming()
    {
        foreach (bool initial in new[] { false, true })
            foreach (bool failure in new[] { false, true })
            {
                var f = new DemandFixture([DemandProvoke(initial)]); f.Peace(false); var w = f.World;
                w.Civilizations[1].LastContactTurn[2] = -10;
                w.Diplomacy.DeclareWar = initial; w.Diplomacy.InitiallyAggressive = true; w.Diplomacy.Attitude = 80;
                f.FailNotice = failure;
                using var op = f.Begin();
                if (failure) { Throws<InvalidOperationException>(() => op.Resume()); Throws<InvalidOperationException>(() => op.Resume()); }
                else { op.Resume(); Equal(true, op.EndsAudience); }
                Equal(true, w.Civilizations[1].Treaties[2].War); Equal(false, w.Civilizations[2].Treaties[1].Aggressor);
                Equal(false, w.Diplomacy.DeclareWar); Equal((short)(initial || !failure ? 100 : -10), w.Civilizations[1].LastContactTurn[2]);
                Equal(initial, f.Notices.Single().Initial); Equal(2, f.Notices.Single().Variant);
                Equal((byte)0, w.Civilizations[1].Reputation); f.T.T.Random.RequireChoicesConsumed();
            }
        foreach (bool broken in new[] { false, true })
        {
            var f = new DemandFixture([DemandProvoke(true)]);
            if (broken) { f.World.Civilizations[2].Treaties[1].BrokenTreaty = true; }
            else { f.World.Civilizations[2].Treaties[1].HostileIntent = true; }
            using var op = f.Begin(5); op.Resume(); Equal(true, op.EndsAudience); Equal((sbyte)1, f.World.Civilizations[2].DiploRequestCount);
        }
    }

    private static void DiplomaticDemandWithdrawal()
    {
        var f = new DemandFixture(); f.Intruder(); f.FailRedraw = true;
        f.Data.AddUnit(1, 2, 3, new(10, 8)); f.Data.Rules.Units[3] = f.Data.Rules.Units[3] with { Role = RuleRole.Trade };
        f.Data.AddUnit(2, 1, 0, new(8, 10));
        f.Data.AddUnit(3, 2, 0, new(10, 10)); f.World.Map.TileAt(new(10, 10)).Terrain = Terrain.Ocean;
        f.Data.AddUnit(4, 2, 0, new(12, 8)); // Weighted distance exactly 3.
        f.Data.AddUnit(5, 2, 9, new(8, 10)); f.Data.Rules.Units[9] = f.Data.Rules.Units[9] with { Role = (RuleRole)200 };
        using var op = f.Begin(5); op.Resume(); Equal(true, op.Completed); Equal(false, op.EndsAudience);
        Equal(new MapPoint(20, 8), f.Data.Unit(0).Position); Equal(new MapPoint(20, 8), f.Data.Unit(5).Position);
        Equal(new MapPoint(10, 8), f.Data.Unit(1).Position); Equal(new MapPoint(8, 10), f.Data.Unit(2).Position);
        Equal(new MapPoint(10, 10), f.Data.Unit(3).Position); Equal(new MapPoint(12, 8), f.Data.Unit(4).Position);
        Equal(UnitOrder.None, f.Data.Unit(0).Orders); Equal(default(CivilizationSet), f.Data.Unit(0).Visibility); Equal((byte)7, f.Data.Unit(0).MovesUsed);
        Equal(2, f.Redraws.Count); Equal(2, op.PresentationFailures.Count); Equal(DiplomaticDemandNoticeKind.Apologize, f.Notices.Single().Kind);
        Equal((byte)22, f.World.Civilizations[2].Attitude[1]); f.T.T.Random.RequireChoicesConsumed();
        var fail = new DemandFixture(); fail.Intruder(); fail.FailNotice = true;
        using var broken = fail.Begin(5); Throws<InvalidOperationException>(() => broken.Resume()); Equal(new MapPoint(20, 8), fail.Data.Unit(0).Position);
        Equal((byte)22, fail.World.Civilizations[2].Attitude[1]);
    }

    private static void DiplomaticDemandWithdrawalDraws()
    {
        foreach (bool changed in new[] { false, true })
        {
            var choices = new List<Choice> { DemandPrimary("withdraw-government", 2, 0) };
            if (!changed) { choices.Add(DemandProvoke(false)); }
            var f = new DemandFixture(choices); f.Intruder(); var w = f.World;
            w.Diplomacy.InitiallyAggressive = true; w.Diplomacy.Attitude = 80; w.Civilizations[2].Government = Government.Monarchy;
            w.Civilizations[2].Treaties[1].ChangedThisTurn = changed;
            using var op = f.Begin(5); op.Resume(); Equal(!changed, op.EndsAudience);
            Equal(changed ? new MapPoint(20, 8) : new MapPoint(10, 8), f.Data.Unit(0).Position); f.T.T.Random.RequireChoicesConsumed();
        }
        var noViolators = new DemandFixture([DemandPrimary("demand-avoid-war", 3, 0)]); noViolators.Peace(false);
        noViolators.World.Diplomacy.InitiallyAggressive = true; noViolators.World.Diplomacy.Attitude = 20;
        using var avoid = noViolators.Begin(5); avoid.Resume(); Equal(false, avoid.EndsAudience);
        Equal(DiplomaticDemandNoticeKind.NoViolators, noViolators.Notices.Single().Kind); noViolators.T.T.Random.RequireChoicesConsumed();
        var protectedActor = new DemandFixture(); protectedActor.Intruder(); protectedActor.Data.AddCity(2, 1, new(28, 8));
        protectedActor.World.Globals.WonderCity[WonderIds.GreatWall] = 2;
        protectedActor.World.Diplomacy.InitiallyAggressive = true; protectedActor.World.Diplomacy.Attitude = 80;
        using var protectedWithdrawal = protectedActor.Begin(5); protectedWithdrawal.Resume();
        Equal(new MapPoint(20, 8), protectedActor.Data.Unit(0).Position); protectedActor.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticDemandTributeAndCeasefire()
    {
        foreach (int age in new[] { 7, 8, 15, 16 })
            foreach (bool peace in new[] { false, true })
            {
                var f = new DemandFixture(); f.Peace(peace); f.World.Diplomacy.Fear = 3; f.World.Diplomacy.LastContact = 100 - age;
                f.World.Civilizations[2].Gold = 700;
                int gold = age < 8 ? 0 : age < 16 ? 75 : 150; if (!peace) { gold /= 2; }
                using var op = f.Begin(); op.Resume(); Equal(true, op.Completed); Equal(false, op.EndsAudience);
                Equal(5000 + gold, f.World.Civilizations[1].Gold); Equal(700 - gold, f.World.Civilizations[2].Gold);
                Equal(!peace && gold != 0, f.World.Civilizations[1].Treaties[2].Ceasefire);
                Equal(!peace && gold != 0, f.World.Civilizations[1].Treaties[2].Tribute); f.T.T.Random.RequireChoicesConsumed();
            }
        var former = new DemandFixture(); former.Peace(false); former.World.Diplomacy.Fear = 2; former.World.Diplomacy.HadCeasefire = true;
        using var paid = former.Begin(); paid.Resume(); Equal(true, former.World.Civilizations[1].Treaties[2].Ceasefire); Equal(false, former.World.Civilizations[1].Treaties[2].Tribute);
        var negative = new DemandFixture(); negative.World.Civilizations[2].Gold = -50; negative.World.Diplomacy.Fear = 3; negative.FailRefresh = true;
        using var reverse = negative.Begin(); reverse.Resume(); Equal(4950, negative.World.Civilizations[1].Gold); Equal(0, negative.World.Civilizations[2].Gold);
        Equal(1, reverse.PresentationFailures.Count);
    }

    private static void DiplomaticDemandAlliedExchange()
    {
        foreach (sbyte captured in new sbyte[] { 1, 2 })
        {
            var f = new DemandFixture(); f.Alliance(); f.T.Know(2, 94); f.World.Civilizations[2].NumAdvances = 20;
            f.World.Civilizations[2].DiploRequestCount = captured; f.World.Diplomacy.Fear = 0;
            using var op = f.Begin(4); var decision = op.Resume();
            if (captured < 2)
            { Equal(null, decision); Equal(true, f.World.Civilizations[1].Advances.Contains(94)); }
            else
            {
                Equal(true, decision is DiplomaticDemandDecision.Technology { Decision: TechnologyNegotiationDecision.Sale });
                op.Resume(new(0)); Equal(false, f.World.Civilizations[1].Advances.Contains(94));
            }
            Equal(true, op.Completed); Equal(false, op.EndsAudience); Equal(0, f.Notices.Count); f.T.T.Random.RequireChoicesConsumed();
        }
        foreach (int outcome in new[] { 0, 1, 2 })
        {
            var f = new DemandFixture(); f.Alliance(); f.World.Globals.PowerRank[1] = (byte)(outcome == 0 ? 3 : 4);
            f.World.Diplomacy.InitiallyAggressive = outcome == 2;
            using var op = f.Begin(3); op.Resume(); Equal(true, op.Completed);
            Equal(new[] { DiplomaticDemandNoticeKind.Sympathy, DiplomaticDemandNoticeKind.FeebleAlly, DiplomaticDemandNoticeKind.TauntAlly }[outcome], f.Notices.Single().Kind);
            Equal(outcome == 2, op.EndsAudience); Equal(outcome != 2, f.World.Civilizations[1].Treaties[2].Alliance);
        }
    }

    private static void DiplomaticDemandGrantContinuation()
    {
        foreach (bool failure in new[] { false, true })
        {
            var f = new DemandFixture(); f.Peace(false); f.World.Diplomacy.Fear = 2;
            f.T.Know(2, AdvanceIds.Monarchy); f.World.Diplomacy.WantedTechnology = AdvanceIds.Monarchy;
            f.T.T.Retained!.FailRevolution = failure;
            using var op = f.Begin(); Equal(true, op.Resume() is DiplomaticDemandDecision.Grant { Decision: ResearchGrantDecision.Government });
            Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); Equal(AdvanceIds.Monarchy, f.World.Diplomacy.WantedTechnology);
            Equal(false, f.World.Civilizations[1].Treaties[2].Ceasefire); Equal(5000, f.World.Civilizations[1].Gold);
            if (failure) { Throws<InvalidOperationException>(() => op.Resume(new(1))); Equal(AdvanceIds.Monarchy, f.World.Diplomacy.WantedTechnology); }
            else { op.Resume(new(0)); Equal(true, op.Completed); Equal((int?)null, f.World.Diplomacy.WantedTechnology); Equal(true, f.World.Civilizations[1].Treaties[2].Ceasefire); }
        }
        var recent = new DemandFixture(); recent.World.Diplomacy.LastContact = 99; recent.World.Diplomacy.WantedTechnology = 94;
        recent.World.Diplomacy.Fear = 2; using var cleared = recent.Begin(); cleared.Resume();
        Equal((int?)null, recent.World.Diplomacy.WantedTechnology); Equal(DiplomaticDemandNoticeKind.Feeble, recent.Notices.Single().Kind);
    }

    private static void DiplomaticProposalCancelAlliance()
    {
        foreach (bool failure in new[] { false, true })
        {
            var f = new DemandFixture(); f.Alliance(); f.FailCancel = failure; f.World.Civilizations[1].Reputation = 255;
            f.Intruder(); f.World.Map.TileAt(new(10, 8)).Region = 1; f.World.Civilizations[1].ContinentCities[1] = 1;
            using var op = f.Proposals.BeginResponse(1, 2, 6, "proposal");
            if (failure) { Throws<InvalidOperationException>(() => op.Resume()); }
            else { op.Resume(); Equal(true, op.Completed); Equal(false, op.EndsAudience); }
            Equal(false, f.World.Civilizations[1].Treaties[2].Alliance); Equal((byte)70, f.World.Civilizations[2].Attitude[1]);
            Equal((byte)(failure ? 255 : 0), f.World.Civilizations[1].Reputation); Equal(1, f.Cancellations);
            Equal(failure ? 0 : 1, f.F.WarEffects.Notices.Count); f.T.T.Random.RequireChoicesConsumed();
            Equal(failure ? new MapPoint(10, 8) : new MapPoint(20, 8), f.Data.Unit(0).Position);
        }
    }

    private static void DiplomaticDemandAidAndWonderGates()
    {
        var f = new DemandFixture(); f.Alliance(); f.World.Globals.PowerRank[1] = 2; f.World.Globals.PowerRank[2] = 5;
        using var aid = f.Begin(3); aid.Resume(); Equal(3, f.World.Diplomacy.Fear);
        Equal(5150, f.World.Civilizations[1].Gold); Equal((byte)20, f.World.Civilizations[2].Attitude[1]);
        var technology = new DemandFixture(); technology.Alliance(); technology.T.Know(2, 94);
        technology.World.Globals.PowerRank[1] = 4; technology.World.Globals.PowerRank[2] = 5;
        technology.World.Civilizations[2].NumAdvances = 10; technology.World.Diplomacy.WantedTechnology = 94;
        using var gap = technology.Begin(3); gap.Resume(); Equal(true, gap.Completed);
        Equal(true, technology.World.Civilizations[1].Advances.Contains(94)); Equal(1, technology.World.Diplomacy.Fear);
        Equal(5000, technology.World.Civilizations[1].Gold); Equal((byte)20, technology.World.Civilizations[2].Attitude[1]);
        foreach (int wonder in new[] { WonderIds.GreatWall, WonderIds.UnitedNations })
        {
            var protectedOther = new DemandFixture(); protectedOther.World.Globals.WonderCity[wonder] = 0;
            protectedOther.World.Diplomacy.Fear = 3;
            using var blocked = protectedOther.Begin(); blocked.Resume(); Equal(5000, protectedOther.World.Civilizations[1].Gold);
            Equal(DiplomaticDemandNoticeKind.Feeble, protectedOther.Notices.Single().Kind);
        }
        var notorious = new DemandFixture([DemandPrimary("withdraw-government", 2, 0), DemandProvoke(false)]);
        notorious.Intruder(); notorious.Data.AddCity(2, 1, new(28, 8)); notorious.World.Globals.WonderCity[WonderIds.GreatWall] = 2;
        notorious.World.Civilizations[1].Reputation = 7; notorious.World.Diplomacy.InitiallyAggressive = true; notorious.World.Diplomacy.Attitude = 80;
        using var unprotected = notorious.Begin(5); unprotected.Resume(); Equal(true, unprotected.EndsAudience);
        Equal(new MapPoint(10, 8), notorious.Data.Unit(0).Position); notorious.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticProposalPrivateGrant()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var f = new DemandFixture(data: CreationFixture(new(runtime.Process, runtime.World))); f.Peace(false);
        f.T.Know(2, AdvanceIds.Monarchy); f.World.Diplomacy.Fear = 2; f.World.Diplomacy.WantedTechnology = AdvanceIds.Monarchy;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.proposal", "Authored diplomatic demand", AccessClass.Player, new([]), new([new("endsAudience", ValueKind.Boolean)])),
            (_, _, _) => new(), _ => new DiplomaticProposalOperation(f.Proposals.BeginResponse(1, 2, 4, "proposal"), _ => recipient,
                _ => throw new InvalidOperationException("Unexpected intelligence"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
        var command = actor.Submit(new("test.proposal", new([]))).Correlation.Command; runtime.AdvanceOne();
        var prompt = RequiredInteraction(player, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); Equal(false, f.World.Civilizations[1].Treaties[2].Ceasefire);
        Equal(false, actor.Reply(prompt, 0).Accepted); Equal(true, player.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, f.World.Civilizations[1].Treaties[2].Ceasefire);
        Equal((int?)null, f.World.Diplomacy.WantedTechnology); Equal(1, f.BlinkClears); f.T.T.Random.RequireChoicesConsumed();
    }

    private static void DiplomaticProposalSessionCancellation()
    {
        foreach (var input in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
            foreach (bool treaty in new[] { false, true })
            {
                var f = new DemandFixture(treaty ? [Choice.Create(new("V05.treaty-gold", "proposal/treaty"), new SamplingLaw.SecondaryRange(0, 1), ChoiceCode.Secondary(0))] : []);
                f.World.Civilizations[2].DiploRequestCount = 2;
                if (!treaty) { f.Alliance(); f.T.Know(2, 94); f.World.Civilizations[2].NumAdvances = 20; }
                var context = new OperationContext(f.World, f.Data.Process, f.T.T.Random, new PresentationRandom(1, "proposal"),
                    new(default, new(1)), default, (_, _) => { }, () => { });
                using var op = new DiplomaticProposalOperation(f.Proposals.BeginResponse(1, 2, treaty ? 1 : 3, "proposal"), c => new((ulong)c),
                    _ => throw new InvalidOperationException("Unexpected intelligence"));
                Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
                Equal(CommandStatus.Completed, op.Resume(context, new(input)).Status);
                Equal(5000, f.World.Civilizations[1].Gold); Equal(false, f.World.Civilizations[1].Advances.Contains(94));
                Equal(!treaty, f.World.Civilizations[1].Treaties[2].Alliance); f.T.T.Random.RequireChoicesConsumed();
            }
        var stopped = new DemandFixture(); stopped.Peace(false); stopped.T.Know(2, AdvanceIds.Monarchy);
        stopped.World.Diplomacy.Fear = 2; stopped.World.Diplomacy.WantedTechnology = AdvanceIds.Monarchy;
        using var pending = stopped.Proposals.BeginResponse(1, 2, 4, "proposal"); pending.Resume(); pending.Dispose();
        Throws<ObjectDisposedException>(() => pending.Resume(new(0)));
        Equal(true, stopped.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy)); Equal(false, stopped.World.Civilizations[1].Treaties[2].Ceasefire);
    }
}
