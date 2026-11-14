#pragma once
#include <cstdint>
#include <functional>
#include <string>
#include <string_view>
#include <vector>

namespace openciv::session {
using FileLabel = std::function<std::string(std::size_t)>;
void select_classic_extension(bool scenario, std::uint8_t mode, std::string& extension);
struct FileType {
    std::string description, pattern;
    bool operator==(const FileType&) const = default;
};
// F06 semantic file types. Native filter encoding belongs to a platform adapter.
std::vector<FileType> classic_file_types(std::uint8_t mode, bool scenario, bool raw_map,
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
struct FileChoiceServices {
    std::function<FileChoiceResult(const FileChoiceRequest&)> choose;
};
// F06 production boundary. Only accepted paths are published; native return
// conventions and cancelled drafts are not inputs to session orchestration.
FileChoiceResult choose_classic_file(const FileChoiceRequest& request,
                                     const FileChoiceServices& services);
} // namespace openciv::session
