export module openciv.session.classic_file_choice;

import std;
export import openciv.base.callback_ref;
export import openciv.state.choices;

export namespace openciv::session {
using FileLabel = base::CallbackRef<std::string(std::size_t)>;
void select_classic_extension(bool scenario, state::GameMode mode, std::string& extension);
struct FileType {
    std::string description, pattern;
    bool operator==(const FileType&) const = default;
};
// F06 semantic file types. Native filter encoding belongs to a platform adapter.
std::vector<FileType> classic_file_types(state::GameMode mode, bool scenario, bool raw_map,
                                         bool current_only, const FileLabel& label);
struct FileChoiceRequest {
    bool save = false, require_existing = false;
    std::string title, initial_path, default_extension;
    std::vector<FileType> types;
};
enum class FileChoiceStatus { accepted, cancelled, failed };
struct FileChoiceResult {
    FileChoiceStatus status = FileChoiceStatus::cancelled;
    std::string path, error;
};
// One owned chooser per operation. begin starts presentation and returns;
// poll never waits. A result is consumed once. cancel dismisses any remaining
// native/browser presentation and is also safe after a result or begin failure.
class FileChooser {
public:
    FileChooser() = default;
    FileChooser(const FileChooser&) = delete;
    FileChooser& operator=(const FileChooser&) = delete;
    virtual ~FileChooser() = default;
    virtual void begin(FileChoiceRequest request) = 0;
    virtual std::optional<FileChoiceResult> poll() = 0;
    virtual void cancel() noexcept = 0;
};
FileChoiceResult validate_file_choice(FileChoiceResult result);
using FileChoiceCallback = base::CallbackRef<FileChoiceResult(const FileChoiceRequest&)>;
// F06 production boundary. Only accepted paths are published; native return
// conventions and cancelled drafts are not inputs to session orchestration.
FileChoiceResult choose_classic_file(const FileChoiceRequest& request,
                                     const FileChoiceCallback& services);
} // namespace openciv::session

module :private;
namespace openciv::session {
void select_classic_extension(bool scenario, state::GameMode mode, std::string& extension) {
    if (scenario)
        extension = ".SCN";
    else if (mode == state::GameMode::single_player)
        extension = ".SAV";
    else if (mode == state::GameMode::hotseat)
        extension = ".HOT";
    else if (mode == state::GameMode::email)
        extension = ".EML";
    else if (mode >= state::GameMode::tcp && mode <= state::GameMode::serial)
        extension = ".NET";
}
std::vector<FileType> classic_file_types(state::GameMode mode, bool scenario, bool raw_map,
                                         bool current_only, const FileLabel& label) {
    if (!label)
        throw std::invalid_argument("File types require label resolution");
    std::vector<FileType> result;
    const auto append = [&](std::size_t index, std::string_view extension) {
        result.push_back({label(index), "*." + std::string(extension)});
    };
    if (scenario)
        append(415, "scn");
    else if (raw_map)
        append(261, "mp");
    else {
        if (mode == state::GameMode::single_player)
            append(238, "sav");
        else if (mode == state::GameMode::hotseat)
            append(855, "hot");
        else if (mode == state::GameMode::email)
            append(858, "eml");
        else if (mode >= state::GameMode::tcp && mode <= state::GameMode::serial)
            append(861, "net");
        else
            throw std::invalid_argument("Unsupported file-choice mode");
        if (!current_only) {
            if (mode != state::GameMode::single_player)
                append(238, "sav");
            if (mode < state::GameMode::tcp)
                append(861, "net");
            if (mode != state::GameMode::hotseat)
                append(855, "hot");
        }
    }
    return result;
}
FileChoiceResult choose_classic_file(const FileChoiceRequest& request,
                                     const FileChoiceCallback& services) {
    if (!services)
        throw std::invalid_argument("Missing file chooser");
    return validate_file_choice(services(request));
}
FileChoiceResult validate_file_choice(FileChoiceResult result) {
    if (result.status == FileChoiceStatus::accepted) {
        if (result.path.empty() || result.path.find('\0') != std::string::npos)
            return {FileChoiceStatus::failed, {}, "Invalid selected path"};
    } else if (result.status == FileChoiceStatus::cancelled ||
               result.status == FileChoiceStatus::failed) {
        result.path.clear();
    } else {
        throw std::invalid_argument("Unknown file-choice result");
    }
    return result;
}
} // namespace openciv::session
