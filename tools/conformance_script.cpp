import std;
import openciv.tools.conformance_script;
import openciv.tools.conformance_io;
import openciv.state.records;

// F02 §9 shared-workspace dispatch, with C03's pipeline explicitly supplied.
// X02 §4 scripts drive only this genuine child boundary and registered reentry.

namespace openciv::conformance {
namespace {
using namespace io;
constexpr std::string_view operation_id = "F02.cityCalculationEntry";
constexpr std::string_view provider_id = "C03.fullCityRecalculation";
constexpr std::string_view fields[]{"F02.workspaceCity", "F02.happyCount", "F02.unhappyCount"};
std::optional<state::CitySlot> city(std::int32_t value) {
    if (value < -1 || value >= 256)
        throw std::invalid_argument("City outside supplied backing");
    return value == -1 ? std::nullopt
                       : std::optional{state::CitySlot{static_cast<std::uint16_t>(value)}};
}
bool boolean(std::int32_t value) {
    if (value != 0 && value != 1)
        throw std::invalid_argument("Invalid boolean input");
    return value != 0;
}
std::string city_json(std::optional<state::CitySlot> value) {
    return value ? scalar("u16", static_cast<std::uint16_t>(*value)) : "{\"$type\":\"null\"}";
}
std::string arguments_json(std::optional<state::CitySlot> value, bool full) {
    return "{\"city\":" + city_json(value) +
           ",\"full\":{\"$type\":\"bool\",\"value\":" + (full ? "true}}" : "false}}");
}
std::string segment(std::string_view value) {
    std::string result;
    for (const char byte : value) {
        if (byte == '~')
            result += "~0";
        else if (byte == '/')
            result += "~1";
        else
            result += byte;
    }
    return result;
}
struct Action {
    std::string id;
    unsigned kind = 0, target = 0;
    std::int32_t value = 0;
    bool full = false;
    std::string text;
};
struct Provider {
    std::string id, request;
    std::optional<state::CitySlot> city;
    std::vector<Action> actions;
    std::int32_t result = 0;
    bool unavailable = false;
    std::string reason;
};
std::size_t count() {
    const auto value = input<std::uint32_t>();
    if (value > 10000)
        throw std::invalid_argument("Script collection exceeds adapter bound");
    return value;
}
std::vector<Provider> providers() {
    std::vector<Provider> result;
    const auto size = count();
    std::set<std::string> identities;
    for (std::size_t i = 0; i < size; ++i) {
        Provider provider;
        provider.id = unhex(word());
        provider.request = unhex(word());
        if (!identities.insert(provider.id).second)
            throw std::invalid_argument("Duplicate provider identity");
        provider.city = city(input<std::int32_t>());
        const auto actions = count();
        std::set<std::string> action_ids;
        for (std::size_t j = 0; j < actions; ++j) {
            Action action;
            action.id = unhex(word());
            action.kind = input<unsigned>();
            if (!action_ids.insert(action.id).second)
                throw std::invalid_argument("Duplicate action identity");
            switch (action.kind) {
            case 0:
                action.target = input<unsigned>();
                action.value = input<std::int32_t>();
                if (action.target > 2)
                    throw std::invalid_argument("Unregistered provider output");
                if (action.target == 0)
                    city(action.value);
                break;
            case 1:
                action.text = unhex(word());
                action.value = input<std::int32_t>();
                city(action.value);
                action.full = boolean(input<std::int32_t>());
                break;
            case 2:
            case 3:
                action.text = unhex(word());
                break;
            default:
                throw std::invalid_argument("Unregistered provider action");
            }
            provider.actions.push_back(std::move(action));
        }
        provider.unavailable = boolean(input<std::int32_t>());
        if (provider.unavailable)
            provider.reason = unhex(word());
        else
            provider.result = input<std::int32_t>();
        result.push_back(std::move(provider));
    }
    return result;
}
struct Stop {
    std::string category, boundary, reason;
};
class Script {
public:
    state::CityMoodWorkspace mood;
    std::vector<Provider> inputs;
    std::size_t event_limit = 0, provider_limit = 0;

