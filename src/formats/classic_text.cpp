
import std;
import openciv.formats.classic_text;
import openciv.formats.error;

namespace openciv::formats {
namespace {
// https://www.unicode.org/Public/MAPPINGS/VENDORS/MICSFT/WINDOWS/CP1252.TXT
constexpr std::array<std::uint32_t, 32> extended{
    0x20ac, 0,      0x201a, 0x0192, 0x201e, 0x2026, 0x2020, 0x2021, 0x02c6, 0x2030, 0x0160,
    0x2039, 0x0152, 0,      0x017d, 0,      0,      0x2018, 0x2019, 0x201c, 0x201d, 0x2022,
    0x2013, 0x2014, 0x02dc, 0x2122, 0x0161, 0x203a, 0x0153, 0,      0x017e, 0x0178};
[[noreturn]] void fail(std::string_view where) {
    throw Error("text-encoding", std::string(where),
                "text is not reversibly representable in windows-1252");
}
void utf8(std::string& out, std::uint32_t value) {
    if (value < 0x80)
        out += static_cast<char>(value);
    else if (value < 0x800) {
        out += static_cast<char>(0xc0 | (value >> 6));
        out += static_cast<char>(0x80 | (value & 63));
    } else {
        out += static_cast<char>(0xe0 | (value >> 12));
        out += static_cast<char>(0x80 | ((value >> 6) & 63));
        out += static_cast<char>(0x80 | (value & 63));
    }
}
} // namespace
std::string decode_windows1252(std::string_view bytes, std::string_view location) {
    std::string out;
    for (char character : bytes) {
        const auto byte = static_cast<unsigned char>(character);
        std::uint32_t point = byte;
        if (byte >= 0x80 && byte < 0xa0) {
            point = extended[byte - 0x80];
            if (!point)
                fail(location);
        }
        utf8(out, point);
    }
    return out;
}
std::string encode_windows1252(std::string_view input, std::string_view location) {
    std::string out;
    for (std::size_t i = 0; i < input.size();) {
        const auto first = static_cast<unsigned char>(input[i++]);
        std::uint32_t point = first, minimum = 0;
        unsigned continuation = 0;
        if (first >= 0xc2 && first <= 0xdf) {
            point = first & 31;
            minimum = 0x80;
            continuation = 1;
        } else if (first >= 0xe0 && first <= 0xef) {
            point = first & 15;
            minimum = 0x800;
            continuation = 2;
        } else if (first >= 0xf0 && first <= 0xf4) {
            point = first & 7;
            minimum = 0x10000;
            continuation = 3;
        } else if (first >= 0x80)
            fail(location);
        for (unsigned j = 0; j < continuation; ++j) {
            if (i == input.size())
                fail(location);
            const auto next = static_cast<unsigned char>(input[i++]);
            if ((next & 0xc0) != 0x80)
                fail(location);
            point = (point << 6) | (next & 63);
        }
        if (point < minimum || point > 0x10ffff || (point >= 0xd800 && point <= 0xdfff))
            fail(location);
        if (point < 0x80 || (point >= 0xa0 && point <= 0xff))
            out += static_cast<char>(point);
        else {
            const auto found = std::find(extended.begin(), extended.end(), point);
            if (found == extended.end())
                fail(location);
            out += static_cast<char>(0x80 + std::distance(extended.begin(), found));
        }
    }
    return out;
}
} // namespace openciv::formats
