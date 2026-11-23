using System.Text.Json.Nodes;

namespace OpenCiv2.Automation;

// Owned reply values. Account for payload independently of the chosen interpreter transport.
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

    internal JsonNode? Nil() { Take(1); return null; }
    internal JsonNode Boolean(bool value) { Take(1); return JsonValue.Create(value)!; }
    internal JsonNode Integer(int value) { Take(5); return JsonValue.Create(value)!; }
    internal JsonNode Text(string value)
    {
        Take(5 + LuaMachine.Utf8.GetByteCount(value));
        return JsonValue.Create(value)!;
    }

    internal JsonNode Array(IEnumerable<JsonNode?> values)
    {
        Take(5);
        var table = new JsonArray();
        foreach (var value in values)
        {
            if (table.Count == 4096)
            {
                throw new InvalidOperationException("host_reply_budget");
            }

            table.Add(value);
        }
        return table;
    }

    internal JsonNode Object(params (string Key, JsonNode? Value)[] fields)
    {
        if (fields.Length > 4096)
        {
            throw new InvalidOperationException("host_reply_budget");
        }

        Take(5);
        var table = new JsonObject();
        foreach (var (key, value) in fields)
        {
            Take(5 + LuaMachine.Utf8.GetByteCount(key));
            table.Add(key, value);
        }

        return table;
    }
}
