// Native, read-only comparison driver for externally supplied original files.
// No load transformation, original execution or semantic validity is claimed.
#include "formats/classic_save.hpp"
#include <fstream>
#include <iostream>
#include <iterator>

int main(int argc, char** argv) {
    try {
        if (argc != 2)
            throw std::invalid_argument("Usage: oc2_save_roundtrip INPUT.SAV");
        std::ifstream input(argv[1], std::ios::binary);
        if (!input)
            throw std::runtime_error("Cannot open save input");
        const openciv::formats::Bytes bytes{std::istreambuf_iterator<char>{input}, {}};
        if (input.bad())
            throw std::runtime_error("Save input read failed");
        const auto save = openciv::formats::decode_stored_save(bytes);
        if (openciv::formats::encode_stored_save(save) != bytes)
            throw std::runtime_error("Decoded/encoded bytes differ");
        std::cout << "{\"version\":" << save.header.version
                  << ",\"width\":" << save.map.header.width
                  << ",\"height\":" << save.map.header.height
                  << ",\"unitSlots\":" << save.units.size()
                  << ",\"citySlots\":" << save.cities.size()
                  << ",\"mode\":" << static_cast<unsigned>(save.globals.multiplayer)
                  << ",\"scenario\":" << (save.scenario ? "true" : "false")
                  << ",\"eventBlock\":" << (save.events ? "true" : "false")
                  << ",\"events\":" << (save.events ? save.events->entries.size() : 0)
                  << ",\"trailingBytes\":" << save.trailing.size() << ",\"byteEqual\":true}\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
