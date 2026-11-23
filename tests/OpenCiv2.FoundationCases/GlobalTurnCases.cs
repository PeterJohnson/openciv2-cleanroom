using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static Choice HistoryDraw(int value) => Choice.Create(new("F04.history", "advance"), new SamplingLaw.PrimaryModulo(40), ChoiceCode.Primary(value));
    private sealed class GlobalTurnFixture : IBarbarianSpawningEffects
    {
        public UnitTurnFixture Units { get; }
        public RevelationFixture Data => Units.Data;
        public GameState World => Data.World;
        public GameplayRandom Random => Units.Life.Random;
        public GlobalTurn Turn { get; }
        public List<BarbarianUprisingNotice> Uprisings { get; } = [];
        public List<WarmingNotice> Warnings { get; } = [];
        public bool FailPresentation;
        public GlobalTurnFixture(List<Choice>? choices = null, RevelationFixture? data = null)
        {
            Units = new(choices, data);
            World.Globals.Turn = 0; World.Globals.Year = 123; World.Globals.FirstLaunchTurn = -1;
            World.Globals.Difficulty = Difficulty.Chieftain; World.Globals.BarbarianActivity = 0;
            World.Globals.NextHistoryTurn = short.MaxValue; World.Globals.TransientFlags.EndOfTurn = true;
            Data.Unit(0).MovesUsed = 4;
            Data.Rules.Units[UnitTypeIds.Armor] = Data.Rules.Units[4] with { Prerequisite = (AdvanceReference)AdvanceIds.MobileWarfare };
            var spawning = new BarbarianSpawning(World, Data.Rules, Random, new(World, Data.Process, Data.Stacks, Data), Data.Stacks,
                Units.Life.Loss, Units.Life.Elimination, Units.Incidents.Incidents, this);
            var warming = new GlobalWarming(World, n => { Warnings.Add(n); if (FailPresentation) throw new InvalidOperationException("Notice unavailable"); }, _ => { });
            Turn = new(World, Data.Rules, Random, GameplayContent.Builtin.Values.Calendar, spawning, warming,
                new(World, Random), new(World, ScoreFor(Data)), Units.Reset);
        }
        public void Palace(int turn = 2)
        {
            Units.City(new(10, 10), BuildingIds.Palace); World.Entities.Cities.Storage[0].Size = 4;
            World.Civilizations[1].TotalCitySize = 4; World.Globals.Turn = (short)turn;
            World.Globals.BarbarianActivity = 2; World.Globals.GameOptions.Throne = true;
        }
        public void Ship(int owner)
        { World.Civilizations[owner].SpaceshipFlags.Launched = true; World.Civilizations[owner].SpaceshipArrivalYear = short.MinValue; }
        public GlobalTurn.Operation Begin() => Turn.Begin(0, "advance");
        public void Complete(GlobalTurn.Operation op)
        {
            for (int i = 0; i < 100 && !op.Completed; ++i) Equal(null, op.Resume());
            Equal(true, op.Completed); Random.RequireChoicesConsumed();
        }
        public void Notice(BarbarianUprisingNotice notice)
        { Uprisings.Add(notice); if (FailPresentation) throw new InvalidOperationException("Uprising notice unavailable"); }
        public void Redraw(MapPoint point) { }
    }

    // F04 §4: initial advance skips all four old-turn children but performs the real reset.
    private static void GlobalTurnInitialAndIdempotence()
    {
        var f = new GlobalTurnFixture(); f.World.Globals.BarbarianActivity = 255;
        f.World.Globals.WarmingCounter = 17; f.World.Globals.WarmingPollution = 9;
        f.World.Globals.PowerRank[1] = 99; f.World.Score.Total = 1234;
        f.Data.Unit(0).Flags.Moved = f.Data.Unit(0).Flags.Waiting = true;
        using var op = f.Begin(); f.Complete(op);
        Equal((short)1, f.World.Globals.Turn); Equal((sbyte)17, f.World.Globals.WarmingCounter);
        Equal((short)9, f.World.Globals.WarmingPollution); Equal((byte)99, f.World.Globals.PowerRank[1]);
        Equal(1234, f.World.Score.Total); Equal(null, op.Progress); Equal((byte)0, f.Data.Unit(0).MovesUsed);
        Equal(false, f.Data.Unit(0).Flags.Waiting); Equal(true, f.Data.Unit(0).Flags.Moved);
        Equal(false, f.World.Globals.TransientFlags.EndOfTurn);
        Equal(null, op.Resume(new GlobalTurnReply.Acknowledge())); Equal((short)1, f.World.Globals.Turn);
    }
    private static void GlobalTurnOldTurnAndThrone()
    {
        var f = new GlobalTurnFixture(); f.Palace(3);
        f.World.Globals.WarmingCounter = 16; f.World.Globals.PollutedTiles = 20; f.World.Globals.WarmingPollution = 2;
        f.World.Civilizations[1].FutureTechs = 40;
        using var op = f.Begin(); var pending = op.Resume(); Equal(true, pending is GlobalTurnDecision.Throne);
        Equal((short)3, f.World.Globals.Turn); Equal((short)123, f.World.Globals.Year);
        Equal((sbyte)1, f.World.Globals.GlobalWarmings); Equal((short)0, f.World.Globals.WarmingPollution);
        Equal(-200, f.World.Score.Pollution); Equal(4, f.World.Score.Total); // Warming clears the tally before actual throne scoring.
        Equal((byte)4, f.World.PowerHistory[0][1]); Equal((byte)0, f.World.PowerHistory[1][1]);
        Equal((short)1, f.World.Civilizations[1].ThroneRoomUpgrades); Equal((byte)4, f.Data.Unit(0).MovesUsed);
        Equal(pending, op.Resume()); Equal(pending, op.Resume(new GlobalTurnReply.Acknowledge()));
        Equal(pending, op.Resume(new GlobalTurnReply.Throne(new(ThroneUpgradeKind.Overlay, 0))));
        Equal(null, op.Resume(new GlobalTurnReply.Throne(new(ThroneUpgradeKind.Piece, 0)))); f.Complete(op);
        Equal((short)4, f.World.Globals.Turn); Equal((byte)1, f.World.Civilizations[1].ThroneRoom[0]);
        Equal((byte)0, f.Data.Unit(0).MovesUsed); Equal((short)1, f.World.Civilizations[1].ThroneRoomUpgrades);
        var early = new GlobalTurnFixture(); early.Palace(1); using var first = early.Begin(); early.Complete(first);
        Equal((short)2, early.World.Globals.Turn); Equal((short)0, early.World.Civilizations[1].ThroneRoomUpgrades);
    }
    private static void GlobalTurnRealSpawningAndCosmeticFailures()
    {
        var choices = new List<Choice>();
        foreach (var (site, bound, value) in new[] { ("treasury", 100, 99), ("land-x", 13, 8), ("land-y", 13, 6), ("size", 8, 6), ("mounted", 3, 1) })
            choices.Add(Choice.Create(new("W06.spawn-" + site, "advance/barbarians"), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value)));
        var f = new GlobalTurnFixture(choices); f.Units.City(new(10, 10)); f.World.Entities.Cities.Storage[0].Size = 8;
        f.World.Civilizations[1].Gold = 1000; f.World.Civilizations[1].TotalCitySize = 8;
        f.World.Globals.Turn = 30; f.World.Globals.Difficulty = Difficulty.Deity; f.World.Globals.BarbarianActivity = 3;
        foreach (ref var tile in f.World.Map.Tiles) tile.RevealedTo = CivilizationSet.Only(1);
        f.World.Globals.WarmingCounter = 11; f.World.Globals.PollutedTiles = 10; f.FailPresentation = true;
        using var op = f.Begin(); f.Complete(op);
        Equal(8, f.World.Entities.Units.End); Equal(1, f.Uprisings.Count); Equal(WarmingNotice.FearWarming, f.Warnings.Single());
        Equal(2, op.PresentationFailures.Count); Equal((short)31, f.World.Globals.Turn);
        Equal((byte)11, f.World.PowerHistory[7][1]);
        for (int i = 0; i < 8; ++i) { Equal((byte)0, f.Data.Unit(i).MovesUsed); Equal(true, f.World.Map.TileAt(f.Data.Unit(i).Position).Unit); }
    }
    private static void GlobalTurnCalendarAndPeaceWrap()
    {
        foreach (var (before, after, peace) in new[] { (198, 199, 32767), (199, 200, -32768), (32767, -32768, 32767) })
        {
            var f = new GlobalTurnFixture(); f.World.Globals.Turn = (short)before; f.World.Globals.PeaceTurns = 32767;
            f.World.Globals.Status.Scenario = true; f.World.Scenario.StartYear = 100; f.World.Scenario.YearIncrement = 10;
            using var op = f.Begin(); f.Complete(op);
            Equal((short)after, f.World.Globals.Turn); Equal((short)peace, f.World.Globals.PeaceTurns);
            Equal(unchecked((short)(99 + 10 * after)), f.World.Globals.Year);
        }
    }
    private static void GlobalTurnHistoryDeadlines()
    {
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new GlobalTurnFixture([HistoryDraw(39)]); f.World.Globals.NextHistoryTurn = 0;
            f.World.Multiplayer.Saved.AutoplayActive = autoplay;
            using var op = f.Begin(); var pending = op.Resume();
            Equal(!autoplay, pending is GlobalTurnDecision.History); Equal((short)60, f.World.Globals.NextHistoryTurn);
            if (!autoplay)
            {
                Equal(pending, op.Resume()); Equal(null, op.Resume(new GlobalTurnReply.Acknowledge()));
            }
            f.Complete(op); Equal((short)1, f.World.Globals.Turn);
        }
        var equal = new GlobalTurnFixture(); equal.World.Globals.NextHistoryTurn = 1;
        using var noReport = equal.Begin(); equal.Complete(noReport); Equal((short)1, equal.World.Globals.NextHistoryTurn);
        var wrap = new GlobalTurnFixture([HistoryDraw(39)]); wrap.World.Globals.Turn = 32766; wrap.World.Globals.NextHistoryTurn = 32766;
        wrap.World.Multiplayer.Saved.AutoplayActive = true;
        using var wrapped = wrap.Begin(); wrap.Complete(wrapped); Equal((short)-32710, wrap.World.Globals.NextHistoryTurn);
    }
    private static void HistorianRankingAndPrivacy()
    {
        var f = new GlobalTurnFixture(); f.World.Globals.Difficulty = Difficulty.King;
        f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2).With(3).With(4).With(5);
        f.World.Civilizations[1].Gold = 50; f.World.Civilizations[2].Gold = 100; f.World.Civilizations[3].Gold = 100;
        f.World.Civilizations[4].Gold = -1; f.World.Civilizations[5].Gold = 0; f.World.Civilizations[6].Gold = 999;
        f.World.Civilizations[1].Treaties[2].Contact = true;
        var report = HistorianReport.Prepare(f.World);
        Equal(2, report.Ranks.Count); Equal(new HistorianRank(2, 2), report.Ranks[0]); Equal(new HistorianRank(3, 1), report.Ranks[1]);
        f.World.Globals.Difficulty = Difficulty.Chieftain;
        var all = HistorianReport.Prepare(f.World); Equal(4, all.Ranks.Count);
        Equal(new HistorianRank(1, 3), all.Ranks[0]); Equal(new HistorianRank(4, 5), all.Ranks[3]);
        f.World.Civilizations[2].Gold = 0; Equal(new HistorianRank(2, 2), report.Ranks[0]); // Snapshot, no repaint recalculation.
    }
    private static void GlobalTurnResetBeforeReputationAndHistory()
    {
        var f = new GlobalTurnFixture([HistoryDraw(0)]); f.Units.City(new(16, 10));
        f.World.Globals.Turn = 23; f.World.Globals.NextHistoryTurn = 0;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2; f.Units.Incidents.Treaty();
        f.World.Civilizations[7].Reputation = 5;
        f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).AuxiliaryTimer = 1; f.Data.Unit(0).Counter = 2;
        f.Data.AddUnit(1, 1, 4, new(12, 10)); f.Data.Unit(1).MovesUsed = 4; f.Ship(2);
        using var op = f.Begin(); var incident = op.Resume(); Equal(true, incident is GlobalTurnDecision.Lifecycle { Choice: EspionagePretext });
        Equal((short)24, f.World.Globals.Turn); Equal(false, f.World.Globals.TransientFlags.EndOfTurn);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal((byte)4, f.Data.Unit(1).MovesUsed);
        Equal((byte)5, f.World.Civilizations[7].Reputation); Equal((short)0, f.World.Globals.NextHistoryTurn);
        var history = op.Resume(new GlobalTurnReply.Lifecycle(0));
        for (int n = 0; n < 20 && history is null && !op.Completed; ++n) history = op.Resume();
        Equal(true, history is GlobalTurnDecision.History); Equal((byte)0, f.Data.Unit(1).MovesUsed);
        Equal((byte)4, f.World.Civilizations[7].Reputation); Equal((short)44, f.World.Globals.NextHistoryTurn);
        Equal(false, f.World.Civilizations[2].SpaceshipFlags.Arrived);
        Equal(null, op.Resume(new GlobalTurnReply.Acknowledge())); f.Complete(op);
        Equal(true, f.World.Globals.Status.Won); Equal(EndReason.LocalSpace, f.World.PendingEndReason);
    }
    private static void GlobalTurnArrivalOrderAndNotices()
    {
        var f = new GlobalTurnFixture(); f.World.Globals.CivsAlive = CivilizationSet.Only(1); f.Ship(2); f.Ship(3);
        using var op = f.Begin(); var landing = op.Resume(); Equal(true, landing is GlobalTurnDecision.Landing { Arrival.Civilization: 2 });
        Equal(true, f.World.Civilizations[2].SpaceshipFlags.Arrived); Equal(false, f.World.Civilizations[3].SpaceshipFlags.Arrived);
        Equal(false, f.World.Globals.Status.Won); Equal(2, f.World.SpaceshipWinner); Equal(EndReason.OtherSpace, f.World.PendingEndReason);
        Equal(landing, op.Resume()); Equal(null, op.Resume(new GlobalTurnReply.Acknowledge())); f.Complete(op);
        Equal(true, f.World.Globals.Status.Won); Equal(false, f.World.Civilizations[3].SpaceshipFlags.Arrived);
        f.World.Multiplayer.Saved.AutoplayActive = true; using var next = f.Begin(); f.Complete(next);
        Equal(true, f.World.Civilizations[3].SpaceshipFlags.Arrived); Equal(false, f.World.Civilizations[3].SpaceshipFlags.FirstArrivalVictory);
        Equal(2, f.World.SpaceshipWinner); Equal(EndReason.OtherSpace, f.World.PendingEndReason);
    }
    private static void GlobalTurnRequiredFailurePrefixes()
    {
        var early = new GlobalTurnFixture(); early.Units.City(new(10, 10)); early.World.Globals.Turn = 30;
        early.World.Globals.Difficulty = Difficulty.Deity; early.World.Globals.BarbarianActivity = 3; early.World.Globals.WarmingPollution = 9;
        using var spawning = early.Begin(); Throws<ChoiceException>(() => spawning.Resume());
        Equal((short)30, early.World.Globals.Turn); Equal((short)9, early.World.Globals.WarmingPollution); Equal((byte)4, early.Data.Unit(0).MovesUsed);
        Throws<InvalidOperationException>(() => spawning.Resume());
        var late = new GlobalTurnFixture(); late.World.Globals.Turn = 23; late.World.Globals.NextHistoryTurn = 0; late.World.Civilizations[7].Reputation = 5;
        late.Ship(2); using var history = late.Begin(); Throws<ChoiceException>(() => history.Resume());
        Equal((short)24, late.World.Globals.Turn); Equal((byte)0, late.Data.Unit(0).MovesUsed);
        Equal((byte)4, late.World.Civilizations[7].Reputation); Equal((short)0, late.World.Globals.NextHistoryTurn);
        Equal(false, late.World.Civilizations[2].SpaceshipFlags.Arrived); Throws<InvalidOperationException>(() => history.Resume());
        Equal((short)24, late.World.Globals.Turn);
    }
    private static void GlobalTurnCancellationDisposalAndLocalAdmission()
    {
        var cancel = new GlobalTurnFixture(); cancel.Palace(); using var op = cancel.Begin();
        Equal(true, op.Resume() is GlobalTurnDecision.Throne); op.Resume(new GlobalTurnReply.Throne(null)); cancel.Complete(op);
        Equal((short)3, cancel.World.Globals.Turn); Equal((short)1, cancel.World.Civilizations[1].ThroneRoomUpgrades);
        Equal((byte)0, cancel.World.Civilizations[1].ThroneRoom[0]);
        var stop = new GlobalTurnFixture(); stop.Palace(); var stopped = stop.Begin();
        Equal(true, stopped.Resume() is GlobalTurnDecision.Throne); stopped.Dispose();
        Equal((short)2, stop.World.Globals.Turn); Equal((short)1, stop.World.Civilizations[1].ThroneRoomUpgrades);
        Equal((byte)4, stop.Data.Unit(0).MovesUsed); Throws<ObjectDisposedException>(() => stopped.Resume());
        var remote = new GlobalTurnFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var rejected = remote.Begin(); Throws<InvalidOperationException>(() => rejected.Resume());
        Equal((short)0, remote.World.Globals.Turn); Equal((byte)4, remote.Data.Unit(0).MovesUsed);
    }
    private static void GlobalTurnPrivateSessionChoices()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(751));
            var f = new GlobalTurnFixture([HistoryDraw(0)], new(runtime.Process, runtime.World)); f.Palace(); f.World.Globals.NextHistoryTurn = 0;
            ParticipantId ownerSeat = default;
            runtime.RegisterCommand(new(new(1, "test.global", "Advance global turn", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
                _ => new GlobalTurnOperation(f.Begin(), _ => ownerSeat)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var owner = SessionClient(runtime, 1); ownerSeat = owner.Identity.Participant;
            var command = actor.Submit(new("test.global", new([]))).Correlation.Command; runtime.AdvanceOne();
            var throne = RequiredInteraction(owner, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal((short)2, f.World.Globals.Turn); Equal(false, actor.Reply(throne, 0).Accepted); Equal(false, owner.Reply(throne, 100).Accepted);
            if (cancel) owner.Cancel(command); else owner.Reply(throne, 0);
            runtime.AdvanceOne(); var history = RequiredInteraction(owner, command); Equal(false, owner.Reply(throne, 0).Accepted);
            Equal(null, RequiredResult(actor, command).Interaction); Equal((short)3, f.World.Globals.Turn);
            owner.Reply(history, 0); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
            Equal((short)1, f.World.Civilizations[1].ThroneRoomUpgrades); Equal((byte)(cancel ? 0 : 1), f.World.Civilizations[1].ThroneRoom[0]);
            Equal((short)23, f.World.Globals.NextHistoryTurn); Equal(false, owner.Reply(history, 0).Accepted); f.Random.RequireChoicesConsumed();
        }
    }
    private static void GlobalTurnSessionLifecycleAndTerminalEffects()
    {
        var f = new GlobalTurnFixture(); f.Units.City(new(16, 10)); f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        f.World.Multiplayer.LocalHuman = 2; f.Units.Incidents.Treaty();
        f.Data.Unit(0).Flags.SpyMission = true; f.Data.Unit(0).AuxiliaryTimer = 1; f.Data.Unit(0).Counter = 2;
        var effects = new List<string>(); var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(2, "global"),
            new(default, new(1)), default, (name, _) => effects.Add(name), () => { });
        var recipient = new ParticipantId(2); using var op = new GlobalTurnOperation(f.Begin(), _ => recipient);
        var step = op.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, step.Status); Equal(recipient, step.Interaction!.Recipient);
        step = op.Resume(context, new(OperationInputKind.Timeout));
        for (int i = 0; i < 20 && !step.Status.Terminal(); ++i) step = op.Resume(context, new());
        Equal(CommandStatus.Completed, step.Status); Equal(false, f.World.Civilizations[2].Treaties[1].War);
        Equal(step, op.Resume(context, new())); Equal(1, effects.Count(e => e == "turn.global.advanced"));
        Equal((short)1, f.World.Globals.Turn);
        var display = new GlobalTurnFixture(); display.World.Globals.Turn = 1; display.FailPresentation = true;
        display.World.Globals.WarmingCounter = 11; display.World.Globals.PollutedTiles = 10;
        using var failedDisplay = new GlobalTurnOperation(display.Begin(), _ => recipient);
        var displayContext = OutcomeContext(display.Data);
        var done = failedDisplay.Resume(displayContext, new());
        Equal(CommandStatus.Failed, done.Status); Equal("global_turn_presentation_failed", done.Code);
        Equal((short)2, display.World.Globals.Turn); Equal((byte)0, display.Data.Unit(0).MovesUsed);
        Equal(done, failedDisplay.Resume(displayContext, new())); Equal((short)2, display.World.Globals.Turn);
    }
}
