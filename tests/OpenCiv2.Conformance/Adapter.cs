using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;
using OpenCiv2.Network;
using OpenCiv2.Query;
using OpenCiv2.Session;

namespace OpenCiv2.Conformance;

/// <summary>X02 test-only portable adapter. Receives operation inputs and supplied-child scripts,
/// never expected outputs. Native and browser hosts execute these same production engine calls.</summary>
public static partial class Adapter
{
    private static void Require(bool value, string message) { if (!value) throw new ArgumentException(message); }
    private static string Decimal<T>(T value) where T : IFormattable => value.ToString(null, CultureInfo.InvariantCulture);
    private static T Number<T>(string text) where T : struct, IBinaryInteger<T>
    {
        Require(T.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out T value) && Decimal(value) == text, "Noncanonical or out-of-range adapter integer"); return value;
    }
    private sealed class Input
    {
        private readonly string[] words; private int position;
        public Input(string text) { Require(text.Length <= 16 * 1024 * 1024, "Adapter input exceeds 16 MiB"); words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); }
        public string Word() { Require(position < words.Length, "Missing adapter input"); return words[position++]; }
        public T Read<T>() where T : struct, IBinaryInteger<T> => Number<T>(Word());
        public bool Boolean() { uint value = Read<uint>(); Require(value <= 1, "Noncanonical boolean"); return value != 0; }
        public int Count(int maximum) { uint count = Read<uint>(); Require(count <= maximum, "Adapter collection exceeds input bound"); return (int)count; }
        public void End() => Require(position == words.Length, "Trailing adapter input");
    }
    private static byte[] Unhex(string text, int maximum = 65536)
    {
        Require(text.Length != 0 && text.Length % 2 == 0 && text.Length / 2 <= maximum && text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "Invalid encoded adapter text"); return Convert.FromHexString(text);
    }
    private static string TextInput(Input input) => new UTF8Encoding(false, true).GetString(Unhex(input.Word()));
    // Clone at containment boundaries: observations may intentionally repeat the same immutable logical value.
    private static JsonObject O(params (string Key, JsonNode? Value)[] values) => new(values.Select(v => new KeyValuePair<string, JsonNode?>(v.Key, v.Value?.DeepClone())));
    private static JsonObject S<T>(string type, T value) where T : IFormattable => O(("$type", type), ("value", Decimal(value)));
    private static JsonObject T(string value) => O(("$type", "text"), ("value", value));
    private static JsonObject B(bool value) => O(("$type", "bool"), ("value", value));
    private static JsonObject Null() => O(("$type", "null"));
    private static JsonObject Bytes(ReadOnlySpan<byte> value) => O(("$type", "bytes"), ("hex", Convert.ToHexStringLower(value)));
    private static JsonArray A<T>(IEnumerable<T> values, Func<T, JsonNode> encode) => new(values.Select(encode).ToArray());
    private static JsonObject Event(int sequence, string id, string kind, string call, JsonNode payload) => O(("seq", sequence), ("id", id), ("kind", kind), ("call", call), ("payload", payload));
    private static JsonObject Entry(string operation, JsonNode arguments) => Event(0, "root.enter", "enter", "root", O(("operation", operation), ("parent", null), ("arguments", arguments)));
    private static JsonObject Returned(string operation, JsonNode arguments, JsonNode result, JsonNode state) => O(("outcome", "return"), ("result", result), ("state", state),
        ("trace", new JsonArray(Entry(operation, arguments), Event(1, "root.return", "return", "root", O(("result", result))), Event(2, "root.exit", "snapshot", "root", O(("label", "exit"), ("state", state))))));
    public static string Run(IReadOnlyList<string> arguments, string input)
    {
        Require(arguments.Count is > 0 and <= 6 && arguments.All(s => s.Length <= 4096), "Expected a registered operation and bounded arguments");
        var words = new Input(input); string operation = arguments[0]; JsonNode output;
        if (operation == "F02.prepareNewGame") { Require(arguments.Count == 1, "Fresh-game boundary has no arguments"); output = FreshGame(words); }
        else if (operation == "F02.cityCalculationEntry") output = Workspace(arguments, words);
        else if (operation is "F01.primaryCode" or "F01.primaryModulo" or "F01.secondaryUnit" or "F01.secondaryRange") output = Random(arguments, words);
        else output = Primitive(arguments, words);
        return output.ToJsonString();
    }
    private static JsonNode Primitive(IReadOnlyList<string> args, Input input)
    {
        string operation = args[0]; JsonObject arguments = new(), state = new(); JsonNode result;
        if (operation == "F01.primaryModuloFromCode" && args.Count == 3)
        {
            int code = Number<int>(args[1]), n = Number<int>(args[2]); arguments = O(("code", S("i32", code)), ("n", S("i32", n))); input.End(); result = S("i32", Arithmetic.PrimaryModuloFromCode(code, n));
        }
        else if (operation == "F01.secondaryRangeFromCode" && args.Count == 4)
        {
            int lo = Number<int>(args[1]), hi = Number<int>(args[2]); uint code = Number<uint>(args[3]); arguments = O(("lo", S("i32", lo)), ("hi", S("i32", hi)), ("U", S("u32", code))); input.End(); result = S("i32", Arithmetic.SecondaryRangeFromCode(lo, hi, code));
        }
        else if (operation is "F02.assignUnitToken" or "F02.assignCityToken" && args.Count == 1)
        {
            var process = new ProcessTokens { NextUnit = input.Read<uint>(), NextCity = input.Read<uint>() }; input.End(); result = S("u32", operation == "F02.assignUnitToken" ? process.AssignUnit().Value : process.AssignCity().Value);
            state = O(("F02.nextUnitToken", S("u32", process.NextUnit)), ("F02.nextCityToken", S("u32", process.NextCity)));
        }
        else if (operation == "F02.reserveUnitSlot" && args.Count == 2)
        {
            byte owner = Number<byte>(args[1]); Require(owner < 8, "Civilization owner outside 0..7"); uint end = (uint)input.Count(2048); var scanned = new UnitToken[end];
            for (int i = 0; i < scanned.Length; ++i) scanned[i] = new(input.Read<uint>()); var military = new short[8]; for (int i = 0; i < 8; ++i) military[i] = input.Read<short>(); byte humans = input.Read<byte>(); input.End();
            arguments = O(("owner", S("u8", owner))); var slot = EntityAllocation.ReserveUnit(scanned, ref end, new(owner), military[owner], humans); result = slot is { } assigned ? S("u16", assigned.Value) : Null();
            state = O(("F02.unitEnd", S("u32", end)), ("F02.unitTokenScan", A(scanned, v => S("u32", v.Value))), ("F02.militaryCounts", A(military, v => S("i16", v))), ("F02.effectiveHumanSet", S("u8", humans)));
        }
        else if ((operation == "W01.wrapX" && args.Count == 2) || (operation == "W01.tileIndex" && args.Count == 3) || (operation is "W01.stepDistance" or "W01.weightedDistance" && args.Count == 5))
        {
            int width = input.Read<int>(), height = input.Read<int>(); var geometry = new Geometry(width, height); bool flat = operation != "W01.tileIndex" && input.Boolean(); input.End();
            state = O(("W01.width", S("i32", width)), ("W01.height", S("i32", height))); if (operation != "W01.tileIndex") state["W01.flat"] = B(flat);
            if (operation == "W01.wrapX") { int x = Number<int>(args[1]); arguments = O(("x", S("i32", x))); result = S("i32", geometry.WrapX(x, flat)); }
            else if (operation == "W01.tileIndex")
            {
                int x = Number<int>(args[1]), y = Number<int>(args[2]); arguments = O(("x", S("i32", x)), ("y", S("i32", y))); result = geometry.TileIndex(new(x, y)) is { } index ? S("u32", (uint)index) : Null();
            }
            else
            {
                int x1 = Number<int>(args[1]), y1 = Number<int>(args[2]), x2 = Number<int>(args[3]), y2 = Number<int>(args[4]); arguments = O(("x1", S("i32", x1)), ("y1", S("i32", y1)), ("x2", S("i32", x2)), ("y2", S("i32", y2)));
                result = S("i32", operation == "W01.stepDistance" ? geometry.StepDistance(new(x1, y1), new(x2, y2), flat) : geometry.WeightedDistance(new(x1, y1), new(x2, y2), flat));
            }
        }
        else throw new ArgumentException("Unknown operation or wrong argument count");
        return Returned(operation, arguments, result, state);
    }
    public static string Identity(Assembly host)
    {
        var assemblies = new[] { typeof(Arithmetic).Assembly, typeof(CoreFile).Assembly, typeof(CommandRequest).Assembly, typeof(Geometry).Assembly,
            typeof(StoredSave).Assembly, typeof(StoredSnapshot).Assembly, typeof(Messages).Assembly, typeof(SessionRuntime).Assembly, typeof(Adapter).Assembly, host }.Distinct();
        var build = new JsonArray(assemblies.Select(a => JsonSerializer.SerializeToNode(BuildIdentity.Read(a), AdapterJson.Default.CompiledInputs)).ToArray());
        return O(("format", "openciv2-managed-conformance-adapter/1"), ("policy", RandomPolicy.Id), ("policyVersion", RandomPolicy.Version), ("Build", build)).ToJsonString();
    }
}
[JsonSerializable(typeof(CompiledInputs))]
internal partial class AdapterJson : JsonSerializerContext;
