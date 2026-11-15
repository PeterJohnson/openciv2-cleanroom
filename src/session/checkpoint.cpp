import std;
import openciv.session.runtime_internal;
import openciv.base.hash;
import openciv.formats.content_reader;
import openciv.formats.random_checkpoint;

namespace openciv::session {
namespace {
using formats::content::detail::Reader;
constexpr std::size_t payload_limit = 4 * 1024 * 1024, owner_limit = 512 * 1024;
void require(bool condition, const char* message) {
    if (!condition)
        throw std::invalid_argument(message);
}
std::span<const std::uint8_t> bytes(std::string_view value) {
    return {reinterpret_cast<const std::uint8_t*>(value.data()), value.size()};
}
std::string hex(std::span<const std::uint8_t> value) {
    require(value.size() <= owner_limit, "Checkpoint owner payload too large");
    return base::hex_bytes(value);
}
std::string text_hex(std::string_view value) {
    require(value.size() <= 4096, "Checkpoint text too long");
    return hex(bytes(value));
}
CheckpointBytes unhex(const Reader& reader, std::size_t limit = owner_limit) {
    const auto text = reader.text(false);
    if (text.size() % 2 != 0 || text.size() / 2 > limit)
        reader.error("invalid byte-string size");
    const auto digit = [&](char c) -> unsigned {
        if (c >= '0' && c <= '9')
            return static_cast<unsigned>(c - '0');
        if (c >= 'a' && c <= 'f')
            return static_cast<unsigned>(c - 'a' + 10);
        reader.error("expected lowercase hexadecimal");
    };
    CheckpointBytes result(text.size() / 2);
    for (std::size_t i = 0; i < result.size(); ++i)
        result[i] = static_cast<std::uint8_t>((digit(text[2 * i]) << 4) | digit(text[2 * i + 1]));
    return result;
}
std::string read_text(const Reader& reader) {
    const auto value = unhex(reader, 4096);
    return {value.begin(), value.end()};
}
template <class T = std::uint64_t> T number(const Reader& reader) {
    const auto text = reader.text();
    T result{};
    const auto parsed = std::from_chars(text.data(), text.data() + text.size(), result);
    if (parsed.ec != std::errc{} || parsed.ptr != text.data() + text.size() ||
        std::to_string(result) != text)
        reader.error("expected canonical integer string");
    return result;
}
template <class T> std::string id(T value) {
    return std::to_string(static_cast<std::uint64_t>(value));
}
template <class T> T read_id(const Reader& reader) {
    const auto result = number(reader);
    if (result == 0)
        reader.error("zero identity");
    return T{result};
}
bool boolean(const Reader& reader) {
    if (!reader.value.isBool())
        reader.error("expected boolean");
    return reader.value.getBool();
}
std::size_t count(const Reader& reader) {
    const auto n = reader.array_size();
    if (n > 4096)
        reader.error("checkpoint array limit exceeded");
    return n;
}
jt::Json array() {
    jt::Json result;
    result.setArray();
    return result;
}
jt::Json value_json(const CommandValue& value) {
    jt::Json result;
    std::visit(
        [&](const auto& v) {
            using T = std::decay_t<decltype(v)>;
            if constexpr (std::is_same_v<T, std::int32_t>) {
                result["kind"] = "i32";
                result["value"] = std::to_string(v);
            } else if constexpr (std::is_same_v<T, bool>) {
                result["kind"] = "bool";
                result["value"] = v;
            } else if constexpr (std::is_same_v<T, std::string>) {
                result["kind"] = "text";
                result["value"] = text_hex(v);
            } else if constexpr (std::is_same_v<T, state::CivilizationSlot>) {
                result["kind"] = "civilization";
                result["value"] = id(v);
            } else {
                result["kind"] = std::is_same_v<T, UnitTarget> ? "unit" : "city";
                result["slot"] = id(v.slot);
                result["token"] = id(v.token);
            }
        },
        value);
    return result;
}
CommandValue read_value(const Reader& reader) {
    const auto kind = reader.at("kind").text();
    if (kind == "unit" || kind == "city") {
        reader.fields({"kind", "slot", "token"});
        const auto slot = number<std::uint16_t>(reader.at("slot"));
        const auto token = number<std::uint32_t>(reader.at("token"));
        if (kind == "unit")
            return UnitTarget{state::UnitSlot{slot}, state::UnitToken{token}};
        return CityTarget{state::CitySlot{slot}, state::CityToken{token}};
    }
    reader.fields({"kind", "value"});
    const auto value = reader.at("value");
    if (kind == "i32")
        return number<std::int32_t>(value);
    if (kind == "bool")
        return boolean(value);
    if (kind == "text")
        return read_text(value);
    if (kind == "civilization") {
        const auto slot = number<std::uint8_t>(value);
        if (slot >= 8)
            value.error("invalid civilization");
        return state::CivilizationSlot{slot};
    }
    reader.error("unknown command value type");
}
jt::Json values_json(const CommandValues& values) {
    require(values.size() <= 4096, "Too many checkpoint values");
    auto result = array();
    for (const auto& v : values) {
        jt::Json item;
        item["name"] = text_hex(v.name);
        item["value"] = value_json(v.value);
        result.getArray().push_back(std::move(item));
    }
    return result;
}
CommandValues read_values(const Reader& reader) {
    CommandValues result;
    std::set<std::string> names;
    for (std::size_t i = 0, n = count(reader); i < n; ++i) {
        const auto row = reader.element(i);
        row.fields({"name", "value"});
        auto name = read_text(row.at("name"));
        if (name.empty() || !names.insert(name).second)
            row.error("invalid or duplicate named value");
        result.push_back({std::move(name), read_value(row.at("value"))});
    }
    return result;
}
jt::Json interaction_json(const Interaction& interaction) {
    const auto& spec = interaction.spec;
    jt::Json result;
    result["id"] = id(interaction.id);
    result["recipient"] = id(interaction.recipient);
    result["observer"] = id(interaction.observer);
    result["prompt"] = text_hex(spec.prompt);
    result["specifiedRecipient"] = spec.recipient ? jt::Json(id(*spec.recipient)) : jt::Json{};
    result["target"] = spec.target ? value_json(*spec.target) : jt::Json{};
    result["deadline"] = spec.deadline ? jt::Json(std::to_string(*spec.deadline)) : jt::Json{};
    result["default"] =
        spec.default_choice ? jt::Json(std::to_string(*spec.default_choice)) : jt::Json{};
    result["cancellable"] = spec.cancellable;
    result["choices"] = array();
    require(spec.choices.size() <= 4096, "Too many checkpoint choices");
    for (auto choice : spec.choices)
        result["choices"].getArray().emplace_back(std::to_string(choice));
    return result;
}
Interaction read_interaction(const Reader& reader, ParticipantId participant,
                             std::uint64_t next_interaction) {
    reader.fields({"id", "recipient", "observer", "prompt", "specifiedRecipient", "target",
                   "deadline", "default", "cancellable", "choices"});
    Interaction result;
    result.id = read_id<InteractionId>(reader.at("id"));
    result.recipient = read_id<ParticipantId>(reader.at("recipient"));
    require(result.recipient == participant && number(reader.at("observer")) == 1 &&
                static_cast<std::uint64_t>(result.id) < next_interaction,
            "Invalid checkpoint interaction identity");
    result.observer = state::CivilizationSlot{1};
    auto& spec = result.spec;
    spec.prompt = read_text(reader.at("prompt"));
    if (!reader.at("specifiedRecipient").value.isNull()) {
        spec.recipient = read_id<ParticipantId>(reader.at("specifiedRecipient"));
        require(*spec.recipient == participant, "Unsupported checkpoint recipient");
    }
    if (!reader.at("target").value.isNull())
        spec.target = read_value(reader.at("target"));
    if (!reader.at("deadline").value.isNull())
        spec.deadline = number(reader.at("deadline"));
    if (!reader.at("default").value.isNull())
        spec.default_choice = number<std::int32_t>(reader.at("default"));
    spec.cancellable = boolean(reader.at("cancellable"));
    const auto choices = reader.at("choices");
    std::set<std::int32_t> unique;
    for (std::size_t i = 0, n = count(choices); i < n; ++i) {
        const auto choice = number<std::int32_t>(choices.element(i));
        require(unique.insert(choice).second, "Duplicate checkpoint choice");
        spec.choices.push_back(choice);
    }
    require(!spec.prompt.empty() && !spec.choices.empty() &&
                (!spec.default_choice || (spec.deadline && unique.contains(*spec.default_choice))),
            "Invalid checkpoint interaction");
    return result;
}
void profile_valid(const CheckpointProfile& profile, std::string_view content_hash) {
    require(!profile.id.empty() && profile.id.size() <= 128 && profile.encode && profile.decode,
            "Checkpoint requires a named owner codec");
    require(
        content_hash.size() == 64 &&
            std::all_of(content_hash.begin(), content_hash.end(),
                        [](char c) { return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'); }),
        "Invalid checkpoint content hash");
}
const ContinuationCodec& operation_codec(const CommandDefinition& definition) {
    require(definition.checkpoint && !definition.checkpoint->id.empty() &&
                definition.checkpoint->version && definition.checkpoint->encode &&
                definition.checkpoint->decode,
            "Operation has no checkpoint codec");
    return *definition.checkpoint;
}
void local_boundary(const detail::RuntimeState& state) {
    require(state.active && !state.advancing && !state.activities &&
                state.participants.size() == 1 && state.gameplay && state.presentation,
            "Checkpoint requires an active local participant between steps");
    const auto& participant = state.participants.begin()->second;
    require(participant.civilization == state::CivilizationSlot{1} && !participant.connection,
            "Checkpoint requires local civilization 1");
    require(state.commands.size() <= 1 && state.ready.empty() && state.requests.empty() &&
                state.replies.empty() && state.received.empty(),
            "Checkpoint excludes additional commands, queued inputs and peer work");
    for (const auto& [id, record] : state.commands) {
        (void)id;
        require(record.update.status == CommandStatus::awaiting_interaction &&
                    record.update.interaction && record.continuation && !record.executor &&
                    !record.reply_to && !record.result_application && record.resolved.empty() &&
                    !record.actor.source && record.actor.participant == participant.id &&
                    record.actor.civilization == participant.civilization &&
                    record.update.interaction->recipient == participant.id,
                "Checkpoint supports only an owned local suspended decision");
    }
}
} // namespace

CheckpointBytes SessionRuntime::checkpoint(const CheckpointProfile& profile,
                                           std::string_view content_hash) const {
    profile_valid(profile, content_hash);
    const auto& state = *state_;
    local_boundary(state);
    jt::Json root;
    root["profile"] = text_hex(profile.id);
    root["content"] = std::string(content_hash);
    root["origin"] = id(state.identity);
    root["session"] = id(state.session);
    root["participant"] = id(state.participants.begin()->first);
    auto& counters = root["counters"];
    counters["session"] = std::to_string(state.next_session);
    counters["participant"] = std::to_string(state.next_participant);
    counters["controller"] = std::to_string(state.next_controller);
    counters["command"] = std::to_string(state.next_command);
    counters["interaction"] = std::to_string(state.next_interaction);
    counters["revision"] = std::to_string(state.revision);
    root["world"] = hex(profile.encode(*state.world, state.process));
    root["gameplay"] = formats::encode_random_checkpoint(state.gameplay->checkpoint());
    root["presentation"] = formats::encode_random_checkpoint(state.presentation->checkpoint());
    auto& command = root["command"];
    if (!state.commands.empty()) {
        const auto& [command_id, record] = *state.commands.begin();
        const auto& codec = operation_codec(*record.definition);
        command["id"] = id(command_id);
        command["name"] = text_hex(record.request.name);
        command["descriptorVersion"] = std::to_string(record.definition->descriptor.version);
        command["arguments"] = values_json(record.request.arguments);
        command["controller"] =
            record.actor.controller ? jt::Json(id(*record.actor.controller)) : jt::Json{};
        command["access"] = record.actor.access == AccessClass::player ? "player" : "developer";
        command["origin"] = id(record.update.correlation.runtime);
        command["session"] = id(record.update.correlation.session);
        command["code"] = text_hex(record.update.code);
        command["result"] = values_json(record.update.result);
        command["interaction"] = interaction_json(*record.update.interaction);
        command["effects"] = array();
        require(record.update.committed.size() <= 4096, "Too many checkpoint effects");
        for (const auto& effect : record.update.committed) {
            jt::Json row;
            row["index"] = std::to_string(effect.index);
            row["name"] = text_hex(effect.name);
            row["values"] = values_json(effect.values);
            command["effects"].getArray().push_back(std::move(row));
        }
        command["codec"] = text_hex(codec.id);
        command["codecVersion"] = std::to_string(codec.version);
        command["locals"] = hex(codec.encode(*record.continuation));
    }
    const auto payload = root.toString();
    require(payload.size() <= payload_limit, "Checkpoint payload exceeds limit");
    jt::Json envelope;
    envelope["format"] = "openciv2-local-checkpoint/1";
    envelope["payload"] = payload;
    envelope["sha256"] = base::sha256(bytes(payload));
    const auto encoded = envelope.toString();
    return {encoded.begin(), encoded.end()};
}

void SessionRuntime::restore_checkpoint(std::span<const std::uint8_t> encoded,
                                        const CheckpointProfile& profile,
                                        std::string_view content_hash) {
    profile_valid(profile, content_hash);
    require(!state_->advancing && !state_->activities,
            "Cannot restore during a command step or owner workflow");
    require(encoded.size() <= 16 * 1024 * 1024, "Checkpoint input exceeds limit");
    // Replacing a pending local decision is explicit, but never discard peer work
    // or an already queued client input at the destination.
    require(state_->ready.empty() && state_->requests.empty() && state_->replies.empty() &&
                state_->received.empty(),
            "Cannot replace queued destination work");
    for (const auto& [id, record] : state_->commands) {
        (void)id;
        require(!record.executor && !record.reply_to && !record.actor.source,
                "Cannot replace remote destination work");
    }
    for (const auto& [id, participant] : state_->participants) {
        (void)id;
        require(!participant.connection, "Cannot replace a connected destination participant");
    }
    const std::string input(encoded.begin(), encoded.end());
    auto envelope_doc = formats::content::parse_json(input, "checkpoint");
    const Reader envelope{envelope_doc.value, envelope_doc.numbers, "checkpoint"};
    envelope.fields({"format", "payload", "sha256"});
    envelope.at("format").constant("openciv2-local-checkpoint/1");
    const auto payload = envelope.at("payload").text();
    require(payload.size() <= payload_limit &&
                base::sha256(bytes(payload)) == envelope.at("sha256").sha256(),
            "Checkpoint payload integrity failure");
    auto doc = formats::content::parse_json(payload, "checkpoint/payload");
    const Reader root{doc.value, doc.numbers, "checkpoint/payload"};
    root.fields({"profile", "content", "origin", "session", "participant", "counters", "world",
                 "gameplay", "presentation", "command"});
    require(read_text(root.at("profile")) == profile.id &&
                root.at("content").sha256() == content_hash,
            "Checkpoint profile/content mismatch");
    const auto origin = read_id<RuntimeId>(root.at("origin"));
    auto prepared = std::make_shared<detail::RuntimeState>(state_->process, state_->identity);
    prepared->definitions = state_->definitions;
    prepared->session = read_id<SessionId>(root.at("session"));
    const auto participant = read_id<ParticipantId>(root.at("participant"));
    prepared->participants.emplace(participant,
                                   Participant{participant, state::CivilizationSlot{1}, {}});
    const auto counters = root.at("counters");
    counters.fields({"session", "participant", "controller", "command", "interaction", "revision"});
    prepared->next_session = number(counters.at("session"));
    prepared->next_participant = number(counters.at("participant"));
    prepared->next_controller = number(counters.at("controller"));
    prepared->next_command = number(counters.at("command"));
    prepared->next_interaction = number(counters.at("interaction"));
    prepared->revision = number(counters.at("revision"));
    require(prepared->next_session > static_cast<std::uint64_t>(prepared->session) &&
                prepared->next_participant > static_cast<std::uint64_t>(participant) &&
                prepared->next_controller && prepared->next_command && prepared->next_interaction,
            "Invalid checkpoint identity counters");
    auto gameplay = formats::decode_random_checkpoint(root.at("gameplay").text());
    auto presentation = formats::decode_random_checkpoint(root.at("presentation").text());
    prepared->gameplay.emplace(0, gameplay.context);
    prepared->gameplay->restore(gameplay);
    prepared->presentation.emplace(0, presentation.context);
    prepared->presentation->restore(presentation);
    auto process = std::make_unique<state::ProcessContext>();
    profile.decode(unhex(root.at("world")), *prepared->world, *process);
    const auto command = root.at("command");
    if (!command.value.isNull()) {
        command.fields({"id", "name", "descriptorVersion", "arguments", "controller", "access",
                        "origin", "session", "code", "result", "interaction", "effects", "codec",
                        "codecVersion", "locals"});
        detail::CommandRecord record;
        const auto command_id = read_id<CommandId>(command.at("id"));
        require(static_cast<std::uint64_t>(command_id) < prepared->next_command,
                "Invalid checkpoint command counter");
        record.request = {read_text(command.at("name")), read_values(command.at("arguments"))};
        const auto found = prepared->definitions.find(record.request.name);
        require(found != prepared->definitions.end(), "Unknown checkpoint operation");
        record.definition = &found->second;
        const auto& definition = *record.definition;
        const auto& codec = operation_codec(definition);
        require(number<unsigned>(command.at("descriptorVersion")) ==
                        definition.descriptor.version &&
                    read_text(command.at("codec")) == codec.id &&
                    number<unsigned>(command.at("codecVersion")) == codec.version,
                "Unknown checkpoint operation codec/version");
        require(!validate_values(definition.descriptor.arguments, record.request.arguments),
                "Invalid checkpoint arguments");
        record.actor.participant = participant;
        record.actor.civilization = state::CivilizationSlot{1};
        if (!command.at("controller").value.isNull()) {
            record.actor.controller = read_id<ControllerId>(command.at("controller"));
            require(static_cast<std::uint64_t>(*record.actor.controller) <
                        prepared->next_controller,
                    "Invalid actor controller");
        }
        const auto access = command.at("access").text();
        require(access == "player" || access == "developer", "Invalid checkpoint access");
        record.actor.access = access == "player" ? AccessClass::player : AccessClass::developer;
        require(definition.descriptor.access == AccessClass::player ||
                    record.actor.access == AccessClass::developer,
                "Checkpoint actor lacks descriptor grant");
        record.update.correlation = {read_id<RuntimeId>(command.at("origin")),
                                     read_id<SessionId>(command.at("session")), command_id};
        // Correlations may retain a previous runtime origin across a restored run.
        (void)origin;
        require(record.update.correlation.session == prepared->session,
                "Checkpoint correlation session mismatch");
        record.update.status = CommandStatus::awaiting_interaction;
        record.update.code = read_text(command.at("code"));
        record.update.result = read_values(command.at("result"));
        record.update.interaction =
            read_interaction(command.at("interaction"), participant, prepared->next_interaction);
        const auto effects = command.at("effects");
        for (std::size_t i = 0, n = count(effects); i < n; ++i) {
            const auto row = effects.element(i);
            row.fields({"index", "name", "values"});
            const auto index = number(row.at("index"));
            require(index == i + 1, "Invalid checkpoint effect sequence");
            auto name = read_text(row.at("name"));
            require(!name.empty(), "Empty checkpoint effect name");
            record.update.committed.push_back(
                {index, std::move(name), read_values(row.at("values"))});
        }
        record.continuation =
            codec.decode(unhex(command.at("locals")), record.request, record.update);
        require(record.continuation != nullptr, "Operation codec returned no continuation");
        prepared->commands.emplace(command_id, std::move(record));
    }
    prepared->active = true;
    local_boundary(*prepared);
    // Preserve destination high-water marks too: replacement cannot resurrect a
    // prior client binding or reuse IDs assigned since this snapshot was captured.
    prepared->next_session = std::max(prepared->next_session, state_->next_session);
    prepared->next_participant = std::max(prepared->next_participant, state_->next_participant);
    prepared->next_controller = std::max(prepared->next_controller, state_->next_controller);
    prepared->next_command = std::max(prepared->next_command, state_->next_command);
    prepared->next_interaction = std::max(prepared->next_interaction, state_->next_interaction);
    static_assert(std::is_nothrow_swappable_v<state::ProcessContext>);
    using std::swap;
    swap(state_->process, *process);
    for (auto& [id, lease] : state_->leases) {
        (void)id;
        lease->active = false;
    }
    state_->active = false;
    state_.swap(prepared);
}
} // namespace openciv::session
