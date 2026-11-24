using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class TurnNoticeFixture
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public GameplayRandom Random { get; }
        public List<MapPoint> Centers { get; } = [];
        public bool FailCenter;
        public TurnNoticeFixture(Choice[]? choices = null, RevelationFixture? data = null)
        {
            Data = data ?? new(); Random = new(new ControlledChoices(new(choices ?? [])), "turn-notices");
            World.Globals.GameOptions.Council = World.Globals.GameOptions.Tutorial = true;
            World.Civilizations[1].CityCount = 6; World.Globals.Turn = 51; World.Globals.Year = -3000;
        }
        public HumanTurnNotices.Operation Begin() => new HumanTurnNotices(World, Random, p =>
        { Centers.Add(p); if (FailCenter) { throw new IOException("Center unavailable"); } }).Begin(1, "notices");
        public void City(int slot, int size, bool palace = false, int owner = 1)
        {
            Data.AddCity(slot, owner, new(slot * 2, 4), size);
            ref var city = ref World.Entities.Cities.Storage[slot]; city.Name = "City " + slot;
            city.Improvements = city.Improvements.With(BuildingIds.Palace, palace);
        }
    }
    private static Choice CouncilCityDraw(int slot, int bound, int value) => Choice.Create(
        new("F04.council-city", "notices", slot), new SamplingLaw.PrimaryModulo(bound), ChoiceCode.Primary(value));
    private static void TurnNoticeCouncilScheduleAndTies()
    {
        foreach (short turn in new short[] { -1, 0, 1, 50, 100, 150 })
        {
            var f = new TurnNoticeFixture(); f.World.Globals.Turn = turn; f.World.Globals.GameOptions.Tutorial = false;
            using var no = f.Begin(); Equal(null, no.Resume()); Equal(true, no.Completed); f.Random.RequireChoicesConsumed();
        }
        foreach (short turn in new short[] { 51, 101, 151 })
        {
            var f = new TurnNoticeFixture([CouncilCityDraw(5, 2, 0)]); f.World.Globals.Turn = turn;
            f.World.Multiplayer.Saved.AutoplayActive = true; f.City(2, 1); f.City(5, 2);
            using var op = f.Begin(); var invite = (HumanTurnNotice.CouncilInvitation)op.Resume()!;
            Equal((CitySlot?)new CitySlot(2), invite.City?.Slot); Equal("City 2", invite.CityName); Equal(-3000, invite.Year);
            Equal(invite, op.Resume()); Sequence([new MapPoint(4, 4)], f.Centers);
            Equal(null, op.Resume(1)); Equal(true, op.Completed); f.Random.RequireChoicesConsumed();
        }
        var palace = new TurnNoticeFixture([CouncilCityDraw(4, 8, 7)]);
        palace.City(1, -128, true); palace.City(2, 1); palace.City(3, 100, owner: 2); palace.City(4, 4, true);
        using var winning = palace.Begin(); Equal((CitySlot?)new CitySlot(4), ((HumanTurnNotice.CouncilInvitation)winning.Resume()!).City?.Slot);
        winning.Resume(1); palace.Random.RequireChoicesConsumed();
        var empty = new TurnNoticeFixture(); using var absent = empty.Begin();
        Equal(null, ((HumanTurnNotice.CouncilInvitation)absent.Resume()!).City); Equal(0, empty.Centers.Count); absent.Resume(1);
    }
    private static void TurnNoticeCouncilOwnedSummary()
    {
        var f = new TurnNoticeFixture(); f.City(0, 1); f.City(1, 100, owner: 2);
        f.Data.AddUnit(0, 1, 0, new(4, 8)); f.Data.AddUnit(1, 2, 0, new(6, 8));
        using var op = f.Begin(); op.Resume(); var checkpoint = f.Random.Capture();
        f.World.Entities.Cities.Storage[0].Size = 5; f.World.Entities.Cities.Storage[0].Flags.Disorder = true;
        f.World.Civilizations[1].Gold = 321; f.World.Civilizations[1].ScienceRate = 4; f.World.Civilizations[1].TaxRate = 5;
        f.World.Civilizations[1].Researching = 12; f.World.Civilizations[1].Treaties[2].War = true;
        f.World.Calculation.Processing = true;
        var council = (HumanTurnNotice.Council)op.Resume(0)!;
        Equal(new CouncilSummary(1, 1, 5, 1, 1, 321, 5, 4, 12, 1), council.Summary);
        f.World.Civilizations[1].Gold = 99; Equal(council, op.Resume()); Equal(321, council.Summary.Gold);
        Equal(null, op.Resume(0)); Equal(checkpoint, f.Random.Capture()); Equal(true, f.World.Calculation.Processing);
    }
    private static void TurnNoticeTutorialScheduleAndLiveInputs()
    {
        foreach (var (turn, kind) in new[] { (1, HumanTurnHint.FirstMove), (20, HumanTurnHint.FirstHelp),
            (60, HumanTurnHint.FirstHelp), (40, HumanTurnHint.SecondHelp), (80, HumanTurnHint.SecondHelp) })
        {
            var f = new TurnNoticeFixture(); f.World.Globals.Turn = (short)turn;
            using var op = f.Begin(); Equal(kind, ((HumanTurnNotice.Tutorial)op.Resume()!).Kind);
            Equal(null, op.Resume(0)); Equal(true, op.Completed); f.Random.RequireChoicesConsumed();
        }
        var live = new TurnNoticeFixture(); live.World.Globals.GameOptions.Tutorial = false;
        using var notices = live.Begin(); notices.Resume();
        live.World.Globals.GameOptions.Tutorial = true; live.World.Globals.Turn = 1;
        Equal(HumanTurnHint.FirstMove, ((HumanTurnNotice.Tutorial)notices.Resume(1)!).Kind);
        live.World.Globals.Turn = 20; Equal(HumanTurnHint.FirstHelp, ((HumanTurnNotice.Tutorial)notices.Resume(0)!).Kind);
        live.World.Globals.Turn = 40; Equal(HumanTurnHint.SecondHelp, ((HumanTurnNotice.Tutorial)notices.Resume(0)!).Kind);
        Equal(null, notices.Resume(0)); Equal(true, notices.Completed);
    }
    private static void TurnNoticeExpansionAndOneCity()
    {
        foreach (bool cityExists in new[] { false, true })
        {
            var f = new TurnNoticeFixture(); f.World.Globals.Turn = 35;
            f.World.Civilizations[1].CityCount = 1; f.World.Civilizations[1].TotalCitySize = 3;
            if (cityExists) { f.City(0, 3); }
            using var op = f.Begin(); var notice = op.Resume();
            if (cityExists)
            {
                Equal(HumanTurnHint.OneCity, ((HumanTurnNotice.Tutorial)notice!).Kind);
                Equal((short)0, f.World.Civilizations[1].LastCityTurn); Equal(notice, op.Resume());
                f.World.Globals.Turn = 36; Equal(null, op.Resume(0)); Equal((short)11, f.World.Civilizations[1].LastCityTurn);
                Equal(null, op.Resume(0)); Equal((short)11, f.World.Civilizations[1].LastCityTurn);
            }
            else { Equal(null, notice); Equal((short)0, f.World.Civilizations[1].LastCityTurn); }
            Equal(true, op.Completed);
        }
        for (int gate = 0; gate < 5; gate++)
        {
            var f = new TurnNoticeFixture(); f.World.Globals.Turn = 35; f.World.Civilizations[1].CityCount = 1;
            f.World.Civilizations[1].TotalCitySize = 3; f.World.Civilizations[1].UnitsInProduction[UnitTypeIds.Settlers] = 1;
            switch (gate)
            {
                case 1: f.World.Civilizations[1].LastCityTurn = 11; break;
                case 2: f.World.Civilizations[1].CityCount = 6; break;
                case 3: f.World.Civilizations[1].UnitsActive[UnitTypeIds.Settlers] = 1; break;
                case 4: f.World.Globals.GameOptions.Tutorial = false; break;
            }
            using var op = f.Begin(); var next = op.Resume();
            if (gate == 0)
            {
                Equal(HumanTurnHint.Expansion, ((HumanTurnNotice.Tutorial)next!).Kind);
                Equal((short)0, f.World.Civilizations[1].LastCityTurn); op.Resume(0); Equal((short)35, f.World.Civilizations[1].LastCityTurn);
            }
            else { Equal(null, next); }
            Equal(true, op.Completed);
        }
    }
    private static void TurnNoticeFailuresAndDisposal()
    {
        var f = new TurnNoticeFixture { FailCenter = true }; f.City(0, 1); using var op = f.Begin(); op.Resume(); op.Resume(1);
        Equal(true, op.Completed); Equal(1, op.PresentationFailures.Count);
        var fail = new TurnNoticeFixture(); fail.City(0, 3); using var broken = fail.Begin();
        Throws<ChoiceException>(() => broken.Resume()); Throws<InvalidOperationException>(() => broken.Resume());
        Equal(false, broken.Completed); Equal(0, fail.Centers.Count);
        var stop = new TurnNoticeFixture(); stop.World.Globals.Turn = 25; stop.World.Civilizations[1].CityCount = 2;
        using var abandoned = stop.Begin(); Equal(true, abandoned.Resume() is HumanTurnNotice.Tutorial); abandoned.Dispose();
        Throws<ObjectDisposedException>(() => abandoned.Resume(0)); Equal((short)0, stop.World.Civilizations[1].LastCityTurn);
    }
    private static void TurnNoticePrivateSession()
    {
        foreach (bool accept in new[] { false, true })
        {
            using var runtime = new SessionRuntime(new(), new(262)); var f = new TurnNoticeFixture(data: new(runtime.Process, runtime.World)); f.City(0, 1);
            ParticipantId recipient = default;
            runtime.RegisterCommand(new(new(1, "test.notices", "Turn notices", AccessClass.Player, new([]), new([])), (_, _, _) => new(),
                _ => new HumanTurnNoticesOperation(f.Begin(), _ => recipient)));
            runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var player = SessionClient(runtime, 1); recipient = player.Identity.Participant;
            var command = actor.Submit(new("test.notices", new([]))).Correlation.Command; runtime.AdvanceOne();
            var prompt = RequiredInteraction(player, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, actor.Reply(prompt, 0).Accepted);
            Equal(true, player.Reply(prompt, accept ? 0 : 1).Accepted); runtime.AdvanceOne();
            if (accept)
            {
                var council = RequiredInteraction(player, command); Equal(false, council == prompt);
                Equal(null, RequiredResult(actor, command).Interaction); Equal(false, player.Reply(prompt, 0).Accepted);
                Equal(true, player.Reply(council, 0).Accepted); runtime.AdvanceOne();
            }
            Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); f.Random.RequireChoicesConsumed();
        }
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout })
        {
            foreach (bool tutorial in new[] { false, true })
            {
                var f = new TurnNoticeFixture();
                if (tutorial) { f.World.Globals.Turn = 25; f.World.Civilizations[1].CityCount = 2; }
                var context = new OperationContext(f.World, f.Data.Process, f.Random, new PresentationRandom(1, "notices"),
                    new(default, new(1)), default, (_, _) => { }, () => { });
                using var adapter = new HumanTurnNoticesOperation(f.Begin(), c => new((ulong)c));
                Equal(CommandStatus.AwaitingInteraction, adapter.Resume(context, new()).Status);
                var done = adapter.Resume(context, new(kind)); Equal(CommandStatus.Completed, done.Status);
                Equal(done, adapter.Resume(context, new(kind)));
                Equal((short)(tutorial ? 25 : 0), f.World.Civilizations[1].LastCityTurn);
                f.Random.RequireChoicesConsumed();
            }
        }
    }
    public static (byte[] Initial, byte[] Completed, byte[] Continued) LocalHumanNoticesReference() => LocalHumanNoticesReference(false);
    private static (byte[] Initial, byte[] Completed, byte[] Continued) LocalHumanNoticesReference(bool console)
    {
        var recorder = new LocalReplayRecorder(new string('d', 64)); using var game = new LocalGame(recorder); FoundCouncil(game);
        var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic()); var globals = StateConversion.ToLive(saved.Globals);
        globals.Turn = 50; globals.NextHistoryTurn = short.MaxValue; globals.GameOptions.Council = true;
        saved = saved with { Globals = StateConversion.PreserveOpaque(StateConversion.ToStored(globals), saved.Globals) };
        byte[] initial = ClassicFiles.EncodeStoredSave(saved);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        var command = SubmitLocal(game, console, "turn.end", LocalPlayCommands.EndTurn);
        var update = AwaitLocal(game, command); Decide(game.Player, console, update, 1); game.AdvanceOne(); update = game.Player.Result(command)!;
        while (update.Interaction is { } other && !other.Spec.Prompt.StartsWith("High Council", StringComparison.Ordinal))
        {
            Equal(true, other.Spec.Prompt == "Choose the next advance." || other.Spec.Prompt.StartsWith("Your civilization has reached", StringComparison.Ordinal));
            Decide(game.Player, console, update, other.Spec.Choices[0]); game.AdvanceOne(); update = game.Player.Result(command)!;
        }
        Equal(CommandStatus.AwaitingInteraction, update.Status); Equal(true, update.Interaction!.Spec.Prompt.Contains("Consult your advisors?", StringComparison.Ordinal));
        Equal(false, game.Player.ObserveLocalPlay()!.PlayerPhase); Equal(51, game.Player.ObserveLocalPlay()!.Turn);
        Decide(game.Player, console, update, 0); game.AdvanceOne(); update = game.Player.Result(command)!;
        Equal(true, update.Interaction!.Spec.Prompt.StartsWith("High Council\nMilitary:", StringComparison.Ordinal));
        Decide(game.Player, console, update, 0); game.AdvanceOne(); update = game.Player.Result(command)!;
        Equal(CommandStatus.Completed, update.Status); Equal(true, game.Player.ObserveLocalPlay()!.PlayerPhase);
        byte[] completed = game.SaveClassic(); BytesEqual(completed, LocalReplay.Run(recorder.Capture()));
        Equal(LocalLoadResult.Loaded, game.LoadClassic(completed)); EndLocalTurn(game, console, 52);
        byte[] continued = game.SaveClassic(); BytesEqual(continued, LocalReplay.Run(recorder.Capture()));
        return (initial, completed, continued);
    }
    private static void LocalHumanNoticesAndReplay()
    {
        var direct = LocalHumanNoticesReference(false); var console = LocalHumanNoticesReference(true);
        BytesEqual(direct.Initial, console.Initial); BytesEqual(direct.Completed, console.Completed); BytesEqual(direct.Continued, console.Continued);
    }
}
