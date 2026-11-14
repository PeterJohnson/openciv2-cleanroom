#include "classic_file_choice.hpp"

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
std::optional<std::size_t> build_classic_filter(std::string& filter,
                                                const state::GlobalValues& globals, bool scenario,
                                                bool raw_map, bool current_only,
                                                const FileLabel& label) {
    if (!label)
        throw std::invalid_argument("File filters require label resolution");
    filter.assign(1, '\0');
    std::optional<std::size_t> position = 0;
    const auto append = [&](std::size_t index, std::string_view extension) {
        if (!position)
            throw std::out_of_range("File-filter append has no established write position");
        auto description = label(index);
        description.resize(std::min(description.size(), description.find('\0')));
        const std::string pattern = "*." + std::string(extension);
        filter.resize(*position);
        filter += description + " (" + pattern + ")";
        filter.push_back('\0');
        filter += pattern;
        filter.push_back('\0');
        position = filter.size();
        filter.push_back('\0');
    };
    if (scenario)
        append(415, "scn");
    else if (raw_map)
        append(261, "mp");
    else {
        switch (globals.multiplayer) {
        case 0:
            append(238, "sav");
            break;
        case 1:
            append(855, "hot");
            break;
        case 2:
            append(858, "eml");
            break;
        case 3:
        case 4:
        case 5:
        case 6:
            append(861, "net");
            break;
        default:
            position.reset();
            break;
        }
        if (!current_only) {
            if (globals.multiplayer != 0)
                append(238, "sav");
            if (globals.multiplayer < 3)
                append(861, "net");
            if (globals.multiplayer != 1)
                append(855, "hot");
        }
    }
    return position;
}
bool choose_classic_file(FileChoiceRequest request, std::string& filename,
                         const FileChoiceServices& services) {
    if (!services.choose || !services.extended_error)
        throw std::invalid_argument("File choice requires chooser and extended-error services");
    request.flags = 0x80eU | (request.require_existing ? 0x1000U : 0U);
    request.filter_index = 1;
    request.filename_capacity = 256;
    const auto result = services.choose(request, filename);
    filename.resize(std::min(filename.size(), filename.find('\0')));
    if ((result & 0xffU) == 1)
        return true;
    (void)services.extended_error();
    return false;
}
} // namespace openciv::session
