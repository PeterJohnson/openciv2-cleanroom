export module openciv.formats.classic_text;

import std;

export extern "C++" {
    namespace openciv::formats {
    // Strict, reversible windows-1252; undefined bytes/scalars are diagnosed.
    // Mapping: Unicode's Microsoft CP1252 table version 2.01 (1998-04-15).
    std::string encode_windows1252(std::string_view utf8, std::string_view location);
    std::string decode_windows1252(std::string_view bytes, std::string_view location);
    } // namespace openciv::formats
}
