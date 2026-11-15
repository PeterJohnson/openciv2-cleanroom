#pragma once
#include "formats/classic_rules.hpp"
#include "rules/rules_install.hpp"
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

namespace classic_rule_fixture {
class InstalledRules {
public:
    InstalledRules(openciv::formats::classic::TextReader& reader, openciv::state::RuleState& live,
                   openciv::formats::classic::TextCase comparison,
                   openciv::formats::classic::RuleLoadServices services = {})
        : decoder_(reader, *input_, std::move(comparison), std::move(services)),
          installer_(*input_, live) {}
    void load(std::string_view filename, bool replace) {
        decoder_.load(filename);
        installer_.load(replace);
    }
    void cosmic() {
        decoder_.cosmic();
        installer_.cosmic();
    }
    void advances(bool keep = false) {
        decoder_.advances();
        installer_.advances(keep);
    }
    void improvements(bool keep = false) {
        decoder_.improvements();
        installer_.improvements(keep);
    }
    void wonder_expiry() {
        decoder_.wonder_expiry();
        installer_.wonder_expiry();
    }
    void units(bool keep = false) {
        decoder_.units();
        installer_.units(keep);
    }
    void terrain() {
        decoder_.terrain();
        installer_.terrain();
    }
    void governments() {
        decoder_.governments();
        installer_.governments();
    }
    void leaders(bool keep) {
        decoder_.leaders();
        installer_.leaders(keep);
    }
    void commodities() {
        decoder_.commodities();
        installer_.commodities();
    }
    void orders() {
        decoder_.orders();
        installer_.orders();
    }
    void difficulties() {
        decoder_.difficulties();
        installer_.difficulties();
    }
    void attitudes() {
        decoder_.attitudes();
        installer_.attitudes();
    }

private:
    std::unique_ptr<openciv::state::RuleInput> input_ =
        std::make_unique<openciv::state::RuleInput>();
    openciv::formats::classic::RuleLoader decoder_;
    openciv::rules::RuleInstaller installer_;
};
} // namespace classic_rule_fixture
