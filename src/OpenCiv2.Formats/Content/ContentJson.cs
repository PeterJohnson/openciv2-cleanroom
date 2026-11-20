using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenCiv2.Formats.Content;

public sealed class ContentException(string code, string location, string message) : Exception(
    location + ": " + message)
{
    public string Code { get; } = code;
    public string Location { get; } = location;
}

/// <summary>Native authoring admission retains numeric lexemes; no binary64 conversion.</summary>
public static class ContentJson
{
    public const int MaximumBytes = 16 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static JsonNode? Parse(ReadOnlySpan<byte> bytes, string source)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new ContentException("resource-limit", source, "JSON document exceeds 16 MiB");
        }

        try
        {
            StrictUtf8.GetCharCount(bytes);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 64 });
            var objects = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                // Enforce the 20-level JSON parser budget, including scalar leaves.
                if (reader.CurrentDepth >= 20
                    && reader.TokenType is not (JsonTokenType.EndObject or JsonTokenType.EndArray))
                {
                    throw new ContentException("resource-limit", source, "JSON nesting exceeds the parser limit");
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        objects.Push(new(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.EndObject:
                        objects.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                        if (!objects.Peek().Add(reader.GetString()!))
                        {
                            throw new ContentException(
                                "invalid-json",
                                source + "@" + reader.TokenStartIndex.ToString(CultureInfo.InvariantCulture),
                                "duplicate object key");
                        }

                        break;
                    case JsonTokenType.String:
                        _ = reader.GetString();
                        break;
                }
            }
            return JsonNode.Parse(bytes, documentOptions: new() { MaxDepth = 64 });
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            throw new ContentException("invalid-json", source, error.Message);
        }
    }

    public static string IntegerText(string token, string location)
    {
        ContentException Invalid() => new("invalid-integer", location, "expected an exact mathematical integer");
        static bool Digit(char c) => c is >= '0' and <= '9';
        if (token.Length == 0)
        {
            throw Invalid();
        }

        bool negative = token[0] == '-';
        int i = negative ? 1 : 0;
        if (i == token.Length || !Digit(token[i]))
        {
            throw Invalid();
        }

        var digits = new StringBuilder();
        if (token[i] == '0')
        {
            digits.Append(token[i++]);
        }
        else
        {
            while (i < token.Length && Digit(token[i]))
            {
                digits.Append(token[i++]);
            }
        }

        int fraction = 0;
        if (i < token.Length && token[i] == '.')
        {
            int start = ++i;
            while (i < token.Length && Digit(token[i]))
            {
                digits.Append(token[i++]);
                ++fraction;
            }
            if (i == start)
            {
                throw Invalid();
            }
        }
        long exponent = 0;
        if (i < token.Length && token[i] is 'e' or 'E')
        {
            ++i;
            bool minus = false;
            if (i < token.Length && token[i] is '+' or '-')
            {
                minus = token[i++] == '-';
            }

            int start = i;
            while (i < token.Length && Digit(token[i]))
            {
                exponent = Math.Min(100000000, exponent * 10 + token[i++] - '0');
            }

            if (i == start)
            {
                throw Invalid();
            }

            if (minus)
            {
                exponent = -exponent;
            }
        }
        if (i != token.Length)
        {
            throw Invalid();
        }

        string value = digits.ToString().TrimStart('0');
        if (value.Length == 0)
        {
            return "0";
        }

        long scale = exponent - fraction;
        if (scale < 0)
        {
            long remove = -scale;
            if (remove >= value.Length || value.AsSpan(value.Length - (int)remove).IndexOfAnyExcept('0') >= 0)
            {
                throw Invalid();
            }

            value = value[..(value.Length - (int)remove)];
        }
        else
        {
            if (scale > MaximumBytes || value.Length + scale > MaximumBytes)
            {
                throw new ContentException("resource-limit", location, "integer expansion exceeds 16 MiB");
            }

            value += new string('0', (int)scale);
        }
        return negative ? "-" + value : value;
    }

    public static int Integer32(string token, string location)
    {
        if (!int.TryParse(
            IntegerText(token, location),
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out int result))
        {
            throw new ContentException("integer-range", location, "integer outside signed32 authoring domain");
        }

        return result;
    }

    internal static byte[] CanonicalBytes(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, node);
        }

        return stream.ToArray();
        static void Write(Utf8JsonWriter w, JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                w.WriteStartObject();
                foreach (var (key, value) in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    w.WritePropertyName(key);
                    Write(w, value);
                }
                w.WriteEndObject();
            }
            else if (node is JsonArray array)
            {
                w.WriteStartArray();
                foreach (var value in array)
                {
                    Write(w, value);
                }

                w.WriteEndArray();
            }
            else if (node is null)
            {
                w.WriteNullValue();
            }
            else
            {
                node.WriteTo(w);
            }
        }
    }
}

