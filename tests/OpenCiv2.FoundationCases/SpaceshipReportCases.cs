using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static OperationContext ShipContext(ShipFixture f, List<string>? effects = null) => new(f.World,
        f.Data.Process, new GameplayRandom(1, "ship-report"), new PresentationRandom(2, "ship-report"),
        new(new(9), new(2)), default, (name, _) => effects?.Add(name), () => { });
    private static SpaceshipReport ShipReport(ShipFixture f, bool allow = true) =>
        SpaceshipReport.Prepare(f.World, f.Data.Rules, f.Values, ClassicCalendar, 1, allow);

    private static void SpaceshipReportValuesAndGates()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 8, 4, 4, 4); var r = ShipReport(f);
        Sequence(new short[] { 39, 8, 8, 4, 4, 4 }, r.Parts); Equal(false, r.OverCapacity);
        Equal(4, r.Population); Equal(295, r.Mass); Equal(156, r.FlightTime); Equal(100, r.Success);
        Equal(100, r.Support); Equal(100, r.Energy); Equal(100, r.Fuel); Equal(true, r.CanLaunch);
        Equal((short)1915, f.World.Civilizations[1].SpaceshipArrivalYear);
        f.Parts(15, 8, 8, 1, 1, 1); r = ShipReport(f); Equal(true, r.OverCapacity); Equal((short)8, r.Parts[1]); Equal(4, r.Limits[1]);
        Equal(false, ShipReport(f, false).CanLaunch);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); Equal(false, ShipReport(f).CanLaunch);
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.Parts(39, 8, 8, 0, 4, 4); Equal(false, ShipReport(f).CanLaunch);
        f.Parts(39, 8, 8, 4, 0, 4); Equal(0, ShipReport(f).Success); Equal(false, ShipReport(f).CanLaunch);
        f.Parts(39, 8, 8, 4, 4, 4); f.World.Civilizations[1].SpaceshipFlags.Launched = true;
        f.World.Civilizations[1].SpaceshipArrivalYear = f.World.Globals.Year;
        r = ShipReport(f); Equal(false, r.Arrived); Equal(false, r.CanLaunch); Equal(true, SpaceshipQueries.Arrived(f.World, 1));
        ++f.World.Globals.Year; Equal(true, ShipReport(f).Arrived);
        var custom = new ShipFixture(values: CustomShipValues()); custom.Parts(2, 1, 2, 0, 1, 2);
        var configured = ShipReport(custom); Equal(true, configured.OverCapacity); Equal(0, configured.Limits[3]);
        Equal(false, configured.CanLaunch); Equal(2, configured.Parts[0]);
    }

    private static void SpaceshipReportRetainedLaunch()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 8, 4, 4, 4); var effects = new List<string>(); var context = ShipContext(f, effects);
        using var operation = new SpaceshipStatusOperation(1, true, _ => f.Construction, new(77));
        var report = operation.Resume(context, new()); Equal(new ParticipantId(77), report.Interaction!.Recipient!.Value);
        Sequence(new[] { 0, 1 }, report.Interaction.Choices);
        Equal(true, report.Interaction.Prompt.Contains("Population: 40000", StringComparison.Ordinal));
        f.World.Spaceship.Success = 23;
        var confirm = operation.Resume(context, new(OperationInputKind.Choice, 1));
        Equal(true, confirm.Interaction!.Prompt.Contains("23%", StringComparison.Ordinal));
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.Parts(0, 0, 0, 0, 0, 0);
        f.World.Civilizations[1].SpaceshipFlags.Exists = false;
        var done = operation.Resume(context, new(OperationInputKind.Choice, -7)); // Trusted nonzero modal result.
        Equal(CommandStatus.Completed, done.Status); Equal(true, f.World.Civilizations[1].SpaceshipFlags.Launched);
        Equal((short)401, f.World.Globals.FirstLaunchTurn); Sequence(new[] { -1 }, f.Windows);
        Equal(1, f.Launches.Count); Sequence(new[] { "spaceship.launched" }, effects);
        Equal(done, operation.Resume(context, new(OperationInputKind.Choice, 1))); Equal(1, f.Launches.Count);
    }

    private static void SpaceshipReportCancelDisposeAndFailure()
    {
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout, OperationInputKind.Choice })
        {
            var f = new ShipFixture(); f.Parts(39, 8, 8, 4, 4, 4); var context = ShipContext(f);
            using var operation = new SpaceshipStatusOperation(1, true, _ => f.Construction);
            operation.Resume(context, new()); operation.Resume(context, new(OperationInputKind.Choice, 1));
            Equal(CommandStatus.Completed, operation.Resume(context, new(kind, 0)).Status);
            Equal(false, f.World.Civilizations[1].SpaceshipFlags.Launched); Equal((short)1915, f.World.Civilizations[1].SpaceshipArrivalYear);
        }
        var stopped = new ShipFixture(); stopped.Parts(39, 8, 8, 4, 4, 4); var c = ShipContext(stopped);
        var op = new SpaceshipStatusOperation(1, true, _ => stopped.Construction); op.Resume(c, new()); op.Dispose();
        Throws<ObjectDisposedException>(() => op.Resume(c, new(OperationInputKind.Choice, 1)));
        Equal(false, stopped.World.Civilizations[1].SpaceshipFlags.Launched);
        var failed = new ShipFixture(false); failed.Parts(39, 8, 8, 4, 4, 4); failed.OnLaunch = () => throw new IOException("presentation");
        c = ShipContext(failed); using var error = new SpaceshipStatusOperation(1, false, _ => failed.Construction);
        error.Resume(c, new()); error.Resume(c, new(OperationInputKind.Choice, 4)); // A custom modal extra result bypasses the ordinary offered button.
        Throws<IOException>(() => error.Resume(c, new(OperationInputKind.Choice, 1)));
        Equal(true, failed.World.Civilizations[1].SpaceshipFlags.Launched); Equal((short)401, failed.World.Globals.FirstLaunchTurn);
        Throws<InvalidOperationException>(() => error.Resume(c, new(OperationInputKind.Choice, 1))); Equal(1, failed.Launches.Count);
    }

    private static void SpaceshipUpkeepSumAndLiveGates()
    {
        var f = new ShipFixture(); f.Parts(10, 2, 3, 1, 1, 1); f.World.Civilizations[1].SpaceshipFlags.Exists = true;
        var same = new SpaceshipUpkeep(f.World, 1); f.Parts(10, 3, 2, 1, 1, 1);
        Equal(false, same.NeedsPresentation()); Sequence(new short[] { 10, 2, 3, 1, 1, 1 }, same.BeforeParts);
        foreach (bool humanLoss in new[] { false, true })
        {
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Civilizations[1].SpaceshipFlags.Exists = true;
            var changed = new SpaceshipUpkeep(f.World, 1);
            if (humanLoss) { f.World.Globals.HumanPlayers = CivilizationSet.Only(2); }
            else { SpaceshipLifecycle.Reset(f.World, 1); }
            Equal(true, changed.NeedsPresentation()); Equal(true, changed.NeedsPresentation());
            f.Parts(10, 2, 3, 1, 1, 1);
        }
        f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Civilizations[1].SpaceshipFlags.Exists = false;
        var acquired = new SpaceshipUpkeep(f.World, 1); f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        f.World.Civilizations[1].SpaceshipFlags.Exists = true; Equal(true, acquired.NeedsPresentation());
        var unchanged = new SpaceshipUpkeep(f.World, 1); Equal(false, unchanged.NeedsPresentation());
    }

    private sealed class TestShipWindow(Func<OperationContext, OperationInput, OperationStep> run, bool opened) : ISpaceshipWindowOperation
    {
        public bool Opened => opened;
        public int Disposals;
        public OperationStep Resume(OperationContext context, OperationInput input) => run(context, input);
        public void Dispose() { ++Disposals; }
    }

    private static void SpaceshipWindowFallbackAndSelector()
    {
        var f = new ShipFixture(); f.Parts(39, 8, 8, 4, 4, 4); var context = ShipContext(f); int requests = 0;
        var window = new TestShipWindow((c, _) => { c.World.Globals.HumanPlayers = CivilizationSet.Only(2); return new(); }, false);
        using var operation = new SpaceshipPresentationOperation(1, SpaceshipUpkeep.Capture(f.World, 1), _ => f.Construction,
            (owner, before) => { ++requests; Equal(1, owner); Equal((short)39, before[0]); return window; });
        var result = operation.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, result.Status);
        Sequence(new[] { 0 }, result.Interaction!.Choices); Equal(1, window.Disposals); Equal(1, requests);
        operation.Resume(context, new(OperationInputKind.Choice, 0)); operation.Resume(context, new()); Equal(1, requests);
        var active = new TestShipWindow((_, _) => new(), true); f.World.Civilizations[1].SpaceshipArrivalYear = 888;
        using var already = new SpaceshipPresentationOperation(1, new([1, 2, 3, 4, 5, 6]), _ => f.Construction, (_, _) => active);
        Equal(CommandStatus.Completed, already.Resume(context, new()).Status); Equal((short)888, f.World.Civilizations[1].SpaceshipArrivalYear);
        Equal(1, active.Disposals);
        var pending = new TestShipWindow((_, _) => new(CommandStatus.AwaitingInteraction,
            Interaction: new("Ship window", null, null, new([0]))), false);
        var retained = new SpaceshipPresentationOperation(1, SpaceshipUpkeep.Capture(f.World, 1), _ => f.Construction, (_, _) => pending);
        Equal(CommandStatus.AwaitingInteraction, retained.Resume(context, new()).Status);
        retained.Dispose(); retained.Dispose(); Equal(1, pending.Disposals);
        Throws<ObjectDisposedException>(() => retained.Resume(context, new(OperationInputKind.Choice, 0)));
        f.World.Globals.CivsAlive = default; f.World.Civilizations[1].SpaceshipFlags.Exists = true;
        f.World.Civilizations[4].SpaceshipFlags.Exists = true; f.World.Civilizations[0].SpaceshipFlags.Exists = true;
        using var selector = new SpaceshipSelectionOperation(_ => f.Construction, (_, _) => null);
        var list = selector.Resume(context, new()); Sequence(new[] { 1, 4, -1 }, list.Interaction!.Choices);
        Equal(CommandStatus.AwaitingInteraction, selector.Resume(context, new(OperationInputKind.Choice, 4)).Status);
    }

    private static void SpaceshipUpkeepPrivateAutosave()
    {
        using var files = new GameFileFixture(); files.ReadySave();
        var runtime = files.Runtime;
        var f = new ShipFixture(data: new(runtime.Process, runtime.World)); f.Parts(38, 8, 8, 4, 4, 4);
        f.World.Civilizations[1].SpaceshipFlags.Exists = true;
        f.World.Map.CoarseGrid(0).Fill(0); f.World.Map.CoarseGrid(1).Fill(0);
        f.World.Globals.GameOptions.Autosave = true;
        files.Storage.RemoveFile = _ => false;
        var services = new ClassicAutosaveServices(files.Storage, (_, _) => false, _ => { }, () => "Auto");
        ParticipantId recipient = default; SpaceshipUpkeep? capture = null; SpaceshipPresentationOperation? presentation = null;
        runtime.EndClientSession();
        runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, input) =>
        {
            if (capture is null)
            {
                context.World.Production.CivilizationTurnInProgress = true;
                capture = new(context.World, 1);
                Equal(0, f.Construction.AddPart(1, 0)); // Actual V07 construction between the F04 capture and tail.
                Equal(true, capture.NeedsPresentation());
                presentation = new(1, capture.BeforeParts, _ => f.Construction, (_, _) => null, recipient);
                input = new();
            }
            var result = presentation!.Resume(context, input);
            if (!result.Status.Terminal()) { return result; }
            context.World.Production.CivilizationTurnInProgress = false;
            ClassicAutosave.Run(context, files.Files, services, 1);
            return CommandDone(1);
        }, () => presentation?.Dispose())));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var human = SessionClient(runtime, 1);
        recipient = human.Identity.Participant;
        var command = actor.Submit(CommandRequestFor()).Correlation.Command; runtime.AdvanceOne();
        foreach (int answer in new[] { 1, 1 })
        {
            Equal(0, files.Opens); Equal(true, f.World.Production.CivilizationTurnInProgress);
            var pending = RequiredInteraction(human, command); Equal(null, RequiredResult(actor, command).Interaction);
            Equal(false, actor.Reply(pending, answer).Accepted); Equal(true, human.Reply(pending, answer).Accepted);
            runtime.AdvanceOne(); Equal(false, human.Reply(pending, answer).Accepted);
        }
        Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); Equal(1, files.Writes);
        Equal(false, f.World.Production.CivilizationTurnInProgress);
        var saved = ClassicFiles.DecodeStoredSave(files.Output.ToArray());
        Equal(true, StateConversion.ToLive(saved.Civilizations[1]).SpaceshipFlags.Launched);
        Equal((short)39, StateConversion.ToLive(saved.Civilizations[1]).SpaceshipParts[0]);
        Equal((sbyte)1, saved.Globals.SavedCurrentCiv);
    }

    private static byte[] LocalSpaceshipInput(LocalGame game)
    {
        var save = ClassicFiles.DecodeStoredSave(game.SaveClassic()); var civs = save.Civilizations.ToArray();
        var civ = StateConversion.ToLive(civs[1]); civ.SpaceshipFlags.Exists = true;
        short[] parts = [39, 8, 8, 4, 4, 4]; for (int p = 0; p < 6; ++p) { civ.SpaceshipParts[p] = parts[p]; }
        civs[1] = StateConversion.ToStored(civ); save = save with { Civilizations = new(civs) };
        return ClassicFiles.EncodeStoredSave(save);
    }

    public static (byte[] Initial, byte[] Launched) LocalSpaceshipReference()
    {
        using var game = new LocalGame(); byte[] initial = LocalSpaceshipInput(game);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        var list = AwaitLocal(game, SubmitLocal(game, false, LocalPlayCommands.Spaceship, LocalPlayCommands.Spaceship));
        var report = ReplyLocal(game, false, list, 1); var confirm = ReplyLocal(game, false, report, 1);
        Equal(CommandStatus.Completed, ReplyLocal(game, false, confirm, 1).Status);
        return (initial, game.SaveClassic());
    }

    private static void LocalSpaceshipReportAndLaunch()
    {
        foreach (bool console in new[] { false, true })
        {
            var recorder = new LocalReplayRecorder(new string('d', 64)); using var game = new LocalGame(recorder);
            var none = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Spaceship, LocalPlayCommands.Spaceship));
            Equal("No spaceships.", none.Interaction!.Spec.Prompt); Equal(CommandStatus.Completed, ReplyLocal(game, console, none, 0).Status);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(LocalSpaceshipInput(game)));
            var list = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Spaceship, LocalPlayCommands.Spaceship));
            var report = ReplyLocal(game, console, list, 1); Equal(true, report.Interaction!.Spec.Choices.Contains(1));
            Equal(false, game.Player.Reply(list.Interaction!.Id, 1).Accepted);
            var confirm = ReplyLocal(game, console, report, 1); Equal(true, confirm.Interaction!.Spec.Prompt.StartsWith("Launch spaceship?", StringComparison.Ordinal));
            var done = ReplyLocal(game, console, confirm, 1); Equal(CommandStatus.Completed, done.Status);
            Equal(1, done.Committed.Count(e => e.Name == "spaceship.launched"));
            byte[] final = game.SaveClassic(); var launched = StateConversion.ToLive(ClassicFiles.DecodeStoredSave(final).Civilizations[1]);
            Equal(true, launched.SpaceshipFlags.Launched); Equal((short)1, ClassicFiles.DecodeStoredSave(final).Globals.FirstLaunchTurn);
            BytesEqual(final, LocalReplay.Run(recorder.Capture()));
        }
    }
}
