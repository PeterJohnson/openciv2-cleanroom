using System.Buffers;
using System.Buffers.Binary;

namespace OpenCiv2.IO;

public sealed class DecodeException(int offset, string detail)
    : Exception($"{detail} at byte {offset}")
{
    public int Offset { get; } = offset;
}

/// <summary>Explicit little-endian fields. Borrowing is confined to synchronous calls.</summary>
public ref struct ByteReader(ReadOnlySpan<byte> input)
{
    private readonly ReadOnlySpan<byte> input = input;
    public int Position { get; private set; }
    public readonly ReadOnlySpan<byte> Remaining => input[Position..];
    public ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || count > input.Length - Position) throw new DecodeException(Position, "Truncated record");
        var result = input.Slice(Position, count);
        Position += count;
        return result;
    }
    public byte U8() => Take(1)[0];
    public sbyte I8() => unchecked((sbyte)U8());
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    public short I16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    public int I32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
}

public sealed class ByteWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();
    public int Count => buffer.WrittenCount;
    public byte[] ToArray() => buffer.WrittenSpan.ToArray();
    public void Append(ReadOnlySpan<byte> bytes) => buffer.Write(bytes);
    public void U8(byte value) { buffer.GetSpan(1)[0] = value; buffer.Advance(1); }
    public void I8(sbyte value) => U8(unchecked((byte)value));
    public void U16(ushort value) { BinaryPrimitives.WriteUInt16LittleEndian(buffer.GetSpan(2), value); buffer.Advance(2); }
    public void I16(short value) => U16(unchecked((ushort)value));
    public void U32(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(buffer.GetSpan(4), value); buffer.Advance(4); }
    public void I32(int value) => U32(unchecked((uint)value));
    public void U64(ulong value) { BinaryPrimitives.WriteUInt64LittleEndian(buffer.GetSpan(8), value); buffer.Advance(8); }
}
