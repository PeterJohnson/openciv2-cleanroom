using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class AutosaveFixture : IDisposable
    {
        public GameFileFixture F = new();
        public ClassicAutosaveServices Services;
        public OperationContext Context;
        public AutosaveFixture(string name = "Caesar")
        {
            F.ReadySave();
            F.Runtime.World.Globals.Multiplayer = GameMode.SinglePlayer;
            F.Runtime.World.Globals.GameOptions.Autosave = true;
            F.Runtime.World.Globals.Turn = 2;
            F.Runtime.World.Civilizations[2].Tribe = 0;
            F.Runtime.Process.Rules.Leaders[0].LeaderName = new("", true);
            F.Runtime.World.Names[2].Leader = name;
            F.Files.ScenarioDirectory = "C:\\scenario";
            F.Storage.RemoveFile = path => { F.Trace.Add("delete:" + path); return false; };
            Services = new(F.Storage, (from, to) => { F.Trace.Add("rename:" + from + ":" + to); return false; },
                path => F.Trace.Add("cd:" + path), () => "Auto");
            Context = new(F.Runtime.World, F.Runtime.Process, F.Runtime.GameplayRandom, F.Runtime.PresentationRandom,
                new(default, new(1)), default, (_, _) => { }, F.Runtime.Publish);
        }
        public void Run(int owner = 2) => ClassicAutosave.Run(Context, F.Files, Services, owner);
        public void Dispose() => F.Dispose();
    }

    private static void AutosaveRotationAndWriter()
    {
        // F04 §9 / F06 §5: expected order comes from the specification, bytes from the ordinary writer.
        for (short turn = 0; turn < 16; ++turn)
        {
            using var a = new AutosaveFixture(); var f = a.F; f.Runtime.World.Globals.Turn = turn;
            var random = f.Runtime.GameplayRandom.Capture(); a.Run();
            var expected = new List<string> { "cd:C:\\scenario" };
            if (turn % 8 < 4) { expected.Add("delete:Ca_Auto2.SAV"); expected.Add("rename:Ca_Auto.SAV:Ca_Auto2.SAV"); }
            expected.AddRange(["write-open:Ca_Auto.SAV", "close", "cd:C:\\home"]);
            Sequence(expected, f.Trace); Equal("C:\\home", f.Files.Text.Working);
            Equal("Ca_Auto.SAV", f.Files.OpenedPath); Equal(".SAV", f.Files.Extension);
            var saved = ClassicFiles.DecodeStoredSave(f.Output.ToArray());
            Equal(turn, saved.Globals.Turn); Equal((sbyte)2, saved.Globals.SavedCurrentCiv);
            Equal(false, f.Runtime.World.Globals.Status.ScenarioExport); Equal((sbyte)2, f.Runtime.World.Globals.SavedCurrentCiv);
            Equal(random, f.Runtime.GameplayRandom.Capture());
            BytesEqual(f.Output, ClassicFiles.EncodeStoredSave(saved));
        }
    }

    private static void AutosaveByteNamesAndLiveIdentity()
    {
        foreach (var (name, stem) in new[] { ("", ""), ("A", "A"), (" Cato", "C"), ("\t Caesar", ""),
            ("C\tato", "C"), ("É€third", "É€"), ("\u0081xrest", "\u0081x") })
        {
            using var a = new AutosaveFixture(name); a.Services = a.Services with { Label = () => "Auto été" };
            a.Run(); Equal(stem + "_Auto été.SAV", a.F.Files.OpenedPath);
        }
        using var current = new AutosaveFixture("Old");
        current.F.Runtime.Process.Rules.Leaders[0].LeaderName = new("New leader");
        current.Run(); Equal("Ne_Auto.SAV", current.F.Files.OpenedPath);
        using var terminated = new AutosaveFixture("C\0other");
        Throws<ArgumentException>(() => terminated.Run()); // F06 rejects embedded NUL in persistent names.
        Equal("C_Auto.SAV", terminated.F.Files.OpenedPath); Equal(0, terminated.F.Opens);
    }

    private static void AutosaveGatesAndDirectoryScope()
    {
        using var a = new AutosaveFixture(); var f = a.F;
        f.Files.OpenedPath = "unchanged"; f.Files.Extension = ".SCN";
        a.Services = a.Services with { Label = () => throw new Exception("Nonlocal helper must not read label") };
        a.Run(1); Sequence(new[] { "cd:C:\\scenario", "cd:C:\\home" }, f.Trace);
        Equal("unchanged", f.Files.OpenedPath); Equal(".SCN", f.Files.Extension); Equal(0, f.Opens);
        f.Trace.Clear(); f.Runtime.World.Globals.GameOptions.Autosave = false;
        a.Run(); Equal(0, f.Trace.Count);
        f.Runtime.World.Globals.GameOptions.Autosave = true;
        f.Runtime.World.Production.CivilizationTurnInProgress = true;
        Throws<InvalidOperationException>(() => a.Run()); Equal(0, f.Trace.Count);
        f.Runtime.World.Production.CivilizationTurnInProgress = false; f.Runtime.World.PlayerPhaseActive = true;
        Throws<InvalidOperationException>(() => a.Run()); Equal(0, f.Trace.Count);
        f.Runtime.World.PlayerPhaseActive = false; f.Runtime.World.Globals.Multiplayer = GameMode.Hotseat;
        Throws<InvalidOperationException>(() => a.Run()); Equal(0, f.Trace.Count);
    }

    private static void AutosaveOutputFailuresAndEncoding()
    {
        for (int failure = 0; failure < 4; ++failure)
        {
            using var a = new AutosaveFixture(); var f = a.F;
            f.OpenOk = failure != 0; f.WriteOk = failure != 1; f.CloseOk = failure != 2;
            if (failure == 3) { f.WriteLimit = 40; }
            a.Run(); Equal("cd:C:\\home", f.Trace[^1]); Equal((sbyte)2, f.Runtime.World.Globals.SavedCurrentCiv);
            Equal(false, f.Runtime.World.Globals.Status.ScenarioExport); Equal(failure == 0 ? 0 : 1, f.Closes);
            if (failure == 3) { Equal(40, f.Output.Count); }
        }
        using var invalid = new AutosaveFixture(); invalid.F.Runtime.World.Map.CoarseGrid(0)[0] = null;
        Throws<InvalidOperationException>(() => invalid.Run()); Equal(0, invalid.F.Opens);
        Equal((sbyte)2, invalid.F.Runtime.World.Globals.SavedCurrentCiv); Equal("cd:C:\\home", invalid.F.Trace[^1]);
    }

    private static void AutosaveDuringAdmittedCommand()
    {
        using var a = new AutosaveFixture(); var f = a.F;
        f.Runtime.EndClientSession();
        f.Runtime.RegisterCommand(CommandDefinitionFor(_ => new TestContinuation((context, _) =>
        {
            Throws<InvalidOperationException>(() => f.Save());
            ClassicAutosave.Run(context, f.Files, a.Services, 2);
            return CommandDone(1);
        })));
        f.Runtime.BeginClientSession(new()); var player = SessionClient(f.Runtime);
        var submitted = player.Submit(CommandRequestFor());
        f.Observe = () => { Equal(true, f.Runtime.HasPendingCommands); Throws<InvalidOperationException>(() => f.Runtime.PrepareNewGame()); };
        Equal(true, f.Runtime.AdvanceOne()); Equal(1, f.Writes);
        Equal(CommandStatus.Completed, player.Result(submitted.Correlation.Command)!.Status);
    }

    private static void LocalAutosaveAfterDecisions()
    {
        foreach (bool console in new[] { false, true })
        {
            var outputs = new List<byte[]>(); var paths = new List<string>(); LocalGame? live = null;
            var storage = new CallbackStorage
            {
                RemoveFile = _ => false,
                ReplaceFile = path =>
            {
                Equal(true, live!.Busy); Equal(false, live.Player.ObserveLocalPlay()!.PlayerPhase);
                paths.Add(path); return new CallbackOutput(bytes => { outputs.Add(bytes.ToArray()); return true; }, () => true);
            }
            };
            var recorder = new LocalReplayRecorder(new string('c', 64));
            using var game = new LocalGame(recorder, autosave: new("host", new(storage, (_, _) => false, _ => { }, () => "Auto")));
            live = game; FoundCouncil(game); StartRevolution(game, console, 0);
            EndLocalTurn(game, console, 2); EndLocalTurn(game, console, 3); Equal(2, outputs.Count);
            var picker = GovernmentTurnDecision(game, console); Equal(2, outputs.Count);
            var rates = GovernmentReply(game, console, picker, 1); Equal(2, outputs.Count);
            var finished = GovernmentReply(game, console, rates, 1);
            Equal(CommandStatus.Completed, finished.Status); Equal(3, outputs.Count);
            var saved = ClassicFiles.DecodeStoredSave(outputs[^1]); Equal((short)4, saved.Globals.Turn);
            Equal((byte)1, saved.Civilizations[1].Government); Equal((sbyte)1, saved.Globals.SavedCurrentCiv);
            Equal(true, game.Player.ObserveLocalPlay()!.PlayerPhase);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(outputs[^1])); Equal(3, outputs.Count);
            EndLocalTurn(game, console, 5); Equal(4, outputs.Count); Equal(paths[0], paths[^1]);
            var disabled = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var globals = StateConversion.ToLive(disabled.Globals); globals.GameOptions.Autosave = false;
            disabled = disabled with { Globals = StateConversion.PreserveOpaque(StateConversion.ToStored(globals), disabled.Globals) };
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(disabled)));
            EndLocalTurn(game, console, 6); Equal(4, outputs.Count);
            byte[] final = game.SaveClassic(); BytesEqual(final, LocalReplay.Run(recorder.Capture()));
        }
    }
}
