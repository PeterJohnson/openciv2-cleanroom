using System.Buffers.Binary;

namespace OpenCiv2.Automation;

// Fixed-capacity ABI 1 writer: no serializer, runtime type scanning or source evaluation.
internal sealed class ReplyWriter
{
    private readonly byte[] bytes = new byte[65536];
    private int position;
    private Span<byte> Take(int count)
    {
        if (count > bytes.Length - position) throw new InvalidOperationException("host_reply_budget");
        var result = bytes.AsSpan(position, count); position += count; return result;
    }
    private void Tag(byte tag) => Take(1)[0] = tag;
    private void Size(int count) => BinaryPrimitives.WriteInt32LittleEndian(Take(4), count);
    internal void Nil() => Tag(0);
    internal void Boolean(bool value) => Tag((byte)(value ? 2 : 1));
    internal void Integer(int value) { Tag(3); Size(value); }
    internal void Text(string value)
    {
        int size = LuaMachine.Utf8.GetByteCount(value); Tag(4); Size(size); LuaMachine.Utf8.GetBytes(value, Take(size));
    }
    internal void Array(int count) { Tag(5); Size(count); }
    internal void Object(int count) { Tag(6); Size(count); }
    internal void Field(string key, string value) { Text(key); Text(value); }
    internal void Field(string key, int value) { Text(key); Integer(value); }
    internal void Field(string key, bool value) { Text(key); Boolean(value); }
    internal byte[] Finish() => bytes[..position];
}
