export module openciv.formats.error;

import std;

export namespace openciv::formats {
// Codec diagnostics shared by native and classic decoders.
class Error : public std::runtime_error {
public:
    Error(std::string category, std::string where, std::string message)
        : std::runtime_error(where + ": " + message), code(std::move(category)),
          location(std::move(where)) {}
    const std::string code, location;
};
} // namespace openciv::formats
