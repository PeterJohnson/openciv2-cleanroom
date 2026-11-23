using OpenCiv2.Base;
using OpenCiv2.IO;

namespace OpenCiv2.Formats;

public sealed record StoredHallRecord(short Score, ushort Difficulty, short Turn, short Year, short Population,
    short Objectives, short YearIncrement, short StartYear, short Scenario, short Rating, short Gender, short Title,
    OwnedList<byte> Leader, OwnedList<byte> Civilization);

/// <summary>F06 HALLFAME.DAT: six headerless 72-byte records. Name bytes include retained padding.</summary>
public static class ClassicHallOfFame
{
    public const int RecordSize = 72, Count = 6, Size = Count * RecordSize;

    public static StoredHallRecord Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != RecordSize)
        {
            throw new ArgumentException("Hall record must contain 72 bytes", nameof(bytes));
        }

        var r = new ByteReader(bytes);
        return new(r.I16(), r.U16(), r.I16(), r.I16(), r.I16(), r.I16(), r.I16(), r.I16(), r.I16(), r.I16(), r.I16(), r.I16(),
            new(r.Take(24).ToArray()), new(r.Take(24).ToArray()));
    }

    public static byte[] Encode(StoredHallRecord record)
    {
        if (record.Leader.Count != 24 || record.Civilization.Count != 24)
        {
            throw new ArgumentException("Hall name fields must contain 24 bytes", nameof(record));
        }

        var w = new ByteWriter();
        w.I16(record.Score);
        w.U16(record.Difficulty);
        w.I16(record.Turn);
        w.I16(record.Year);
        w.I16(record.Population);
        w.I16(record.Objectives);
        w.I16(record.YearIncrement);
        w.I16(record.StartYear);
        w.I16(record.Scenario);
        w.I16(record.Rating);
        w.I16(record.Gender);
        w.I16(record.Title);
        foreach (byte value in record.Leader)
        {
            w.U8(value);
        }

        foreach (byte value in record.Civilization)
        {
            w.U8(value);
        }

        return w.ToArray();
    }

    /// <summary>Declared safe writer policy: CP1252, at most 23 bytes, no embedded NUL. No adjacent-field writes.</summary>
    public static OwnedList<byte> Name(string text)
    {
        byte[] encoded = ClassicText.EncodeWindows1252(text);
        if (encoded.Length > 23 || encoded.Contains((byte)0))
        {
            throw new ArgumentException("Hall names require at most 23 CP1252 bytes without NUL", nameof(text));
        }

        byte[] result = new byte[24];
        encoded.CopyTo(result, 0);
        return new(result);
    }

    public static string DisplayName(OwnedList<byte> bytes)
    {
        byte[] raw = bytes.ToArray();
        int end = Array.IndexOf(raw, (byte)0);
        if (end < 0)
        {
            return "[unterminated name]";
        }

        try
        { return ClassicText.DecodeWindows1252(raw.AsSpan(0, end)); }
        catch (Content.ContentException) { return "[invalid name encoding]"; }
    }
}
