#include "classic_file_choice.hpp"
#include <stdexcept>

namespace openciv::session {
void select_classic_extension(bool scenario, std::uint8_t mode, std::string& extension) {
    if (scenario)
        extension = ".SCN";
    else if (mode == 0)
        extension = ".SAV";
    else if (mode == 1)
        extension = ".HOT";
    else if (mode == 2)
        extension = ".EML";
    else if (mode <= 6)
        extension = ".NET";
}
std::vector<FileType> classic_file_types(std::uint8_t mode, bool scenario, bool raw_map,
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
        if (mode == 0)
            append(238, "sav");
        else if (mode == 1)
            append(855, "hot");
        else if (mode == 2)
            append(858, "eml");
        else if (mode <= 6)
            append(861, "net");
        else
            throw std::invalid_argument("Unsupported file-choice mode");
        if (!current_only) {
            if (mode != 0)
                append(238, "sav");
            if (mode < 3)
                append(861, "net");
            if (mode != 1)
                append(855, "hot");
        }
    }
    return result;
}
FileChoiceResult choose_classic_file(const FileChoiceRequest& request,
                                     const FileChoiceServices& services) {
    if (!services.choose)
        throw std::invalid_argument("Missing file chooser");
    return validate_file_choice(services.choose(request));
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
