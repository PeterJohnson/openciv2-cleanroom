#pragma once
#include "state/choices.hpp"
#include <cstdint>
#include <functional>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace openciv::session {
using FileLabel = std::function<std::string(std::size_t)>;
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
    virtual ~FileChooser() = default;
    virtual void begin(FileChoiceRequest request) = 0;
    virtual std::optional<FileChoiceResult> poll() = 0;
    virtual void cancel() noexcept = 0;
};
FileChoiceResult validate_file_choice(FileChoiceResult result);
struct FileChoiceServices {
    std::function<FileChoiceResult(const FileChoiceRequest&)> choose;
};
// F06 production boundary. Only accepted paths are published; native return
// conventions and cancelled drafts are not inputs to session orchestration.
FileChoiceResult choose_classic_file(const FileChoiceRequest& request,
                                     const FileChoiceServices& services);
} // namespace openciv::session
