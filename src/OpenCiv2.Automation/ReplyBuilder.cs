using Lua;

namespace OpenCiv2.Automation;

// Owned Lua values; payload accounting retains the API's 64 KiB response budget.
// There is no serialization, source evaluation, reflection or native ABI.
internal sealed class ReplyBuilder
{
    private int remaining = 65536;

    private void Take(int count)
    {
        if (count > remaining)
        {
            throw new InvalidOperationException("host_reply_budget");
        }

        remaining -= count;
    }

    internal LuaValue Nil() { Take(1); return LuaValue.Nil; }
    internal LuaValue Boolean(bool value) { Take(1); return value; }
    internal LuaValue Integer(int value) { Take(5); return value; }
    internal LuaValue Text(string value)
    {
        Take(5 + LuaMachine.Utf8.GetByteCount(value));
        return LuaByteString.EncodeUtf8(value);
    }

    internal LuaValue Array(IEnumerable<LuaValue> values)
    {
        Take(5);
        var table = new LuaTable();
        int count = 0;
        foreach (var value in values)
        {
            if (++count > 4096)
            {
                throw new InvalidOperationException("host_reply_budget");
            }

            table[count] = value;
        }
        return table;
    }

    internal LuaValue Object(params (string Key, LuaValue Value)[] fields)
    {
        if (fields.Length > 4096)
        {
            throw new InvalidOperationException("host_reply_budget");
        }

        Take(5);
        var table = new LuaTable();
        foreach (var (key, value) in fields)
        {
            table[Text(key)] = value;
        }

        return table;
    }
}