    void run(std::optional<state::CitySlot> selected, bool full) {
        std::string outcome = "return", result = "{\"$type\":\"null\"}";
        try {
            result = scalar("i32", invoke("root", {}, selected, full, 0));
            emit("root/exit", "snapshot", "root",
                 "{\"label\":\"exit\",\"state\":" + state_json() + "}");
        } catch (const Stop& stop) {
            outcome = stop.category;
            result = "{\"$type\":\"null\"}";
            // Two diagnostic slots were reserved before every ordinary event.
            append("root/prefix", "snapshot", active_call_,
                   "{\"label\":\"stop-prefix\",\"state\":" + state_json() + "}");
            append("root/stop", "stop", active_call_,
                   "{\"category\":" + quote(stop.category) + ",\"boundary\":" +
                       quote(stop.boundary) + ",\"reason\":" + quote(stop.reason) +
                       ",\"completedInputs\":" + std::to_string(completed_) +
                       ",\"snapshot\":\"root/prefix\"}");
        }
        std::cout << "{\"outcome\":" << quote(outcome) << ",\"result\":" << result
                  << ",\"state\":" << state_json() << ",\"trace\":[";
        for (std::size_t i = 0; i < trace_.size(); ++i) {
            if (i)
                std::cout << ',';
            std::cout << trace_[i];
        }
        std::cout << "]}\n";
    }

private:
    std::vector<std::string> trace_;
    std::set<std::string> event_ids_;
    std::string active_call_ = "root";
    std::size_t cursor_ = 0, completed_ = 0;
    std::string state_json() const {
        return "{\"F02.workspaceCity\":" + city_json(mood.city) +
               ",\"F02.happyCount\":" + scalar("i32", mood.happy) +
               ",\"F02.unhappyCount\":" + scalar("i32", mood.unhappy) + "}";
    }
    void check(std::string_view id) const {
        if (trace_.size() + 2 >= event_limit)
            throw Stop{"budget-exhausted", std::string(id),
                       "event limit reserves two diagnostic slots"};
        if (event_ids_.contains(std::string(id)))
            throw Stop{"unexpected-input", std::string(id), "duplicate semantic event identity"};
    }
    void append(const std::string& id, std::string_view kind, std::string_view call,
                const std::string& payload) {
        event_ids_.insert(id);
        trace_.push_back("{\"seq\":" + std::to_string(trace_.size()) + ",\"id\":" + quote(id) +
                         ",\"kind\":" + quote(kind) + ",\"call\":" + quote(call) +
                         ",\"payload\":" + payload + "}");
    }
    void emit(const std::string& id, std::string_view kind, std::string_view call,
              const std::string& payload) {
        check(id);
        append(id, kind, call, payload);
    }
    std::int32_t invoke(const std::string& call, std::string_view parent,
                        std::optional<state::CitySlot> selected, bool full, unsigned depth) {
        if (depth >= 64)
            throw Stop{"budget-exhausted", call, "callback depth limit"};
        const auto previous = active_call_;
        active_call_ = call;
        emit(call + "/enter", "enter", call,
             "{\"operation\":" + quote(operation_id) +
                 ",\"parent\":" + (parent.empty() ? "null" : quote(parent)) +
                 ",\"arguments\":" + arguments_json(selected, full) + "}");
        const auto value = state::city_calculation_entry(
            selected, full, mood, [&] { return pipeline(call, selected, depth); });
        if (call == "root" && cursor_ != inputs.size())
            throw Stop{"unexpected-input", "root/return", "unused mandatory provider input"};
        emit(call + "/return", "return", call, "{\"result\":" + scalar("i32", value) + "}");
        active_call_ = previous;
        return value;
    }
    std::int32_t pipeline(const std::string& call, std::optional<state::CitySlot> selected,
                          unsigned depth) {
        const auto id = call + "/pipeline";
        if (cursor_ >= provider_limit)
            throw Stop{"budget-exhausted", id, "provider request limit"};
        emit(id + "/request", "provider", call,
             "{\"stage\":\"request\",\"id\":" + quote(id) + ",\"operation\":" + quote(provider_id) +
                 ",\"arguments\":{\"city\":" + city_json(selected) + "}}");
        if (cursor_ == inputs.size())
            throw Stop{"unexpected-input", id, "provider script exhausted"};
        const auto& provider = inputs[cursor_];
        if (provider.id != id || provider.request != provider_id || provider.city != selected)
            throw Stop{"unexpected-input", id, "provider identity/order/arguments mismatch"};
        ++cursor_;
        for (const auto& action : provider.actions) {
            const auto event_id = call + "/action/" + segment(action.id);
            switch (action.kind) {
            case 0: {
                check(event_id);
                if (action.target == 0)
                    mood.city = city(action.value);
                else if (action.target == 1)
                    mood.happy = action.value;
                else
                    mood.unhappy = action.value;
                const auto value =
                    action.target == 0 ? city_json(mood.city) : scalar("i32", action.value);
                emit(event_id, "effect", call,
                     "{\"name\":\"provider.output\",\"values\":{\"target\":" +
                         text(fields[action.target]) + ",\"value\":" + value + "}}");
                break;
            }
            case 1:
                if (action.text != operation_id)
                    throw Stop{"unexpected-input", event_id, "unregistered callback"};
                invoke(call + "/deliver/" + segment(action.id), call, city(action.value),
                       action.full, depth + 1);
                break;
            case 2:
                emit(event_id, "snapshot", call,
                     "{\"label\":" + quote(action.text) + ",\"state\":" + state_json() + "}");
                break;
            case 3:
                emit(event_id, "effect", call,
                     "{\"name\":\"provider.failure\",\"values\":{\"code\":" + text(action.text) +
                         "}}");
                break;
            }
        }
        if (provider.unavailable)
            throw Stop{"unresolved-input", id + "/return", provider.reason};
        emit(id + "/return", "provider", call,
             "{\"stage\":\"return\",\"id\":" + quote(id) +
                 ",\"result\":" + scalar("i32", provider.result) + "}");
        ++completed_;
        return provider.result;
    }
};
} // namespace
bool run_script(std::string_view operation, std::span<char*> arguments) {
    if (operation != operation_id)
        return false;
    if (arguments.size() != 2)
        throw std::invalid_argument("Wrong city-dispatch argument count");
    const auto selected = city(number<std::int32_t>(arguments[0]));
    const auto full = boolean(number<std::int32_t>(arguments[1]));
    Script script;
    script.mood.city = city(input<std::int32_t>());
    script.mood.happy = input<std::int32_t>();
    script.mood.unhappy = input<std::int32_t>();
    script.event_limit = input<std::uint32_t>();
    script.provider_limit = input<std::uint32_t>();
    if (script.event_limit < 2 || script.provider_limit == 0)
        throw std::invalid_argument("Invalid script budgets");
    script.inputs = providers();
    std::string extra;
    if (std::cin >> extra)
        throw std::invalid_argument("Trailing script input");
    script.run(selected, full);
    return true;
}
} // namespace openciv::conformance
