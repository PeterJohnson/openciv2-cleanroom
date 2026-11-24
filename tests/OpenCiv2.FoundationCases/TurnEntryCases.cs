using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F04 §3: real global advancement, outcome reports and S02 passes, before civilization work.
    private sealed class TurnEntryFixture
    {
        public EventFixture Events { get; }
        public GlobalTurnFixture Global { get; }
        public GameState World => Events.World;
        public int EventFactories;
        public TurnEntryFixture(RevelationFixture? data = null)
        {
            Events = new(data); Global = new(data: Events.Data);
            World.Globals.Multiplayer = GameMode.SinglePlayer;
            World.Globals.HumanPlayers = CivilizationSet.Only(1);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            World.Multiplayer.LocalHuman = 1; World.Multiplayer.ResumeCiv = 0; World.Multiplayer.ResumeEvent = 0;
            World.Globals.GameOptions.Throne = false; World.Events.Loaded = true;
            World.Civilizations[1].Gold = 10;
        }
        public SinglePlayerTurnEntryOperation Begin(Func<int, ParticipantId>? recipient = null) =>
            new(Global.Turn, () => { ++EventFactories; return Events.Triggers; }, new(new BufferedHallStorage()),
                recipient ?? (c => new((ulong)c)), 0, "entry");
        public OperationContext Context() => OutcomeContext(Events.Data);
        public void Consumed() { Global.Random.RequireChoicesConsumed(); Events.Consumed(); }
    }
    private static Event EntryEvent(TurnEntryFixture f, EventTrigger trigger, bool text = false)
    {
        var e = text ? f.Events.Text(trigger.ToString()) : TriggerMoney(f.Events, trigger);
        e.Values.Trigger = trigger; e.Values.Turn = trigger == EventTrigger.Turn ? -1 : 0;
        e.Values.Denominator = 1; e.Values.WhomCiv = 1; e.Values.Technology = -1;
        return e;
    }
    private static readonly EventTrigger[] EntryTriggers =
        [EventTrigger.Turn, EventTrigger.TurnInterval, EventTrigger.RandomTurn, EventTrigger.ReceivedTechnology];

    private static void TurnEntryResumeAdmission()
    {
        foreach (int resume in new[] { 0, 3 })
            foreach (int reprocess in new[] { 0, 1, -7 })
                foreach (bool loaded in new[] { false, true })
                {
                    var f = new TurnEntryFixture(); var w = f.World; var context = f.Context();
                    w.Multiplayer.ResumeCiv = resume; w.Multiplayer.ResumeEvent = reprocess;
                    w.Multiplayer.Saved.ResumeCiv = 6; w.Multiplayer.Saved.ResumeEvent = -19;
                    w.Globals.Turn = (short)(resume == 0 ? 0 : 37); w.Events.Loaded = loaded;
                    w.Civilizations[1].WondersByGroup[0] = 7; w.HumanWonders.Minimum[0] = 99;
                    foreach (var kind in EntryTriggers) EntryEvent(f, kind);
                    using var op = f.Begin(); var result = op.Resume(context, new());
                    Equal(CommandStatus.Completed, result.Status); Equal(result, op.Resume(context, AcknowledgeOutcome));
                    Equal((short)(resume == 0 ? 1 : 37), w.Globals.Turn);
                    bool admitted = loaded && (resume == 0 || reprocess != 0);
                    Equal(admitted ? 14 : 10, w.Civilizations[1].Gold); Equal(admitted ? 1 : 0, f.EventFactories);
                    Equal(7, w.HumanWonders.Minimum[0]); Equal(7, w.HumanWonders.Maximum[0]);
                    Equal(resume, w.Multiplayer.ResumeCiv); Equal(reprocess, w.Multiplayer.ResumeEvent);
                    Equal((byte)6, w.Multiplayer.Saved.ResumeCiv); Equal((short)-19, w.Multiplayer.Saved.ResumeEvent);
                    f.Consumed();
                }
    }
    private static void TurnEntryFourLivePasses()
    {
        var f = new TurnEntryFixture(); var context = f.Context();
        f.World.Multiplayer.ResumeCiv = 1; f.World.Multiplayer.ResumeEvent = 1; f.World.Globals.Turn = 12;
        var entries = EntryTriggers.Select(t => EntryEvent(f, t, true)).ToArray();
        foreach (var e in entries) e.Values.Actions.JustOnce = true;
        entries[0].Values.Turn = 12; entries[1].Values.Turn = 20;
        using var op = f.Begin(); var step = op.Resume(context, new());
        for (int i = 0; i < entries.Length; ++i)
        {
            Equal(CommandStatus.AwaitingInteraction, step.Status);
            Equal(true, step.Interaction!.Prompt.Contains(EntryTriggers[i].ToString(), StringComparison.Ordinal));
            Equal(false, entries[i].Values.Actions.Consumed);
            if (i == 0)
            {
                // A genuine retained player decision separates the passes. Only the first admission is gated.
                f.World.Globals.Turn = 20; f.World.Events.Loaded = false;
                f.World.Multiplayer.ResumeCiv = 3; f.World.Multiplayer.ResumeEvent = 0;
            }
            step = op.Resume(context, AcknowledgeOutcome); Equal(true, entries[i].Values.Actions.Consumed);
        }
        Equal(CommandStatus.Completed, step.Status); Equal(1, f.EventFactories); f.Consumed();
    }
    private static void TurnEntryOutcomeBeforeEvents()
    {
        foreach (bool quit in new[] { false, true })
            foreach (bool admit in new[] { false, true })
            {
                var f = new TurnEntryFixture(); var context = f.Context(); var w = f.World;
                w.Multiplayer.ResumeCiv = 1; w.Multiplayer.ResumeEvent = admit ? 0 : 1;
                w.Globals.Turn = 100; w.Globals.Year = 2020; w.Globals.Status.Won = true;
                w.PendingEndReason = EndReason.Retirement; EntryEvent(f, EventTrigger.Turn);
                using var op = f.Begin(); var step = op.Resume(context, new());
                foreach (string title in new[] { "Power history", "Civilization score", "Civilization rating", "Hall of Fame" })
                {
                    Equal(CommandStatus.AwaitingInteraction, step.Status);
                    Equal(true, step.Interaction!.Prompt.StartsWith(title, StringComparison.Ordinal));
                    Equal(0, f.EventFactories); Equal(10, w.Civilizations[1].Gold);
                    step = op.Resume(context, AcknowledgeOutcome);
                }
                Equal("Keep playing?", step.Interaction!.Prompt);
                w.Multiplayer.ResumeEvent = admit ? 1 : 0;
                step = op.Resume(context, new(OperationInputKind.Choice, quit ? 0 : 1));
                Equal(CommandStatus.Completed, step.Status); Equal(quit, op.Quit);
                Equal(!quit && admit ? 11 : 10, w.Civilizations[1].Gold);
                Equal(!quit && admit ? 1 : 0, f.EventFactories); Equal(EndReason.None, w.PendingEndReason); f.Consumed();
            }
    }
    private static void TurnEntryGlobalBeforeEvents()
    {
        var f = new TurnEntryFixture(); var context = f.Context(); f.Global.Palace(3);
        f.World.Civilizations[1].FutureTechs = 40; EntryEvent(f, EventTrigger.Turn);
        using var op = f.Begin(); var step = op.Resume(context, new());
        Equal(CommandStatus.AwaitingInteraction, step.Status);
        Equal(true, step.Interaction!.Prompt.StartsWith("Throne room", StringComparison.Ordinal));
        Equal((short)3, f.World.Globals.Turn); Equal(10, f.World.Civilizations[1].Gold); Equal(0, f.EventFactories);
        step = op.Resume(context, new(OperationInputKind.Cancel));
        Equal(CommandStatus.Completed, step.Status); Equal((short)4, f.World.Globals.Turn);
        Equal(11, f.World.Civilizations[1].Gold); Equal((short)1, f.World.Civilizations[1].ThroneRoomUpgrades);
        Equal(step, op.Resume(context, AcknowledgeOutcome)); Equal(11, f.World.Civilizations[1].Gold); f.Consumed();
    }
    private static void TurnEntryFailureAndDisposal()
    {
        var f = new TurnEntryFixture(); var context = f.Context(); EntryEvent(f, EventTrigger.Turn);
        var text = EntryEvent(f, EventTrigger.Turn, true); text.Values.Actions.JustOnce = true;
        EntryEvent(f, EventTrigger.TurnInterval);
        var op = f.Begin(); Equal(CommandStatus.AwaitingInteraction, op.Resume(context, new()).Status);
        Equal((short)1, f.World.Globals.Turn); Equal(11, f.World.Civilizations[1].Gold);
        op.Dispose(); Throws<ObjectDisposedException>(() => op.Resume(context, AcknowledgeOutcome));
        Equal(false, text.Values.Actions.Consumed); Equal(11, f.World.Civilizations[1].Gold); f.Consumed();
        var invalid = new TurnEntryFixture(); invalid.World.Globals.Multiplayer = GameMode.Hotseat;
        using var rejected = invalid.Begin(); Throws<InvalidOperationException>(() => rejected.Resume(invalid.Context(), new()));
        invalid.World.Globals.Multiplayer = GameMode.SinglePlayer;
        Throws<InvalidOperationException>(() => rejected.Resume(invalid.Context(), new()));
        Equal((short)0, invalid.World.Globals.Turn); Equal(0, invalid.EventFactories);
        var required = new TurnEntryFixture(); var requiredContext = required.Context();
        EntryEvent(required, EventTrigger.Turn);
        var wave = EntryEvent(required, EventTrigger.Turn, true); wave.Values.Actions.PlayWaveFile = true;
        wave.Values.Actions.JustOnce = true; wave.SetString(34, new("bad.wav"));
        required.Events.Media.FailProbe = true; required.World.Globals.GameOptions.Sound = true;
        EntryEvent(required, EventTrigger.TurnInterval);
        using var failed = required.Begin(); Throws<IOException>(() => failed.Resume(requiredContext, new()));
        Equal((short)1, required.World.Globals.Turn); Equal(11, required.World.Civilizations[1].Gold);
        Equal(false, wave.Values.Actions.Consumed);
        Throws<InvalidOperationException>(() => failed.Resume(requiredContext, AcknowledgeOutcome)); required.Consumed();
        var cosmetic = new TurnEntryFixture(); var cosmeticContext = cosmetic.Context();
        cosmetic.World.Globals.Turn = 3; cosmetic.World.Globals.BarbarianActivity = 2;
        cosmetic.World.Globals.WarmingCounter = 11; cosmetic.World.Globals.PollutedTiles = 10;
        cosmetic.Global.FailPresentation = true; EntryEvent(cosmetic, EventTrigger.Turn);
        using var diagnostic = cosmetic.Begin(); var result = diagnostic.Resume(cosmeticContext, new());
        Equal(CommandStatus.Failed, result.Status); Equal((short)4, cosmetic.World.Globals.Turn);
        Equal(11, cosmetic.World.Civilizations[1].Gold); Equal(1, cosmetic.EventFactories);
        Equal(result, diagnostic.Resume(cosmeticContext, AcknowledgeOutcome)); cosmetic.Consumed();
    }
    private static void TurnEntryPrivateSession()
    {
        using var runtime = new SessionRuntime(new(), new(257)); var f = new TurnEntryFixture(new(runtime.Process, runtime.World));
        var e = f.Events.Library(); e.Values.Trigger = EventTrigger.Turn; e.Values.Turn = -1; e.Values.Actions.JustOnce = true;
        f.World.Globals.CivsAlive = f.World.Globals.CivsAlive.With(3);
        f.World.Civilizations[1].Government = Government.Despotism;
        f.World.Civilizations[1].Treaties[2].Embassy = true; f.World.Globals.Turn = 37;
        f.World.Multiplayer.ResumeCiv = 1; f.World.Multiplayer.ResumeEvent = 1;
        ParticipantId recipient = default;
        runtime.RegisterCommand(new(new(1, "test.turn-entry", "Turn entry", AccessClass.Player, new([]), new([])),
            (_, _, _) => new(), _ => f.Begin(_ => recipient)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1); recipient = human.Identity.Participant;
        var command = actor.Submit(new("test.turn-entry", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(CommandStatus.AwaitingInteraction, RequiredResult(actor, command).Status);
        var grant = RequiredInteraction(human, command); Equal(false, actor.Reply(grant, 0).Accepted);
        Equal(null, RequiredResult(actor, command).Interaction); Equal(false, e.Values.Actions.Consumed);
        Equal(true, human.Reply(grant, 0).Accepted); runtime.AdvanceOne(); Equal(false, human.Reply(grant, 0).Accepted);
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(true, e.Values.Actions.Consumed); f.Consumed();
    }
    private static void TurnEntryResumeStorage()
    {
        // F06 §6 keeps the runtime reprocess flag distinct from the serialized signed word.
        using var runtime = InstallationRuntime(); var input = InstallationInput();
        runtime.World.Multiplayer.ResumeEvent = 17; runtime.World.Multiplayer.Saved.ResumeEvent = -3;
        InstallInput(runtime, input); Equal(17, runtime.World.Multiplayer.ResumeEvent);
        Equal((short)-3, runtime.World.Multiplayer.Saved.ResumeEvent);
        InstallInput(runtime, input with { Multiplayer = new MultiplayerValues { ResumeCiv = 2, ResumeEvent = -9 } });
        Equal(17, runtime.World.Multiplayer.ResumeEvent); Equal((short)-9, runtime.World.Multiplayer.Saved.ResumeEvent);
        Equal(0, new GameState(new()).Multiplayer.ResumeEvent);
    }
}
