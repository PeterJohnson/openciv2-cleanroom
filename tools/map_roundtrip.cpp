// Native comparison driver for externally supplied original maps. This is not
// the inspection CLI or evidence that the original program opened an export.
#include "formats/classic_io.hpp"
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>

int main(int argc, char** argv) {
    try {
        if (argc != 3)
            throw std::invalid_argument("Usage: oc2_map_roundtrip INPUT.MP NEW_OUTPUT.MP");
        const std::filesystem::path source{argv[1]}, destination{argv[2]};
        if (std::filesystem::exists(destination))
            throw std::invalid_argument("Output must be a new path");
        std::ifstream input(source, std::ios::binary);
        if (!input)
            throw std::runtime_error("Cannot open map input");
        const openciv::formats::Bytes bytes{std::istreambuf_iterator<char>{input}, {}};
        if (input.bad())
            throw std::runtime_error("Map input read failed");
        const auto decoded = openciv::formats::decode_raw_map(bytes);
        const auto encoded = openciv::formats::encode_raw_map(decoded);
        if (encoded != bytes)
            throw std::runtime_error("Decoded/encoded bytes differ");
        std::ofstream output(destination, std::ios::binary);
        if (!output)
            throw std::runtime_error("Cannot create map output");
        output.write(reinterpret_cast<const char*>(encoded.data()),
                     static_cast<std::streamsize>(encoded.size()));
        output.close();
        if (!output)
            throw std::runtime_error("Map output write/close failed");
        std::cout << "{\"width\":" << decoded.header.width
                  << ",\"height\":" << decoded.header.height
                  << ",\"tiles\":" << decoded.tiles.size()
                  << ",\"trailingBytes\":" << decoded.trailing.size() << ",\"byteEqual\":true}\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
