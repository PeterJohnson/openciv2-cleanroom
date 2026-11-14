#include "platform/native_files.hpp"
#include <fstream>
#include <iterator>

namespace openciv::io {
formats::Bytes read_file(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input)
        throw std::runtime_error("Cannot open input file");
    formats::Bytes bytes{std::istreambuf_iterator<char>{input}, {}};
    if (input.bad())
        throw std::runtime_error("Input file read failed");
    return bytes;
}
} // namespace openciv::io
