#pragma once
// Authored F03 table inputs shared by parser and ordered-load integration tests.
#include <array>
#include <string>
#include <string_view>

namespace classic_rule_fixture {
inline std::string repeat(std::string_view row, std::size_t count) {
    std::string out;
    while (count--)
        out += std::string(row) + "\n";
    return out;
}
// Authored independently of the native fixture's serialization and the loader.
inline std::array<std::string, 12> complete_sections() {
    std::array<std::string, 12> out{
        "@COSMIC\n3\n2\n2\n5\n5\n1\n2\n7\n14\n8\n12\n10\n20\n3\n3\n8\n1\n50\n50\n10\n75\n5\n",
        "@CIVILIZE\n",
        "@IMPROVE\n",
        "@ENDWONDER\n",
        "@UNITS\n",
        "@TERRAIN\n",
        "@GOVERNMENTS\n",
        "@LEADERS\n",
        "@CARAVAN\n",
        "@ORDERS\n",
        "@DIFFICULTY\n",
        "@ATTITUDES\n"};
    for (int i = 0; i < 100; ++i)
        out[1] += "advances " + std::to_string(i) + ",3,1,nil,nil,0,3\n";
    for (int i = 0; i < 67; ++i)
        out[2] += "improvements " + std::to_string(i) + ",10,1,nil\n";
    out[3] += repeat("nil", 28);
    for (int i = 0; i < 62; ++i)
        out[4] += "units " + std::to_string(i) + ",no,0,2,0,1,1,3,1,10,0,0,nil,0\n";
    for (int i = 0; i < 33; ++i) {
        out[5] += "terrain " + std::to_string(i) + ",1,3,1,1,0";
        if (i < 11)
            out[5] += ",yes,1,3,2,no,0,4,1,Drt";
        out[5] += '\n';
    }
    for (int i = 0; i < 7; ++i)
        out[6] += "governments " + std::to_string(i) + ",Leader,Leader\n";
    for (int i = 0; i < 21; ++i)
        out[7] += "Leader,Leader,1,1,0,tribes " + std::to_string(i) + ",Example,0,0,0\n";
    for (int i = 0; i < 16; ++i)
        out[8] += "commodities " + std::to_string(i) + "\n";
    for (int i = 0; i < 12; ++i)
        out[9] += "orders " + std::to_string(i) + ",x\n";
    for (int i = 0; i < 6; ++i)
        out[10] += "difficulties " + std::to_string(i) + "\n";
    for (int i = 0; i < 9; ++i)
        out[11] += "diplomaticAttitudes " + std::to_string(i) + "\n";
    return out;
}
} // namespace classic_rule_fixture
