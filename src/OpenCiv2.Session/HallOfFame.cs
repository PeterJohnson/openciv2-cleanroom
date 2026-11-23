using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

public enum HallLoadStatus { Loaded, Missing, ShortRead }
public sealed record HallRow(int Rank, StoredHallRecord Record, string Leader, string Civilization,
    int Difficulty, bool Cheated, bool ValidDifficulty, int DisplayYear, int? Month, bool BeforeChrist, bool Objectives);

/// <summary>V08 §4 classic record preparation, sparse load/save and insertion. Partial file transfers
/// remain visible; no atomic replacement, cleanup removal or rollback is added.</summary>
public sealed class HallOfFame(IFileStorage storage)
{
    public const string FileName = "HALLFAME.DAT";
    private readonly byte[] bytes = new byte[ClassicHallOfFame.Size];
    public byte[] Capture() => (byte[])bytes.Clone();
    public StoredHallRecord Record(int index) => ClassicHallOfFame.Decode(Row(index));
    private Span<byte> Row(int index)
    {
        if (index is < 0 or >= ClassicHallOfFame.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return bytes.AsSpan(index * ClassicHallOfFame.RecordSize, ClassicHallOfFame.RecordSize);
    }

    public void Clear()
    {
        for (int i = 0; i < ClassicHallOfFame.Count; ++i)
        {
            Empty(Row(i));
        }
    }

    private static void Empty(Span<byte> row)
    {
        BinaryPrimitives.WriteInt16LittleEndian(row, -1);
        BinaryPrimitives.WriteInt16LittleEndian(row[0x12..], -1);
    }

    public HallLoadStatus Load()
    {
        Clear();
        using var input = storage.Read(FileName);
        if (input is null)
        {
            return HallLoadStatus.Missing;
        }

        for (int i = 0; i < ClassicHallOfFame.Count; ++i)
        {
            var item = input.Item(ClassicHallOfFame.RecordSize);
            if (item.Prefix.Count > ClassicHallOfFame.RecordSize)
            {
                throw new ArgumentException("Hall input exceeds record backing");
            }

            item.Prefix.ToArray().CopyTo(Row(i));
            if (!item.Complete || item.Prefix.Count != ClassicHallOfFame.RecordSize)
            {
                Clear();
                input.Close();
                return HallLoadStatus.ShortRead;
            }
        }
        input.Close();
        return HallLoadStatus.Loaded;
    }

    public bool Save()
    {
        using var output = storage.Replace(FileName);
        if (output is null)
        {
            return false;
        }

        bool written = true;
        for (int i = 0; i < ClassicHallOfFame.Count; ++i)
        {
            if (!output.Item(Row(i)))
            { written = false; break; }
        }

        bool closed = output.Close();
        return written && closed;
    }

    public static StoredHallRecord Prepare(GameState world, ProcessContext process, int owner)
    {
        ref var g = ref world.Globals;
        ref var civ = ref world.Civilizations[owner];
        var score = world.Score;
        bool objective = g.Status.Scenario && world.Scenario.Flags.ObjectiveVictory;
        short total = unchecked((short)Math.Max(score.Total, score.ConquestBonus));
        ushort difficulty = unchecked((ushort)((int)g.Difficulty + (g.Status.Cheated ? 0x80 : 0)));
        short turn = g.Turn, year = g.Year, population = unchecked((short)CivilizationFinances.Population(world, owner));
        short objectives = objective ? unchecked((short)score.CivObjectives) : (short)-1;
        short increment = world.Scenario.YearIncrement, start = world.Scenario.StartYear;
        short scenario = g.Status.Scenario ? (short)0x80 : (short)0;
        short rating = unchecked((short)score.Rating), gender = civ.Flags.FemaleLeader ? (short)1 : (short)0, title = unchecked((short)score.TitleIndex);
        var leader = process.Rules.Leaders[civ.Tribe];
        var name = ClassicHallOfFame.Name(leader.LeaderName.Custom ? world.Names[owner].Leader : leader.LeaderName.Text);
        var plural = ClassicHallOfFame.Name(leader.Plural.Custom ? world.Names[owner].Tribe : leader.Plural.Text);
        return new(total, difficulty, turn, year, population, objectives, increment, start, scenario, rating, gender, title, name, plural);
    }

    public void BeginInsertion() { Load(); Empty(Row(5)); }

    /// <summary>Strict retained-rating comparison; complete records shift, including opaque name padding.</summary>
    public int Insert(StoredHallRecord current, int previousRank = -1)
    {
        for (int i = 0; i < ClassicHallOfFame.Count; ++i)
        {
            if (Record(i).Rating >= current.Rating)
            {
                continue;
            }

            for (int j = 5; j > i; --j)
            {
                Row(j - 1).CopyTo(Row(j));
            }

            ClassicHallOfFame.Encode(current).CopyTo(Row(i));
            return i;
        }
        return previousRank;
    }

    /// <summary>Every display reloads, including after a failed save. Stored invalid text is diagnosed safely.</summary>
    public OwnedList<HallRow> Show(int highlight)
    {
        Load();
        var rows = new List<HallRow>();
        for (int i = 0; i < ClassicHallOfFame.Count; ++i)
        {
            var record = Record(i);
            if (record.Score < 0 || i == 5 && highlight != 5)
            {
                continue;
            }

            int difficulty = record.Difficulty & 15;
            int year = record.Year;
            int? month = null;
            if (record.Scenario != 0 && record.YearIncrement < 0)
            {
                int m = unchecked(record.Turn * Math.Abs((int)record.YearIncrement) + record.StartYear - 1);
                month = Math.Abs(m % 12);
                year = m / 12;
            }
            rows.Add(new(i, record, ClassicHallOfFame.DisplayName(record.Leader), ClassicHallOfFame.DisplayName(record.Civilization),
                difficulty, (record.Difficulty & 0x80) != 0, difficulty <= 5, Math.Abs(year), month, year < 0,
                record.Scenario != 0 && record.Objectives >= 0));
        }
        return new(rows);
    }
}

/// <summary>Headless Hall of Fame storage uses the same record transfers as native persistence.</summary>
public sealed class BufferedHallStorage : IFileStorage
{
    private byte[]? saved;
    public byte[]? Capture() => saved is null ? null : (byte[])saved.Clone();
    public void Install(ReadOnlySpan<byte> data) => saved = data.ToArray();
    public CoreInput? Read(string path) => path == HallOfFame.FileName && saved is { } data ? new Input(data) : null;
    public CoreOutput? Replace(string path)
    {
        if (path != HallOfFame.FileName)
        {
            return null;
        }

        saved = [];
        return new Output(data => saved = data);
    }
    public bool Remove(string path) { if (path != HallOfFame.FileName) { return false; } bool existed = saved is not null; saved = null; return existed; }
    private sealed class Input(byte[] data) : CoreInput
    {
        private int position;
        protected override ReadResult ReadItem(int size)
        {
            int count = Math.Min(size, data.Length - position);
            byte[] prefix = data.AsSpan(position, count).ToArray();
            position += count;
            return new(new(prefix), count == size);
        }
        protected override byte? ReadByte() => position < data.Length ? data[position++] : null;
        protected override bool Finish() => true;
    }
    private sealed class Output(Action<byte[]> publish) : CoreOutput
    {
        private readonly List<byte> data = [];
        protected override bool WriteItem(ReadOnlySpan<byte> bytes) { data.AddRange(bytes.ToArray()); publish(data.ToArray()); return true; }
        protected override bool Finish() => true;
    }
}