internal sealed class ContentReader(JsonNode? value, string path)
{
    public JsonNode? Value { get; private set; } = value;
    public string Path { get; } = path;

    public void Error(string message) => throw new ContentException("invalid-content", Path, message);

    public JsonObject Object()
    {
        if (Value is not JsonObject)
        {
            Error("expected object");
        }

        return (JsonObject)Value!;
    }

    public void Members(
        IReadOnlyCollection<string> required,
        IReadOnlyCollection<string>? optional = null,
        bool extensions = false)
    {
        var obj = Object();
        foreach (string name in required)
        {
            if (!obj.ContainsKey(name))
            {
                Error("missing field " + name);
            }
        }

        foreach (var (key, child) in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (required.Contains(key) || optional?.Contains(key) == true)
            {
                continue;
            }

            if (extensions && key == "extensions")
            {
                if (child is not JsonObject)
                {
                    Error("extensions must be an object");
                }

                foreach (string name in ((JsonObject)child!).Select(p => p.Key))
                {
                    if (!name.Contains('.') || !name.Split('.').All(Identifier))
                    {
                        Error("invalid metadata namespace " + name);
                    }
                }
            }
            else
            {
                Error("unknown field " + key);
            }
        }
    }

    public void Fields(IReadOnlyCollection<string> required, bool extensions = false) =>
        Members(required, extensions: extensions);

    public bool Has(string key) => Value is JsonObject obj && obj.ContainsKey(key);

    public ContentReader At(string key)
    {
        if (!Has(key))
        {
            Error("missing object field " + key);
        }

        return new(Value![key], Path + "/" + key);
    }

    public int ArraySize()
    {
        if (Value is not JsonArray)
        {
            Error("expected array");
        }

        return ((JsonArray)Value!).Count;
    }

    public ContentReader Element(int index)
    {
        if (index < 0 || index >= ArraySize())
        {
            Error("array index out of bounds");
        }

        return new(Value![index], Path + "/" + index.ToString(CultureInfo.InvariantCulture));
    }

    public string Text(bool nonempty = true)
    {
        if (Value?.GetValueKind() != JsonValueKind.String)
        {
            Error("expected Unicode text");
        }

        string result = Value!.GetValue<string>();
        if (nonempty && result.Length == 0)
        {
            Error("expected Unicode text");
        }

        return result;
    }

    private static bool Identifier(string text) => text.Length != 0 && text[0] is >= 'a' and <= 'z' &&
        text.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.');

    public string Id()
    {
        string result = Text();
        if (!Identifier(result))
        {
            Error("invalid semantic identity");
        }

        return result;
    }

    public string Sha256()
    {
        string result = Text();
        if (result.Length != 64 || !result.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            Error("invalid SHA-256");
        }

        return result;
    }

    public void Constant(string expected)
    {
        if (Text() != expected)
        {
            Error("expected " + expected);
        }
    }

    public int Integer(int low = int.MinValue, int high = int.MaxValue)
    {
        if (Value?.GetValueKind() != JsonValueKind.Number)
        {
            Error("expected number");
        }

        int result = ContentJson.Integer32(Value!.ToJsonString(), Path);
        if (result < low || result > high)
        {
            Error("integer outside field domain");
        }

        Set(result);
        return result;
    }

    public void Set(int value)
    {
        var replacement = JsonValue.Create(value);
        if (Value!.Parent is JsonObject obj)
        {
            obj[Value.GetPropertyName()] = replacement;
        }
        else if (Value.Parent is JsonArray array)
        {
            array[Value.GetElementIndex()] = replacement;
        }

        Value = replacement;
    }

    public string Revision()
    {
        if (Value?.GetValueKind() != JsonValueKind.Number)
        {
            Error("expected revision number");
        }

        string result = ContentJson.IntegerText(Value!.ToJsonString(), Path);
        if (result == "0" || result[0] == '-')
        {
            Error("revision must be positive");
        }

        return result;
    }

    public void Texts(bool nonempty)
    {
        for (int i = 0; i < ArraySize(); ++i)
        {
            Element(i).Text(nonempty);
        }
    }
}
