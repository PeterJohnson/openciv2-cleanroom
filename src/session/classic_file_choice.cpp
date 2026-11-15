#include "classic_file_choice.hpp"
#include <stdexcept>

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
