using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static Choice ExecutionRoll(string role, int strength, int code, int index = 0) =>
        Choice.Create(new("U06.round." + role, "combat/rounds", index), new SamplingLaw.PrimaryModulo(strength), ChoiceCode.Primary(code));
    private static Choice ExecutionPromotion(int strength = 84, int code = 0) =>
        Choice.Create(new("U06.promotion", "combat/resolution/promotion"), new SamplingLaw.PrimaryModulo(strength), ChoiceCode.Primary(code));
    private static List<Choice> ExecutionWin() => [ExecutionRoll("attack", 80, 79), ExecutionRoll("defense", 4, 0), ExecutionPromotion()];
    private sealed class ExecutionFixture : ICombatAftermathEffects, IEmissaryEffects, IAllyHelpEffects
    {
        public ContinuationFixture F { get; }
        public GameState World => F.World;
        public CombatWorkspace State => F.State;
        public CombatAftermath Aftermath { get; }
        public CombatExecution Combat { get; }
        public List<string> Calls { get; } = [];
        public List<CombatFinalNotice> Notices { get; } = [];
        public List<EmissaryNotice> Emissaries { get; } = [];
        public WarNotices WarEffects { get; } = new();
        public string? Failure;
        public ExecutionFixture(List<Choice>? choices = null, RevelationFixture? data = null, bool retainGrants = false)
        {
            F = new(choices, data, retainGrants); World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
            World.Names[1].Tribe = "Attackers"; World.Names[2].Tribe = "Defenders";
            var government = new GovernmentChanges(World, F.Data.Process.Rules, F.Data.Stacks,
                _ => throw new InvalidOperationException("Unexpected government choice"), _ => throw new InvalidOperationException("Unexpected rate choice"), _ => { });
            var policy = new GovernmentPolicy(World, F.Data.Process.Rules, government, F.Life.Random);
            var war = War(F.Data, WarEffects, F.Life.Random);
            var reception = new EmissaryReception(World, F.Data.Rules, F.Life.Random, new(World, F.Data.Process.Rules, policy), this);
            Aftermath = new(World, State, war, new(World, reception, war, this), () => Effect("publish"), this);
            var entry = new CombatEntry(World, F.Data.Rules, State, F.Life.Random, new(World, F.Data.Rules, _ => Effect("attack-notice")), war, policy, _ => Effect("treaty-entry"));
            var opening = new CombatOpening(World, F.Data.Rules, State, F.Data.Stacks, new(World, F.Data.Rules, F.Data.Stacks),
                () => Effect("opening-publish"), _ => Effect("opening"));
            Combat = new(World, State, new(new(World, F.Data.Rules, F.Data.Stacks, State), entry, opening,
                new(World, F.Data.Rules, F.Life.Random, State, new RoundEffects()), F.Resolution(), Aftermath, F.Continuation));
        }
        private void Effect(string value) { Calls.Add(value); if (Failure == value) throw new InvalidOperationException("Unavailable " + value); }
        public void Notice(CombatFinalNotice notice) { Notices.Add(notice); Effect("notice:" + notice.Kind); }
        public void Notice(EmissaryNotice notice) { Emissaries.Add(notice); Effect("emissary:" + notice.Kind); }
        public void Redraw(MapPoint origin, MapPoint target) => Effect("redraw");
        public void RefreshInfo() => Effect("info");
        public void RefreshMilitaryAdvisor() => Effect("advisor");
        public void OpenLeader(int counterpart) => Effect("leader");
        public void CloseLeader() => Effect("close-leader");
        public void EnsureVisible(MapPoint point, int observer) => Effect("focus");
        public void DidNotHelp(AllyHelpRequest request) => Effect("did-not-help");
        public void RefreshStatus() => Effect("status");
        public CombatExecution.Operation Begin(bool execute = true, int budget = 64) => Combat.Begin(new(0), 1, new(false, execute), "", "combat", roundBudget: budget);
        public CombatAftermath.Operation Finish(bool ransom = false, bool force = false, bool won = true) =>
            Aftermath.Begin(F.Prepared, new(won ? 1 : 2, won ? 2 : 1, ransom, new(0)), force, "after");
        public void Complete(CombatExecution.Operation operation)
        {
            for (int i = 0; i < 100 && !operation.Completed; ++i) Equal(null, operation.Resume());
            Equal(true, operation.Completed); F.Life.Random.RequireChoicesConsumed();
        }
        public void AllySituation()
        {
            World.Multiplayer.LocalHuman = 3; World.Globals.HumanPlayers = CivilizationSet.Only(3);
            World.Civilizations[2].Treaties[3].Alliance = true; World.Civilizations[2].Gold = 1000;
            World.Civilizations[2].ContinentCities[1] = 2; World.Civilizations[2].ContinentMilitary[1] = 1;
            World.Civilizations[1].ContinentAttack[1] = 10;
            State.AggregateAllyWarning = 1;
        }
    }

    private static void CombatAftermathNoticesAndRansom()
    {
        foreach (int observer in new[] { 1, 2, 3 })
            foreach (bool autoplay in new[] { false, true })
            {
                var f = new ExecutionFixture(); f.World.Multiplayer.LocalHuman = (sbyte)observer; f.World.Multiplayer.Saved.AutoplayActive = autoplay;
                f.State.AnyViewer = true; f.State.KillCount = 3; f.State.AggregateTreatyNotice = true; f.State.AggregateAllyWarning = 1;
                f.World.Globals.BarbarianActivity = 255; f.World.Civilizations[1].Gold = int.MaxValue;
                using var op = f.Finish(ransom: true); Equal(null, op.Resume()); Equal(true, op.Completed);
                Equal(unchecked(int.MaxValue + 12750), f.World.Civilizations[1].Gold); Equal("publish", f.Calls[0]); Equal("redraw", f.Calls[1]); Equal("advisor", f.Calls[^1]);
                Equal(autoplay ? 0 : observer == 1 ? 3 : observer == 2 ? 2 : 1, f.Notices.Count);
                if (!autoplay)
                {
                    Equal(CombatFinalNoticeKind.CancelPeace, f.Notices[^1].Kind); Equal("Defenders", f.Notices[^1].DefenderName);
                    Equal("Attackers", f.Notices[^1].AttackerName); Equal(observer, f.Notices[^1].Recipient);
                    if (observer < 3) { Equal(observer == 1 ? CombatFinalNoticeKind.MultipleWin : CombatFinalNoticeKind.MultipleLoss, f.Notices[0].Kind); Equal(3, f.Notices[0].Count); }
                    if (observer == 1) Equal(12750, f.Notices.Single(n => n.Kind == CombatFinalNoticeKind.Ransom).Gold);
                }
                Equal(!autoplay && observer == 1, f.Calls.Contains("info")); Equal(false, f.Calls.Contains("leader")); f.F.Life.Random.RequireChoicesConsumed();
            }
        var silent = new ExecutionFixture(); silent.State.KillCount = 1;
        using var ordinary = silent.Finish(); ordinary.Resume(); Equal(0, silent.Notices.Count); Equal("publish,advisor", string.Join(',', silent.Calls));
        foreach (int warning in new[] { 2, 3 })
        {
            var f = new ExecutionFixture(); f.State.AggregateAllyWarning = warning;
            using var op = f.Finish(); op.Resume(); Equal(warning == 2 ? 1 : 0, f.Notices.Count);
            if (warning == 2) Equal(CombatFinalNoticeKind.AllyAttacking, f.Notices[0].Kind);
        }
    }

    private static void CombatAftermathAlliesAndHelp()
    {
        var activated = new ExecutionFixture(); activated.World.Civilizations[2].Treaties[1].Contact = true;
        activated.World.Civilizations[2].Treaties[4].Alliance = true; activated.World.Civilizations[4].Treaties[1].Contact = true;
        using var joined = activated.Finish(force: true); joined.Resume(); Equal(true, activated.World.Civilizations[4].Treaties[1].War);
        Equal((byte)100, activated.World.Civilizations[4].Attitude[1]);
        var f = new ExecutionFixture([GreetingChoice("after/help/emissary/greet")]); f.AllySituation();
        using var op = f.Finish(ransom: true); var receive = op.Resume(); Equal(true, receive is AllyHelpDecision.Emissary); Equal(receive, op.Resume());
        int gold = f.World.Civilizations[1].Gold; Equal(1, f.Calls.Count(c => c == "publish")); Equal(false, f.Calls.Contains("advisor"));
        Equal(true, op.Resume(0) is AllyHelpDecision.Demand);
        var bonus = (AllyHelpDecision.Bonus)op.Resume(0)!; Equal(500, bonus.Request.Payment); Equal(3, bonus.Request.Recipient);
        Equal(null, op.Resume(1)); Equal(true, op.Completed); Equal(true, f.World.Civilizations[3].Treaties[1].War);
        Equal(gold, f.World.Civilizations[1].Gold); Equal(500, f.World.Civilizations[2].Gold); Equal(500, f.World.Civilizations[3].Gold);
        Equal("advisor", f.Calls[^1]); Equal(1, f.Calls.Count(c => c == "publish")); f.F.Life.Random.RequireChoicesConsumed();
    }

    private static void CombatAftermathFailuresAndDisposal()
    {
        foreach (string failure in new[] { "redraw", "info", "advisor" })
        {
            var f = new ExecutionFixture { Failure = failure }; f.State.AnyViewer = true; f.World.Globals.BarbarianActivity = 2;
            using var op = f.Finish(ransom: true); op.Resume(); Equal(true, op.Completed); Equal(1, op.PresentationFailures.Count); Equal(110, f.World.Civilizations[1].Gold);
        }
        var notice = new ExecutionFixture { Failure = "notice:Ransom" }; notice.World.Globals.BarbarianActivity = 2;
        using var failed = notice.Finish(ransom: true); Throws<InvalidOperationException>(() => failed.Resume());
        Equal(110, notice.World.Civilizations[1].Gold); Equal(false, failed.Completed); Equal(false, notice.Calls.Contains("advisor")); Throws<InvalidOperationException>(() => failed.Resume());
        var disposed = new ExecutionFixture(); disposed.AllySituation(); var stopped = disposed.Finish(); stopped.Resume(); stopped.Dispose();
        Throws<ObjectDisposedException>(() => stopped.Resume(0)); Equal(true, disposed.World.Diplomacy.AskedEmissary); Equal(false, disposed.Calls.Contains("advisor"));
        var remote = new ExecutionFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Finish(true); Throws<InvalidOperationException>(() => denied.Resume()); Equal(0, remote.Calls.Count);
    }

    private static void CombatExecutionWinAndYield()
    {
        foreach (bool charge in new[] { false, true })
        {
            var f = new ExecutionFixture(ExecutionWin()); f.State.KillCount = 77; using var op = f.Begin(charge); f.Complete(op);
            Equal(1, op.Result); Equal(charge ? (byte)3 : (byte)0, f.F.Data.Unit(0).MovesUsed); Equal(80, op.Combat!.Attack);
            Equal(true, f.F.Data.Unit(0).Token.Occupied && f.F.Data.Unit(0).Flags.Veteran); Equal(false, f.F.Data.Unit(1).Token.Occupied);
            Equal(1, f.State.KillCount); Equal((sbyte)4, f.World.Entities.Cities.Storage[0].Size); Equal(1, f.World.Casualties[2].Count);
            Equal("advisor", f.Calls[^1]); Equal(1, f.Calls.Count(c => c == "opening")); Equal(1, f.Calls.Count(c => c == "publish"));
            Equal(null, op.Resume(new(0))); Equal(1, f.State.KillCount);
        }
        var bounded = new ExecutionFixture([ExecutionRoll("attack", 80, 79), ExecutionRoll("defense", 4, 0),
            ExecutionRoll("attack", 80, 79, 1), ExecutionRoll("defense", 4, 0, 1), ExecutionPromotion()]);
        bounded.F.Data.Rules.Units[0] = bounded.F.Data.Rules.Units[0] with { Firepower = 5 };
        using var rounds = bounded.Begin(budget: 1); Equal(null, rounds.Resume()); Equal(false, rounds.Completed); Equal((byte)5, bounded.F.Data.Unit(1).Damage);
        Equal(null, rounds.Resume(new(1))); Equal(false, rounds.Completed); Equal((byte)10, bounded.F.Data.Unit(1).Damage);
        bounded.Complete(rounds); Equal(1, rounds.Result); Equal((byte)3, bounded.F.Data.Unit(0).MovesUsed); Equal(1, bounded.State.KillCount);
    }

    private static void CombatExecutionEarlyAndZeroFirepower()
    {
        var missing = new ExecutionFixture(); missing.F.ClearDefender(); using var empty = missing.Begin(); missing.Complete(empty);
        Equal(999, empty.Result); Equal((byte)0, missing.F.Data.Unit(0).MovesUsed); Equal(0, missing.Calls.Count);
        var blocked = new ExecutionFixture(); blocked.World.Civilizations[1].Treaties[2].Peace = true;
        using var cancelled = blocked.Begin(); var question = cancelled.Resume(); Equal(true, question is CombatDecision.Attack);
        Equal((byte)3, blocked.F.Data.Unit(0).MovesUsed); Equal(question, cancelled.Resume()); cancelled.Resume(new(0)); blocked.Complete(cancelled);
        Equal(1, cancelled.Result); Equal((byte)3, blocked.F.Data.Unit(0).MovesUsed); Equal(true, blocked.F.Data.Unit(1).Token.Occupied);
        Equal(false, blocked.Calls.Contains("opening") || blocked.Calls.Contains("publish"));
        var zero = new ExecutionFixture([ExecutionPromotion()]); zero.F.Data.Rules.Units[0] = zero.F.Data.Rules.Units[0] with { Firepower = 0 };
        zero.F.Data.Rules.Units[4] = zero.F.Data.Rules.Units[4] with { Firepower = 0 };
        using var loss = zero.Begin(); zero.Complete(loss); Equal(0, loss.Result); Equal(false, zero.F.Data.Unit(0).Token.Occupied);
        Equal(true, zero.F.Data.Unit(1).Token.Occupied && zero.F.Data.Unit(1).Flags.Veteran); Equal(1, zero.World.Casualties[1].Count);
        Equal((byte)1, zero.World.Civilizations[1].AggressionCounter[2]); Equal("advisor", zero.Calls[^1]);
    }

    private static void CombatExecutionNuclearAndFailures()
    {
        foreach (bool intercept in new[] { false, true })
        {
            var f = new ExecutionFixture(); f.F.Missile(); f.F.Ocean(); f.State.KillCount = 9;
            if (intercept) OpenCiv2.Core.City.BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.SdiDefense, true);
            using var op = f.Begin(); f.Complete(op); Equal(0, op.Result); Equal(false, f.F.Data.Unit(0).Token.Occupied);
            Equal(intercept, f.F.Data.Unit(1).Token.Occupied); Equal(intercept ? (sbyte)5 : (sbyte)3, f.World.Entities.Cities.Storage[0].Size);
            Equal(9, f.State.KillCount); Equal(0, f.World.Casualties[1].Count); Equal(false, f.Calls.Contains("publish") || f.Calls.Contains("advisor"));
        }
        var cosmetic = new ExecutionFixture(ExecutionWin()) { Failure = "opening" }; using var continued = cosmetic.Begin(); cosmetic.Complete(continued);
        Equal(1, continued.Result); Equal(1, continued.PresentationFailures.Count); Equal(false, cosmetic.F.Data.Unit(1).Token.Occupied);
        var failed = new ExecutionFixture(); using var noDraw = failed.Begin(); Throws<ChoiceException>(() => noDraw.Resume());
        Equal((byte)3, failed.F.Data.Unit(0).MovesUsed); Equal(true, failed.F.Data.Unit(0).Token.Occupied && failed.F.Data.Unit(1).Token.Occupied);
        Throws<InvalidOperationException>(() => noDraw.Resume()); Equal(false, failed.Calls.Contains("publish"));
        var remote = new ExecutionFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Begin(); Throws<InvalidOperationException>(() => denied.Resume()); Equal((byte)0, remote.F.Data.Unit(0).MovesUsed);
    }

    private static void CombatExecutionOrdinaryLossAndRansom()
    {
        var loss = new ExecutionFixture([ExecutionRoll("attack", 80, 0), ExecutionRoll("defense", 4, 3), ExecutionPromotion()]);
        loss.F.Data.Rules.Units[4] = loss.F.Data.Rules.Units[4] with { Firepower = 10 };
        using var defeated = loss.Begin(); loss.Complete(defeated); Equal(0, defeated.Result);
        Equal(false, loss.F.Data.Unit(0).Token.Occupied); Equal(true, loss.F.Data.Unit(1).Token.Occupied && loss.F.Data.Unit(1).Flags.Veteran);
        Equal(1, loss.World.Casualties[1].Count); Equal((sbyte)5, loss.World.Entities.Cities.Storage[0].Size);

        var ransom = new ExecutionFixture(ExecutionWin()); ransom.World.Globals.BarbarianActivity = 3;
        var target = ransom.F.Prepared.Target; ransom.World.Entities.Cities.Storage[0].Token = default;
        ransom.World.Map.TileAt(target).Features &= ~MapFeatures.CitySite;
        ransom.F.Data.Rules.Units[20] = ransom.F.Data.Rules.Units[4] with { Role = RuleRole.Diplomacy };
        ransom.F.Data.Unit(1).Type = new(20); ransom.F.Data.Unit(1).Owner = new(0); ransom.World.Map.TileAt(target).Owner = 0;
        ransom.F.Data.AddUnit(2, 0, 20, target); ransom.World.Civilizations[0].UnitsActive[20] = 2;
        using var victory = ransom.Begin(); ransom.Complete(victory); Equal(1, victory.Result); Equal(2, ransom.State.KillCount);
        Equal(2, ransom.World.Casualties[0].Count); Equal(160, ransom.World.Civilizations[1].Gold);
        Equal(false, ransom.F.Data.Unit(1).Token.Occupied || ransom.F.Data.Unit(2).Token.Occupied);
        Equal("MultipleWin,Ransom", string.Join(',', ransom.Notices.Select(n => n.Kind))); Equal(150, ransom.Notices[1].Gold);
        Equal(true, ransom.Calls.IndexOf("notice:MultipleWin") < ransom.Calls.IndexOf("notice:Ransom"));
        Equal("advisor", ransom.Calls[^1]);
    }

    private static void CombatExecutionHelpSession()
    {
        using var runtime = new SessionRuntime(new(), new(233));
        var f = new ExecutionFixture([ExecutionPromotion(), GreetingChoice("combat/aftermath/help/emissary/greet")], CreationFixture(new(runtime.Process, runtime.World)));
        f.AllySituation(); f.World.Civilizations[3].Treaties[2].Alliance = true; f.World.Civilizations[1].Treaties[2].Peace = true;
        f.F.Data.Rules.Units[0] = f.F.Data.Rules.Units[0] with { Firepower = 0 }; f.F.Data.Rules.Units[4] = f.F.Data.Rules.Units[4] with { Firepower = 0 };
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.combat-help", "Authored combat with ally help", AccessClass.Player, new([]), new([new("result")])),
            (_, _, _) => new(), _ => new CombatOperation(f.Begin(), _ => recipient, _ => throw new InvalidOperationException("Unexpected intelligence"), _ => throw new InvalidOperationException("Unexpected city"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 3); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.combat-help", new([]))).Correlation.Command; runtime.AdvanceOne();
        var question = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, f.F.Data.Unit(0).Token.Occupied); Equal(1, f.World.Casualties[1].Count); Equal(false, f.Calls.Contains("advisor"));
        Equal(false, actor.Reply(question, 0).Accepted); Equal(true, human.Reply(question, 0).Accepted); runtime.AdvanceOne();
        question = RequiredInteraction(human, command); Equal(true, human.Reply(question, 0).Accepted); runtime.AdvanceOne();
        question = RequiredInteraction(human, command); Equal(true, human.Reply(question, 1).Accepted); runtime.AdvanceOne();
        var result = RequiredResult(actor, command); Equal(CommandStatus.Completed, result.Status); Equal(new CommandValue.Integer(0), result.Result.Single().Value);
        Equal(500, f.World.Civilizations[3].Gold); Equal(true, f.World.Civilizations[3].Treaties[1].War); Equal(1, f.World.Casualties[1].Count);
        Equal(1, f.Calls.Count(c => c == "publish")); Equal("advisor", f.Calls[^1]); f.F.Life.Random.RequireChoicesConsumed();
    }

    private static void CombatExecutionNestedLossSession()
    {
        using var runtime = new SessionRuntime(new(), new(234));
        var f = new ExecutionFixture([ExecutionPromotion()], CreationFixture(new(runtime.Process, runtime.World)));
        f.F.Data.Rules.Units[0] = f.F.Data.Rules.Units[0] with { Firepower = 0, Role = RuleRole.Settle };
        f.F.Data.Rules.Units[4] = f.F.Data.Rules.Units[4] with { Firepower = 0 }; f.World.Civilizations[1].CityCount = 0;
        f.F.Data.AddUnit(2, 3, 4, new(24, 10)); f.F.Data.Unit(2).Flags.SpyMission = true; f.F.Data.Unit(2).Counter = 1;
        f.World.Civilizations[1].Treaties[3].Peace = true; ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.combat-loss", "Authored combat with nested loss", AccessClass.Player, new([]), new([new("result")])),
            (_, _, _) => new(), _ => new CombatOperation(f.Begin(), _ => recipient, _ => throw new InvalidOperationException("Unexpected intelligence"), _ => throw new InvalidOperationException("Unexpected city"))));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var victim = SessionClient(runtime, 1); recipient = victim.Identity.Participant;
        var command = actor.Submit(new("test.combat-loss", new([]))).Correlation.Command; runtime.AdvanceOne();
        var question = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction);
        Equal(false, f.F.Data.Unit(0).Token.Occupied || f.F.Data.Unit(2).Token.Occupied); Equal(1, f.World.Casualties[1].Count);
        Equal(false, actor.Reply(question, 0).Accepted); Equal(true, victim.Reply(question, 0).Accepted); runtime.AdvanceOne();
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(EndReason.Defeat, f.World.PendingEndReason);
        Equal(1, f.State.KillCount); Equal(0, f.F.Life.Checks); f.F.Life.Random.RequireChoicesConsumed();
    }
}
