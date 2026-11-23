using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static StoredHallRecord FameRecord(int rating = 50, int score = 100, string name = "Cedar") =>
        new(unchecked((short)score), 2, 300, 1700, 123, -1, 0, 0, 0, unchecked((short)rating), 0, 5,
            ClassicHallOfFame.Name(name), ClassicHallOfFame.Name("Alderfolk"));

    public static byte[] HallReference()
    {
        var store = new BufferedHallStorage(); var hall = new HallOfFame(store); hall.Clear();
        hall.Insert(FameRecord(50, 700, "Cedar")); hall.Insert(FameRecord(80, 900, "Rowan"));
        Equal(true, hall.Save()); return store.Capture()!;
    }

    private static void HallRecordEncodingAndPreparation()
    {
        var raw = Enumerable.Range(0, 72).Select(i => (byte)i).ToArray(); var record = ClassicHallOfFame.Decode(raw);
        Equal((short)0x100, record.Score); Equal((ushort)0x302, record.Difficulty); Equal((short)0x1312, record.Rating);
        Equal((byte)24, record.Leader[0]); Equal((byte)71, record.Civilization[23]); BytesEqual(raw, ClassicHallOfFame.Encode(record));
        Equal("Édith", ClassicHallOfFame.DisplayName(ClassicHallOfFame.Name("Édith")));
        Throws<ArgumentException>(() => ClassicHallOfFame.Name(new string('a', 24)));
        Throws<ArgumentException>(() => ClassicHallOfFame.Name("a\0b"));
        Equal("[unterminated name]", ClassicHallOfFame.DisplayName(new(Enumerable.Repeat((byte)'a', 24))));
        byte[] invalid = new byte[24]; invalid[0] = 0x81; Equal("[invalid name encoding]", ClassicHallOfFame.DisplayName(new(invalid)));
        var f = WorkingFixture(); var w = f.World; w.Score.Total = 40000; w.Score.ConquestBonus = 30000; w.Score.Rating = 40001; w.Score.TitleIndex = 23;
        w.Globals.Difficulty = Difficulty.Deity; w.Globals.Status.Cheated = true; w.Globals.Status.Scenario = true;
        w.Scenario.Flags.ObjectiveVictory = true; w.Score.CivObjectives = 4; w.Civilizations[1].Flags.FemaleLeader = true;
        w.Globals.Turn = 123; w.Globals.Year = -40; w.Scenario.YearIncrement = -3; w.Scenario.StartYear = 7;
        var leader = f.Process.Rules.Leaders[w.Civilizations[1].Tribe]; leader.LeaderName = new("default", true); leader.Plural = new("Default people", true);
        w.Names[1].Leader = "Édith"; w.Names[1].Tribe = "Alders";
        var prepared = HallOfFame.Prepare(w, f.Process, 1);
        Equal((short)-25536, prepared.Score); Equal((short)-25535, prepared.Rating); Equal((ushort)0x85, prepared.Difficulty);
        Equal((short)4, prepared.Objectives); Equal((short)1, prepared.Gender); Equal((short)23, prepared.Title);
        Equal((short)6, prepared.Population); Equal("Édith", ClassicHallOfFame.DisplayName(prepared.Leader));
        Equal(40000, w.Score.Total); Equal(40001, w.Score.Rating); // No recalculation or rating preparation.
        leader.LeaderName = new("Cedar"); leader.Plural = new("Alderfolk"); w.Globals.Status.Scenario = false; w.Globals.Status.ScenarioExport = true;
        prepared = HallOfFame.Prepare(w, f.Process, 1); Equal((short)0, prepared.Scenario); Equal((short)-1, prepared.Objectives); Equal("Cedar", ClassicHallOfFame.DisplayName(prepared.Leader));
    }

    private static void HallSparseReadsAndWrites()
    {
        byte[] source = Enumerable.Range(0, 432).Select(i => unchecked((byte)(i * 7))).ToArray();
        for (int failed = 0; failed < 6; ++failed)
            for (int prefix = 0; prefix <= 72; ++prefix)
            {
                int reads = 0, closes = 0, capturedFailed = failed, capturedPrefix = prefix;
                var storage = new CallbackStorage
                {
                    ReadFile = _ => new CallbackInput(size =>
                {
                    int i = reads++; Equal(72, size);
                    return new(new(source.AsSpan(i * 72, i == capturedFailed ? capturedPrefix : 72).ToArray()), i != capturedFailed);
                }, () => { ++closes; return true; })
                };
                var hall = new HallOfFame(storage);
                Equal(HallLoadStatus.ShortRead, hall.Load()); Equal(failed + 1, reads); Equal(1, closes);
                byte[] expected = new byte[432]; source.AsSpan(0, failed * 72 + prefix).CopyTo(expected);
                for (int i = 0; i < 6; ++i) { BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(i * 72), -1); BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(i * 72 + 18), -1); }
                BytesEqual(expected, hall.Capture());
            }
        var buffer = new BufferedHallStorage(); buffer.Install([.. source, 99, 98]); var loaded = new HallOfFame(buffer);
        Equal(HallLoadStatus.Loaded, loaded.Load()); BytesEqual(source, loaded.Capture()); loaded.Clear();
        Equal(source[24], loaded.Capture()[24]); Equal((short)-1, loaded.Record(5).Rating);
        buffer.Remove(HallOfFame.FileName); Equal(HallLoadStatus.Missing, loaded.Load()); Equal(source[24], loaded.Capture()[24]);
        for (int failed = 0; failed < 6; ++failed)
        {
            int writes = 0, closes = 0, failedRecord = failed; var output = new List<byte>();
            var storage = new CallbackStorage
            {
                ReplaceFile = _ => new CallbackOutput(data =>
            { Equal(72, data.Length); output.AddRange(data.ToArray()); return writes++ != failedRecord; }, () => { ++closes; return true; })
            };
            var hall = new HallOfFame(storage); hall.Clear(); hall.Insert(FameRecord());
            Equal(false, hall.Save()); Equal(failed + 1, writes); Equal(1, closes); Equal((failed + 1) * 72, output.Count);
        }
    }

    private static void HallRankingAndDisplay()
    {
        var storage = new BufferedHallStorage(); var hall = new HallOfFame(storage); hall.Clear();
        for (int i = 0; i < 6; ++i) Equal(i, hall.Insert(FameRecord(60 - i * 10, 100, "Seat" + i)));
        Equal(true, hall.Save()); Equal(5, hall.Show(-1).Count); Equal(6, hall.Show(5).Count);
        hall.BeginInsertion(); Equal((short)-1, hall.Record(5).Rating);
        Equal(2, hall.Insert(FameRecord(50, 101, "Tie"))); Equal("Seat1", ClassicHallOfFame.DisplayName(hall.Record(1).Leader));
        Equal("Tie", ClassicHallOfFame.DisplayName(hall.Record(2).Leader)); Equal("Seat4", ClassicHallOfFame.DisplayName(hall.Record(5).Leader));
        Equal(4, hall.Insert(FameRecord(-5), 4)); // No insertion preserves the caller's previous rank.
        hall.Clear(); hall.Insert(FameRecord(100, 40000)); hall.Save(); Equal(0, hall.Show(0).Count); // Wrapped negative score is hidden.
        var date = FameRecord() with { Scenario = 0x80, Turn = -2, YearIncrement = -3, StartYear = 0, Objectives = 0, Difficulty = 0x8f, Title = -1 };
        hall.Clear(); hall.Insert(date); hall.Save(); var row = hall.Show(0).Single();
        Equal(7, row.Month); Equal(0, row.DisplayYear); Equal(true, row.Objectives); Equal(false, row.ValidDifficulty); Equal(true, row.Cheated);
    }

    private static void HallInsertionContinuation()
    {
        var f = WorkingFixture(); var w = f.World;
        f.Process.Rules.Leaders[0].LeaderName = new("Cedar"); f.Process.Rules.Leaders[0].Plural = new("Alderfolk");
        w.Score.Total = 700; w.Score.Rating = 50; w.Score.TitleIndex = 5;
        var storage = new BufferedHallStorage(); storage.Install(HallReference()); var hall = new HallOfFame(storage);
        var effects = new List<string>();
        var context = new OperationContext(w, f.Process, new GameplayRandom(1, "hall"), new PresentationRandom(2, "hall"),
            new(default, new(1)), default, (name, _) => effects.Add(name), () => { });
        var operation = new HallOfFameOperation(hall, 1);
        var first = operation.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, first.Status);
        Equal(2, first.Interaction!.Options.Count); Equal(3, hall.Show(2).Count);
        w.Score.Total = 999; w.Score.Rating = 99; // Clear retries the captured record, not these changed results.
        var cleared = operation.Resume(context, new(OperationInputKind.Choice, -7)); Equal(CommandStatus.AwaitingInteraction, cleared.Status);
        Equal(1, hall.Show(0).Count); Equal((short)700, hall.Record(0).Score); Equal((short)50, hall.Record(0).Rating);
        var done = operation.Resume(context, new(OperationInputKind.Choice, 0)); Equal(CommandStatus.Completed, done.Status); Equal(2, effects.Count);
        var viewer = new HallOfFameOperation(hall); Equal(1, viewer.Resume(context, new()).Interaction!.Options.Count);
        Equal(CommandStatus.Completed, viewer.Resume(context, new(OperationInputKind.Cancel)).Status);
        w.Score.Rating = -5;
        var unranked = new HallOfFameOperation(hall, 1);
        Equal(1, unranked.Resume(context, new()).Interaction!.Options.Count);
        Equal(1, unranked.Resume(context, new(OperationInputKind.Choice, -3)).Interaction!.Options.Count);
        Equal(0, hall.Show(-1).Count); // Trusted nonzero result still clears/retries even without an insertion rank.
        unranked.Resume(context, new(OperationInputKind.Choice, 0));
        w.Score.Rating = 99;
        int reads = 0;
        var failed = new HallOfFame(new CallbackStorage { ReadFile = _ => { ++reads; return null; } });
        var failure = new HallOfFameOperation(failed, 1); var shown = failure.Resume(context, new());
        Equal(2, reads); Equal(true, shown.Interaction!.Prompt.Contains("No recorded games.", StringComparison.Ordinal));
        Equal("hall_output_failed", failure.Resume(context, new(OperationInputKind.Choice, 0)).Code);
    }

    private static void LocalHallViewer()
    {
        foreach (bool console in new[] { false, true })
        {
            var storage = new BufferedHallStorage(); storage.Install(HallReference());
            using var game = new LocalGame(null, hallStorage: storage); byte[] before = game.SaveClassic();
            var report = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Hall, LocalPlayCommands.Hall));
            Equal(true, report.Interaction!.Spec.Prompt.Contains("Rowan", StringComparison.Ordinal)); Equal(1, report.Interaction.Spec.Options.Count);
            Decide(game.Player, console, report, 0); Equal(true, game.AdvanceOne());
            Equal(CommandStatus.Completed, game.Player.Result(report.Correlation.Command)!.Status); BytesEqual(before, game.SaveClassic());
            BytesEqual(HallReference(), storage.Capture()!);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(before));
            report = AwaitLocal(game, SubmitLocal(game, console, LocalPlayCommands.Hall, LocalPlayCommands.Hall));
            Equal(true, report.Interaction!.Spec.Prompt.Contains("Rowan", StringComparison.Ordinal));
            Decide(game.Player, console, report, 0); Equal(true, game.AdvanceOne());
        }
    }
}
