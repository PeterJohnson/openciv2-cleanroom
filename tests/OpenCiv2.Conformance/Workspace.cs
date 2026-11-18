using System.Text.Json.Nodes;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Conformance;

public static partial class Adapter
{
    private const string WorkspaceOperation = "F02.cityCalculationEntry", PipelineOperation = "C03.fullCityRecalculation";
    private static readonly string[] WorkspaceFields = ["F02.workspaceCity", "F02.happyCount", "F02.unhappyCount"];
    private static CitySlot? City(int value) { Require(value is >= -1 and < 256, "City outside supplied backing"); return value == -1 ? null : new((ushort)value); }
    private static JsonNode CityJson(CitySlot? city) => city is { } selected ? S("u16", selected.Value) : Null();
    private static JsonNode WorkspaceArguments(CitySlot? city, bool full) => O(("city", CityJson(city)), ("full", B(full)));
    private static string Segment(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    private sealed record ScriptAction(string Id, uint Kind, uint Target, int Value, bool Full, string Text);
    private sealed record Provider(string Id, string Request, CitySlot? City, ScriptAction[] Actions, int Result, bool Unavailable, string Reason);
    private sealed class ScriptStop(string category, string boundary, string reason) : Exception(reason)
    { public string Category = category, Boundary = boundary, Reason = reason; }
    private static Provider[] Providers(Input input)
    {
        var providers = new Provider[input.Count(10000)]; var identities = new HashSet<string>(StringComparer.Ordinal); int remainingActions = 100000;
        for (int i = 0; i < providers.Length; ++i)
        {
            string id = TextInput(input), request = TextInput(input); Require(identities.Add(id), "Duplicate provider identity"); var city = City(input.Read<int>()); int count = input.Count(Math.Min(10000, remainingActions)); remainingActions -= count;
            var actions = new ScriptAction[count]; var actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int j = 0; j < count; ++j)
            {
                string actionId = TextInput(input); uint kind = input.Read<uint>(), target = 0; int value = 0; bool full = false; string text = ""; Require(actionIds.Add(actionId), "Duplicate action identity");
                switch (kind)
                {
                    case 0: target = input.Read<uint>(); value = input.Read<int>(); Require(target <= 2, "Unregistered provider output"); if (target == 0) City(value); break;
                    case 1: text = TextInput(input); value = input.Read<int>(); City(value); full = input.Boolean(); break;
                    case 2: case 3: text = TextInput(input); break;
                    default: throw new ArgumentException("Unregistered provider action");
                }
                actions[j] = new(actionId, kind, target, value, full, text);
            }
            bool unavailable = input.Boolean(); string reason = unavailable ? TextInput(input) : ""; int result = unavailable ? 0 : input.Read<int>(); providers[i] = new(id, request, city, actions, result, unavailable, reason);
        }
        return providers;
    }
    private sealed class WorkspaceScript(CityMoodWorkspace mood, Provider[] inputs, int eventLimit, int providerLimit)
    {
        private readonly JsonArray trace = new(); private readonly HashSet<string> eventIds = new(StringComparer.Ordinal);
        private string activeCall = "root"; private int cursor, completed;
        private JsonNode State() => O((WorkspaceFields[0], CityJson(mood.City)), (WorkspaceFields[1], S("i32", mood.Happy)), (WorkspaceFields[2], S("i32", mood.Unhappy)));
        private void Check(string id)
        {
            if (trace.Count + 2 >= eventLimit) throw new ScriptStop("budget-exhausted", id, "event limit reserves two diagnostic slots");
            if (eventIds.Contains(id)) throw new ScriptStop("unexpected-input", id, "duplicate semantic event identity");
        }
        private void Append(string id, string kind, string call, JsonNode payload) { eventIds.Add(id); trace.Add((JsonNode)Event(trace.Count, id, kind, call, payload)); }
        private void Emit(string id, string kind, string call, JsonNode payload) { Check(id); Append(id, kind, call, payload); }
        private int Invoke(string call, string? parent, CitySlot? selected, bool full, int depth)
        {
            if (depth >= 64) throw new ScriptStop("budget-exhausted", call, "callback depth limit");
            string previous = activeCall; activeCall = call; Emit(call + "/enter", "enter", call, O(("operation", WorkspaceOperation), ("parent", parent), ("arguments", WorkspaceArguments(selected, full))));
            int value = mood.Calculate(selected, full, () => Pipeline(call, selected, depth));
            if (call == "root" && cursor != inputs.Length) throw new ScriptStop("unexpected-input", "root/return", "unused mandatory provider input");
            Emit(call + "/return", "return", call, O(("result", S("i32", value)))); activeCall = previous; return value;
        }
        private int Pipeline(string call, CitySlot? selected, int depth)
        {
            string id = call + "/pipeline"; if (cursor >= providerLimit) throw new ScriptStop("budget-exhausted", id, "provider request limit");
            Emit(id + "/request", "provider", call, O(("stage", "request"), ("id", id), ("operation", PipelineOperation), ("arguments", O(("city", CityJson(selected))))));
            if (cursor == inputs.Length) throw new ScriptStop("unexpected-input", id, "provider script exhausted"); var provider = inputs[cursor];
            if (provider.Id != id || provider.Request != PipelineOperation || provider.City != selected) throw new ScriptStop("unexpected-input", id, "provider identity/order/arguments mismatch"); ++cursor;
            foreach (var action in provider.Actions)
            {
                string eventId = call + "/action/" + Segment(action.Id);
                switch (action.Kind)
                {
                    case 0:
                        Check(eventId); if (action.Target == 0) mood.City = City(action.Value); else if (action.Target == 1) mood.Happy = action.Value; else mood.Unhappy = action.Value;
                        var value = action.Target == 0 ? CityJson(mood.City) : S("i32", action.Value); Emit(eventId, "effect", call, O(("name", "provider.output"), ("values", O(("target", T(WorkspaceFields[action.Target])), ("value", value))))); break;
                    case 1:
                        if (action.Text != WorkspaceOperation) throw new ScriptStop("unexpected-input", eventId, "unregistered callback");
                        Invoke(call + "/deliver/" + Segment(action.Id), call, City(action.Value), action.Full, depth + 1); break;
                    case 2: Emit(eventId, "snapshot", call, O(("label", action.Text), ("state", State()))); break;
                    case 3: Emit(eventId, "effect", call, O(("name", "provider.failure"), ("values", O(("code", T(action.Text)))))); break;
                }
            }
            if (provider.Unavailable) throw new ScriptStop("unresolved-input", id + "/return", provider.Reason);
            Emit(id + "/return", "provider", call, O(("stage", "return"), ("id", id), ("result", S("i32", provider.Result)))); ++completed; return provider.Result;
        }
        public JsonNode Run(CitySlot? selected, bool full)
        {
            string outcome = "return"; JsonNode result = Null();
            try { result = S("i32", Invoke("root", null, selected, full, 0)); Emit("root/exit", "snapshot", "root", O(("label", "exit"), ("state", State()))); }
            catch (ScriptStop stop)
            {
                outcome = stop.Category; result = Null(); Append("root/prefix", "snapshot", activeCall, O(("label", "stop-prefix"), ("state", State())));
                Append("root/stop", "stop", activeCall, O(("category", stop.Category), ("boundary", stop.Boundary), ("reason", stop.Reason), ("completedInputs", completed), ("snapshot", "root/prefix")));
            }
            return O(("outcome", outcome), ("result", result), ("state", State()), ("trace", trace));
        }
    }
    private static JsonNode Workspace(IReadOnlyList<string> args, Input input)
    {
        Require(args.Count == 3, "Wrong city-dispatch argument count"); var city = City(Number<int>(args[1])); int full = Number<int>(args[2]); Require(full is 0 or 1, "Invalid full flag");
        var mood = new CityMoodWorkspace { City = City(input.Read<int>()), Happy = input.Read<int>(), Unhappy = input.Read<int>() }; int events = input.Count(100000), providers = input.Count(10000); Require(events >= 2 && providers > 0, "Invalid script budgets");
        var supplied = Providers(input); input.End(); return new WorkspaceScript(mood, supplied, events, providers).Run(city, full != 0);
    }
}
