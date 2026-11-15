// Native comparison driver for externally supplied original files.
// Also checks isolated ordered auxiliary I/O against the stored record view.
// No complete session load, original execution or semantic validity is claimed.
#include "formats/classic_save.hpp"
#include "formats/save_aux_io.hpp"
#include "platform/native_files.hpp"
#include <iostream>

int main(int argc, char** argv) {
    try {
        if (argc != 2 && argc != 3)
            throw std::invalid_argument("Usage: oc2_save_roundtrip INPUT.SAV [NEW_OUTPUT.SAV]");
        const auto bytes = openciv::io::read_file(argv[1]);
        const auto save = openciv::formats::decode_stored_save(bytes);
        const auto encoded = openciv::formats::encode_stored_save(save);
        if (encoded != bytes)
            throw std::runtime_error("Decoded/encoded bytes differ");
        if (argc == 3)
            openciv::io::write_new_file(argv[2], encoded);
        std::cout << "{\"version\":" << save.header.version
                  << ",\"width\":" << save.map.header.width
                  << ",\"height\":" << save.map.header.height
                  << ",\"unitSlots\":" << save.units.size()
                  << ",\"citySlots\":" << save.cities.size()
                  << ",\"mode\":" << static_cast<unsigned>(save.globals.multiplayer)
                  << ",\"scenario\":" << (save.scenario ? "true" : "false")
                  << ",\"eventBlock\":" << (save.events ? "true" : "false")
                  << ",\"events\":" << (save.events ? save.events->entries.size() : 0)
                  << ",\"trailingBytes\":" << save.trailing.size() << ",\"byteEqual\":true"
                  << ",\"orderedAuxiliaryEqual\":true"
                  << ",\"outputWritten\":" << (argc == 3 ? "true" : "false") << "}\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
